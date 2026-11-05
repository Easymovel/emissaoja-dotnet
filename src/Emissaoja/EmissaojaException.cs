using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Emissaoja;

/// <summary>
/// Erro da API do emissaoja (resposta fora de 2xx) ou do próprio cliente (rede, tempo esgotado, validação local).
/// Decida pelo <see cref="Code"/>, nunca pelo texto da mensagem.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Status"/>: status HTTP da resposta, ou <c>0</c> para erro local (<c>rede</c>, <c>tempo_esgotado</c>,
/// <c>chave_invalida</c> sem chave, <c>chave_idempotencia_invalida</c> com referência inválida).</item>
/// <item><see cref="Code"/>: código da lista fechada da API (<c>nota_bloqueada</c>, <c>limite_notas</c>,
/// <c>possivel_duplicada</c>, <c>rate_limited</c>...); <c>erro_desconhecido</c> quando a resposta não traz um;
/// <c>resposta_invalida</c> quando uma resposta de sucesso não é o JSON esperado.</item>
/// <item><see cref="Extras"/>: os demais campos do corpo do erro (<c>bloqueios</c>, <c>nota_igual_recente</c>,
/// <c>pedido_id</c>, <c>retry_after</c>, <c>details</c>...).</item>
/// <item><see cref="RequestId"/>: cabeçalho <c>x-request-id</c>; informe ao suporte.</item>
/// </list>
/// </remarks>
public class EmissaojaException : Exception
{
    private static readonly IReadOnlyDictionary<string, JsonElement> SemExtras = new Dictionary<string, JsonElement>();

    private IReadOnlyList<Bloqueio>? _bloqueios;

    /// <summary>Cria o erro.</summary>
    /// <param name="status">Status HTTP, ou <c>0</c> para erro local (rede, tempo esgotado, validação).</param>
    /// <param name="code">Código da lista fechada da API; vazio vira <c>erro_desconhecido</c>.</param>
    /// <param name="message">Mensagem em português.</param>
    /// <param name="extras">Demais campos do corpo do erro.</param>
    /// <param name="requestId">Valor do cabeçalho <c>x-request-id</c>.</param>
    /// <param name="innerException">Causa, se houver.</param>
    public EmissaojaException(
        int status,
        string code,
        string message,
        IReadOnlyDictionary<string, JsonElement>? extras = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        Code = code is { Length: > 0 } ? code : "erro_desconhecido";
        Extras = extras ?? SemExtras;
        RequestId = requestId;
    }

    /// <summary>Status HTTP da resposta, ou <c>0</c> para erro local (rede, tempo esgotado, validação antes de chamar a API).</summary>
    public int Status { get; }

    /// <summary>Código da lista fechada da API, por exemplo <c>nota_bloqueada</c> ou <c>limite_notas</c>.</summary>
    public string Code { get; }

    /// <summary>
    /// Campos do corpo do erro além de <c>status</c>, <c>code</c> e <c>message</c>: <c>bloqueios</c>,
    /// <c>nota_igual_recente</c>, <c>pedido_id</c>, <c>retry_after</c>, <c>details</c>... Vazio quando não há.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Extras { get; }

    /// <summary>Identificador da requisição (cabeçalho <c>x-request-id</c>), quando a API respondeu. Informe ao suporte.</summary>
    public string? RequestId { get; }

    /// <summary>
    /// Quanto esperar antes de tentar de novo (cabeçalho <c>Retry-After</c> ou campo <c>retry_after</c>), quando a API
    /// informa; aparece em <c>429 rate_limited</c>. O cliente não repete sozinho.
    /// </summary>
    public TimeSpan? RetryAfter { get; internal set; }

    /// <summary>
    /// O que impede a emissão (<c>422 nota_bloqueada</c>): cada item traz <c>Code</c>, <c>Mensagem</c> e <c>Acao</c>.
    /// Lista vazia nos outros erros.
    /// </summary>
    public IReadOnlyList<Bloqueio> Bloqueios => _bloqueios ??= LerBloqueios(Extras);

    /// <summary>
    /// A nota igual emitida hoje (<c>409 possivel_duplicada</c>). Se for mesmo outra nota, emita de novo com
    /// <see cref="CamposNota.PermitirRepetidaDe"/> igual a <see cref="Emissaoja.NotaIgualRecente.PedidoId"/>.
    /// <c>null</c> nos outros erros.
    /// </summary>
    public NotaIgualRecente? NotaIgualRecente => LerNotaIgual(Extras);

    /// <summary>
    /// Campo <c>pedido_id</c> do erro, quando houver (<c>emissao_repetida_recente</c>, <c>incerta_pendente</c>,
    /// <c>tempo_esgotado</c> de <see cref="NotasRecurso.AguardarAsync"/>...).
    /// </summary>
    public string? PedidoId => Texto(Extras, "pedido_id");

    private static IReadOnlyList<Bloqueio> LerBloqueios(IReadOnlyDictionary<string, JsonElement> extras)
    {
        if (!extras.TryGetValue("bloqueios", out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Bloqueio>();
        }

        var bloqueios = new List<Bloqueio>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var b = new Bloqueio();
            foreach (var campo in item.EnumerateObject())
            {
                switch (campo.Name)
                {
                    case "code":
                        b.Code = TextoDe(campo.Value) ?? string.Empty;
                        break;
                    case "mensagem":
                        b.Mensagem = TextoDe(campo.Value);
                        break;
                    case "acao":
                        b.Acao = TextoDe(campo.Value);
                        break;
                    default:
                        var outros = b.Extras ??= new Dictionary<string, JsonElement>();
                        outros[campo.Name] = campo.Value;
                        break;
                }
            }

            bloqueios.Add(b);
        }

        return bloqueios;
    }

    private static NotaIgualRecente? LerNotaIgual(IReadOnlyDictionary<string, JsonElement> extras)
    {
        if (!extras.TryGetValue("nota_igual_recente", out var n) || n.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new NotaIgualRecente
        {
            PedidoId = Texto(n, "pedido_id") ?? string.Empty,
            NumeroNfse = Texto(n, "numero_nfse"),
            EmitidaEm = Texto(n, "emitida_em"),
            Status = Texto(n, "status"),
        };
    }

    private static string? TextoDe(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static string? Texto(JsonElement objeto, string nome) =>
        objeto.TryGetProperty(nome, out var valor) ? TextoDe(valor) : null;

    private static string? Texto(IReadOnlyDictionary<string, JsonElement> extras, string nome) =>
        extras.TryGetValue(nome, out var valor) ? TextoDe(valor) : null;
}
