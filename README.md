# emissaoja: API de NFS-e nacional para .NET (C#)

[![NuGet](https://img.shields.io/nuget/v/Emissaoja.svg)](https://www.nuget.org/packages/Emissaoja)
[![CI](https://github.com/Easymovel/emissaoja-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/Easymovel/emissaoja-dotnet/actions/workflows/ci.yml)

Cliente oficial em C# da **API do [emissaoja](https://emissaoja.com.br)**: emita **NFS-e** (nota fiscal de serviço eletrônica) no **Emissor Nacional** a partir do seu sistema .NET, **sem certificado digital**, só com o CNPJ e a senha do portal. Feito para **MEI** e empresas do **Simples Nacional**: cadastre tomadores e serviços, emita, acompanhe o pedido, cancele e baixe o **PDF (DANFSe)** e o **XML** de cada nota.

- .NET 8+ e .NET Standard 2.0 (.NET Framework 4.6.2+, .NET Core 3.1+). Sem dependências no .NET 8; no .NET Standard 2.0, só `System.Text.Json`.
- Modelos tipados (`Pedido`, `Tomador`, `Servico`, `Nota`...), com JSON gerado em compilação: compatível com trimming e Native AOT.
- Emissão idempotente: a referência da nota vai no cabeçalho `Idempotency-Key` e é validada antes de chamar a API.
- `AguardarAsync` acompanha o pedido no ritmo que a API pede (`proxima_consulta_ms`).
- Erros como `EmissaojaException`, com `Code`, `Status`, `Extras` (bloqueios, nota igual), `RetryAfter` e `RequestId`.
- Assíncrono de ponta a ponta, com `CancellationToken`, e pronto para `IHttpClientFactory`.

## Instalação

```bash
dotnet add package Emissaoja
```

[Crie a conta no emissaoja](https://emissaoja.com.br/?origem=lib-dotnet) (entre com a conta Google em [emissaoja.com.br/app](https://emissaoja.com.br/app)), aceite os termos e crie a chave em **Conta → API**. A chave aparece uma vez (prefixo `ej_live_`): guarde-a fora do código, por exemplo na variável de ambiente `EMISSAOJA_KEY` ou no `dotnet user-secrets`.

## Teste no sandbox em 2 minutos

O sandbox (`https://sandbox.emissaoja.com.br`) é o mesmo emissaoja com um Emissor Nacional simulado: nada vai ao fisco e nada é cobrado. Entre lá com a conta Google, crie uma chave `ej_test_` em **Conta → API** e rode:

```csharp
using Emissaoja;

using var ej = new EmissaojaClient(new EmissaojaOptions
{
    Chave = Environment.GetEnvironmentVariable("EMISSAOJA_KEY"), // ej_test_... (só vale no sandbox)
    BaseUrl = EmissaojaClient.BaseUrlSandbox,
});

// 1. Conectar a empresa de exemplo: MEI pronto para emitir (no sandbox, qualquer senha serve, menos "errada")
if ((await ej.Empresa.ObterAsync()).Empresa is null)
{
    await ej.Empresa.ConectarAsync("11111111000191", "qualquer-senha");
    await ej.Empresa.AguardarCredencialAsync(); // até a senha ser conferida no portal
}

// 2. Esperar o cadastro fiscal ficar pronto (no sandbox, poucos segundos)
for (var i = 0; i < 30 && (await ej.Empresa.ObterAsync()).Onboarding.Estado != "pronto"; i++)
    await Task.Delay(TimeSpan.FromSeconds(2));

// 3. Tomador pelo CNPJ: nome e endereço vêm da Receita
var tomador = (await ej.Tomadores.CriarAsync("12345678000195")).Tomador;

// 4. Emitir: a referência é um id seu, único por nota. Rodar de novo com a mesma referência devolve o mesmo pedido.
var emissao = await ej.Notas.EmitirAsync("pedido-0001", new CamposNota
{
    TomadorTipo = tomador.Tipo,
    TomadorId = tomador.Id,
    Valor = 1500m,
    Descricao = "Consultoria de outubro",
});

// 5. Acompanhar até autorizada, recusada ou incerta, e baixar o PDF
var final = await ej.Notas.AguardarAsync(emissao.Pedido.Id);
if (final.Pedido.Estado == EstadoPedido.Autorizada && final.Pedido.Nota is { } nota)
{
    byte[] pdf = await ej.Notas.PdfAsync(nota.Origem, nota.Id);
    await File.WriteAllBytesAsync($"nfse-{final.Pedido.Numero}.pdf", pdf);
    Console.WriteLine($"NFS-e {final.Pedido.Numero} autorizada. Chave de acesso: {final.Pedido.ChaveAcesso}");
}
```

Dados de exemplo do sandbox:

| CNPJ da empresa | O que acontece |
| --- | --- |
| `11111111000191` | MEI pronto para emitir, com notas e clientes dos últimos 90 dias |
| `22222222000191` | ME/EPP que pede a alíquota do Simples antes de emitir (`Empresa.FiscalAsync`) |
| `33333333000191` | MEI sem notas anteriores |
| `44444444000191` | Empresa bloqueada (não optante do Simples) |
| qualquer um, com a senha `errada` | Senha recusada pelo portal |

Tomadores de exemplo: CNPJ `12345678000195` e CPF `52998224725`. Valores com significado ao emitir: `13.13m` fica `incerta` (resolva com `Emissoes.ConferirAsync`, que leva a `autorizada`), `77.77m` fica `incerta` e não saiu, `66.66m` é recusada pelo fisco e `99.99m` é bloqueada já na prévia (`422 nota_bloqueada`).

## Em produção

Troque a URL base e a chave: sem `BaseUrl`, o cliente usa `https://emissaoja.com.br`; sem `Chave`, lê `EMISSAOJA_KEY` na primeira chamada que precisa dela.

```csharp
using var ej = new EmissaojaClient(); // produção, chave ej_live_... em EMISSAOJA_KEY

// ou, com a chave da configuração:
using var ej2 = new EmissaojaClient(new EmissaojaOptions { Chave = configuracao["Emissaoja:Chave"] });
```

> **A chave é secreta.** Quem tem a chave emite notas em nome da empresa. Use este pacote só no servidor (ASP.NET Core, workers, Azure Functions, scripts). Nunca coloque a chave em app desktop ou mobile distribuído nem em Blazor WebAssembly. Se a chave vazar, revogue em **Conta → API**.

Para conectar a empresa real, use o CNPJ (só números) e a senha do **Emissor Nacional** (não é a conta gov.br). O portal bloqueia depois de 3 senhas erradas em 24 horas: se a API avisar que é a última tentativa (`409 confirmar_ultima_tentativa`), confira a senha e conecte com `cienteUltimaTentativa: true`.

## ASP.NET Core e injeção de dependência

O cliente é thread-safe. Registre uma instância só (singleton):

```csharp
using Emissaoja;

var builder = WebApplication.CreateBuilder(args);

// A chave vem da configuração: user-secrets, variável de ambiente ou cofre de segredos
builder.Services.AddSingleton(_ => new EmissaojaClient(new EmissaojaOptions
{
    Chave = builder.Configuration["Emissaoja:Chave"],
}));

var app = builder.Build();

app.MapPost("/pedidos/{id}/nota", async (string id, NotaDoPedido entrada, EmissaojaClient ej, CancellationToken ct) =>
{
    // a referência é o id do pedido: repetir a chamada nunca emite duas vezes
    var emissao = await ej.Notas.EmitirAsync($"pedido-{id}", new CamposNota
    {
        TomadorTipo = entrada.TomadorTipo,
        TomadorId = entrada.TomadorId,
        Valor = entrada.Valor,
        Descricao = entrada.Descricao,
    }, ct);
    return Results.Accepted(value: new { pedido = emissao.Pedido.Id, estado = emissao.Pedido.Estado });
});

app.Run();

record NotaDoPedido(string TomadorTipo, string TomadorId, decimal Valor, string? Descricao);
```

Ou como cliente tipado do `IHttpClientFactory` (o ASP.NET Core já traz `AddHttpClient`; num worker ou console, use o pacote `Microsoft.Extensions.Http`):

```csharp
builder.Services.AddHttpClient("emissaoja")
    .AddTypedClient((http, sp) => new EmissaojaClient(http, new EmissaojaOptions
    {
        Chave = sp.GetRequiredService<IConfiguration>()["Emissaoja:Chave"],
    }));
```

Com a chave só na variável `EMISSAOJA_KEY`, basta `builder.Services.AddHttpClient<EmissaojaClient>();`.

O construtor `new EmissaojaClient(HttpClient http, EmissaojaOptions? opcoes = null)` não altera o `BaseAddress` nem os cabeçalhos padrão do `HttpClient` recebido e não o descarta. Cada chamada tem tempo máximo de 30 s (`EmissaojaOptions.Timeout`), e o `Timeout` do próprio `HttpClient` também vale.

## Empresa

Uma conta tem uma empresa. `onboarding.estado` segue `configurando` → `perguntas` → `pronto`; emitir antes de `pronto` devolve `422 nota_bloqueada` com o que falta.

```csharp
var e = await ej.Empresa.ObterAsync();      // e.Empresa (null antes de conectar), e.Credencial.Estado, e.Onboarding.Estado
var t = await ej.Empresa.ConectarAsync("11222333000181", "senha-do-emissor-nacional"); // 202: verificação em segundo plano
var c = await ej.Empresa.AguardarCredencialAsync(); // c.Tentativa?.ProximoPasso: pronto, configurando, corrigir_senha...

JsonElement onboarding = await ej.Empresa.OnboardingAsync(); // estágios e perguntas pendentes
await ej.Empresa.FiscalAsync(aliquotaSimples: 6.0m);        // responde a pergunta da alíquota do Simples Nacional
await ej.Empresa.RelerFiscoAsync();                         // relê o cadastro fiscal no portal
```

## Tomadores

```csharp
var pj = await ej.Tomadores.CriarAsync("12.345.678/0001-95");               // CNPJ: nome e endereço da Receita
var pf = await ej.Tomadores.CriarAsync("529.982.247-25", "Maria da Silva");  // CPF: informe o nome
// já cadastrado: devolve o existente com pj.JaExistia == true

var pagina = await ej.Tomadores.ListarAsync(new FiltroTomadores { Busca = "silva", Ordem = "recentes", Pagina = 1 });
var achados = await ej.Tomadores.BuscarAsync("12345678");
var ficha = await ej.Tomadores.ObterAsync(TipoTomador.Pj, pj.Tomador.Id);  // com endereço e contato
await ej.Tomadores.AtualizarAsync(TipoTomador.Pj, pj.Tomador.Id, new CamposTomador { Email = "financeiro@cliente.com.br" });
```

Tomador não se apaga: notas emitidas apontam para ele. O documento é imutável.

## Serviços

```csharp
var catalogo = await ej.Servicos.CatalogoAsync("desenvolvimento de software", limite: 10); // códigos nacionais
var novo = await ej.Servicos.CriarAsync(new CamposServico
{
    Descricao = "Desenvolvimento de software",
    CTribNac = catalogo.Itens[0].CTribNac,
    TribIssqn = "tributavel", // ou "nao_incidencia"
    RetemIss = false,
});

var servicos = await ej.Servicos.ListarAsync("todos"); // "1" ativos, "0" inativos, "todos"
await ej.Servicos.AtualizarAsync(novo.Servico.Id, new CamposServico { DescricaoDaNota = "Desenvolvimento de software sob encomenda" });
await ej.Servicos.DesativarAsync(novo.Servico.Id);    // o último serviço ativo não pode: 409 ultimo_servico_ativo
```

`TribIssqn` aceita `tributavel` e `nao_incidencia`; isenção, imunidade e exportação ficam fora do emissaoja. Sem `ServicoId`, a nota usa o serviço principal.

## Notas

```csharp
var emissao = await ej.Notas.EmitirAsync("pedido-2026-11-0042", new CamposNota
{
    TomadorTipo = TipoTomador.Pj,
    TomadorId = tomadorId,
    Valor = 89.90m,                // reais, até 2 casas
    ServicoId = servicoId,         // opcional: sem ele, o serviço principal
    Descricao = "Mensalidade de novembro",
    Competencia = "2026-11-01",    // opcional (AAAA-MM-DD): sem ela, hoje
});

var final = await ej.Notas.AguardarAsync(emissao.Pedido.Id); // timeout padrão de 180 s
switch (final.Pedido.Estado)
{
    case EstadoPedido.Autorizada: Console.WriteLine($"Nº {final.Pedido.Numero}"); break;
    case EstadoPedido.Recusada: Console.WriteLine($"Recusada: {final.Pedido.Motivo}"); break; // recusada não conta no plano
    case EstadoPedido.Incerta: await ej.Emissoes.ConferirAsync(final.Pedido.Id); break;       // nunca emita de novo
}

byte[] pdf = await ej.Notas.PdfAsync(OrigemNota.Local, notaId);   // DANFSe
string xml = await ej.Notas.XmlAsync(OrigemNota.Local, notaId);

// Lista paginada por cursor (locais e importadas do portal)
string? cursor = null;
do
{
    var pagina = await ej.Notas.ListarAsync(new FiltroNotas { Periodo = "mes_atual", Situacao = "emitidas", Cursor = cursor, Limite = 50 });
    foreach (var n in pagina.Notas)
        Console.WriteLine($"{n.Numero} {n.Tomador?.Nome} {n.Valor}");
    cursor = pagina.ProximoCursor;
} while (cursor is not null);

// Cancelar (dentro do prazo do portal) e acompanhar a operação
var op = await ej.Notas.CancelarAsync(OrigemNota.Local, notaId, MotivoCancelamento.ErroNaEmissao, "Valor digitado errado na nota.");
var fim = await ej.Operacoes.AguardarAsync(op.Operacao.Id);
if (fim.Operacao.Estado == "falhou")
    Console.WriteLine(fim.Operacao.Erro?.Code); // por exemplo cancelamento_fora_do_prazo
```

`FiltroNotas.Periodo` aceita `mes_atual`, `mes_anterior`, `ano_atual`, `12_meses` (padrão) e `personalizado` (com `De` e `Ate`). Nota não se altera: para corrigir, cancele e emita outra.

## Emissões, CNPJ e status

```csharp
var andamento = await ej.Emissoes.ListarAsync();          // pedidos na fila, emitindo ou confirmando
var incertas = await ej.Emissoes.ListarAsync("incerta");
var pedido = await ej.Emissoes.ObterAsync(pedidoId);       // uma consulta só; para esperar, Notas.AguardarAsync

JsonElement cnpj = await ej.Cnpj.ConsultarAsync("11.222.333/0001-81"); // dados da Receita; não cria tomador
Console.WriteLine(cnpj.GetProperty("razao_social").GetString());

var status = await ej.StatusAsync();                       // sem chave: Versao, Motor, Ambiente ("producao" ou "sandbox")
```

Respostas tipadas trazem `Extras` com os campos que esta versão ainda não modela. Valores em reais são `decimal`; datas e horas vêm como texto ISO 8601, como a API devolve. Respostas sem formato fixo (onboarding, fiscal, CNPJ) vêm como `JsonElement`.

## Erros

Toda resposta fora de 2xx vira `EmissaojaException`. Decida pelo `Code`, nunca pelo texto:

```csharp
try
{
    await ej.Notas.EmitirAsync(referencia, campos);
}
catch (EmissaojaException e) when (e.Code == "nota_bloqueada")
{
    foreach (var b in e.Bloqueios)
        Console.WriteLine($"{b.Code}: {b.Mensagem} (ação: {b.Acao})");
}
catch (EmissaojaException e)
{
    Console.WriteLine($"{e.Status} {e.Code}: {e.Message} (request_id {e.RequestId})");
}
```

| Code | HTTP | O que fazer |
| --- | --- | --- |
| `chave_invalida` | 401 (0 sem chave) | Confira a chave: `ej_live_` só vale em produção e `ej_test_` só no sandbox. Sem chave nas opções, defina `EMISSAOJA_KEY`. |
| `aceite_pendente` | 409 | Entre no app e aceite os termos. |
| `sem_empresa` | 409 | Conecte a empresa com `Empresa.ConectarAsync`. |
| `validation_error`, `valor_invalido`, `tomador_obrigatorio`, `cnpj_invalido` | 400 | Corrija o campo; `e.Extras["details"]` diz qual. |
| `chave_idempotencia_invalida` | 0 | A referência precisa de 8 a 64 caracteres: letras, números, `_` e `-`. |
| `nota_bloqueada` | 422 | Resolva os itens de `e.Bloqueios` (cadastro, alíquota, código municipal) e emita de novo. |
| `limite_notas` | 402 | Limite do plano no mês. Planos em [emissaoja.com.br/#precos](https://emissaoja.com.br/#precos). |
| `possivel_duplicada` | 409 | Nota igual hoje. Se for mesmo outra, repita com `PermitirRepetidaDe` (veja abaixo). |
| `emissao_repetida_recente` | 409 | A mesma nota há menos de 2 minutos. Se for mesmo outra, repita com `ConfirmoRepeticao = true`. |
| `incerta_pendente` | 409 | Há um pedido incerto igual: confira com `Emissoes.ConferirAsync(e.PedidoId)` antes. |
| `tomador_nao_encontrado`, `servico_nao_encontrado`, `nota_nao_encontrada`, `pedido_nao_encontrado` | 404 | Confira o id. |
| `ultimo_servico_ativo` | 409 | Ative ou crie outro serviço antes de desativar este. |
| `rate_limited` | 429 | Espere `e.RetryAfter` e repita. |
| `fila_indisponivel` | 503 | Repita com a **mesma referência**. |
| `motor_indisponivel`, `receita_indisponivel` | 503 | Portal ou Receita fora do ar; tente de novo em instantes. |
| `rede`, `tempo_esgotado` | 0 | Falha de rede ou sem resposta. Numa emissão, repita com a mesma referência. |
| `resposta_invalida` | 2xx | A resposta não era o JSON esperado (proxy no caminho?). Informe o `RequestId` ao suporte. |

Limites: 60 chamadas por minuto por chave, 10 emissões por minuto, 120 por minuto em buscas e catálogo e 30 por minuto na consulta de CNPJ.

## Idempotência e nota em dobro

- **Referência.** Cada nota leva uma referência sua (o id do pedido no seu sistema serve), enviada no cabeçalho `Idempotency-Key`. Repetir a mesma referência devolve o mesmo pedido, com `Repetido == true`, e nunca emite duas vezes.
- **A biblioteca não repete chamadas sozinha**: emitir tem efeito fiscal. Em `fila_indisponivel` (503), `rede` ou `tempo_esgotado`, chame `EmitirAsync` de novo **com a mesma referência**.
- **`possivel_duplicada`** (409): já saiu hoje uma nota igual (mesmo tomador, valor e competência). Se for mesmo outra nota, repita amarrando a confirmação à nota mostrada:

```csharp
var campos = new CamposNota { TomadorTipo = TipoTomador.Pj, TomadorId = tomadorId, Valor = 1500m };
try
{
    await ej.Notas.EmitirAsync("pedido-0002", campos);
}
catch (EmissaojaException e) when (e.Code == "possivel_duplicada" && e.NotaIgualRecente is { } igual)
{
    // igual.NumeroNfse e igual.EmitidaEm ajudam a pessoa a reconhecer a nota anterior
    campos.PermitirRepetidaDe = igual.PedidoId;
    await ej.Notas.EmitirAsync("pedido-0002", campos);
}
```

- **`emissao_repetida_recente`** (409): a mesma nota em menos de 2 minutos. Se for mesmo outra, repita com `ConfirmoRepeticao = true`.
- **`incerta`**: o portal não confirmou. Não emita de novo; use `Emissoes.ConferirAsync` e `Notas.AguardarAsync`.

## Perguntas frequentes

**Precisa de certificado digital?** Não. O emissaoja entra no Emissor Nacional com o CNPJ e a senha do próprio portal.

**Funciona para MEI?** Sim. O emissaoja atende MEI, ME e EPP optantes pelo Simples Nacional.

**Tem ambiente de testes?** Sim: `https://sandbox.emissaoja.com.br` (`EmissaojaClient.BaseUrlSandbox`), grátis, com o Emissor Nacional simulado e chaves `ej_test_`. Nada vai ao fisco.

**Quanto custa?** O plano Grátis tem 1 nota por mês; a nota conta quando o Emissor Nacional aceita o pedido (recusada não conta). Os outros planos estão em [emissaoja.com.br/#precos](https://emissaoja.com.br/#precos).

**Tem webhook?** Ainda não. Acompanhe o pedido com `Notas.AguardarAsync`, que segue o `proxima_consulta_ms` da API.

## Links

- Documentação: [emissaoja.com.br/docs](https://emissaoja.com.br/docs/) (em Markdown: [docs.md](https://emissaoja.com.br/docs.md))
- OpenAPI 3.1: [emissaoja.com.br/v1/openapi.json](https://emissaoja.com.br/v1/openapi.json)
- Para IAs: [emissaoja.com.br/llms.txt](https://emissaoja.com.br/llms.txt) · MCP: `https://emissaoja.com.br/mcp`
- Status: [emissaoja.com.br/v1/status](https://emissaoja.com.br/v1/status) · Suporte: [suporte@emissaoja.com.br](mailto:suporte@emissaoja.com.br)
- Changelog: [CHANGELOG.md](https://github.com/Easymovel/emissaoja-dotnet/blob/main/CHANGELOG.md)
- Outras bibliotecas oficiais: [npm `emissaoja`](https://www.npmjs.com/package/emissaoja), [PyPI `emissaoja`](https://pypi.org/project/emissaoja/), [Packagist `emissaoja/emissaoja`](https://packagist.org/packages/emissaoja/emissaoja), [NuGet `Emissaoja`](https://www.nuget.org/packages/Emissaoja), [Go `emissaoja.com.br/go`](https://pkg.go.dev/emissaoja.com.br/go), [RubyGems `emissaoja`](https://rubygems.org/gems/emissaoja), [n8n `n8n-nodes-emissaoja`](https://www.npmjs.com/package/n8n-nodes-emissaoja)

## In English

The `Emissaoja` NuGet package is the official .NET (C#) SDK for [emissaoja](https://emissaoja.com.br/?origem=lib-dotnet), a Brazilian **NFS-e (service invoice) API** that issues invoices through the national issuer (Emissor Nacional) with the company's CNPJ and portal password, **no digital certificate required**. Built for MEI and Simples Nacional companies: customers, services, idempotent issuing, status polling, cancellation, PDF (DANFSe) and XML. Targets .NET 8+ and .NET Standard 2.0; method names are in Portuguese, matching the API. Try it for free in the sandbox (`EmissaojaClient.BaseUrlSandbox`); docs at [emissaoja.com.br/docs](https://emissaoja.com.br/docs/).

## Licença

[MIT](https://github.com/Easymovel/emissaoja-dotnet/blob/main/LICENSE). Copyright (c) 2026 BINOBO LTDA.

O emissaoja é um serviço privado e não tem vínculo com a Receita Federal nem com o Emissor Nacional.
