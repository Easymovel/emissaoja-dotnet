using System.Net;
using static Emissaoja.Tests.ServidorFalso;

namespace Emissaoja.Tests;

/// <summary>
/// Testes que mexem na variável de ambiente EMISSAOJA_KEY. Ficam numa classe só (o xUnit roda os testes de uma classe
/// em sequência) e devolvem o valor original no fim. Os demais testes sempre passam a chave nas opções.
/// </summary>
public class ChaveTests
{
    [Fact]
    public async Task Sem_chave_e_sem_variavel_lanca_chave_invalida_sem_chamar_a_api()
    {
        var anterior = Environment.GetEnvironmentVariable(EmissaojaClient.VariavelChave);
        Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, null);
        try
        {
            var srv = new ServidorFalso();
            var ej = new EmissaojaClient(new HttpClient(srv), new EmissaojaOptions { BaseUrl = Fabrica.Base });

            var e = await Assert.ThrowsAsync<EmissaojaException>(() => ej.Empresa.ObterAsync());

            Assert.Equal("chave_invalida", e.Code);
            Assert.Equal(0, e.Status);
            Assert.Contains("EMISSAOJA_KEY", e.Message);
            Assert.Contains("https://emissaoja.com.br/?origem=lib-dotnet", e.Message);
            Assert.Equal(0, srv.Total);

            // status não precisa de chave
            srv.Responder(HttpStatusCode.OK, """{"ok":true,"ambiente":"producao"}""");
            Assert.True((await ej.StatusAsync()).Ok);
            Assert.Null(srv.Ultima.Cabecalho("Authorization"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, anterior);
        }
    }

    [Fact]
    public async Task Chave_vem_da_variavel_na_primeira_chamada_que_precisa_dela()
    {
        var anterior = Environment.GetEnvironmentVariable(EmissaojaClient.VariavelChave);
        Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, null);
        try
        {
            var srv = new ServidorFalso(_ => Json(HttpStatusCode.OK, "{}"));

            // criado sem chave e sem a variável: a leitura fica para a primeira chamada
            var ej = new EmissaojaClient(new HttpClient(srv), new EmissaojaOptions { BaseUrl = Fabrica.Base, Chave = "  " });
            Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, " ej_test_do_ambiente ");
            await ej.Empresa.ObterAsync();

            // lida uma vez: trocar a variável depois não muda o cliente
            Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, "ej_test_outra");
            await ej.Tomadores.ListarAsync();

            Assert.Equal(2, srv.Total);
            Assert.All(srv.Requisicoes, r => Assert.Equal("Bearer ej_test_do_ambiente", r.Cabecalho("Authorization")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, anterior);
        }
    }

    [Fact]
    public async Task Chave_das_opcoes_vale_mais_que_a_variavel()
    {
        var anterior = Environment.GetEnvironmentVariable(EmissaojaClient.VariavelChave);
        Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, "ej_test_da_variavel");
        try
        {
            var srv = new ServidorFalso(_ => Json(HttpStatusCode.OK, "{}"));
            var ej = new EmissaojaClient(new HttpClient(srv), new EmissaojaOptions { BaseUrl = Fabrica.Base, Chave = "ej_test_das_opcoes" });

            await ej.Empresa.ObterAsync();

            Assert.Equal("Bearer ej_test_das_opcoes", srv.Ultima.Cabecalho("Authorization"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EmissaojaClient.VariavelChave, anterior);
        }
    }
}
