# Avisos de terceiros

O Scraps usa ferramentas externas. Elas não são versionadas no git: o script `scripts/fetch-tools.ps1` as baixa das fontes oficiais e confere o SHA-256 de cada arquivo contra o manifesto `scripts/tools.manifest.json`.

Este arquivo é um resumo técnico. Os textos completos das licenças estão na pasta `licenses/`.

## yt-dlp 2026.08.19

- **Origem:** https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19
- **Código-fonte correspondente:** https://github.com/yt-dlp/yt-dlp/tree/2026.08.19
- **Licença do projeto:** The Unlicense (domínio público). Texto: [`licenses/yt-dlp/LICENSE`](licenses/yt-dlp/LICENSE).
- **Atenção:** o executável oficial, gerado com PyInstaller, inclui código de outros projetos sob licenças diferentes, incluindo GPLv3 ou posterior. O próprio projeto yt-dlp documenta isso. Quem redistribuir o executável deve cumprir essas licenças e oferecer o código-fonte correspondente, disponível no link acima.
- **SHA-256 verificado:** `66674953FE251B89F4D08C5F0E35E0728679BD67AB3D7D05C0562AF101DD3E7A`

## ffmpeg 9.0.2 e ffprobe (build essentials de gyan.dev)

- **Origem do binário:** https://www.gyan.dev/ffmpeg/builds/ (arquivo `ffmpeg-release-essentials.zip`)
- **Código-fonte correspondente:**
  - Release oficial: https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz
  - Commit usado no build: https://github.com/FFmpeg/FFmpeg/commit/946fcce07b
- **Licença:** GPLv3. O build inclui componentes sob GPL, como `libx264` e `libx265`, por isso o conjunto é distribuído sob GPLv3. Texto: [`licenses/ffmpeg/LICENSE`](licenses/ffmpeg/LICENSE).
- **Versões das bibliotecas incluídas:** [`licenses/ffmpeg/BUILD-INFO.txt`](licenses/ffmpeg/BUILD-INFO.txt), que vem do próprio build e lista cada componente com a versão.
- **SHA-256 verificado:** `60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA`

## Não distribuídos pelo projeto

- **Deno ou Node.js:** runtimes JavaScript que o yt-dlp usa para os desafios do YouTube. O Scraps não os inclui; o usuário os instala separadamente.

## Licença do Scraps

O código do Scraps ainda não tem licença definida. Enquanto isso, o código não pode ser reutilizado por terceiros.

## Aviso

Este documento não substitui os textos das licenças. Para distribuição comercial, consulte um advogado.
