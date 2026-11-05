namespace Emissaoja;

/// <summary>Estados do pedido de emissão (<see cref="Pedido.Estado"/>).</summary>
public static class EstadoPedido
{
    /// <summary>Na fila do motor.</summary>
    public const string NaFila = "na_fila";

    /// <summary>Sendo enviado ao Emissor Nacional.</summary>
    public const string Emitindo = "emitindo";

    /// <summary>Enviado; confirmando o resultado no portal.</summary>
    public const string Confirmando = "confirmando";

    /// <summary>Nota autorizada: traz número, chave de acesso e a nota (PDF e XML).</summary>
    public const string Autorizada = "autorizada";

    /// <summary>Recusada pelo fisco (não conta no plano): veja o motivo.</summary>
    public const string Recusada = "recusada";

    /// <summary>Sem confirmação do portal: confira com <see cref="EmissoesRecurso.ConferirAsync"/> antes de emitir de novo.</summary>
    public const string Incerta = "incerta";
}

/// <summary>Tipos de tomador.</summary>
public static class TipoTomador
{
    /// <summary>Pessoa jurídica (CNPJ).</summary>
    public const string Pj = "pj";

    /// <summary>Pessoa física (CPF).</summary>
    public const string Pf = "pf";
}

/// <summary>Origens de uma nota.</summary>
public static class OrigemNota
{
    /// <summary>Emitida pelo emissaoja.</summary>
    public const string Local = "local";

    /// <summary>Importada do portal (notas emitidas fora do emissaoja).</summary>
    public const string Importada = "importada";
}

/// <summary>Motivos de cancelamento aceitos pelo Emissor Nacional.</summary>
public static class MotivoCancelamento
{
    /// <summary>Erro na emissão (<c>"1"</c>).</summary>
    public const string ErroNaEmissao = "1";

    /// <summary>Serviço não prestado (<c>"2"</c>).</summary>
    public const string ServicoNaoPrestado = "2";
}
