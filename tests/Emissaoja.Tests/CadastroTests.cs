using System.Net;
using System.Text.Json;

namespace Emissaoja.Tests;

/// <summary>Empresa, tomadores e serviços: corpos em snake_case sem nulos e respostas tipadas.</summary>
public class CadastroTests
{
    [Fact]
    public async Task Conectar_e_aguardar_a_credencial()
    {
        var (ej, srv, tempo) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Accepted, """
            {"tentativa":{"id":"t1","estado":"verificando","proximo_passo":"aguardar","code":null,"cnpj_formatado":"11.111.111/0001-91",
             "tentativas":{"limite":3,"falhas_24h":0,"restantes":3,"libera_em":null,"ultima_exige_confirmacao":false}}}
            """)
           .Responder(HttpStatusCode.OK, """{"credencial":{"estado":"verificando"},"onboarding":{"estado":"verificando"},"tentativa":{"id":"t1","estado":"verificando","proximo_passo":"aguardar"}}""")
           .Responder(HttpStatusCode.OK, """{"credencial":{"estado":"ativa"},"onboarding":{"estado":"configurando"},"tentativa":{"id":"t1","estado":"aceita","proximo_passo":"configurando"}}""");

        var t = await ej.Empresa.ConectarAsync("11111111000191", "senha-do-portal");

        Assert.Equal("aguardar", t.Tentativa.ProximoPasso);
        Assert.Equal(3, t.Tentativa.Tentativas?.Restantes);
        Assert.False(t.Tentativa.Tentativas?.UltimaExigeConfirmacao);
        var conectar = srv.Ultima;
        Assert.Equal("PUT", conectar.Metodo);
        Assert.Equal("/v1/empresa/credencial", conectar.Url.AbsolutePath);
        Assert.Equal("application/json", conectar.TipoConteudo);
        using (var corpo = JsonDocument.Parse(conectar.Corpo!))
        {
            Assert.Equal("11111111000191", corpo.RootElement.GetProperty("cnpj").GetString());
            Assert.Equal("senha-do-portal", corpo.RootElement.GetProperty("senha").GetString());
            Assert.False(corpo.RootElement.GetProperty("ciente_ultima_tentativa").GetBoolean());
        }

        var c = await ej.Empresa.AguardarCredencialAsync();

        Assert.Equal("configurando", c.Tentativa?.ProximoPasso);
        Assert.Equal("ativa", c.Credencial.Estado);
        Assert.Equal("configurando", c.Onboarding.Estado);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, tempo.Esperas.ToArray());
        Assert.Equal(3, srv.Total);
    }

    [Fact]
    public async Task Aguardar_credencial_estoura_tempo_esgotado()
    {
        var srv = new ServidorFalso(_ => ServidorFalso.Json(HttpStatusCode.OK, """{"credencial":{"estado":"verificando"},"onboarding":{"estado":"verificando"},"tentativa":{"id":"t1","estado":"verificando","proximo_passo":"aguardar"}}"""));
        var (ej, _, _) = Fabrica.Criar(srv);

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.AguardarCredencialAsync(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));

        Assert.Equal("tempo_esgotado", e.Code);
        Assert.Equal(0, e.Status);
        Assert.Null(e.PedidoId);
        Assert.Equal(3, srv.Total);
    }

    [Fact]
    public async Task Fiscal_manda_so_o_que_foi_informado()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"ok":true,"aliquota_simples":{"ok":true}}""");

        var r = await ej.Empresa.FiscalAsync(aliquotaSimples: 6.5m);

        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal("PUT", srv.Ultima.Metodo);
        Assert.Equal("""{"aliquota_simples":6.5}""", srv.Ultima.Corpo);
    }

    [Fact]
    public async Task Criar_tomador_por_cnpj_e_por_cpf()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.Created, """
            {"tomador":{"tipo":"pj","id":"t1","nome":"EMPRESA TESTE LTDA","documento":"12345678000195","documento_formatado":"12.345.678/0001-95","cidade":"Belo Horizonte","uf":"MG"},"ja_existia":false}
            """)
           .Responder(HttpStatusCode.OK, """{"tomador":{"tipo":"pf","id":"t2","nome":"Maria da Silva","documento":"52998224725"},"ja_existia":true}""");

        var pj = await ej.Tomadores.CriarAsync("12.345.678/0001-95");
        Assert.Equal(TipoTomador.Pj, pj.Tomador.Tipo);
        Assert.Equal("MG", pj.Tomador.Uf);
        Assert.False(pj.JaExistia);
        using (var doc = JsonDocument.Parse(srv.Ultima.Corpo!))
        {
            Assert.Equal("12.345.678/0001-95", doc.RootElement.GetProperty("documento").GetString());
            Assert.False(doc.RootElement.TryGetProperty("nome", out _));
        }

        var pf = await ej.Tomadores.CriarAsync("529.982.247-25", "Maria da Silva");
        Assert.Equal(TipoTomador.Pf, pf.Tomador.Tipo);
        Assert.True(pf.JaExistia);
        using var corpo = JsonDocument.Parse(srv.Ultima.Corpo!);
        Assert.Equal("Maria da Silva", corpo.RootElement.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Atualizar_tomador_manda_so_os_campos_preenchidos()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """
            {"tomador":{"id":"t1","tipo":"pj","nome":"EMPRESA TESTE LTDA","documento":"12345678000195","cep":"30130000","endereco":"Avenida Afonso Pena",
             "numero":"1000","bairro":"Centro","cidade":"Belo Horizonte","uf":"MG","codigo_municipio_ibge":"3106200","email":"financeiro@exemplo.com.br","endereco_completo":true}}
            """);

        var r = await ej.Tomadores.AtualizarAsync(TipoTomador.Pj, "t1", new CamposTomador { Email = "financeiro@exemplo.com.br", Numero = "1000" });

        Assert.Equal("3106200", r.Tomador.CodigoMunicipioIbge);
        Assert.True(r.Tomador.EnderecoCompleto);
        Assert.Equal("PUT", srv.Ultima.Metodo);
        Assert.Equal("/v1/tomadores/pj/t1", srv.Ultima.Url.AbsolutePath);
        using var corpo = JsonDocument.Parse(srv.Ultima.Corpo!);
        var nomes = corpo.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "email", "numero" }, nomes);
    }

    [Fact]
    public async Task Servicos_criar_listar_e_catalogo()
    {
        var (ej, srv, _) = Fabrica.Criar();
        const string servico = """
            {"id":"s1","descricao":"Consultoria","descricao_da_nota":null,"principal":true,"ativo":true,"c_trib_nac":"171501",
             "c_trib_mun":null,"item_lc116":"17.01","nbs":"115011000","nbs_sugeridos":[{"nbs":"115011000","descricao":"Consultoria"}],
             "trib_issqn":"tributavel","tp_oper":null,"retem_iss":false,"aliquota_iss":2.5}
            """;
        srv.Responder(HttpStatusCode.Created, "{\"servico\":" + servico + "}")
           .Responder(HttpStatusCode.OK, "{\"servicos\":[" + servico + "]}")
           .Responder(HttpStatusCode.OK, """{"itens":[{"c_trib_nac":"171501","descricao":"Consultoria","item_lc116":"17.01","nbs_sugeridos":[],"mei_vedado":false}]}""");

        var criado = await ej.Servicos.CriarAsync(new CamposServico
        {
            Descricao = "Consultoria",
            CTribNac = "171501",
            TribIssqn = "tributavel",
            RetemIss = false,
        });
        using (var corpo = JsonDocument.Parse(srv.Ultima.Corpo!))
        {
            var nomes = corpo.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "c_trib_nac", "descricao", "retem_iss", "trib_issqn" }, nomes);
        }

        Assert.Equal(2.5m, criado.Servico.AliquotaIss);
        Assert.Equal("115011000", Assert.Single(criado.Servico.NbsSugeridos).Nbs);
        Assert.False(criado.Servico.RetemIss);

        var lista = await ej.Servicos.ListarAsync("todos");
        Assert.Equal("https://emissaoja.test/v1/servicos?ativos=todos", srv.Ultima.Url.AbsoluteUri);
        Assert.True(Assert.Single(lista.Servicos).Principal);

        var catalogo = await ej.Servicos.CatalogoAsync("consultoria", 10);
        Assert.Equal("https://emissaoja.test/v1/catalogo/servicos?q=consultoria&limite=10", srv.Ultima.Url.AbsoluteUri);
        Assert.Equal("17.01", Assert.Single(catalogo.Itens).ItemLc116);
    }
}
