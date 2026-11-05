using System;

namespace Emissaoja;

/// <summary>
/// Opções do <see cref="EmissaojaClient"/>: chave, URL base e tempo máximo de cada chamada.
/// </summary>
public sealed class EmissaojaOptions
{
    /// <summary>
    /// Chave de API (<c>ej_live_...</c> em produção, <c>ej_test_...</c> no sandbox), criada no app em Conta → API.
    /// Se ficar vazia, o cliente lê a variável de ambiente <c>EMISSAOJA_KEY</c> na primeira chamada que precisar dela.
    /// A chave é secreta: use só no servidor.
    /// </summary>
    public string? Chave { get; set; }

    /// <summary>
    /// URL base da API. Vazia: produção (<see cref="EmissaojaClient.BaseUrlPadrao"/>). Para testar sem efeito fiscal,
    /// use o sandbox (<see cref="EmissaojaClient.BaseUrlSandbox"/>). Barra e <c>/v1</c> no fim são ignorados.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Tempo máximo de cada chamada HTTP (padrão 30 s). <c>System.Threading.Timeout.InfiniteTimeSpan</c> desliga.
    /// Estourar vira <see cref="EmissaojaException"/> com <c>Code == "tempo_esgotado"</c> e <c>Status == 0</c>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
