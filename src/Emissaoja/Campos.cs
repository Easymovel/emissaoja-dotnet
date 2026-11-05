using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emissaoja;

/// <summary>
/// Campos da nota para <see cref="NotasRecurso.EmitirAsync"/> (corpo de <c>POST /v1/notas</c>, em snake_case).
/// Obrigatórios: <see cref="TomadorTipo"/>, <see cref="TomadorId"/> e <see cref="Valor"/>. Campos <c>null</c> não vão no corpo.
/// </summary>
public sealed class CamposNota
{
    /// <summary><c>pj</c> ou <c>pf</c> (constantes em <see cref="Emissaoja.TipoTomador"/>), como veio em <see cref="Tomador.Tipo"/>.</summary>
    [JsonPropertyName("tomador_tipo")] public string TomadorTipo { get; set; } = string.Empty;

    /// <summary>Id do tomador (<see cref="Tomador.Id"/>).</summary>
    [JsonPropertyName("tomador_id")] public string TomadorId { get; set; } = string.Empty;

    /// <summary>Valor em reais, até 2 casas (por exemplo <c>1500m</c> ou <c>89.90m</c>).</summary>
    [JsonPropertyName("valor")] public decimal Valor { get; set; }

    /// <summary>Id do serviço; omitido, a API usa o serviço principal.</summary>
    [JsonPropertyName("servico_id")] public string? ServicoId { get; set; }

    /// <summary>Descrição da nota (até 2000 caracteres); omitida, a API usa a descrição do serviço.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Competência no formato <c>AAAA-MM-DD</c>; omitida, a API usa hoje.</summary>
    [JsonPropertyName("competencia")] public string? Competencia { get; set; }

    /// <summary>ISS retido pelo tomador; informe só quando um bloqueio pedir.</summary>
    [JsonPropertyName("retem_iss")] public bool? RetemIss { get; set; }

    /// <summary>Alíquota do ISS em %; informe só quando um bloqueio pedir.</summary>
    [JsonPropertyName("aliquota_iss")] public decimal? AliquotaIss { get; set; }

    /// <summary>Código IBGE do município da prestação; informe só quando um bloqueio pedir.</summary>
    [JsonPropertyName("municipio_prestacao")] public string? MunicipioPrestacao { get; set; }

    /// <summary><c>true</c> confirma a mesma nota em menos de 2 minutos (resposta a <c>409 emissao_repetida_recente</c>).</summary>
    [JsonPropertyName("confirmo_repeticao")] public bool? ConfirmoRepeticao { get; set; }

    /// <summary>
    /// Confirma uma nota igual à de hoje (resposta a <c>409 possivel_duplicada</c>): o <c>pedido_id</c> informado em
    /// <see cref="EmissaojaException.NotaIgualRecente"/>.
    /// </summary>
    [JsonPropertyName("permitir_repetida_de")] public string? PermitirRepetidaDe { get; set; }

    /// <summary>Campos extras enviados como estão (campos novos da API ou valores <c>null</c> explícitos).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>
/// Campos do serviço para <see cref="ServicosRecurso.CriarAsync"/> e <see cref="ServicosRecurso.AtualizarAsync"/>.
/// Para criar: <see cref="Descricao"/>, <see cref="CTribNac"/>, <see cref="TribIssqn"/> e <see cref="RetemIss"/>.
/// Na edição, só os campos preenchidos mudam (campos <c>null</c> não vão no corpo).
/// </summary>
public sealed class CamposServico
{
    /// <summary>Descrição do serviço.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Texto padrão da descrição na nota.</summary>
    [JsonPropertyName("descricao_da_nota")] public string? DescricaoDaNota { get; set; }

    /// <summary>Torna o serviço o principal (usado quando a nota não informa o serviço).</summary>
    [JsonPropertyName("principal")] public bool? Principal { get; set; }

    /// <summary>Ativa ou desativa o serviço.</summary>
    [JsonPropertyName("ativo")] public bool? Ativo { get; set; }

    /// <summary>Código de tributação nacional (cTribNac); procure em <see cref="ServicosRecurso.CatalogoAsync"/>.</summary>
    [JsonPropertyName("c_trib_nac")] public string? CTribNac { get; set; }

    /// <summary>Código de tributação municipal (cTribMun), quando a prefeitura exige.</summary>
    [JsonPropertyName("c_trib_mun")] public string? CTribMun { get; set; }

    /// <summary>Código NBS.</summary>
    [JsonPropertyName("nbs")] public string? Nbs { get; set; }

    /// <summary><c>tributavel</c> ou <c>nao_incidencia</c> (isenção, imunidade e exportação ficam fora do emissaoja).</summary>
    [JsonPropertyName("trib_issqn")] public string? TribIssqn { get; set; }

    /// <summary>Tipo de operação do leiaute nacional (1 a 5).</summary>
    [JsonPropertyName("tp_oper")] public int? TpOper { get; set; }

    /// <summary><c>true</c> se o tomador retém o ISS.</summary>
    [JsonPropertyName("retem_iss")] public bool? RetemIss { get; set; }

    /// <summary>Alíquota do ISS em %.</summary>
    [JsonPropertyName("aliquota_iss")] public decimal? AliquotaIss { get; set; }

    /// <summary>Campos extras enviados como estão (campos novos da API ou valores <c>null</c> explícitos).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>
/// Campos de endereço e contato para <see cref="TomadoresRecurso.AtualizarAsync"/> (edição parcial: campos <c>null</c>
/// não vão no corpo). O documento é imutável.
/// </summary>
public sealed class CamposTomador
{
    /// <summary>Nome (CPF) ou razão social.</summary>
    [JsonPropertyName("nome")] public string? Nome { get; set; }

    /// <summary>CEP.</summary>
    [JsonPropertyName("cep")] public string? Cep { get; set; }

    /// <summary>Logradouro.</summary>
    [JsonPropertyName("endereco")] public string? Endereco { get; set; }

    /// <summary>Número.</summary>
    [JsonPropertyName("numero")] public string? Numero { get; set; }

    /// <summary>Complemento.</summary>
    [JsonPropertyName("complemento")] public string? Complemento { get; set; }

    /// <summary>Bairro.</summary>
    [JsonPropertyName("bairro")] public string? Bairro { get; set; }

    /// <summary>Cidade.</summary>
    [JsonPropertyName("cidade")] public string? Cidade { get; set; }

    /// <summary>UF (sigla).</summary>
    [JsonPropertyName("estado")] public string? Estado { get; set; }

    /// <summary>Código IBGE do município.</summary>
    [JsonPropertyName("codigo_municipio_ibge")] public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Telefone.</summary>
    [JsonPropertyName("telefone")] public string? Telefone { get; set; }

    /// <summary>E-mail.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }

    /// <summary>Inscrição municipal.</summary>
    [JsonPropertyName("inscricao_municipal")] public string? InscricaoMunicipal { get; set; }

    /// <summary><c>true</c> relê nome e endereço na Receita (tomador com CNPJ).</summary>
    [JsonPropertyName("atualizar_pela_receita")] public bool? AtualizarPelaReceita { get; set; }

    /// <summary>Campos extras enviados como estão (campos novos da API ou valores <c>null</c> explícitos).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Filtro de <see cref="TomadoresRecurso.ListarAsync"/>. Campos vazios ficam fora da URL.</summary>
public sealed class FiltroTomadores
{
    /// <summary>Texto (nome ou documento).</summary>
    public string? Busca { get; set; }

    /// <summary>Página, a partir de 1.</summary>
    public int? Pagina { get; set; }

    /// <summary>Itens por página (até 50).</summary>
    public int? Limite { get; set; }

    /// <summary><c>nome</c> ou <c>recentes</c>.</summary>
    public string? Ordem { get; set; }
}

/// <summary>Filtro de <see cref="NotasRecurso.ListarAsync"/>. Campos vazios ficam fora da URL.</summary>
public sealed class FiltroNotas
{
    /// <summary>
    /// <c>mes_atual</c>, <c>mes_anterior</c>, <c>ano_atual</c>, <c>12_meses</c> (padrão da API) ou <c>personalizado</c>
    /// (com <see cref="De"/> e <see cref="Ate"/>).
    /// </summary>
    public string? Periodo { get; set; }

    /// <summary>Data inicial <c>AAAA-MM-DD</c> (período <c>personalizado</c>).</summary>
    public string? De { get; set; }

    /// <summary>Data final <c>AAAA-MM-DD</c> (período <c>personalizado</c>).</summary>
    public string? Ate { get; set; }

    /// <summary><c>todas</c> (padrão), <c>emitidas</c> ou <c>canceladas</c>.</summary>
    public string? Situacao { get; set; }

    /// <summary>Número da nota, nome ou documento do tomador.</summary>
    public string? Busca { get; set; }

    /// <summary>Cursor da página seguinte (<see cref="RespostaNotas.ProximoCursor"/>).</summary>
    public string? Cursor { get; set; }

    /// <summary>Itens por página (1 a 50; padrão 20).</summary>
    public int? Limite { get; set; }
}

/// <summary>Corpo de <c>PUT /v1/empresa/credencial</c>.</summary>
internal sealed class CorpoCredencial
{
    [JsonPropertyName("cnpj")] public string Cnpj { get; set; } = string.Empty;

    [JsonPropertyName("senha")] public string Senha { get; set; } = string.Empty;

    [JsonPropertyName("ciente_ultima_tentativa")] public bool CienteUltimaTentativa { get; set; }
}

/// <summary>Corpo de <c>POST /v1/tomadores</c>.</summary>
internal sealed class CorpoTomador
{
    [JsonPropertyName("documento")] public string Documento { get; set; } = string.Empty;

    [JsonPropertyName("nome")] public string? Nome { get; set; }
}

/// <summary>Corpo de <c>PUT /v1/empresa/fiscal</c>.</summary>
internal sealed class CorpoFiscal
{
    [JsonPropertyName("aliquota_simples")] public decimal? AliquotaSimples { get; set; }

    [JsonPropertyName("reg_ap_trib_sn")] public int? RegApTribSn { get; set; }
}

/// <summary>Corpo de <c>POST /v1/notas/{origem}/{id}/cancelar</c>.</summary>
internal sealed class CorpoCancelamento
{
    [JsonPropertyName("motivo")] public string Motivo { get; set; } = string.Empty;

    [JsonPropertyName("justificativa")] public string? Justificativa { get; set; }
}
