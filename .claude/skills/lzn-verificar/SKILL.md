---
name: lzn-verificar
description: Ritual de verificação do LZN Tweaker depois de mudar código — compila sem esbarrar no app aberto, roda os testes do núcleo e de interface, atualiza o grafo, confere os prints e faz o commit. Use ao terminar qualquer mudança em src/ ou tests/, antes de dizer que está pronto.
argument-hint: "[mensagem do commit]"
---

# Verificar uma mudança

Siga na ordem e pare no primeiro passo que falhar: conserte a causa, não pule o passo.

## 1. Compilar

O app aberto trava `%TEMP%\lzn-build`. Compile em outra pasta:

```bash
dotnet build src/LznTweaker.App "-p:BaseOutputPath=$TEMP/lzn-build2/" -v q 2>&1 | grep -E "error|Aviso|Erro|Compilação|Build succeeded"
```

Zero erros e zero avisos novos.

## 2. Testes do núcleo (sem elevação)

```bash
dotnet test ./tests/LznTweaker.Core.Tests "-p:BaseOutputPath=$TEMP/lzn-test/" 2>&1 | grep -E "Aprovado!|Com falha!|error" | tail -3
```

## 3. Testes de interface

Abrem todas as páginas fora da tela e salvam prints em `%TEMP%\lzn-smoke`; falham com erro de XAML, recurso, binding
ou contraste. Levam ~45 s.

```bash
rm -rf "$TEMP/lzn-smoke"; dotnet test ./tests/LznTweaker.App.Smoke "-p:BaseOutputPath=$TEMP/lzn-smoke-test/" 2>&1 | grep -E "Aprovado!|Com falha!|error" | head
```

## 4. Conferir os prints

Abra com Read os prints das telas que a mudança tocou (`ls "$TEMP/lzn-smoke"`; nomes como `home.png`,
`general-list.png`, `games-fortnite.png`, `sidebar-expanded.png`). Procure texto cortado ou sobreposto, botão roxo
repetido na mesma tela, recomendação aparecendo como "Aplicado" e cor fora do tema. Para uma revisão maior, use o
subagente `revisor-visual`.

## 5. Área sensível

Se o diff toca `sensitive_paths` (`tools/graph/expectations.json`), rode o subagente `revisor-seguranca-windows`
sobre o diff antes do commit.

## 6. Commit

Um commit por mudança (regra do CLAUDE.md), mensagem em português no estilo do `git log`, terminando com a linha
`Co-Authored-By` da sessão. Use o argumento como título, se houver.

## 7. Grafo

```bash
powershell -NoProfile -File tools/graph/graph.ps1 update 2>&1 | tail -2
```

"FALHOU" só pelos arquivos de licença (`OFL.txt`, `LICENSE-GPL-3.0.txt`) é conhecido e não bloqueia; qualquer outro
problema, investigue. Para mudança estrutural, siga o roteiro completo do CLAUDE.md (`snapshot antes` → `diff antes`).

## 8. Relatório

Diga o que passou com números (ex.: 158 do núcleo, 24 de interface), o hash do commit e o que não foi verificado
(por exemplo, o app aberto de verdade).
