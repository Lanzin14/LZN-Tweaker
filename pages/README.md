# Atalho `lanzin14.github.io/win`

Conteúdo do repositório `Lanzin14/Lanzin14.github.io` (GitHub Pages do usuário). O arquivo `win/index.html` não é
HTML: é uma linha de PowerShell que baixa o `install.ps1` da última Release e o executa. Assim, o comando

```powershell
irm https://lanzin14.github.io/win | iex
```

nunca precisa mudar: cada nova versão só publica uma Release nova (`.github/workflows/release.yml`).

Para atualizar o atalho, copie `win/index.html` para a raiz daquele repositório, na mesma pasta `win/`.
