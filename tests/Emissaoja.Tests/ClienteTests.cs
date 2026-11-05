using System.Net;
using System.Reflection;
using System.Text.Json;
using static Emissaoja.Tests.ServidorFalso;

namespace Emissaoja.Tests;

public class ClienteTests
{
    [Fact]
    public async Task Status_nao_manda_a_chave_e_manda_User_Agent_e_Accept()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"ok":true,"versao":"0.1.0","motor":"simulado","ambiente":"sandbox","tempo":"2026-11-05T12:00:00.000Z"}""");

        var s = await ej.StatusAsync();

        Assert.True(s.Ok);
        Assert.Equal("simulado", s.Motor);
        Assert.Equal("sandbox", s.Ambiente);
        var req = Assert.Single(srv.Requisicoes);
        Assert.Equal("GET", req.Metodo);
        Assert.Equal("https://emissaoja.test/v1/status", req.Url.AbsoluteUri);
        Assert.Null(req.Cabecalho("Authorization"));
        Assert.Equal("emissaoja-dotnet/" + EmissaojaClient.Versao, req.Cabecalho("User-Agent"));
        Assert.Equal("application/json", req.Cabecalho("Accept"));
        Assert.Null(req.Corpo);
        Assert.Null(req.TipoConteudo);
    }

    [Fact]
    public async Task Chamada_autenticada_manda_Bearer_e_le_a_empresa_tipada()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """
            {"usuario":{"email":"dev@exemplo.com.br"},
             "empresa":{"cnpj":"11111111000191","cnpj_formatado":"11.111.111/0001-91","nome":"MEI EXEMPLO","porte_simples":"mei","vinculo":"ativo"},
             "credencial":{"estado":"ativa","congelada_em":null,"tentativa_em_voo":null},
             "onboarding":{"estado":"pronto"},"importacao":null,"avisos":[],"aceite":{"pendente":false},
             "tempo":"2026-11-05T12:00:00.000Z","campo_novo":1}
            """);

        var r = await ej.Empresa.ObterAsync();

        Assert.Equal("11111111000191", r.Empresa?.Cnpj);
        Assert.Equal("mei", r.Empresa?.PorteSimples);
        Assert.Equal("ativa", r.Credencial.Estado);
        Assert.Equal("pronto", r.Onboarding.Estado);
        Assert.False(r.Aceite?.Pendente);
        Assert.Null(r.Importacao);
        Assert.Equal(JsonValueKind.Array, r.Avisos?.ValueKind);
        Assert.True(r.Extras!.ContainsKey("campo_novo"));

        var req = Assert.Single(srv.Requisicoes);
        Assert.Equal("Bearer " + Fabrica.ChaveTeste, req.Cabecalho("Authorization"));
        Assert.Equal("emissaoja-dotnet/" + EmissaojaClient.Versao, req.Cabecalho("User-Agent"));
        Assert.Null(req.TipoConteudo);
    }

    [Fact]
    public async Task Query_omite_parametros_nulos_e_vazios()
    {
        var (ej, srv, _) = Fabrica.Criar(new ServidorFalso(_ => Json(HttpStatusCode.OK, """{"tomadores":[],"total":0,"pagina":2,"limite":20}""")));

        await ej.Tomadores.ListarAsync(new FiltroTomadores { Busca = "", Pagina = 2, Limite = 20, Ordem = null });
        Assert.Equal("https://emissaoja.test/v1/tomadores?pagina=2&limite=20", srv.Ultima.Url.AbsoluteUri);

        await ej.Tomadores.ListarAsync();
        Assert.Equal("https://emissaoja.test/v1/tomadores", srv.Ultima.Url.AbsoluteUri);

        await ej.Tomadores.BuscarAsync("a b&c");
        Assert.Equal("https://emissaoja.test/v1/tomadores/busca?q=a%20b%26c", srv.Ultima.Url.AbsoluteUri);

        await ej.Tomadores.BuscarAsync("joão & cia");
        Assert.StartsWith("?q=", srv.Ultima.Url.Query);
        Assert.Equal("joão & cia", Uri.UnescapeDataString(srv.Ultima.Url.Query.Substring(3)));
    }

    [Fact]
    public async Task Segmentos_do_caminho_sao_codificados()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"tomador":{"tipo":"pj","id":"a/b c?","nome":"X"}}""");

        var r = await ej.Tomadores.ObterAsync("pj", "a/b c?");

        Assert.Equal("a/b c?", r.Tomador.Id);
        Assert.Equal("/v1/tomadores/pj/a%2Fb%20c%3F", srv.Ultima.Url.AbsolutePath);
    }

    [Fact]
    public async Task Segmento_nulo_ou_vazio_nao_chama_a_api()
    {
        var (ej, srv, _) = Fabrica.Criar();

        await Assert.ThrowsAsync<ArgumentException>(() => ej.Emissoes.ObterAsync(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ej.Emissoes.ObterAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => ej.Notas.AguardarAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => ej.Notas.PdfAsync("local", ""));

        Assert.Equal(0, srv.Total);
    }

    [Fact]
    public async Task Cnpj_consultar_tira_a_pontuacao()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"cnpj":"11222333000181","razao_social":"EMPRESA TESTE LTDA"}""");

        var doc = await ej.Cnpj.ConsultarAsync("11.222.333/0001-81");

        Assert.Equal("EMPRESA TESTE LTDA", doc.GetProperty("razao_social").GetString());
        Assert.Equal("/v1/cnpj/11222333000181", srv.Ultima.Url.AbsolutePath);

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Cnpj.ConsultarAsync("./-"));
        Assert.Equal("cnpj_invalido", e.Code);
        Assert.Equal(0, e.Status);
        Assert.Equal(1, srv.Total);
    }

    [Fact]
    public async Task Falha_de_rede_vira_code_rede_com_status_zero()
    {
        var (ej, srv, _) = Fabrica.Criar(new ServidorFalso(_ => throw new HttpRequestException("conexão recusada")));

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());

        Assert.Equal("rede", e.Code);
        Assert.Equal(0, e.Status);
        Assert.IsType<HttpRequestException>(e.InnerException);
        Assert.Equal(1, srv.Total);
    }

    [Fact]
    public async Task Sem_resposta_no_prazo_vira_tempo_esgotado()
    {
        var ej = new EmissaojaClient(
            new HttpClient(new ServidorLento()),
            new EmissaojaOptions { Chave = Fabrica.ChaveTeste, BaseUrl = Fabrica.Base, Timeout = TimeSpan.FromMilliseconds(50) });

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());

        Assert.Equal("tempo_esgotado", e.Code);
        Assert.Equal(0, e.Status);
    }

    [Fact]
    public async Task Cancelamento_de_quem_chama_nao_vira_tempo_esgotado()
    {
        var ej = new EmissaojaClient(new HttpClient(new ServidorLento()), new EmissaojaOptions { Chave = Fabrica.ChaveTeste, BaseUrl = Fabrica.Base });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ej.Empresa.ObterAsync(cts.Token));
    }

    [Fact]
    public async Task Resposta_2xx_que_nao_e_json_vira_resposta_invalida()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, "<html>proxy</html>", ("x-request-id", "r_proxy"));

        var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());

        Assert.Equal("resposta_invalida", e.Code);
        Assert.Equal(200, e.Status);
        Assert.Equal("r_proxy", e.RequestId);
    }

    [Fact]
    public async Task Corpo_vazio_em_2xx_vira_objeto_vazio()
    {
        var (ej, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, "")
           .Responder(HttpStatusCode.Accepted, "");

        var r = await ej.Servicos.DesativarAsync("s1");
        var fisco = await ej.Empresa.RelerFiscoAsync();

        Assert.Equal(string.Empty, r.Servico.Id);
        Assert.Equal(JsonValueKind.Object, fisco.ValueKind);
        Assert.Equal("DELETE", srv.Requisicoes.First().Metodo);
        Assert.Equal("POST", srv.Ultima.Metodo);
        Assert.Null(srv.Ultima.Corpo);
        Assert.Null(srv.Ultima.TipoConteudo);
    }

    [Theory]
    [InlineData(null, "https://emissaoja.com.br")]
    [InlineData("", "https://emissaoja.com.br")]
    [InlineData("https://sandbox.emissaoja.com.br", "https://sandbox.emissaoja.com.br")]
    [InlineData("https://sandbox.emissaoja.com.br/", "https://sandbox.emissaoja.com.br")]
    [InlineData("https://sandbox.emissaoja.com.br/v1/", "https://sandbox.emissaoja.com.br")]
    [InlineData("http://localhost:3000", "http://localhost:3000")]
    public void BaseUrl_normalizada(string? baseUrl, string esperado)
    {
        using var ej = new EmissaojaClient(new EmissaojaOptions { BaseUrl = baseUrl });

        Assert.Equal(esperado, ej.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), ej.Timeout);
    }

    [Fact]
    public void Opcoes_invalidas_falham_no_construtor()
    {
        Assert.Throws<ArgumentException>(() => new EmissaojaClient(new EmissaojaOptions { BaseUrl = "emissaoja.com.br" }));
        Assert.Throws<ArgumentException>(() => new EmissaojaClient(new EmissaojaOptions { BaseUrl = "ftp://emissaoja.com.br" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmissaojaClient(new EmissaojaOptions { Timeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentNullException>(() => new EmissaojaClient((EmissaojaOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new EmissaojaClient((HttpClient)null!));

        using var semLimite = new EmissaojaClient(new EmissaojaOptions { Timeout = System.Threading.Timeout.InfiniteTimeSpan });
        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, semLimite.Timeout);
    }

    [Fact]
    public void Versao_bate_com_a_do_pacote()
    {
        var informacional = typeof(EmissaojaClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.NotNull(informacional);
        Assert.StartsWith(EmissaojaClient.Versao, informacional);
    }

    private sealed class ServidorLento : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
