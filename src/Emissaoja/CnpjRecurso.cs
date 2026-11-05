using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Emissaoja;

/// <summary>Consulta de CNPJ na base da Receita Federal (<c>GET /v1/cnpj/{cnpj}</c>, 30 consultas por minuto).</summary>
public sealed class CnpjRecurso
{
    private readonly EmissaojaClient _c;

    internal CnpjRecurso(EmissaojaClient cliente)
    {
        _c = cliente;
    }

    /// <summary>
    /// Dados cadastrais do CNPJ: razão social, situação, CNAEs, endereço, Simples, MEI e sócios (CPF mascarado).
    /// Não cria tomador; para isso use <see cref="TomadoresRecurso.CriarAsync"/>.
    /// </summary>
    /// <param name="cnpj">CNPJ com ou sem pontuação (a pontuação é retirada antes da chamada).</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O JSON como a API devolve.</returns>
    /// <exception cref="EmissaojaException"><c>cnpj_invalido</c> (status 0, sem chamar a API) se não sobrar nenhum dígito.</exception>
    public Task<JsonElement> ConsultarAsync(string cnpj, CancellationToken ct = default)
    {
        if (cnpj is null)
        {
            throw new ArgumentNullException(nameof(cnpj));
        }

        var digitos = new StringBuilder(cnpj.Length);
        foreach (var c in cnpj)
        {
            if (c >= '0' && c <= '9')
            {
                digitos.Append(c);
            }
        }

        if (digitos.Length == 0)
        {
            throw new EmissaojaException(0, "cnpj_invalido", "Informe o CNPJ (14 dígitos, com ou sem pontuação).");
        }

        return _c.ElementoAsync(HttpMethod.Get, "/v1/cnpj/" + Uri.EscapeDataString(digitos.ToString()), ct);
    }
}
