using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Serviços da empresa (<c>/v1/servicos</c>) e catálogo nacional de códigos de serviço (<c>/v1/catalogo/servicos</c>).
/// </summary>
public sealed class ServicosRecurso
{
    private readonly EmissaojaClient _c;

    internal ServicosRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>Lista os serviços (<c>GET /v1/servicos</c>).</summary>
    /// <param name="ativos"><c>"1"</c> (ativos), <c>"0"</c> (inativos) ou <c>"todos"</c>; <c>null</c> usa o padrão da API.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os serviços.</returns>
    public Task<RespostaServicos> ListarAsync(string? ativos = null, CancellationToken ct = default)
    {
        var consulta = new[] { EmissaojaClient.Par("ativos", ativos) };
        return _c.JsonAsync(HttpMethod.Get, "/v1/servicos", EmissaojaJsonContext.Default.RespostaServicos, ct, consulta);
    }

    /// <summary>
    /// Cria um serviço (<c>POST /v1/servicos</c>). Obrigatórios: descrição, <c>c_trib_nac</c>, <c>trib_issqn</c> e <c>retem_iss</c>.
    /// </summary>
    /// <param name="campos">Campos do serviço.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O serviço criado.</returns>
    public Task<RespostaServico> CriarAsync(CamposServico campos, CancellationToken ct = default)
    {
        if (campos is null)
        {
            throw new ArgumentNullException(nameof(campos));
        }

        var corpo = EmissaojaClient.Serializar(campos, EmissaojaJsonContext.Default.CamposServico);
        return _c.JsonAsync(HttpMethod.Post, "/v1/servicos", EmissaojaJsonContext.Default.RespostaServico, ct, corpo: corpo);
    }

    /// <summary>Edita um serviço (<c>PUT /v1/servicos/{id}</c>, edição parcial).</summary>
    /// <param name="id">Id do serviço.</param>
    /// <param name="campos">Só os campos preenchidos mudam.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O serviço atualizado.</returns>
    public Task<RespostaServico> AtualizarAsync(string id, CamposServico campos, CancellationToken ct = default)
    {
        if (campos is null)
        {
            throw new ArgumentNullException(nameof(campos));
        }

        var caminho = "/v1/servicos/" + EmissaojaClient.Segmento(id, nameof(id));
        var corpo = EmissaojaClient.Serializar(campos, EmissaojaJsonContext.Default.CamposServico);
        return _c.JsonAsync(HttpMethod.Put, caminho, EmissaojaJsonContext.Default.RespostaServico, ct, corpo: corpo);
    }

    /// <summary>
    /// Desativa um serviço (<c>DELETE /v1/servicos/{id}</c>). Notas emitidas continuam apontando para ele; o último
    /// serviço ativo não pode ser desativado (<c>409 ultimo_servico_ativo</c>).
    /// </summary>
    /// <param name="id">Id do serviço.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O serviço desativado.</returns>
    public Task<RespostaServico> DesativarAsync(string id, CancellationToken ct = default)
    {
        var caminho = "/v1/servicos/" + EmissaojaClient.Segmento(id, nameof(id));
        return _c.JsonAsync(HttpMethod.Delete, caminho, EmissaojaJsonContext.Default.RespostaServico, ct);
    }

    /// <summary>
    /// Busca no catálogo nacional de códigos de serviço por texto (<c>GET /v1/catalogo/servicos?q=&amp;limite=</c>).
    /// </summary>
    /// <param name="q">Texto da busca (2 caracteres ou mais), por exemplo <c>"consultoria"</c>.</param>
    /// <param name="limite">Máximo de itens (até 50); <c>null</c> usa o padrão da API.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os itens do catálogo.</returns>
    public Task<RespostaCatalogo> CatalogoAsync(string q, int? limite = null, CancellationToken ct = default)
    {
        if (q is null)
        {
            throw new ArgumentNullException(nameof(q));
        }

        var consulta = new[] { EmissaojaClient.Par("q", q), EmissaojaClient.Par("limite", limite) };
        return _c.JsonAsync(HttpMethod.Get, "/v1/catalogo/servicos", EmissaojaJsonContext.Default.RespostaCatalogo, ct, consulta);
    }
}
