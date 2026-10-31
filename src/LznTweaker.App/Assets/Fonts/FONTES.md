# Fontes embutidas

As duas famílias são de licença livre (SIL Open Font License 1.1, texto em `OFL.txt` de cada pasta) e ficam
embutidas no executável; o `OFL.txt` acompanha a pasta de saída.

| Família | Uso | Arquivos | Origem (baixado em 31/10/2026) |
|---|---|---|---|
| Space Grotesk | Conteúdo do app (`Lzn.Font`) | `SpaceGrotesk-Regular/Medium/Bold.ttf` (arquivos originais, sem modificação; não há SemiBold, então títulos usam o peso mais próximo) | Repositório oficial, `github.com/floriankarsten/space-grotesk/fonts/ttf/static` (baixado em 31/10/2026, substitui a Manrope) |
| IBM Plex Sans | Sidebar (`Lzn.SidebarFont`) | `IBMPlexSans-Regular/Medium/SemiBold.ttf` (arquivos completos, sem modificação) | Repositório oficial da IBM, `github.com/IBM/plex/packages/plex-sans/fonts/complete/ttf`; licença de `github.com/google/fonts/ofl/ibmplexsans` |

Observações:

- A IBM Plex reserva o nome "Plex" na licença. Por isso os arquivos são os originais, sem subconjunto: uma versão
  modificada não poderia usar esse nome.
- Se uma fonte faltar, o WPF cai para Segoe UI Variable / Segoe UI (listadas depois na mesma `FontFamily`).
