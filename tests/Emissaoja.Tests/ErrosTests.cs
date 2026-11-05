using System.Net;
using System.Text.Json;

namespace Emissaoja.Tests;

public class ErrosTests
{
    [Fact]
    public async Task Erro_422_traz_bloqueios_nos_extras_e_o_request_id()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder((HttpStatusCode)422, """
            {"status":"ERROR","code":"nota_bloqueada","message":"Antes de emitir, resolva:",
             "bloqueios":[{"code":"tomador_incompleto","mensagem":"Complete o endereço do tomador.","acao":"completar_tomador","campo":"cep"}],
             "avisos":[]}
            """, ("x-request-id", "r_abc123"));

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.EmitirAsync("pedido-0002", Fabrica.Campos()));

        Assert.Equal(422, e.Status);
        Assert.Equal("nota_bloqueada", e.Code);
        Assert.Equal("Antes de emitir, resolva:", e.Message);
        Assert.Equal("r_abc123", e.RequestId);
        Assert.False(e.Extras.ContainsKey("status"));
        Assert.False(e.Extras.ContainsKey("code"));
        Assert.False(e.Extras.ContainsKey("message"));
        Assert.Equal(JsonValueKind.Array, e.Extras["bloqueios"].ValueKind);
        Assert.Equal(JsonValueKind.Array, e.Extras["avisos"].ValueKind);

        var b = Assert.Single(e.Bloqueios);
        Assert.Equal("tomador_incompleto", b.Code);
        Assert.Equal("Complete o endereço do tomador.", b.Mensagem);
        Assert.Equal("completar_tomador", b.Acao);
        Assert.Equal("cep", b.Extras!["campo"].GetString());
        Assert.Null(e.NotaIgualRecente);
        Assert.Null(e.RetryAfter);
    }

    [Fact]
    public async Task Corpo_de_erro_que_nao_e_json_vira_a_mensagem()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.BadGateway, "<html><body>502 Bad Gateway</body></html>")
           .Responder(HttpStatusCode.ServiceUnavailable, new string('x', 500))
           .Responder(HttpStatusCode.ServiceUnavailable, "")
           .Responder(HttpStatusCode.InternalServerError, """{"status":"ERROR"}""");

        var html = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());
        Assert.Equal(502, html.Status);
        Assert.Equal("erro_desconhecido", html.Code);
        Assert.Equal("<html><body>502 Bad Gateway</body></html>", html.Message);
        Assert.Empty(html.Extras);
        Assert.Null(html.RequestId);
        Assert.Empty(html.Bloqueios);

        var longo = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());
        Assert.Equal(new string('x', 200), longo.Message);

        var vazio = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());
        Assert.Equal("HTTP 503", vazio.Message);
        Assert.Equal("erro_desconhecido", vazio.Code);

        var semCode = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());
        Assert.Equal("HTTP 500", semCode.Message);
        Assert.Equal("erro_desconhecido", semCode.Code);
    }

    [Fact]
    public async Task Rate_limited_traz_Retry_After_e_nao_repete_sozinho()
    {
        var (ej, srv, tempo) = Fabrica.Criar();
        srv.Responder(
            HttpStatusCode.TooManyRequests,
            """{"status":"ERROR","code":"rate_limited","message":"Muitas ações seguidas. Espere 7 s.","retry_after":7}""",
            ("Retry-After", "7"));

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Tomadores.ListarAsync());

        Assert.Equal(429, e.Status);
        Assert.Equal("rate_limited", e.Code);
        Assert.Equal(TimeSpan.FromSeconds(7), e.RetryAfter);
        Assert.Equal(7, e.Extras["retry_after"].GetInt32());
        Assert.Equal(1, srv.Total);
        Assert.Empty(tempo.Esperas);
    }

    [Fact]
    public async Task Retry_after_do_corpo_vale_sem_o_cabecalho()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.TooManyRequests, """{"status":"ERROR","code":"rate_limited","message":"Espere.","retry_after":12}""");

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Servicos.ListarAsync());

        Assert.Equal(TimeSpan.FromSeconds(12), e.RetryAfter);
    }

    [Fact]
    public async Task Possivel_duplicada_traz_a_nota_igual_para_confirmar()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Conflict, """
            {"status":"ERROR","code":"possivel_duplicada","message":"Já foi emitida hoje uma nota igual a esta. Nada foi emitido agora.",
             "nota_igual_recente":{"pedido_id":"7d1f0c1e-0000-4000-8000-000000000001","numero_nfse":"82","emitida_em":"2026-11-05T12:24:00.000Z","status":"autorizada"}}
            """)
           .Responder(HttpStatusCode.Accepted, """{"pedido":{"id":"p3","estado":"na_fila"}}""");

        var campos = Fabrica.Campos();
        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.EmitirAsync("pedido-0003", campos));

        Assert.Equal(409, e.Status);
        Assert.Equal("possivel_duplicada", e.Code);
        Assert.Equal("7d1f0c1e-0000-4000-8000-000000000001", e.NotaIgualRecente?.PedidoId);
        Assert.Equal("82", e.NotaIgualRecente?.NumeroNfse);
        Assert.Equal("autorizada", e.NotaIgualRecente?.Status);

        // É mesmo outra nota: repete com a mesma referência e o "sim" amarrado à nota igual
        campos.PermitirRepetidaDe = e.NotaIgualRecente!.PedidoId;
        var r = await ej.Notas.EmitirAsync("pedido-0003", campos);

        Assert.Equal("p3", r.Pedido.Id);
        Assert.Equal("pedido-0003", srv.Ultima.Cabecalho("Idempotency-Key"));
        using var corpo = JsonDocument.Parse(srv.Ultima.Corpo!);
        Assert.Equal("7d1f0c1e-0000-4000-8000-000000000001", corpo.RootElement.GetProperty("permitir_repetida_de").GetString());
    }

    [Fact]
    public async Task Emissao_repetida_recente_traz_o_pedido_id()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Conflict, """{"status":"ERROR","code":"emissao_repetida_recente","message":"Nota igual há menos de 2 minutos.","pedido_id":"p1"}""");

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Notas.EmitirAsync("pedido-0004", Fabrica.Campos()));

        Assert.Equal("emissao_repetida_recente", e.Code);
        Assert.Equal("p1", e.PedidoId);
    }

    [Fact]
    public void Excecao_criada_a_mao_tolera_extras_ausentes_ou_estranhos()
    {
        using var doc = JsonDocument.Parse("""{"bloqueios":"nao-e-lista","nota_igual_recente":42}""");
        var extras = new Dictionary<string, JsonElement>();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            extras[p.Name] = p.Value.Clone();
        }

        var e = new EmissaojaException(400, "", "Campo inválido", extras);
        var semNada = new EmissaojaException(0, "rede", "Falha de rede");

        Assert.Equal("erro_desconhecido", e.Code);
        Assert.Empty(e.Bloqueios);
        Assert.Null(e.NotaIgualRecente);
        Assert.Null(e.PedidoId);
        Assert.Empty(semNada.Extras);
        Assert.Empty(semNada.Bloqueios);
    }
}
