# Changelog

Todas as mudanças relevantes deste pacote. Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões seguem [SemVer](https://semver.org/lang/pt-BR/).

## [0.1.0] - 2026-11-05

Primeira versão pública.

### Adicionado

- `EmissaojaClient` com `EmissaojaOptions` (`Chave`, `BaseUrl`, `Timeout`) e construtor que aceita um `HttpClient` externo (`IHttpClientFactory`). Chave lida de `EMISSAOJA_KEY` na primeira chamada que precisa dela; `BaseUrlSandbox` para o ambiente de testes.
- Recursos `Empresa` (obter, conectar com CNPJ e senha do Emissor Nacional, credencial, aguardar a verificação, onboarding, perguntas fiscais, reler o fisco), `Tomadores`, `Servicos` (com o catálogo nacional), `Notas` (emitir, aguardar, listar por cursor, PDF, XML, cancelar), `Emissoes`, `Operacoes`, `Cnpj` e `StatusAsync`.
- Emissão idempotente: a referência vai no cabeçalho `Idempotency-Key` e é validada antes de chamar a API.
- `Notas.AguardarAsync` respeita `proxima_consulta_ms`; `Empresa.AguardarCredencialAsync` e `Operacoes.AguardarAsync` com intervalo e tempo máximo.
- Modelos tipados (`Pedido`, `Tomador`, `Servico`, `Nota`, `Operacao`, `Tentativa`, `Credencial` e as respostas) com `Extras` para campos novos; JSON gerado em compilação (trimming e Native AOT no .NET 8).
- `EmissaojaException` com `Status`, `Code`, `Extras`, `RequestId`, `RetryAfter`, `Bloqueios`, `NotaIgualRecente` e `PedidoId`.
- Alvos `net8.0` (sem dependências) e `netstandard2.0` (`System.Text.Json`).

[0.1.0]: https://github.com/Easymovel/emissaoja-dotnet/releases/tag/v0.1.0
