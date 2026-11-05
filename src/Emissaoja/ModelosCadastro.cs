using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emissaoja;

/// <summary>
/// Tomador da nota (cliente). Na lista e na busca vêm os dados principais; na ficha
/// (<see cref="TomadoresRecurso.ObterAsync"/>), também endereço e contato.
/// </summary>
public sealed record Tomador
{
    /// <summary><c>pj</c> (CNPJ) ou <c>pf</c> (CPF).</summary>
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = string.Empty;

    /// <summary>Id do tomador (use em <see cref="CamposNota.TomadorId"/>).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Nome ou razão social.</summary>
    [JsonPropertyName("nome")] public string? Nome { get; set; }

    /// <summary>CNPJ ou CPF só com números.</summary>
    [JsonPropertyName("documento")] public string? Documento { get; set; }

    /// <summary>CNPJ ou CPF formatado.</summary>
    [JsonPropertyName("documento_formatado")] public string? DocumentoFormatado { get; set; }

    /// <summary>Cidade.</summary>
    [JsonPropertyName("cidade")] public string? Cidade { get; set; }

    /// <summary>UF.</summary>
    [JsonPropertyName("uf")] public string? Uf { get; set; }

    /// <summary>Notas emitidas para este tomador (na lista).</summary>
    [JsonPropertyName("notas_emitidas")] public int? NotasEmitidas { get; set; }

    /// <summary>Data da última nota (ISO 8601), na lista.</summary>
    [JsonPropertyName("ultima_nota_em")] public string? UltimaNotaEm { get; set; }

    /// <summary>Nome fantasia (ficha).</summary>
    [JsonPropertyName("nome_fantasia")] public string? NomeFantasia { get; set; }

    /// <summary>Inscrição municipal (ficha).</summary>
    [JsonPropertyName("inscricao_municipal")] public string? InscricaoMunicipal { get; set; }

    /// <summary>CEP (ficha).</summary>
    [JsonPropertyName("cep")] public string? Cep { get; set; }

    /// <summary>Logradouro (ficha).</summary>
    [JsonPropertyName("endereco")] public string? Endereco { get; set; }

    /// <summary>Número do endereço (ficha).</summary>
    [JsonPropertyName("numero")] public string? Numero { get; set; }

    /// <summary>Complemento (ficha).</summary>
    [JsonPropertyName("complemento")] public string? Complemento { get; set; }

    /// <summary>Bairro (ficha).</summary>
    [JsonPropertyName("bairro")] public string? Bairro { get; set; }

    /// <summary>Código IBGE do município (ficha).</summary>
    [JsonPropertyName("codigo_municipio_ibge")] public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Telefone (ficha).</summary>
    [JsonPropertyName("telefone")] public string? Telefone { get; set; }

    /// <summary>E-mail (ficha).</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }

    /// <summary><c>true</c> se o endereço está completo para a nota (ficha).</summary>
    [JsonPropertyName("endereco_completo")] public bool? EnderecoCompleto { get; set; }

    /// <summary>Criação do cadastro (ISO 8601, ficha).</summary>
    [JsonPropertyName("criado_em")] public string? CriadoEm { get; set; }

    /// <summary>Última alteração do cadastro (ISO 8601, ficha).</summary>
    [JsonPropertyName("atualizado_em")] public string? AtualizadoEm { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resposta com um tomador (criar, obter, atualizar).</summary>
public sealed record RespostaTomador
{
    /// <summary>O tomador.</summary>
    [JsonPropertyName("tomador")] public Tomador Tomador { get; set; } = new Tomador();

    /// <summary>Em <see cref="TomadoresRecurso.CriarAsync"/>: <c>true</c> se o documento já estava cadastrado (nada foi criado).</summary>
    [JsonPropertyName("ja_existia")] public bool JaExistia { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Lista de tomadores (listar e buscar).</summary>
public sealed record RespostaTomadores
{
    /// <summary>Os tomadores.</summary>
    [JsonPropertyName("tomadores")] public IReadOnlyList<Tomador> Tomadores { get; set; } = Array.Empty<Tomador>();

    /// <summary>Total de tomadores (só em <see cref="TomadoresRecurso.ListarAsync"/>).</summary>
    [JsonPropertyName("total")] public int? Total { get; set; }

    /// <summary>Página atual (só em <see cref="TomadoresRecurso.ListarAsync"/>).</summary>
    [JsonPropertyName("pagina")] public int? Pagina { get; set; }

    /// <summary>Itens por página (só em <see cref="TomadoresRecurso.ListarAsync"/>).</summary>
    [JsonPropertyName("limite")] public int? Limite { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Serviço da empresa (o que vai na nota: código nacional, tributação do ISS, alíquota).</summary>
public sealed record Servico
{
    /// <summary>Id do serviço (use em <see cref="CamposNota.ServicoId"/>).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Descrição do serviço.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Texto padrão da descrição na nota.</summary>
    [JsonPropertyName("descricao_da_nota")] public string? DescricaoDaNota { get; set; }

    /// <summary><c>true</c> se é o serviço usado quando a nota não informa <c>servico_id</c>.</summary>
    [JsonPropertyName("principal")] public bool Principal { get; set; }

    /// <summary><c>true</c> se está ativo.</summary>
    [JsonPropertyName("ativo")] public bool Ativo { get; set; }

    /// <summary>Código de tributação nacional (cTribNac).</summary>
    [JsonPropertyName("c_trib_nac")] public string? CTribNac { get; set; }

    /// <summary>Código de tributação nacional formatado.</summary>
    [JsonPropertyName("c_trib_nac_formatado")] public string? CTribNacFormatado { get; set; }

    /// <summary>Código de tributação municipal (cTribMun), quando a prefeitura exige.</summary>
    [JsonPropertyName("c_trib_mun")] public string? CTribMun { get; set; }

    /// <summary>Item da lista da LC 116.</summary>
    [JsonPropertyName("item_lc116")] public string? ItemLc116 { get; set; }

    /// <summary>Descrição do código de tributação.</summary>
    [JsonPropertyName("descricao_tributacao")] public string? DescricaoTributacao { get; set; }

    /// <summary>Código NBS.</summary>
    [JsonPropertyName("nbs")] public string? Nbs { get; set; }

    /// <summary>Códigos NBS sugeridos para o item.</summary>
    [JsonPropertyName("nbs_sugeridos")] public IReadOnlyList<NbsSugerido> NbsSugeridos { get; set; } = Array.Empty<NbsSugerido>();

    /// <summary><c>tributavel</c> ou <c>nao_incidencia</c>.</summary>
    [JsonPropertyName("trib_issqn")] public string? TribIssqn { get; set; }

    /// <summary>Tipo de operação do leiaute nacional (1 a 5), ou <c>null</c>.</summary>
    [JsonPropertyName("tp_oper")] public int? TpOper { get; set; }

    /// <summary><c>true</c> se o tomador retém o ISS.</summary>
    [JsonPropertyName("retem_iss")] public bool? RetemIss { get; set; }

    /// <summary>Alíquota do ISS em %, quando informada.</summary>
    [JsonPropertyName("aliquota_iss")] public decimal? AliquotaIss { get; set; }

    /// <summary>Última alteração (ISO 8601).</summary>
    [JsonPropertyName("atualizado_em")] public string? AtualizadoEm { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Código NBS sugerido para um item de serviço.</summary>
public sealed record NbsSugerido
{
    /// <summary>Código NBS.</summary>
    [JsonPropertyName("nbs")] public string? Nbs { get; set; }

    /// <summary>Descrição do código.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resposta com um serviço (criar, atualizar, desativar).</summary>
public sealed record RespostaServico
{
    /// <summary>O serviço.</summary>
    [JsonPropertyName("servico")] public Servico Servico { get; set; } = new Servico();

    /// <summary>Em <see cref="ServicosRecurso.AtualizarAsync"/>: quantos vínculos foram atualizados junto.</summary>
    [JsonPropertyName("vinculos_atualizados")] public int? VinculosAtualizados { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Lista de serviços (<c>GET /v1/servicos</c>).</summary>
public sealed record RespostaServicos
{
    /// <summary>Os serviços.</summary>
    [JsonPropertyName("servicos")] public IReadOnlyList<Servico> Servicos { get; set; } = Array.Empty<Servico>();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Item do catálogo nacional de códigos de serviço.</summary>
public sealed record ItemCatalogo
{
    /// <summary>Código de tributação nacional (use em <see cref="CamposServico.CTribNac"/>).</summary>
    [JsonPropertyName("c_trib_nac")] public string? CTribNac { get; set; }

    /// <summary>Código de tributação nacional formatado.</summary>
    [JsonPropertyName("c_trib_nac_formatado")] public string? CTribNacFormatado { get; set; }

    /// <summary>Descrição do código.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Item da lista da LC 116.</summary>
    [JsonPropertyName("item_lc116")] public string? ItemLc116 { get; set; }

    /// <summary>Descrição do item da LC 116.</summary>
    [JsonPropertyName("descricao_item_lc116")] public string? DescricaoItemLc116 { get; set; }

    /// <summary>Códigos NBS sugeridos.</summary>
    [JsonPropertyName("nbs_sugeridos")] public IReadOnlyList<NbsSugerido> NbsSugeridos { get; set; } = Array.Empty<NbsSugerido>();

    /// <summary><c>true</c> se o código é vedado ao MEI.</summary>
    [JsonPropertyName("mei_vedado")] public bool MeiVedado { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resultado da busca no catálogo (<c>GET /v1/catalogo/servicos</c>).</summary>
public sealed record RespostaCatalogo
{
    /// <summary>Os itens encontrados.</summary>
    [JsonPropertyName("itens")] public IReadOnlyList<ItemCatalogo> Itens { get; set; } = Array.Empty<ItemCatalogo>();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}
