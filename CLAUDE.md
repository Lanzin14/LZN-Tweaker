# LznTweaker: instruções para o assistente

Otimizador do Windows em WPF/.NET 10 (`src/LznTweaker.App`, `src/LznTweaker.Core`, `tests/`). Visão geral em `README.md`.

## Grafo de conhecimento

Antes de mudar código, use o grafo em `graphify-out/` (ver `docs/GRAPHIFY.md`) e confirme no arquivo real:

- `graphify explain "<Tipo>"`, `graphify affected "<Tipo>" --depth 2`, `graphify query "<pergunta>"`.
- Mudança estrutural: `.\tools\graph\graph.ps1 snapshot antes` → mudança → `dotnet test` →
  `.\tools\graph\graph.ps1 update` → `.\tools\graph\graph.ps1 diff antes` → rodar os testes listados.
- Relações `INFERRED` são hipóteses. O grafo sem problemas não prova que uma mudança é segura.
- Use `update` (sem LLM) para código; reconstrução completa (`/graphify`) só quando documentos mudarem
  ou o `check` pedir.

## Segurança

Áreas sensíveis (`sensitive_paths` em `tools/graph/expectations.json`) alteram o Windows: preserve backup, ponto de
restauração obrigatório, `VerifyScript`, desfazer e o registro no histórico. Teste sem aplicar mudanças reais ao sistema.

## Build e testes

O app aberto trava `src/LznTweaker.App/bin`; compile com `-p:BaseOutputPath="$env:TEMP\lzn-build\"`.
Os testes rodam sem elevação: `dotnet test .\tests\LznTweaker.Core.Tests -p:BaseOutputPath="$env:TEMP\lzn-test\"`.
