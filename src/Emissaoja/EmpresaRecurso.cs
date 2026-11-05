using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Empresa da conta (<c>/v1/empresa</c>): conectar com o CNPJ e a senha do Emissor Nacional, acompanhar a verificação,
/// o onboarding e responder as perguntas fiscais. Uma conta tem uma empresa.
/// </summary>
public sealed class EmpresaRecurso
{
    private readonly EmissaojaClient _c;

    internal EmpresaRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>A empresa, o estado da credencial e do onboarding, importação e avisos (<c>GET /v1/empresa</c>).</summary>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A empresa e o estado da configuração.</returns>
    public Task<RespostaEmpresa> ObterAsync(CancellationToken ct = default) =>
        _c.JsonAsync(HttpMethod.Get, "/v1/empresa", EmissaojaJsonContext.Default.RespostaEmpresa, ct);

    /// <summary>
    /// Conecta a empresa com o CNPJ e a senha do Emissor Nacional (<c>PUT /v1/empresa/credencial</c>, resposta <c>202</c>).
    /// A verificação segue em segundo plano: acompanhe com <see cref="AguardarCredencialAsync"/>. A senha nunca é
    /// guardada pelo cliente nem devolvida pela API.
    /// </summary>
    /// <param name="cnpj">CNPJ da empresa, só com números.</param>
    /// <param name="senha">Senha do Emissor Nacional (não é a conta gov.br).</param>
    /// <param name="cienteUltimaTentativa">
    /// <c>true</c> quando a API avisa que esta é a última tentativa antes do bloqueio do portal
    /// (<c>409 confirmar_ultima_tentativa</c>).
    /// </param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A tentativa iniciada.</returns>
    public Task<RespostaTentativa> ConectarAsync(string cnpj, string senha, bool cienteUltimaTentativa = false, CancellationToken ct = default)
    {
        if (cnpj is null)
        {
            throw new ArgumentNullException(nameof(cnpj));
        }

        if (senha is null)
        {
            throw new ArgumentNullException(nameof(senha));
        }

        var corpo = EmissaojaClient.Serializar(
            new CorpoCredencial { Cnpj = cnpj, Senha = senha, CienteUltimaTentativa = cienteUltimaTentativa },
            EmissaojaJsonContext.Default.CorpoCredencial);
        return _c.JsonAsync(HttpMethod.Put, "/v1/empresa/credencial", EmissaojaJsonContext.Default.RespostaTentativa, ct, corpo: corpo);
    }

    /// <summary>Estado da credencial e da tentativa em andamento ou da última (<c>GET /v1/empresa/credencial</c>).</summary>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A credencial, o onboarding e a tentativa.</returns>
    public Task<RespostaCredencial> CredencialAsync(CancellationToken ct = default) =>
        _c.JsonAsync(HttpMethod.Get, "/v1/empresa/credencial", EmissaojaJsonContext.Default.RespostaCredencial, ct);

    /// <summary>
    /// Consulta a credencial até <c>tentativa.proximo_passo</c> sair de <c>aguardar</c> (ou não haver tentativa).
    /// </summary>
    /// <param name="intervalo">Espera entre consultas (padrão 2 s).</param>
    /// <param name="timeout">Tempo máximo (padrão 120 s).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>A última resposta de <see cref="CredencialAsync"/>: veja <c>Tentativa.ProximoPasso</c>.</returns>
    /// <exception cref="EmissaojaException"><c>tempo_esgotado</c> se a verificação não terminar a tempo.</exception>
    public async Task<RespostaCredencial> AguardarCredencialAsync(TimeSpan? intervalo = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var passo = intervalo ?? TimeSpan.FromSeconds(2);
        var limite = timeout ?? TimeSpan.FromSeconds(120);
        var inicio = _c.Relogio();
        while (true)
        {
            var r = await CredencialAsync(ct).ConfigureAwait(false);
            if (r.Tentativa is null || r.Tentativa.ProximoPasso != "aguardar")
            {
                return r;
            }

            if (_c.Relogio() - inicio >= limite)
            {
                throw EmissaojaClient.TempoEsgotado("A verificação da senha não terminou a tempo.");
            }

            await _c.EsperarAsync(passo, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Estágios, perguntas pendentes e bloqueio do onboarding (<c>GET /v1/empresa/onboarding</c>).</summary>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O JSON como a API devolve.</returns>
    public Task<JsonElement> OnboardingAsync(CancellationToken ct = default) =>
        _c.ElementoAsync(HttpMethod.Get, "/v1/empresa/onboarding", ct);

    /// <summary>
    /// Responde as perguntas fiscais que o cadastro não responde sozinho (<c>PUT /v1/empresa/fiscal</c>).
    /// Informe ao menos um dos dois.
    /// </summary>
    /// <param name="aliquotaSimples">Alíquota do Simples Nacional em % (maior que 0 e até 33).</param>
    /// <param name="regApTribSn">Regime de apuração dos tributos do Simples Nacional (1, 2 ou 3).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O JSON como a API devolve.</returns>
    public Task<JsonElement> FiscalAsync(decimal? aliquotaSimples = null, int? regApTribSn = null, CancellationToken ct = default)
    {
        var corpo = EmissaojaClient.Serializar(
            new CorpoFiscal { AliquotaSimples = aliquotaSimples, RegApTribSn = regApTribSn },
            EmissaojaJsonContext.Default.CorpoFiscal);
        return _c.ElementoAsync(HttpMethod.Put, "/v1/empresa/fiscal", ct, corpo: corpo);
    }

    /// <summary>Lê o cadastro fiscal de novo no portal (<c>POST /v1/empresa/reler-fisco</c>, resposta <c>202</c>).</summary>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O JSON como a API devolve.</returns>
    public Task<JsonElement> RelerFiscoAsync(CancellationToken ct = default) =>
        _c.ElementoAsync(HttpMethod.Post, "/v1/empresa/reler-fisco", ct);
}
