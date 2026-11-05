using System.Text.Json.Serialization;

namespace Emissaoja
{
    /// <summary>
    /// Metadados de JSON gerados em compilação (sem reflexão: compatível com trimming e Native AOT).
    /// Na escrita, propriedades <c>null</c> ficam fora do corpo.
    /// </summary>
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(StatusServico))]
    [JsonSerializable(typeof(RespostaEmpresa))]
    [JsonSerializable(typeof(RespostaTentativa))]
    [JsonSerializable(typeof(RespostaCredencial))]
    [JsonSerializable(typeof(RespostaTomador))]
    [JsonSerializable(typeof(RespostaTomadores))]
    [JsonSerializable(typeof(RespostaServico))]
    [JsonSerializable(typeof(RespostaServicos))]
    [JsonSerializable(typeof(RespostaCatalogo))]
    [JsonSerializable(typeof(RespostaPedido))]
    [JsonSerializable(typeof(RespostaPedidos))]
    [JsonSerializable(typeof(RespostaNotas))]
    [JsonSerializable(typeof(RespostaOperacao))]
    [JsonSerializable(typeof(CamposNota))]
    [JsonSerializable(typeof(CamposServico))]
    [JsonSerializable(typeof(CamposTomador))]
    [JsonSerializable(typeof(CorpoCredencial))]
    [JsonSerializable(typeof(CorpoTomador))]
    [JsonSerializable(typeof(CorpoFiscal))]
    [JsonSerializable(typeof(CorpoCancelamento))]
    internal sealed partial class EmissaojaJsonContext : JsonSerializerContext
    {
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill para <c>record</c> e <c>init</c> no .NET Standard 2.0.</summary>
    internal static class IsExternalInit
    {
    }
}
#endif
