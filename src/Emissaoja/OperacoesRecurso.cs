using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>Operações longas (<c>/v1/operacoes</c>), como o cancelamento: <c>processando</c> → <c>concluida</c> ou <c>falhou</c>.</summary>
public sealed class OperacoesRecurso
{
    private readonly EmissaojaClient _c;

    internal OperacoesRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>Estado atual da operação (<c>GET /v1/operacoes/{id}</c>).</summary>
    /// <param name="id">Id da operação.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A operação.</returns>
    public Task<RespostaOperacao> ObterAsync(string id, CancellationToken ct = default)
    {
        var caminho = "/v1/operacoes/" + EmissaojaClient.Segmento(id, nameof(id));
        return _c.JsonAsync(HttpMethod.Get, caminho, EmissaojaJsonContext.Default.RespostaOperacao, ct);
    }

    /// <summary>Consulta a operação até sair de <c>processando</c>.</summary>
    /// <param name="id">Id da operação.</param>
    /// <param name="intervalo">Espera entre consultas (padrão 2 s).</param>
    /// <param name="timeout">Tempo máximo (padrão 180 s).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A operação <c>concluida</c> ou <c>falhou</c> (veja <see cref="Operacao.Erro"/>).</returns>
    /// <exception cref="EmissaojaException"><c>tempo_esgotado</c> se a operação não terminar a tempo.</exception>
    public async Task<RespostaOperacao> AguardarAsync(string id, TimeSpan? intervalo = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        _ = EmissaojaClient.Segmento(id, nameof(id));
        var passo = intervalo ?? TimeSpan.FromSeconds(2);
        var limite = timeout ?? TimeSpan.FromSeconds(180);
        var inicio = _c.Relogio();
        while (true)
        {
            var r = await ObterAsync(id, ct).ConfigureAwait(false);
            if (r.Operacao?.Estado != "processando")
            {
                return r;
            }

            if (_c.Relogio() - inicio >= limite)
            {
                throw EmissaojaClient.TempoEsgotado("A operação não terminou a tempo; consulte de novo mais tarde.");
            }

            await _c.EsperarAsync(passo, ct).ConfigureAwait(false);
        }
    }
}
