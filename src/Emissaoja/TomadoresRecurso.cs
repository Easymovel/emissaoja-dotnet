using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Tomadores (<c>/v1/tomadores</c>): os clientes para quem a nota é emitida. Tomador não se apaga: notas emitidas
/// apontam para ele.
/// </summary>
public sealed class TomadoresRecurso
{
    private readonly EmissaojaClient _c;

    internal TomadoresRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>Lista paginada (<c>GET /v1/tomadores</c>).</summary>
    /// <param name="filtro">Busca, página, limite e ordem; <c>null</c> lista a primeira página.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os tomadores da página, com total, página e limite.</returns>
    public Task<RespostaTomadores> ListarAsync(FiltroTomadores? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new FiltroTomadores();
        var consulta = new[]
        {
            EmissaojaClient.Par("busca", f.Busca),
            EmissaojaClient.Par("pagina", f.Pagina),
            EmissaojaClient.Par("limite", f.Limite),
            EmissaojaClient.Par("ordem", f.Ordem),
        };
        return _c.JsonAsync(HttpMethod.Get, "/v1/tomadores", EmissaojaJsonContext.Default.RespostaTomadores, ct, consulta);
    }

    /// <summary>Busca rápida por nome ou documento, a partir de 2 caracteres (<c>GET /v1/tomadores/busca?q=</c>).</summary>
    /// <param name="q">Texto da busca.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>Os tomadores encontrados.</returns>
    public Task<RespostaTomadores> BuscarAsync(string q, CancellationToken ct = default)
    {
        if (q is null)
        {
            throw new ArgumentNullException(nameof(q));
        }

        var consulta = new[] { EmissaojaClient.Par("q", q) };
        return _c.JsonAsync(HttpMethod.Get, "/v1/tomadores/busca", EmissaojaJsonContext.Default.RespostaTomadores, ct, consulta);
    }

    /// <summary>
    /// Cria um tomador (<c>POST /v1/tomadores</c>): com CNPJ, nome e endereço vêm da Receita; com CPF, informe o nome.
    /// Se o documento já está cadastrado, devolve o existente com <see cref="RespostaTomador.JaExistia"/> <c>true</c>.
    /// </summary>
    /// <param name="documento">CNPJ ou CPF, com ou sem pontuação.</param>
    /// <param name="nome">Nome completo (obrigatório para CPF).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O tomador.</returns>
    public Task<RespostaTomador> CriarAsync(string documento, string? nome = null, CancellationToken ct = default)
    {
        if (documento is null)
        {
            throw new ArgumentNullException(nameof(documento));
        }

        var corpo = EmissaojaClient.Serializar(
            new CorpoTomador { Documento = documento, Nome = string.IsNullOrEmpty(nome) ? null : nome },
            EmissaojaJsonContext.Default.CorpoTomador);
        return _c.JsonAsync(HttpMethod.Post, "/v1/tomadores", EmissaojaJsonContext.Default.RespostaTomador, ct, corpo: corpo);
    }

    /// <summary>Ficha do tomador, com endereço e contato (<c>GET /v1/tomadores/{tipo}/{id}</c>).</summary>
    /// <param name="tipo"><c>pj</c> ou <c>pf</c> (<see cref="TipoTomador"/>).</param>
    /// <param name="id">Id do tomador.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O tomador.</returns>
    public Task<RespostaTomador> ObterAsync(string tipo, string id, CancellationToken ct = default)
    {
        var caminho = "/v1/tomadores/" + EmissaojaClient.Segmento(tipo, nameof(tipo)) + "/" + EmissaojaClient.Segmento(id, nameof(id));
        return _c.JsonAsync(HttpMethod.Get, caminho, EmissaojaJsonContext.Default.RespostaTomador, ct);
    }

    /// <summary>
    /// Edita endereço e contato (<c>PUT /v1/tomadores/{tipo}/{id}</c>, edição parcial). O documento é imutável.
    /// </summary>
    /// <param name="tipo"><c>pj</c> ou <c>pf</c> (<see cref="TipoTomador"/>).</param>
    /// <param name="id">Id do tomador.</param>
    /// <param name="campos">Só os campos preenchidos mudam.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O tomador atualizado.</returns>
    public Task<RespostaTomador> AtualizarAsync(string tipo, string id, CamposTomador campos, CancellationToken ct = default)
    {
        if (campos is null)
        {
            throw new ArgumentNullException(nameof(campos));
        }

        var caminho = "/v1/tomadores/" + EmissaojaClient.Segmento(tipo, nameof(tipo)) + "/" + EmissaojaClient.Segmento(id, nameof(id));
        var corpo = EmissaojaClient.Serializar(campos, EmissaojaJsonContext.Default.CamposTomador);
        return _c.JsonAsync(HttpMethod.Put, caminho, EmissaojaJsonContext.Default.RespostaTomador, ct, corpo: corpo);
    }
}
