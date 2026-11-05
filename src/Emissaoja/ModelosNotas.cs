using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emissaoja;

/// <summary>
/// Pedido de emissão: <c>na_fila</c> → <c>emitindo</c> → <c>confirmando</c> → <c>autorizada</c>, <c>recusada</c> ou
/// <c>incerta</c> (veja <see cref="EstadoPedido"/>).
/// </summary>
public sealed record Pedido
{
    /// <summary>Id do pedido (acompanhe com <see cref="NotasRecurso.AguardarAsync"/>).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Estado do pedido (constantes em <see cref="EstadoPedido"/>).</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;

    /// <summary>Código do motivo quando <c>recusada</c> (por exemplo <c>recusada_fisco</c>).</summary>
    [JsonPropertyName("motivo_code")] public string? MotivoCode { get; set; }

    /// <summary>Motivo da recusa dado pelo fisco, quando houver.</summary>
    [JsonPropertyName("motivo")] public string? Motivo { get; set; }

    /// <summary>Número da NFS-e, quando <c>autorizada</c>.</summary>
    [JsonPropertyName("numero")] public string? Numero { get; set; }

    /// <summary>Chave de acesso da NFS-e, quando <c>autorizada</c>.</summary>
    [JsonPropertyName("chave_acesso")] public string? ChaveAcesso { get; set; }

    /// <summary>Link da consulta pública da nota, quando houver.</summary>
    [JsonPropertyName("consulta_publica_url")] public string? ConsultaPublicaUrl { get; set; }

    /// <summary>A nota gerada (<c>Origem</c> e <c>Id</c> para PDF e XML), quando <c>autorizada</c>.</summary>
    [JsonPropertyName("nota")] public RefNota? Nota { get; set; }

    /// <summary>Ações possíveis agora (por exemplo <c>conferir</c> num pedido <c>incerta</c>).</summary>
    [JsonPropertyName("acoes")] public IReadOnlyList<string> Acoes { get; set; } = Array.Empty<string>();

    /// <summary>Em quantos milissegundos consultar de novo; <c>null</c> quando o pedido parou de mudar.</summary>
    [JsonPropertyName("proxima_consulta_ms")] public int? ProximaConsultaMs { get; set; }

    /// <summary>Tomador da nota (tipo, id, nome e documento formatado).</summary>
    [JsonPropertyName("tomador")] public Tomador? Tomador { get; set; }

    /// <summary>Valor da nota, em reais.</summary>
    [JsonPropertyName("valor")] public decimal? Valor { get; set; }

    /// <summary>Descrição da nota.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Competência (<c>AAAA-MM-DD</c>).</summary>
    [JsonPropertyName("competencia")] public string? Competencia { get; set; }

    /// <summary>Criação do pedido (ISO 8601).</summary>
    [JsonPropertyName("criado_em")] public string? CriadoEm { get; set; }

    /// <summary>Emissão da nota (ISO 8601), quando <c>autorizada</c>.</summary>
    [JsonPropertyName("emitida_em")] public string? EmitidaEm { get; set; }

    /// <summary>Última mudança do pedido (ISO 8601).</summary>
    [JsonPropertyName("atualizado_em")] public string? AtualizadoEm { get; set; }

    /// <summary>
    /// <c>true</c> quando o pedido parou de mudar: <c>autorizada</c>, <c>recusada</c> ou <c>incerta</c>
    /// (<c>incerta</c> se resolve com <see cref="EmissoesRecurso.ConferirAsync"/>).
    /// </summary>
    [JsonIgnore]
    public bool Terminal =>
        Estado == EstadoPedido.Autorizada || Estado == EstadoPedido.Recusada || Estado == EstadoPedido.Incerta;

    /// <summary>Campos que esta versão do pacote ainda não modela (<c>faltando</c>, <c>documentos</c>, <c>conciliacao</c>...).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Referência a uma nota: <c>Origem</c> (<c>local</c> ou <c>importada</c>) e <c>Id</c>.</summary>
public sealed record RefNota
{
    /// <summary><c>local</c> (emitida pelo emissaoja) ou <c>importada</c> (veio do portal).</summary>
    [JsonPropertyName("origem")] public string Origem { get; set; } = string.Empty;

    /// <summary>Id da nota.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Prévia calculada antes de emitir: resumo da nota e avisos.</summary>
public sealed record Previa
{
    /// <summary>Resumo da nota montada (JSON como a API devolve).</summary>
    [JsonPropertyName("resumo")] public JsonElement? Resumo { get; set; }

    /// <summary>Avisos que não impedem a emissão.</summary>
    [JsonPropertyName("avisos")] public IReadOnlyList<Bloqueio> Avisos { get; set; } = Array.Empty<Bloqueio>();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Bloqueio (<c>422 nota_bloqueada</c>) ou aviso da prévia: o que é, em texto, e a ação que resolve.</summary>
public sealed record Bloqueio
{
    /// <summary>Código do bloqueio ou aviso (por exemplo <c>tomador_incompleto</c>).</summary>
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    /// <summary>Mensagem em português.</summary>
    [JsonPropertyName("mensagem")] public string? Mensagem { get; set; }

    /// <summary>Ação que resolve (por exemplo <c>completar_tomador</c>).</summary>
    [JsonPropertyName("acao")] public string? Acao { get; set; }

    /// <summary>Demais campos (por exemplo <c>detalhe</c>).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resposta com um pedido (emitir, obter, conferir, aguardar).</summary>
public sealed record RespostaPedido
{
    /// <summary>O pedido.</summary>
    [JsonPropertyName("pedido")] public Pedido Pedido { get; set; } = new Pedido();

    /// <summary>Em <see cref="NotasRecurso.EmitirAsync"/>: a prévia (resumo e avisos).</summary>
    [JsonPropertyName("previa")] public Previa? Previa { get; set; }

    /// <summary>
    /// Em <see cref="NotasRecurso.EmitirAsync"/>: <c>true</c> quando a referência já tinha sido usada e a API devolveu
    /// o pedido original, sem emitir de novo.
    /// </summary>
    [JsonPropertyName("repetido")] public bool Repetido { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Lista de pedidos (<c>GET /v1/emissoes</c>).</summary>
public sealed record RespostaPedidos
{
    /// <summary>Os pedidos.</summary>
    [JsonPropertyName("pedidos")] public IReadOnlyList<Pedido> Pedidos { get; set; } = Array.Empty<Pedido>();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Nota da lista (emitidas pelo emissaoja e importadas do portal).</summary>
public sealed record Nota
{
    /// <summary><c>local</c> ou <c>importada</c> (constantes em <see cref="OrigemNota"/>).</summary>
    [JsonPropertyName("origem")] public string Origem { get; set; } = string.Empty;

    /// <summary>Id da nota (com <c>Origem</c>, baixa PDF e XML).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Número da NFS-e.</summary>
    [JsonPropertyName("numero")] public string? Numero { get; set; }

    /// <summary><c>emitida</c>, <c>cancelada</c>, <c>cancelando</c>, <c>emitindo</c>, <c>conferindo</c> ou <c>nao_emitida</c>.</summary>
    [JsonPropertyName("situacao")] public string? Situacao { get; set; }

    /// <summary>Emissão (ISO 8601).</summary>
    [JsonPropertyName("emitida_em")] public string? EmitidaEm { get; set; }

    /// <summary>Competência (<c>AAAA-MM-DD</c>).</summary>
    [JsonPropertyName("competencia")] public string? Competencia { get; set; }

    /// <summary>Tomador da nota (tipo, nome e documento).</summary>
    [JsonPropertyName("tomador")] public Tomador? Tomador { get; set; }

    /// <summary>Descrição.</summary>
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }

    /// <summary>Valor, em reais.</summary>
    [JsonPropertyName("valor")] public decimal? Valor { get; set; }

    /// <summary>Chave de acesso.</summary>
    [JsonPropertyName("chave_acesso")] public string? ChaveAcesso { get; set; }

    /// <summary>Id do pedido de emissão, nas notas locais.</summary>
    [JsonPropertyName("pedido_id")] public string? PedidoId { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Página de notas (<c>GET /v1/notas</c>), paginada por cursor.</summary>
public sealed record RespostaNotas
{
    /// <summary>As notas da página.</summary>
    [JsonPropertyName("notas")] public IReadOnlyList<Nota> Notas { get; set; } = Array.Empty<Nota>();

    /// <summary>Total de notas no filtro.</summary>
    [JsonPropertyName("total")] public int Total { get; set; }

    /// <summary><c>true</c> se o período tem mais notas do que a janela de busca alcança.</summary>
    [JsonPropertyName("janela_cheia")] public bool JanelaCheia { get; set; }

    /// <summary>Cursor da próxima página (passe em <see cref="FiltroNotas.Cursor"/>), ou <c>null</c> na última.</summary>
    [JsonPropertyName("proximo_cursor")] public string? ProximoCursor { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Operação longa (cancelamento): <c>processando</c> → <c>concluida</c> ou <c>falhou</c>.</summary>
public sealed record Operacao
{
    /// <summary>Id da operação.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>Tipo (por exemplo <c>cancelamento</c>).</summary>
    [JsonPropertyName("tipo")] public string? Tipo { get; set; }

    /// <summary><c>processando</c>, <c>concluida</c> ou <c>falhou</c>.</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;

    /// <summary>A nota alvo da operação.</summary>
    [JsonPropertyName("alvo")] public RefNota? Alvo { get; set; }

    /// <summary>Resultado quando <c>concluida</c> (JSON como a API devolve).</summary>
    [JsonPropertyName("resultado")] public JsonElement? Resultado { get; set; }

    /// <summary>Erro quando <c>falhou</c>.</summary>
    [JsonPropertyName("erro")] public ErroOperacao? Erro { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Erro de uma operação que falhou.</summary>
public sealed record ErroOperacao
{
    /// <summary>Código (por exemplo <c>cancelamento_fora_do_prazo</c> ou <c>cancelamento_recusado</c>).</summary>
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    /// <summary>Mensagem em português.</summary>
    [JsonPropertyName("message")] public string? Message { get; set; }

    /// <summary>Motivo dado pelo fisco (JSON como a API devolve), quando houver.</summary>
    [JsonPropertyName("motivo_fisco")] public JsonElement? MotivoFisco { get; set; }

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Resposta com uma operação (cancelar, obter, aguardar).</summary>
public sealed record RespostaOperacao
{
    /// <summary>A operação.</summary>
    [JsonPropertyName("operacao")] public Operacao Operacao { get; set; } = new Operacao();

    /// <summary>Campos que esta versão do pacote ainda não modela.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
}

/// <summary>Nota igual emitida hoje (<see cref="EmissaojaException.NotaIgualRecente"/> em <c>409 possivel_duplicada</c>).</summary>
public sealed record NotaIgualRecente
{
    /// <summary>Pedido da nota igual: passe em <see cref="CamposNota.PermitirRepetidaDe"/> se for mesmo outra nota.</summary>
    [JsonPropertyName("pedido_id")] public string PedidoId { get; set; } = string.Empty;

    /// <summary>Número da NFS-e igual, quando já autorizada.</summary>
    [JsonPropertyName("numero_nfse")] public string? NumeroNfse { get; set; }

    /// <summary>Emissão da nota igual (ISO 8601).</summary>
    [JsonPropertyName("emitida_em")] public string? EmitidaEm { get; set; }

    /// <summary><c>autorizada</c>, <c>processando</c> ou <c>incerta</c>.</summary>
    [JsonPropertyName("status")] public string? Status { get; set; }
}
