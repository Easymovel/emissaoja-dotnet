using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Notas (<c>/v1/notas</c>): emitir, acompanhar o pedido, listar, baixar PDF (DANFSe) e XML e cancelar.
/// Nota não se altera: para corrigir, cancele e emita outra.
/// </summary>
public sealed class NotasRecurso
{
    private readonly EmissaojaClient _c;

    internal NotasRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>
    /// Emite uma nota: prévia e emissão num passo (<c>POST /v1/notas</c>, resposta <c>202</c> com o pedido).
    /// A <paramref name="referencia"/> vai no cabeçalho <c>Idempotency-Key</c>: repetir a mesma referência devolve o
    /// mesmo pedido (<see cref="RespostaPedido.Repetido"/> <c>true</c>) e nunca emite duas vezes.
    /// </summary>
    /// <param name="referencia">
    /// Um id seu, único por nota (por exemplo o id do pedido no seu sistema): 8 a 64 caracteres, só letras, números,
    /// <c>_</c> e <c>-</c>. Validada antes de chamar a API.
    /// </param>
    /// <param name="campos">Tomador, valor e os campos opcionais da nota.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O pedido (acompanhe com <see cref="AguardarAsync"/>) e a prévia.</returns>
    /// <exception cref="EmissaojaException">
    /// <c>chave_idempotencia_invalida</c> (status 0, sem chamar a API) com referência inválida; da API, por exemplo
    /// <c>422 nota_bloqueada</c> (veja <see cref="EmissaojaException.Bloqueios"/>), <c>402 limite_notas</c>,
    /// <c>409 possivel_duplicada</c> e <c>409 emissao_repetida_recente</c>.
    /// </exception>
    public async Task<RespostaPedido> EmitirAsync(string referencia, CamposNota campos, CancellationToken ct = default)
    {
        if (!ReferenciaValida(referencia))
        {
            throw new EmissaojaException(
                0,
                "chave_idempotencia_invalida",
                "Informe a referência da nota (8 a 64 caracteres: letras, números, _ e -), única por nota. Ela vai no cabeçalho Idempotency-Key.");
        }

        if (campos is null)
        {
            throw new ArgumentNullException(nameof(campos));
        }

        var corpo = EmissaojaClient.Serializar(campos, EmissaojaJsonContext.Default.CamposNota);
        return await _c.JsonAsync(
            HttpMethod.Post,
            "/v1/notas",
            EmissaojaJsonContext.Default.RespostaPedido,
            ct,
            corpo: corpo,
            idempotencia: referencia).ConfigureAwait(false);
    }

    /// <summary>
    /// Consulta o pedido (<see cref="EmissoesRecurso.ObterAsync"/>) até <c>autorizada</c>, <c>recusada</c> ou
    /// <c>incerta</c>, esperando entre as consultas o que a API pede em <c>proxima_consulta_ms</c> (3 s se não pedir),
    /// nunca menos que <paramref name="intervaloMinimo"/>.
    /// </summary>
    /// <param name="pedidoId">Id do pedido (<see cref="Pedido.Id"/>).</param>
    /// <param name="timeout">Tempo máximo (padrão 180 s).</param>
    /// <param name="intervaloMinimo">Espera mínima entre consultas (padrão 1 s).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O pedido no estado final.</returns>
    /// <exception cref="EmissaojaException">
    /// <c>tempo_esgotado</c> (status 0, com <see cref="EmissaojaException.PedidoId"/>) se o pedido não terminar a tempo;
    /// consulte de novo mais tarde, sem emitir outra vez.
    /// </exception>
    public async Task<RespostaPedido> AguardarAsync(string pedidoId, TimeSpan? timeout = null, TimeSpan? intervaloMinimo = null, CancellationToken ct = default)
    {
        _ = EmissaojaClient.Segmento(pedidoId, nameof(pedidoId));
        var limite = timeout ?? TimeSpan.FromSeconds(180);
        var minimo = intervaloMinimo ?? TimeSpan.FromSeconds(1);
        var inicio = _c.Relogio();
        while (true)
        {
            var r = await _c.Emissoes.ObterAsync(pedidoId, ct).ConfigureAwait(false);
            if (r.Pedido is not null && r.Pedido.Terminal)
            {
                return r;
            }

            if (_c.Relogio() - inicio >= limite)
            {
                throw EmissaojaClient.TempoEsgotado("O pedido não terminou a tempo; consulte de novo mais tarde, sem emitir outra vez.", pedidoId);
            }

            var ms = r.Pedido?.ProximaConsultaMs;
            var sugerido = ms.HasValue && ms.Value > 0 ? TimeSpan.FromMilliseconds(ms.Value) : TimeSpan.FromSeconds(3);
            await _c.EsperarAsync(sugerido > minimo ? sugerido : minimo, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Lista as notas, locais e importadas, paginadas por cursor (<c>GET /v1/notas</c>).</summary>
    /// <param name="filtro">Período, situação, busca, cursor e limite; <c>null</c> usa o padrão da API (últimos 12 meses).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A página de notas e o cursor da próxima.</returns>
    public Task<RespostaNotas> ListarAsync(FiltroNotas? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new FiltroNotas();
        var consulta = new[]
        {
            EmissaojaClient.Par("periodo", f.Periodo),
            EmissaojaClient.Par("de", f.De),
            EmissaojaClient.Par("ate", f.Ate),
            EmissaojaClient.Par("situacao", f.Situacao),
            EmissaojaClient.Par("busca", f.Busca),
            EmissaojaClient.Par("cursor", f.Cursor),
            EmissaojaClient.Par("limite", f.Limite),
        };
        return _c.JsonAsync(HttpMethod.Get, "/v1/notas", EmissaojaJsonContext.Default.RespostaNotas, ct, consulta);
    }

    /// <summary>PDF da nota, o DANFSe (<c>GET /v1/notas/{origem}/{id}/pdf</c>).</summary>
    /// <param name="origem"><c>local</c> ou <c>importada</c> (<see cref="RefNota.Origem"/>).</param>
    /// <param name="id">Id da nota (<see cref="RefNota.Id"/>).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os bytes do PDF.</returns>
    public Task<byte[]> PdfAsync(string origem, string id, CancellationToken ct = default) =>
        _c.BytesAsync(CaminhoNota(origem, id, "pdf"), ct);

    /// <summary>XML da nota (<c>GET /v1/notas/{origem}/{id}/xml</c>).</summary>
    /// <param name="origem"><c>local</c> ou <c>importada</c> (<see cref="RefNota.Origem"/>).</param>
    /// <param name="id">Id da nota (<see cref="RefNota.Id"/>).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O XML, decodificado em UTF-8.</returns>
    public async Task<string> XmlAsync(string origem, string id, CancellationToken ct = default)
    {
        var bytes = await _c.BytesAsync(CaminhoNota(origem, id, "xml"), ct).ConfigureAwait(false);
        return EmissaojaClient.Utf8SemBom(bytes);
    }

    /// <summary>
    /// Pede o cancelamento da nota (<c>POST /v1/notas/{origem}/{id}/cancelar</c>, resposta <c>202</c> com a operação).
    /// Acompanhe com <see cref="OperacoesRecurso.AguardarAsync"/>.
    /// </summary>
    /// <param name="origem"><c>local</c> ou <c>importada</c>.</param>
    /// <param name="id">Id da nota.</param>
    /// <param name="motivo"><c>"1"</c> (erro na emissão) ou <c>"2"</c> (serviço não prestado); veja <see cref="MotivoCancelamento"/>.</param>
    /// <param name="justificativa">Texto opcional (15 a 255 caracteres).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A operação de cancelamento.</returns>
    public Task<RespostaOperacao> CancelarAsync(string origem, string id, string motivo, string? justificativa = null, CancellationToken ct = default)
    {
        if (motivo is null)
        {
            throw new ArgumentNullException(nameof(motivo));
        }

        var caminho = CaminhoNota(origem, id, "cancelar");
        var corpo = EmissaojaClient.Serializar(
            new CorpoCancelamento { Motivo = motivo, Justificativa = string.IsNullOrEmpty(justificativa) ? null : justificativa },
            EmissaojaJsonContext.Default.CorpoCancelamento);
        return _c.JsonAsync(HttpMethod.Post, caminho, EmissaojaJsonContext.Default.RespostaOperacao, ct, corpo: corpo);
    }

    /// <summary><c>true</c> se a referência tem 8 a 64 caracteres, só letras ASCII, números, <c>_</c> e <c>-</c>.</summary>
    internal static bool ReferenciaValida(string? referencia)
    {
        if (referencia is null || referencia.Length < 8 || referencia.Length > 64)
        {
            return false;
        }

        foreach (var c in referencia)
        {
            var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static string CaminhoNota(string origem, string id, string acao) =>
        "/v1/notas/" + EmissaojaClient.Segmento(origem, nameof(origem)) + "/" + EmissaojaClient.Segmento(id, nameof(id)) + "/" + acao;
}
