using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>
/// Cliente da API do emissaoja (https://emissaoja.com.br/docs/): emissão de NFS-e no Emissor Nacional sem certificado
/// digital, para MEI e empresas do Simples Nacional.
/// </summary>
/// <remarks>
/// <para>
/// Recursos: <see cref="Empresa"/>, <see cref="Tomadores"/>, <see cref="Servicos"/>, <see cref="Notas"/>,
/// <see cref="Emissoes"/>, <see cref="Operacoes"/> e <see cref="Cnpj"/>, além de <see cref="StatusAsync"/>.
/// Erros da API viram <see cref="EmissaojaException"/> com <see cref="EmissaojaException.Code"/>.
/// </para>
/// <para>
/// A chave de API é secreta: use este cliente só no servidor. Thread-safe: crie uma instância e reaproveite
/// (no ASP.NET Core, registre como singleton ou como cliente tipado do <c>IHttpClientFactory</c>).
/// </para>
/// <para>
/// O cliente não repete chamadas sozinho: emitir tem efeito fiscal. Para repetir uma emissão com segurança, chame
/// <see cref="NotasRecurso.EmitirAsync"/> de novo com a mesma referência.
/// </para>
/// </remarks>
public sealed class EmissaojaClient : IDisposable
{
    /// <summary>URL base de produção.</summary>
    public const string BaseUrlPadrao = "https://emissaoja.com.br";

    /// <summary>URL base do sandbox: Emissor Nacional simulado, nada vai ao fisco e nada é cobrado (chaves <c>ej_test_</c>).</summary>
    public const string BaseUrlSandbox = "https://sandbox.emissaoja.com.br";

    /// <summary>Versão deste pacote (vai no <c>User-Agent</c>).</summary>
    public const string Versao = "0.1.0";

    /// <summary>Variável de ambiente lida quando a chave não é informada nas opções.</summary>
    public const string VariavelChave = "EMISSAOJA_KEY";

    private const string UserAgent = "emissaoja-dotnet/" + Versao;
    private const string UrlCadastro = "https://emissaoja.com.br/?origem=lib-dotnet";

    private static readonly Stopwatch Cronometro = Stopwatch.StartNew();

    private readonly HttpClient _http;
    private readonly bool _httpProprio;
    private string? _chave;

    /// <summary>
    /// Cria o cliente com as opções padrão: produção, chave lida de <c>EMISSAOJA_KEY</c> e tempo máximo de 30 s por chamada.
    /// </summary>
    public EmissaojaClient()
        : this(new EmissaojaOptions(), (HttpClient?)null)
    {
    }

    /// <summary>Cria o cliente com um <see cref="HttpClient"/> próprio, descartado em <see cref="Dispose"/>.</summary>
    /// <param name="opcoes">Chave, URL base e tempo máximo de cada chamada.</param>
    /// <exception cref="ArgumentNullException"><paramref name="opcoes"/> é <c>null</c>.</exception>
    /// <exception cref="ArgumentException">URL base inválida ou tempo máximo fora do intervalo.</exception>
    public EmissaojaClient(EmissaojaOptions opcoes)
        : this(opcoes, (HttpClient?)null)
    {
    }

    /// <summary>
    /// Cria o cliente sobre um <see cref="HttpClient"/> externo (por exemplo, o do <c>IHttpClientFactory</c>).
    /// O cliente não altera <c>BaseAddress</c> nem os cabeçalhos padrão do <see cref="HttpClient"/> recebido e não o descarta.
    /// </summary>
    /// <param name="http">O <see cref="HttpClient"/> a usar. O <c>Timeout</c> dele também vale.</param>
    /// <param name="opcoes">Chave, URL base e tempo máximo de cada chamada; <c>null</c> usa o padrão.</param>
    /// <exception cref="ArgumentNullException"><paramref name="http"/> é <c>null</c>.</exception>
    /// <exception cref="ArgumentException">URL base inválida ou tempo máximo fora do intervalo.</exception>
    public EmissaojaClient(HttpClient http, EmissaojaOptions? opcoes = null)
        : this(opcoes ?? new EmissaojaOptions(), http ?? throw new ArgumentNullException(nameof(http)))
    {
    }

    private EmissaojaClient(EmissaojaOptions? opcoes, HttpClient? http)
    {
        if (opcoes is null)
        {
            throw new ArgumentNullException(nameof(opcoes));
        }

        BaseUrl = NormalizarBaseUrl(opcoes.BaseUrl);
        Timeout = ValidarTimeout(opcoes.Timeout);
        var chave = opcoes.Chave?.Trim();
        _chave = string.IsNullOrEmpty(chave) ? null : chave;

        _httpProprio = http is null;
        _http = http ?? new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        Empresa = new EmpresaRecurso(this);
        Tomadores = new TomadoresRecurso(this);
        Servicos = new ServicosRecurso(this);
        Notas = new NotasRecurso(this);
        Emissoes = new EmissoesRecurso(this);
        Operacoes = new OperacoesRecurso(this);
        Cnpj = new CnpjRecurso(this);
    }

    /// <summary>URL base em uso, sem barra e sem <c>/v1</c> no fim.</summary>
    public string BaseUrl { get; }

    /// <summary>Tempo máximo de cada chamada HTTP.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Empresa da conta: conectar com CNPJ e senha do Emissor Nacional, credencial, onboarding e dados fiscais.</summary>
    public EmpresaRecurso Empresa { get; }

    /// <summary>Tomadores (clientes da nota): listar, buscar, criar por CNPJ ou CPF, ler e editar.</summary>
    public TomadoresRecurso Tomadores { get; }

    /// <summary>Serviços da empresa e catálogo nacional de códigos de serviço.</summary>
    public ServicosRecurso Servicos { get; }

    /// <summary>Notas: emitir, aguardar, listar, PDF, XML e cancelar.</summary>
    public NotasRecurso Notas { get; }

    /// <summary>Pedidos de emissão: acompanhar, listar os em andamento e conferir os incertos.</summary>
    public EmissoesRecurso Emissoes { get; }

    /// <summary>Operações longas (cancelamento): acompanhar até concluir.</summary>
    public OperacoesRecurso Operacoes { get; }

    /// <summary>Consulta de CNPJ na base da Receita Federal.</summary>
    public CnpjRecurso Cnpj { get; }

    /// <summary>Espera entre consultas dos métodos <c>Aguardar...Async</c> (trocada nos testes para não dormir).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Esperar { get; set; } = (t, ct) => Task.Delay(t, ct);

    /// <summary>Relógio monotônico dos métodos <c>Aguardar...Async</c> (trocado nos testes).</summary>
    internal Func<TimeSpan> Relogio { get; set; } = () => Cronometro.Elapsed;

    /// <summary>Status do serviço (<c>GET /v1/status</c>, sem chave): versão, motor e ambiente.</summary>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O status do serviço.</returns>
    public Task<StatusServico> StatusAsync(CancellationToken ct = default) =>
        JsonAsync(HttpMethod.Get, "/v1/status", EmissaojaJsonContext.Default.StatusServico, ct, autenticar: false);

    /// <summary>Descarta o <see cref="HttpClient"/> interno, se foi o cliente que o criou.</summary>
    public void Dispose()
    {
        if (_httpProprio)
        {
            _http.Dispose();
        }
    }

    // ------------------------------------------------------------------ uso interno dos recursos

    /// <summary>Chamada que devolve JSON tipado. Resposta fora de 2xx vira <see cref="EmissaojaException"/>.</summary>
    internal async Task<T> JsonAsync<T>(
        HttpMethod metodo,
        string caminho,
        JsonTypeInfo<T> tipo,
        CancellationToken ct,
        IEnumerable<KeyValuePair<string, string?>>? consulta = null,
        byte[]? corpo = null,
        string? idempotencia = null,
        bool autenticar = true)
        where T : class, new()
    {
        var r = await EnviarAsync(metodo, caminho, ct, consulta, corpo, idempotencia, autenticar).ConfigureAwait(false);
        if (!r.Sucesso)
        {
            throw Erro(r);
        }

        var texto = r.Texto();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize(texto, tipo) ?? new T();
        }
        catch (JsonException e)
        {
            throw RespostaInvalida(r, e);
        }
    }

    /// <summary>Chamada que devolve o JSON solto (<see cref="JsonElement"/> independente, sem nada para descartar).</summary>
    internal async Task<JsonElement> ElementoAsync(
        HttpMethod metodo,
        string caminho,
        CancellationToken ct,
        IEnumerable<KeyValuePair<string, string?>>? consulta = null,
        byte[]? corpo = null)
    {
        var r = await EnviarAsync(metodo, caminho, ct, consulta, corpo).ConfigureAwait(false);
        if (!r.Sucesso)
        {
            throw Erro(r);
        }

        var texto = r.Texto();
        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = "{}";
        }

        try
        {
            using var doc = JsonDocument.Parse(texto);
            return doc.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw RespostaInvalida(r, e);
        }
    }

    /// <summary>Chamada que devolve os bytes do corpo (PDF, XML).</summary>
    internal async Task<byte[]> BytesAsync(string caminho, CancellationToken ct)
    {
        var r = await EnviarAsync(HttpMethod.Get, caminho, ct).ConfigureAwait(false);
        if (!r.Sucesso)
        {
            throw Erro(r);
        }

        return r.Corpo;
    }

    internal static byte[] Serializar<T>(T valor, JsonTypeInfo<T> tipo) =>
        JsonSerializer.SerializeToUtf8Bytes(valor, tipo);

    /// <summary>Segmento de caminho codificado; recusa nulo ou vazio antes de chamar a API.</summary>
    internal static string Segmento(string valor, string nome)
    {
        if (valor is null)
        {
            throw new ArgumentNullException(nome);
        }

        if (valor.Trim().Length == 0)
        {
            throw new ArgumentException("O valor não pode ser vazio.", nome);
        }

        return Uri.EscapeDataString(valor);
    }

    internal static KeyValuePair<string, string?> Par(string nome, string? valor) =>
        new KeyValuePair<string, string?>(nome, valor);

    internal static KeyValuePair<string, string?> Par(string nome, int? valor) =>
        new KeyValuePair<string, string?>(nome, valor.HasValue ? valor.Value.ToString(CultureInfo.InvariantCulture) : null);

    internal static string Utf8SemBom(byte[] bytes)
    {
        var inicio = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, inicio, bytes.Length - inicio);
    }

    /// <summary>Erro local <c>tempo_esgotado</c> dos métodos <c>Aguardar...Async</c> (com <c>pedido_id</c> nos extras, quando houver).</summary>
    internal static EmissaojaException TempoEsgotado(string mensagem, string? pedidoId = null)
    {
        Dictionary<string, JsonElement>? extras = null;
        if (pedidoId is not null)
        {
            extras = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["pedido_id"] = JsonSerializer.SerializeToElement(pedidoId, EmissaojaJsonContext.Default.String),
            };
        }

        return new EmissaojaException(0, "tempo_esgotado", mensagem, extras);
    }

    /// <summary>Espera não negativa (o que vier negativo vira zero).</summary>
    internal Task EsperarAsync(TimeSpan tempo, CancellationToken ct) =>
        Esperar(tempo < TimeSpan.Zero ? TimeSpan.Zero : tempo, ct);

    // ------------------------------------------------------------------ HTTP

    private async Task<Resposta> EnviarAsync(
        HttpMethod metodo,
        string caminho,
        CancellationToken ct,
        IEnumerable<KeyValuePair<string, string?>>? consulta = null,
        byte[]? corpo = null,
        string? idempotencia = null,
        bool autenticar = true)
    {
        var chave = autenticar ? ObterChave() : null;

        using var req = new HttpRequestMessage(metodo, MontarUrl(caminho, consulta));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (chave is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        }

        if (idempotencia is not null)
        {
            req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencia);
        }

        if (corpo is not null)
        {
            var conteudo = new ByteArrayContent(corpo);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            req.Content = conteudo;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            cts.CancelAfter(Timeout);
        }

        try
        {
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var bytes = await res.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
#else
            var bytes = res.Content is null ? Array.Empty<byte>() : await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
            return new Resposta((int)res.StatusCode, bytes ?? Array.Empty<byte>(), Cabecalho(res.Headers, "x-request-id"), LerRetryAfter(res.Headers));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException e)
        {
            throw new EmissaojaException(0, "tempo_esgotado", "A API não respondeu em " + Segundos(Timeout) + " s.", innerException: e);
        }
        catch (HttpRequestException e)
        {
            throw new EmissaojaException(0, "rede", "Falha de rede ao chamar a API: " + e.Message, innerException: e);
        }
    }

    private string ObterChave()
    {
        var chave = _chave;
        if (chave is not null)
        {
            return chave;
        }

        var ambiente = Environment.GetEnvironmentVariable(VariavelChave)?.Trim();
        if (ambiente is null || ambiente.Length == 0)
        {
            throw new EmissaojaException(
                0,
                "chave_invalida",
                "Informe a chave: new EmissaojaClient(new EmissaojaOptions { Chave = \"ej_live_...\" }) ou a variável de ambiente "
                + VariavelChave + ". A chave é criada no app, em Conta → API. Ainda não tem conta? " + UrlCadastro);
        }

        _chave = ambiente;
        return ambiente;
    }

    private string MontarUrl(string caminho, IEnumerable<KeyValuePair<string, string?>>? consulta)
    {
        var url = new StringBuilder(BaseUrl).Append(caminho);
        if (consulta is not null)
        {
            var separador = '?';
            foreach (var par in consulta)
            {
                var valor = par.Value;
                if (valor is null || valor.Length == 0)
                {
                    continue;
                }

                url.Append(separador).Append(Uri.EscapeDataString(par.Key)).Append('=').Append(Uri.EscapeDataString(valor));
                separador = '&';
            }
        }

        return url.ToString();
    }

    private static EmissaojaException Erro(Resposta r)
    {
        var texto = r.Texto();
        string? code = null;
        string? mensagem = null;
        Dictionary<string, JsonElement>? extras = null;
        var objeto = false;

        if (!string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                using var doc = JsonDocument.Parse(texto);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    objeto = true;
                    var campos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var campo in doc.RootElement.EnumerateObject())
                    {
                        switch (campo.Name)
                        {
                            case "code":
                                code = campo.Value.ValueKind == JsonValueKind.String ? campo.Value.GetString() : null;
                                break;
                            case "message":
                                mensagem = campo.Value.ValueKind == JsonValueKind.String ? campo.Value.GetString() : null;
                                break;
                            case "status":
                                break;
                            default:
                                campos[campo.Name] = campo.Value.Clone();
                                break;
                        }
                    }

                    extras = campos;
                }
            }
            catch (JsonException)
            {
                // corpo não é JSON: a mensagem vira o começo do texto
            }
        }

        if (mensagem is null || mensagem.Length == 0)
        {
            mensagem = objeto ? Http(r.Status) : Resumo(texto) ?? Http(r.Status);
        }

        return new EmissaojaException(r.Status, code ?? string.Empty, mensagem, extras, r.RequestId)
        {
            RetryAfter = r.RetryAfter ?? RetryAfterDoCorpo(extras),
        };
    }

    private static EmissaojaException RespostaInvalida(Resposta r, Exception causa) =>
        new EmissaojaException(r.Status, "resposta_invalida", "Resposta inesperada da API (não é o JSON esperado).", null, r.RequestId, causa);

    private static string Http(int status) => "HTTP " + status.ToString(CultureInfo.InvariantCulture);

    /// <summary>Até 200 caracteres do corpo que não é JSON (sem cortar um par substituto ao meio).</summary>
    private static string? Resumo(string texto)
    {
        var t = texto.Trim();
        if (t.Length == 0)
        {
            return null;
        }

        if (t.Length <= 200)
        {
            return t;
        }

        return t.Substring(0, char.IsHighSurrogate(t[199]) ? 199 : 200);
    }

    private static TimeSpan? RetryAfterDoCorpo(Dictionary<string, JsonElement>? extras)
    {
        if (extras is null
            || !extras.TryGetValue("retry_after", out var valor)
            || valor.ValueKind != JsonValueKind.Number
            || !valor.TryGetDouble(out var segundos)
            || double.IsNaN(segundos)
            || segundos < 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(Math.Min(segundos, 86400));
    }

    private static TimeSpan? LerRetryAfter(HttpResponseHeaders cabecalhos)
    {
        var ra = cabecalhos.RetryAfter;
        if (ra?.Delta is TimeSpan delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (ra?.Date is DateTimeOffset data)
        {
            var falta = data - DateTimeOffset.UtcNow;
            return falta < TimeSpan.Zero ? TimeSpan.Zero : falta;
        }

        return null;
    }

    private static string? Cabecalho(HttpResponseHeaders cabecalhos, string nome) =>
        cabecalhos.TryGetValues(nome, out var valores) ? valores.FirstOrDefault() : null;

    private static string Segundos(TimeSpan t) => t.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static string NormalizarBaseUrl(string? baseUrl)
    {
        var b = baseUrl?.Trim();
        if (b is null || b.Length == 0)
        {
            return BaseUrlPadrao;
        }

        b = b.TrimEnd('/');
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            b = b.Substring(0, b.Length - 3).TrimEnd('/');
        }

        if (!Uri.TryCreate(b, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "BaseUrl deve ser uma URL http(s) absoluta, como " + BaseUrlPadrao + " ou " + BaseUrlSandbox + ".",
                nameof(EmissaojaOptions.BaseUrl));
        }

        return b;
    }

    private static TimeSpan ValidarTimeout(TimeSpan tempo)
    {
        if (tempo == System.Threading.Timeout.InfiniteTimeSpan)
        {
            return tempo;
        }

        if (tempo <= TimeSpan.Zero || tempo.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EmissaojaOptions.Timeout),
                "Timeout deve ser positivo (ou System.Threading.Timeout.InfiniteTimeSpan para desligar).");
        }

        return tempo;
    }

    /// <summary>Resposta já lida: status, corpo e os cabeçalhos que interessam.</summary>
    private sealed class Resposta
    {
        public Resposta(int status, byte[] corpo, string? requestId, TimeSpan? retryAfter)
        {
            Status = status;
            Corpo = corpo;
            RequestId = requestId;
            RetryAfter = retryAfter;
        }

        public int Status { get; }

        public byte[] Corpo { get; }

        public string? RequestId { get; }

        public TimeSpan? RetryAfter { get; }

        public bool Sucesso => Status >= 200 && Status < 300;

        public string Texto() => Utf8SemBom(Corpo);
    }
}
