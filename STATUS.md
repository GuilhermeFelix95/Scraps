# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 93 testes passando localmente. O yt-dlp recebe a localização do ffmpeg embutido (`--ffmpeg-location`), o que corrige a falha de mesclagem com faixas separadas. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/` (etapa 1 do modelo MediaDownloader); o app resolve essas ferramentas antes do PATH (etapa 2) e atualiza o yt-dlp embutido ao iniciar (etapa 3). Avisos de licença em THIRD-PARTY-NOTICES.md (etapa 4). Sem interface Avalonia ainda.

**Próximos passos:**
1. Revisar os quatro blocos `catch` vazios (`DownloadCoordinator.cs:88`, `ConsoleWorkflow.cs:180`, `SystemExternalProcessRunner.cs:69-70`).
2. Iniciar a interface Avalonia consumindo a composição descrita no README.
3. Configurar um hook local de pré-commit com build, format e testes.
4. Medir cobertura da lógica de negócio (meta ≥80%).
5. Ajustar a mensagem de falha do merge: ela manda verificar o PATH mesmo quando o ffmpeg é o embutido (`YtDlpYouTubeVideoSource.cs`).
