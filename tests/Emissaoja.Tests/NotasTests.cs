using System.Net;
using System.Text;
using System.Text.Json;
using static Emissaoja.Tests.ServidorFalso;

namespace Emissaoja.Tests;

public class NotasTests
{
    [Fact]
    public async Task Emitir_com_referencia_invalida_nao_chama_a_api()
    {
        var (ej, srv, _) = Fabrica.Criar();
        var invalidas = new[] { "", "curta", "pedido 0001", "pedido/0001", "pedido-çã-01", "pedido-0001\n", new string('a', 65) };

        foreach (var referencia in invalidas)
        {
            var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.EmitirAsync(referencia, Fabrica.Campos()));
            Assert.Equal("chave_idempotencia_invalida", e.Code);
            Assert.Equal(0, e.Status);
        }

        var nula = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.EmitirAsync(null!, Fabrica.Campos()));
        Assert.Equal("chave_idempotencia_invalida", nula.Code);
        Assert.Equal(0, srv.Total);

        Assert.True(NotasRecurso.ReferenciaValida("pedido_2026-10-0001"));
        Assert.True(NotasRecurso.ReferenciaValida(new string('a', 64)));
        Assert.True(NotasRecurso.ReferenciaValida("12345678"));
    }

    [Fact]
    public async Task Emitir_manda_Idempotency_Key_e_deixa_campos_nulos_fora_do_corpo()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Accepted, """
            {"pedido":{"id":"p1","estado":"na_fila","motivo_code":null,"numero":null,"nota":null,"acoes":[],"proxima_consulta_ms":2000},
             "previa":{"resumo":{"valor":150.5},"avisos":[{"code":"retencao_irrf","mensagem":"Confira a retenção.","acao":"revisar","detalhe":{"irrf":2.26,"csrf":null}}]}}
            """);

        var r = await ej.Notas.EmitirAsync("pedido-0001", new CamposNota
        {
            TomadorTipo = TipoTomador.Pj,
            TomadorId = "t1",
            Valor = 150.50m,
            Descricao = "Consultoria de outubro",
        });

        Assert.Equal("p1", r.Pedido.Id);
        Assert.Equal(EstadoPedido.NaFila, r.Pedido.Estado);
        Assert.Equal(2000, r.Pedido.ProximaConsultaMs);
        Assert.False(r.Pedido.Terminal);
        Assert.False(r.Repetido);
        var aviso = Assert.Single(r.Previa!.Avisos);
        Assert.Equal("retencao_irrf", aviso.Code);
        Assert.Equal("revisar", aviso.Acao);
        Assert.True(aviso.Extras!.ContainsKey("detalhe"));

        var req = Assert.Single(srv.Requisicoes);
        Assert.Equal("POST", req.Metodo);
        Assert.Equal("https://emissaoja.test/v1/notas", req.Url.AbsoluteUri);
        Assert.Equal("pedido-0001", req.Cabecalho("Idempotency-Key"));
        Assert.Equal("Bearer " + Fabrica.ChaveTeste, req.Cabecalho("Authorization"));
        Assert.Equal("application/json", req.TipoConteudo);

        using var corpo = JsonDocument.Parse(req.Corpo!);
        var nomes = corpo.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "descricao", "tomador_id", "tomador_tipo", "valor" }, nomes);
        Assert.Equal("pj", corpo.RootElement.GetProperty("tomador_tipo").GetString());
        Assert.Equal(150.50m, corpo.RootElement.GetProperty("valor").GetDecimal());
    }

    [Fact]
    public async Task Emitir_com_confirmacoes_e_campos_extras()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Accepted, """{"pedido":{"id":"p2","estado":"na_fila"}}""");
        using var nulo = JsonDocument.Parse("null");
        var campos = Fabrica.Campos();
        campos.ConfirmoRepeticao = true;
        campos.PermitirRepetidaDe = "p1";
        campos.Competencia = "2026-10-01";
        campos.Extras = new Dictionary<string, JsonElement> { ["campo_novo"] = nulo.RootElement.Clone() };

        await ej.Notas.EmitirAsync("pedido-0002", campos);

        using var corpo = JsonDocument.Parse(srv.Ultima.Corpo!);
        var raiz = corpo.RootElement;
        Assert.True(raiz.GetProperty("confirmo_repeticao").GetBoolean());
        Assert.Equal("p1", raiz.GetProperty("permitir_repetida_de").GetString());
        Assert.Equal("2026-10-01", raiz.GetProperty("competencia").GetString());
        Assert.Equal(JsonValueKind.Null, raiz.GetProperty("campo_novo").ValueKind);
        Assert.False(raiz.TryGetProperty("servico_id", out _));
        Assert.False(raiz.TryGetProperty("retem_iss", out _));
    }

    [Fact]
    public async Task Emitir_com_a_mesma_referencia_devolve_o_pedido_original()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"pedido":{"id":"p1","estado":"autorizada","numero":"82"},"repetido":true}""");

        var r = await ej.Notas.EmitirAsync("pedido-0001", Fabrica.Campos());

        Assert.True(r.Repetido);
        Assert.True(r.Pedido.Terminal);
        Assert.Equal("82", r.Pedido.Numero);
    }

    [Fact]
    public async Task Aguardar_respeita_proxima_consulta_ms_e_o_intervalo_minimo()
    {
        var (ej, srv, tempo) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"pedido":{"id":"p1","estado":"na_fila","proxima_consulta_ms":1500}}""")
           .Responder(HttpStatusCode.OK, """{"pedido":{"id":"p1","estado":"emitindo","proxima_consulta_ms":null}}""")
           .Responder(HttpStatusCode.OK, """{"pedido":{"id":"p1","estado":"confirmando","proxima_consulta_ms":200}}""")
           .Responder(HttpStatusCode.OK, """
                {"pedido":{"id":"p1","estado":"autorizada","numero":"82","chave_acesso":"31062001234567890123456789012345678901234567890",
                 "consulta_publica_url":null,"nota":{"origem":"local","id":"n1"},"acoes":[],"proxima_consulta_ms":null,
                 "tomador":{"tipo":"pj","id":"t1","nome":"EMPRESA TESTE","documento_formatado":"12.345.678/0001-95"},
                 "valor":1500,"competencia":"2026-10-30","campo_novo":true}}
                """);

        var r = await ej.Notas.AguardarAsync("p1", intervaloMinimo: TimeSpan.FromMilliseconds(500));

        Assert.Equal(EstadoPedido.Autorizada, r.Pedido.Estado);
        Assert.Equal("82", r.Pedido.Numero);
        Assert.Equal(OrigemNota.Local, r.Pedido.Nota?.Origem);
        Assert.Equal("n1", r.Pedido.Nota?.Id);
        Assert.Equal("EMPRESA TESTE", r.Pedido.Tomador?.Nome);
        Assert.Equal(1500m, r.Pedido.Valor);
        Assert.True(r.Pedido.Extras!.ContainsKey("campo_novo"));
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(1500), TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(500) },
            tempo.Esperas.ToArray());
        Assert.Equal(4, srv.Total);
        Assert.All(srv.Requisicoes, q => Assert.Equal("/v1/emissoes/p1", q.Url.AbsolutePath));
    }

    [Fact]
    public async Task Aguardar_estoura_tempo_esgotado_com_o_pedido_nos_extras()
    {
        var srv = new ServidorFalso(_ => Json(HttpStatusCode.OK, """{"pedido":{"id":"p9","estado":"confirmando","proxima_consulta_ms":5000}}"""));
        var (ej, _, tempo) = Fabrica.Criar(srv);

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.AguardarAsync("p9", timeout: TimeSpan.FromSeconds(12)));

        Assert.Equal("tempo_esgotado", e.Code);
        Assert.Equal(0, e.Status);
        Assert.Equal("p9", e.PedidoId);
        Assert.Equal("p9", e.Extras["pedido_id"].GetString());
        Assert.Equal(4, srv.Total);
        Assert.Equal(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5) }, tempo.Esperas.ToArray());
    }

    [Fact]
    public async Task Pdf_em_bytes_e_xml_em_texto()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0xE2, 0xE3, 0xCF, 0xD3, 0x00, 0xFF };
        const string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><NFSe><xDescServ>Manutenção de software</xDescServ></NFSe>";
        var xmlComBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(xml)).ToArray();
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(_ => Bytes(HttpStatusCode.OK, pdf, "application/pdf"))
           .Responder(_ => Bytes(HttpStatusCode.OK, xmlComBom, "application/xml; charset=utf-8"));

        Assert.Equal(pdf, await ej.Notas.PdfAsync(OrigemNota.Local, "n1"));
        Assert.Equal(xml, await ej.Notas.XmlAsync(OrigemNota.Importada, "n 2"));

        var reqs = srv.Requisicoes.ToArray();
        Assert.Equal("/v1/notas/local/n1/pdf", reqs[0].Url.AbsolutePath);
        Assert.Equal("/v1/notas/importada/n%202/xml", reqs[1].Url.AbsolutePath);
        Assert.Equal("Bearer " + Fabrica.ChaveTeste, reqs[0].Cabecalho("Authorization"));
    }

    [Fact]
    public async Task Pdf_com_erro_da_api_lanca()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.NotFound, """{"status":"ERROR","code":"nota_nao_encontrada","message":"Nota não encontrada."}""", ("x-request-id", "r_pdf"));

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.PdfAsync(OrigemNota.Local, "nao-existe"));

        Assert.Equal(404, e.Status);
        Assert.Equal("nota_nao_encontrada", e.Code);
        Assert.Equal("Nota não encontrada.", e.Message);
        Assert.Equal("r_pdf", e.RequestId);
    }

    [Fact]
    public async Task Listar_notas_monta_a_query_e_le_a_pagina()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """
            {"notas":[{"origem":"local","id":"n1","numero":"82","situacao":"emitida","emitida_em":"2026-10-30T12:00:00.000Z",
              "competencia":"2026-10-30","tomador":{"tipo":"pj","nome":"EMPRESA TESTE","documento":"12345678000195","documento_formatado":"12.345.678/0001-95"},
              "descricao":"Consultoria","valor":89.9,"chave_acesso":"3106","pedido_id":"p1"}],
             "total":21,"janela_cheia":false,"proximo_cursor":"eyJvIjoyMH0"}
            """);

        var r = await ej.Notas.ListarAsync(new FiltroNotas
        {
            Periodo = "personalizado",
            De = "2026-10-01",
            Ate = "2026-10-31",
            Situacao = "emitidas",
            Busca = "",
            Limite = 20,
        });

        Assert.Equal(
            "https://emissaoja.test/v1/notas?periodo=personalizado&de=2026-10-01&ate=2026-10-31&situacao=emitidas&limite=20",
            srv.Ultima.Url.AbsoluteUri);
        var nota = Assert.Single(r.Notas);
        Assert.Equal(89.9m, nota.Valor);
        Assert.Equal("12.345.678/0001-95", nota.Tomador?.DocumentoFormatado);
        Assert.Equal(21, r.Total);
        Assert.Equal("eyJvIjoyMH0", r.ProximoCursor);
    }

    [Fact]
    public async Task Cancelar_e_aguardar_a_operacao()
    {
        var (ej, srv, tempo) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Accepted, """{"operacao":{"id":"op1","tipo":"cancelamento","estado":"processando"}}""")
           .Responder(HttpStatusCode.OK, """{"operacao":{"id":"op1","tipo":"cancelamento","estado":"processando","alvo":{"origem":"local","id":"n1"},"resultado":null,"erro":null}}""")
           .Responder(HttpStatusCode.OK, """{"operacao":{"id":"op1","tipo":"cancelamento","estado":"concluida","alvo":{"origem":"local","id":"n1"},"resultado":{"situacao":"cancelada"},"erro":null}}""");

        var op = await ej.Notas.CancelarAsync(OrigemNota.Local, "n1", MotivoCancelamento.ErroNaEmissao, "Valor digitado errado na nota.");
        Assert.Equal("op1", op.Operacao.Id);
        Assert.Equal("/v1/notas/local/n1/cancelar", srv.Ultima.Url.AbsolutePath);
        using (var corpo = JsonDocument.Parse(srv.Ultima.Corpo!))
        {
            Assert.Equal("1", corpo.RootElement.GetProperty("motivo").GetString());
            Assert.Equal("Valor digitado errado na nota.", corpo.RootElement.GetProperty("justificativa").GetString());
        }

        var fim = await ej.Operacoes.AguardarAsync("op1");

        Assert.Equal("concluida", fim.Operacao.Estado);
        Assert.Equal("n1", fim.Operacao.Alvo?.Id);
        Assert.Equal("cancelada", fim.Operacao.Resultado?.GetProperty("situacao").GetString());
        Assert.Null(fim.Operacao.Erro);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, tempo.Esperas.ToArray());
        Assert.Equal(3, srv.Total);
    }

    [Fact]
    public async Task Cancelar_sem_justificativa_nao_manda_o_campo()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Accepted, """{"operacao":{"id":"op2","tipo":"cancelamento","estado":"processando"}}""");

        await ej.Notas.CancelarAsync(OrigemNota.Local, "n1", MotivoCancelamento.ServicoNaoPrestado);

        using var corpo = JsonDocument.Parse(srv.Ultima.Corpo!);
        Assert.Equal("2", corpo.RootElement.GetProperty("motivo").GetString());
        Assert.False(corpo.RootElement.TryGetProperty("justificativa", out _));
    }
}
