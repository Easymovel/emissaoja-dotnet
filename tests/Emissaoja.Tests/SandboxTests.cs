using System.Text;
using Xunit.Abstractions;

namespace Emissaoja.Tests;

/// <summary>
/// Só roda com EMISSAOJA_TEST_KEY (chave ej_test_ do sandbox); sem ela, o teste aparece como pulado (é o caso do CI).
/// EMISSAOJA_TEST_BASE_URL troca o endereço (padrão https://sandbox.emissaoja.com.br).
/// </summary>
public sealed class FatoSandboxAttribute : FactAttribute
{
    public FatoSandboxAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMISSAOJA_TEST_KEY")))
        {
            Skip = "Defina EMISSAOJA_TEST_KEY (chave ej_test_ do sandbox) para rodar contra o sandbox.";
        }
    }
}

public class SandboxTests
{
    private readonly ITestOutputHelper _saida;

    public SandboxTests(ITestOutputHelper saida) => _saida = saida;

    private static EmissaojaClient Cliente()
    {
        var baseUrl = Environment.GetEnvironmentVariable("EMISSAOJA_TEST_BASE_URL");
        return new EmissaojaClient(new EmissaojaOptions
        {
            Chave = Environment.GetEnvironmentVariable("EMISSAOJA_TEST_KEY"),
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? EmissaojaClient.BaseUrlSandbox : baseUrl,
            Timeout = TimeSpan.FromSeconds(60),
        });
    }

    [FatoSandbox]
    public async Task Conectar_emitir_aguardar_e_baixar_no_sandbox()
    {
        using var ej = Cliente();

        var status = await ej.StatusAsync();
        Assert.True(status.Ok);
        Assert.Equal("sandbox", status.Ambiente);

        // Empresa de exemplo: MEI pronto para emitir (no sandbox, qualquer senha serve, menos "errada")
        if ((await ej.Empresa.ObterAsync()).Empresa is null)
        {
            await ej.Empresa.ConectarAsync("11111111000191", "senha-do-sandbox");
            var credencial = await ej.Empresa.AguardarCredencialAsync();
            _saida.WriteLine("Credencial: " + credencial.Tentativa?.ProximoPasso);
        }

        for (var i = 0; i < 60 && (await ej.Empresa.ObterAsync()).Onboarding.Estado != "pronto"; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        var tomador = (await ej.Tomadores.CriarAsync("12345678000195")).Tomador;
        Assert.Equal(TipoTomador.Pj, tomador.Tipo);

        // Valor aleatório entre 10,01 e 10,89: nenhum é valor com significado no sandbox
        var campos = new CamposNota
        {
            TomadorTipo = tomador.Tipo,
            TomadorId = tomador.Id,
            Valor = 10m + (Random.Shared.Next(1, 90) / 100m),
            Descricao = "Teste do cliente .NET do emissaoja",
        };
        var referencia = "dotnet-" + Guid.NewGuid().ToString("N").Substring(0, 24);

        RespostaPedido emissao;
        try
        {
            emissao = await EmitirConfirmandoAsync(ej, referencia, campos);
        }
        catch (EmissaojaException e) when (e.Code == "limite_notas")
        {
            _saida.WriteLine("Limite de notas do plano no mês: a emissão não foi testada. " + e.Message);
            return;
        }

        // A mesma referência devolve o mesmo pedido e não emite de novo
        var repetida = await ej.Notas.EmitirAsync(referencia, campos);
        Assert.True(repetida.Repetido);
        Assert.Equal(emissao.Pedido.Id, repetida.Pedido.Id);

        var final = await ej.Notas.AguardarAsync(emissao.Pedido.Id);
        Assert.Equal(EstadoPedido.Autorizada, final.Pedido.Estado);
        var nota = final.Pedido.Nota;
        Assert.NotNull(nota);
        _saida.WriteLine($"Nota {final.Pedido.Numero} autorizada ({nota!.Origem}/{nota.Id}).");

        var pdf = await ej.Notas.PdfAsync(nota.Origem, nota.Id);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        var xml = await ej.Notas.XmlAsync(nota.Origem, nota.Id);
        Assert.Contains("<", xml);

        var pagina = await ej.Notas.ListarAsync(new FiltroNotas { Periodo = "mes_atual", Limite = 50 });
        Assert.NotEmpty(pagina.Notas);
    }

    [FatoSandbox]
    public async Task Catalogo_e_servicos_no_sandbox()
    {
        using var ej = Cliente();

        var catalogo = await ej.Servicos.CatalogoAsync("consultoria", 5);
        Assert.NotEmpty(catalogo.Itens);

        var servicos = await ej.Servicos.ListarAsync("todos");
        _saida.WriteLine("Serviços: " + servicos.Servicos.Count);
    }

    /// <summary>Emite tratando a nota em dobro como o README ensina: confirma e repete com a mesma referência.</summary>
    private static async Task<RespostaPedido> EmitirConfirmandoAsync(EmissaojaClient ej, string referencia, CamposNota campos)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            try
            {
                return await ej.Notas.EmitirAsync(referencia, campos);
            }
            catch (EmissaojaException e) when (tentativa < 2 && e.Code == "possivel_duplicada" && e.NotaIgualRecente is not null)
            {
                campos.PermitirRepetidaDe = e.NotaIgualRecente.PedidoId;
            }
            catch (EmissaojaException e) when (tentativa < 2 && e.Code == "emissao_repetida_recente")
            {
                campos.ConfirmoRepeticao = true;
            }
        }
    }
}
