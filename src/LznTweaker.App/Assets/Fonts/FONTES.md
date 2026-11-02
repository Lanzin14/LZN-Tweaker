# Fontes embutidas

As duas famílias são de licença livre (SIL Open Font License 1.1, texto em `OFL.txt` de cada pasta) e ficam
embutidas no executável; o `OFL.txt` acompanha a pasta de saída e o `LznTweaker-licencas.zip` de cada Release.

| Família | Uso | Arquivos | Origem |
|---|---|---|---|
| Geist | App inteiro: conteúdo (`Lzn.Font`) e sidebar (`Lzn.SidebarFont`) | `Geist-Regular/Medium/SemiBold/Bold.ttf` (originais, sem modificação) | Repositório oficial da Vercel, `github.com/vercel/geist-font/fonts/Geist/ttf`, commit `a0a06a3` (02/04/2026), baixado em 02/11/2026 |
| Geist Mono | Textos técnicos: caminhos, comandos, valores (`Lzn.MonoFont`) | `GeistMono-Regular/Medium.ttf` (originais, sem modificação) | Mesmo repositório, `fonts/GeistMono/ttf`, mesmo commit |

Observações:

- Escolhida em 02/11/2026 no lugar da Space Grotesk (conteúdo) e da IBM Plex Sans (sidebar): uma família só deixa o
  app coeso, e a Geist tem o peso SemiBold que os títulos usam (a Space Grotesk não tinha).
- Só os pesos usados no XAML (Regular, Medium, SemiBold e Bold) são embutidos, para não pesar no `.exe`.
- Se uma fonte faltar, o WPF cai para Segoe UI Variable / Segoe UI e Cascadia Mono / Consolas (listadas depois na
  mesma `FontFamily`).
