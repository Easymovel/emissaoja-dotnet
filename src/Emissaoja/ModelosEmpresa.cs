using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emissaoja;

/// <summary>Status do serviço (<c>GET /v1/status</c>).</summary>
public sealed record StatusServico
{
    /// <summary><c>true</c> quando a API está no ar.</summary>
    [JsonPropertyName("ok")] public bool Ok { get; set; }

    /// <summary>Versão da API.</summary>
    [JsonPropertyName("versao")] public string? Versao { get; set; }

    /// <summary>Motor fiscal: <c>ok</c>, <c>degradado</c> ou <c>simulado</c> (sandbox).</summary>
    [JsonPropertyName("motor")] public string? Motor { get; set; }

    /// <summary><c>producao</c> ou <c>sandbox</c>.</summary>
    [JsonPropertyName("ambiente")] public string? Ambiente { get; set; }

    /// <summary>Hora do servidor (ISO 8601).</summary>
    [JsonPropertyName("tempo")] public string? Tempo { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>A empresa da conta e o estado da configuração (<c>GET /v1/empresa</c>).</summary>
public sealed record RespostaEmpresa
{
    /// <summary>Usuário dono da chave.</summary>
    [JsonPropertyName("usuario")] public UsuarioConta? Usuario { get; set; }

    /// <summary>A empresa conectada, ou <c>null</c> antes de <see cref="EmpresaRecurso.ConectarAsync"/>.</summary>
    [JsonPropertyName("empresa")] public DadosEmpresa? Empresa { get; set; }

    /// <summary>Estado da credencial do Emissor Nacional.</summary>
    [JsonPropertyName("credencial")] public Credencial Credencial { get; set; } = new Credencial();

    /// <summary>Estado do onboarding (<c>configurando</c>, <c>perguntas</c>, <c>pronto</c>...).</summary>
    [JsonPropertyName("onboarding")] public EstadoOnboarding Onboarding { get; set; } = new EstadoOnboarding();

    /// <summary>Importação das notas dos últimos 90 dias (JSON como a API devolve), ou <c>null</c>.</summary>
    [JsonPropertyName("importacao")] public JsonElement? Importacao { get; set; }

    /// <summary>Avisos da conta (lista JSON como a API devolve).</summary>
    [JsonPropertyName("avisos")] public JsonElement? Avisos { get; set; }

    /// <summary>Aceite dos termos.</summary>
    [JsonPropertyName("aceite")] public Aceite? Aceite { get; set; }

    /// <summary>Hora do servidor (ISO 8601).</summary>
    [JsonPropertyName("tempo")] public string? Tempo { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Usuário da conta.</summary>
public sealed record UsuarioConta
{
    /// <summary>E-mail da conta Google.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Dados da empresa conectada.</summary>
public sealed record DadosEmpresa
{
    /// <summary>CNPJ só com números.</summary>
    [JsonPropertyName("cnpj")] public string Cnpj { get; set; } = string.Empty;

    /// <summary>CNPJ formatado.</summary>
    [JsonPropertyName("cnpj_formatado")] public string? CnpjFormatado { get; set; }

    /// <summary>Razão social.</summary>
    [JsonPropertyName("nome")] public string? Nome { get; set; }

    /// <summary><c>mei</c>, <c>me_epp</c> ou <c>null</c>.</summary>
    [JsonPropertyName("porte_simples")] public string? PorteSimples { get; set; }

    /// <summary>Estado do vínculo da conta com a empresa.</summary>
    [JsonPropertyName("vinculo")] public string? Vinculo { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Credencial do Emissor Nacional (a senha nunca é devolvida).</summary>
public sealed record Credencial
{
    /// <summary><c>ausente</c>, <c>verificando</c>, <c>ativa</c> ou <c>congelada</c>.</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;

    /// <summary>Quando a credencial foi congelada (ISO 8601), ou <c>null</c>.</summary>
    [JsonPropertyName("congelada_em")] public string? CongeladaEm { get; set; }

    /// <summary>Id da tentativa de verificação em andamento, ou <c>null</c>.</summary>
    [JsonPropertyName("tentativa_em_voo")] public string? TentativaEmVoo { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Estado do onboarding da empresa: <c>configurando</c> → <c>perguntas</c> → <c>pronto</c>.</summary>
public sealed record EstadoOnboarding
{
    /// <summary>Estado atual. Emitir antes de <c>pronto</c> devolve <c>422 nota_bloqueada</c> com o que falta.</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Aceite dos termos de uso.</summary>
public sealed record Aceite
{
    /// <summary><c>true</c> se falta aceitar os termos no app.</summary>
    [JsonPropertyName("pendente")] public bool Pendente { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Tentativa de verificação da senha no Emissor Nacional.</summary>
public sealed record Tentativa
{
    /// <summary>Id da tentativa.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>
    /// <c>verificando</c>, <c>aceita</c>, <c>senha_recusada</c>, <c>portal_indisponivel</c>, <c>bloqueada</c>,
    /// <c>interrompida</c> ou <c>erro</c>.
    /// </summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;

    /// <summary>
    /// O que fazer: <c>aguardar</c>, <c>configurando</c>, <c>pronto</c>, <c>corrigir_senha</c>, <c>tentar_mais_tarde</c>,
    /// <c>redefinir_no_portal</c> ou <c>suporte</c>.
    /// </summary>
    [JsonPropertyName("proximo_passo")] public string ProximoPasso { get; set; } = string.Empty;

    /// <summary>Código do resultado, quando houver.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }

    /// <summary>CNPJ formatado.</summary>
    [JsonPropertyName("cnpj_formatado")] public string? CnpjFormatado { get; set; }

    /// <summary>Início da tentativa (ISO 8601).</summary>
    [JsonPropertyName("iniciada_em")] public string? IniciadaEm { get; set; }

    /// <summary>Fim da tentativa (ISO 8601), ou <c>null</c>.</summary>
    [JsonPropertyName("concluida_em")] public string? ConcluidaEm { get; set; }

    /// <summary>Contagem de falhas do CNPJ nas últimas 24 h (o portal bloqueia depois de 3).</summary>
    [JsonPropertyName("tentativas")] public ContagemTentativas? Tentativas { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Falhas de senha do CNPJ nas últimas 24 h.</summary>
public sealed record ContagemTentativas
{
    /// <summary>Falhas permitidas em 24 h.</summary>
    [JsonPropertyName("limite")] public int? Limite { get; set; }

    /// <summary>Falhas nas últimas 24 h.</summary>
    [JsonPropertyName("falhas_24h")] public int? Falhas24h { get; set; }

    /// <summary>Tentativas que restam.</summary>
    [JsonPropertyName("restantes")] public int? Restantes { get; set; }

    /// <summary>Quando volta a liberar (ISO 8601), se esgotou.</summary>
    [JsonPropertyName("libera_em")] public string? LiberaEm { get; set; }

    /// <summary><c>true</c> se a próxima é a última: conecte com <c>cienteUltimaTentativa: true</c>.</summary>
    [JsonPropertyName("ultima_exige_confirmacao")] public bool UltimaExigeConfirmacao { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resposta de <see cref="EmpresaRecurso.ConectarAsync"/> (<c>202</c>).</summary>
public sealed record RespostaTentativa
{
    /// <summary>A tentativa iniciada; acompanhe com <see cref="EmpresaRecurso.AguardarCredencialAsync"/>.</summary>
    [JsonPropertyName("tentativa")] public Tentativa Tentativa { get; set; } = new Tentativa();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Estado da credencial e da última tentativa (<c>GET /v1/empresa/credencial</c>).</summary>
public sealed record RespostaCredencial
{
    /// <summary>Estado da credencial.</summary>
    [JsonPropertyName("credencial")] public Credencial Credencial { get; set; } = new Credencial();

    /// <summary>Estado do onboarding.</summary>
    [JsonPropertyName("onboarding")] public EstadoOnboarding Onboarding { get; set; } = new EstadoOnboarding();

    /// <summary>A tentativa em andamento ou a última, ou <c>null</c> se nunca houve.</summary>
    [JsonPropertyName("tentativa")] public Tentativa? Tentativa { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}
