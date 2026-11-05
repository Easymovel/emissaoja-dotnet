using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Emissaoja.Tests;

/// <summary>Requisição como saiu do cliente, copiada no envio (o cliente descarta a mensagem depois).</summary>
internal sealed record Requisicao(string Metodo, Uri Url, IReadOnlyDictionary<string, string> Cabecalhos, string? Corpo, string? TipoConteudo)
{
    public string? Cabecalho(string nome) => Cabecalhos.TryGetValue(nome, out var valor) ? valor : null;
}

/// <summary>HttpMessageHandler falso: responde com funções enfileiradas (ou uma resposta padrão) e grava as requisições.</summary>
internal sealed class ServidorFalso : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _fila = new();
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _padrao;

    public ServidorFalso(Func<HttpRequestMessage, HttpResponseMessage>? padrao = null) => _padrao = padrao;

    public ConcurrentQueue<Requisicao> Requisicoes { get; } = new();

    public int Total => Requisicoes.Count;

    public Requisicao Ultima => Requisicoes.Last();

    public ServidorFalso Responder(Func<HttpRequestMessage, HttpResponseMessage> resposta)
    {
        _fila.Enqueue(resposta);
        return this;
    }

    public ServidorFalso Responder(HttpStatusCode status, string corpo, params (string Nome, string Valor)[] cabecalhos) =>
        Responder(_ => Json(status, corpo, cabecalhos));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var cabecalhos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cabecalho in request.Headers)
        {
            cabecalhos[cabecalho.Key] = string.Join(", ", cabecalho.Value);
        }

        Requisicoes.Enqueue(new Requisicao(request.Method.Method, request.RequestUri!, cabecalhos, corpo, request.Content?.Headers.ContentType?.MediaType));

        if (_fila.TryDequeue(out var resposta))
        {
            return resposta(request);
        }

        if (_padrao is not null)
        {
            return _padrao(request);
        }

        throw new InvalidOperationException("ServidorFalso sem resposta para " + request.RequestUri);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string corpo, params (string Nome, string Valor)[] cabecalhos)
    {
        var r = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        foreach (var (nome, valor) in cabecalhos)
        {
            r.Headers.TryAddWithoutValidation(nome, valor);
        }

        return r;
    }

    public static HttpResponseMessage Bytes(HttpStatusCode status, byte[] corpo, string tipo)
    {
        var conteudo = new ByteArrayContent(corpo);
        conteudo.Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);
        return new HttpResponseMessage(status) { Content = conteudo };
    }
}

/// <summary>Relógio e esperas falsos: registram as esperas e avançam o tempo na hora, sem dormir.</summary>
internal sealed class TempoFalso
{
    public TimeSpan Agora { get; private set; }

    public List<TimeSpan> Esperas { get; } = new();

    public Task Esperar(TimeSpan tempo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Esperas.Add(tempo);
        Agora += tempo;
        return Task.CompletedTask;
    }

    public TimeSpan Relogio() => Agora;
}

internal static class Fabrica
{
    public const string ChaveTeste = "ej_live_teste_0123456789abcdef";
    public const string Base = "https://emissaoja.test";

    public static (EmissaojaClient Ej, ServidorFalso Servidor, TempoFalso Tempo) Criar(ServidorFalso? servidor = null, string? chave = ChaveTeste)
    {
        servidor ??= new ServidorFalso();
        var tempo = new TempoFalso();
        var ej = new EmissaojaClient(new HttpClient(servidor), new EmissaojaOptions { Chave = chave, BaseUrl = Base })
        {
            Esperar = tempo.Esperar,
            Relogio = tempo.Relogio,
        };
        return (ej, servidor, tempo);
    }

    /// <summary>Campos mínimos de uma nota.</summary>
    public static CamposNota Campos() => new()
    {
        TomadorTipo = TipoTomador.Pj,
        TomadorId = "0b6f1d1e-2f43-4b5e-9a39-6f0c2b7d9a10",
        Valor = 150.50m,
    };
}
