using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Pedidos de emissão (<c>/v1/emissoes</c>): acompanhar até <c>autorizada</c>, <c>recusada</c> ou <c>incerta</c>,
/// listar os em andamento e conferir no portal os incertos.
/// </summary>
public sealed class EmissoesRecurso
{
    private readonly EmissaojaClient _c;

    internal EmissoesRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>
    /// Estado atual do pedido (<c>GET /v1/emissoes/{id}</c>). Para esperar o fim, use <see cref="NotasRecurso.AguardarAsync"/>.
    /// </summary>
    /// <param name="id">Id do pedido.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O pedido.</returns>
    public Task<RespostaPedido> ObterAsync(string id, CancellationToken ct = default)
    {
        var caminho = "/v1/emissoes/" + EmissaojaClient.Segmento(id, nameof(id));
        return _c.JsonAsync(HttpMethod.Get, caminho, EmissaojaJsonContext.Default.RespostaPedido, ct);
    }

    /// <summary>Pedidos em andamento, ou nos estados pedidos (<c>GET /v1/emissoes?estados=</c>).</summary>
    /// <param name="estados">Estados separados por vírgula (por exemplo <c>"incerta"</c> ou <c>"na_fila,emitindo"</c>); <c>null</c>: os em andamento.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os pedidos.</returns>
    public Task<RespostaPedidos> ListarAsync(string? estados = null, CancellationToken ct = default)
    {
        var consulta = new[] { EmissaojaClient.Par("estados", estados) };
        return _c.JsonAsync(HttpMethod.Get, "/v1/emissoes", EmissaojaJsonContext.Default.RespostaPedidos, ct, consulta);
    }

    /// <summary>
    /// Confere no portal um pedido <c>incerta</c> (<c>POST /v1/emissoes/{id}/conferir</c>, resposta <c>202</c>).
    /// Depois acompanhe com <see cref="NotasRecurso.AguardarAsync"/>.
    /// </summary>
    /// <param name="id">Id do pedido.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O pedido.</returns>
    public Task<RespostaPedido> ConferirAsync(string id, CancellationToken ct = default)
    {
        var caminho = "/v1/emissoes/" + EmissaojaClient.Segmento(id, nameof(id)) + "/conferir";
        return _c.JsonAsync(HttpMethod.Post, caminho, EmissaojaJsonContext.Default.RespostaPedido, ct);
    }
}
