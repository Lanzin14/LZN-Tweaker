# Fontes embutidas

As duas famílias são de licença livre (SIL Open Font License 1.1, texto em `OFL.txt` de cada pasta) e ficam
embutidas no executável; o `OFL.txt` acompanha a pasta de saída.

| Família | Uso | Arquivos | Origem (baixado em 31/10/2026) |
|---|---|---|---|
| Manrope | Conteúdo do app (`Lzn.Font`) | `Manrope-400/500/600/700.ttf` (subconjunto latino, cobre os acentos do português) | Fontsource via jsDelivr, `cdn.jsdelivr.net/fontsource/fonts/manrope@latest/latin-{peso}-normal.ttf`; licença de `github.com/google/fonts/ofl/manrope` |
| IBM Plex Sans | Sidebar (`Lzn.SidebarFont`) | `IBMPlexSans-Regular/Medium/SemiBold.ttf` (arquivos completos, sem modificação) | Repositório oficial da IBM, `github.com/IBM/plex/packages/plex-sans/fonts/complete/ttf`; licença de `github.com/google/fonts/ofl/ibmplexsans` |

Observações:

- A IBM Plex reserva o nome "Plex" na licença. Por isso os arquivos são os originais, sem subconjunto: uma versão
  modificada não poderia usar esse nome.
- Os arquivos da Manrope declaram a família como "Manrope ExtraLight" (efeito de terem sido gerados a partir da fonte
  variável); os pesos estão corretos. Por isso o `FontFamily` usa `#Manrope ExtraLight`.
- Se uma fonte faltar, o WPF cai para Segoe UI Variable / Segoe UI (listadas depois na mesma `FontFamily`).
