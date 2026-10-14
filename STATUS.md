# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 111 testes passando localmente. O fluxo de download é um caso de uso da Application (`MediaImportService`), reaproveitável pela interface; a CLI é só um adaptador de terminal, e a composição das dependências fica em `ScrapsComposition`. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/`; o app resolve essas ferramentas antes do PATH e atualiza o yt-dlp embutido ao iniciar. O yt-dlp recebe a localização do ffmpeg embutido (`--ffmpeg-location`). Avisos de licença em THIRD-PARTY-NOTICES.md. Interface Avalonia em `src/Scraps.Desktop`: etapas 3 (nova mídia) e 4 (fila com progresso e cancelamento) concluídas. Próxima etapa: histórico (exige OK de contrato para paginação por cursor).

**Próximos passos:**
1. Etapa 5 da interface Avalonia: histórico paginado por cursor. Antes, pedir OK para mudar o contrato de `IDownloadHistoryRepository`.
2. Etapa 6: painel de ferramentas (yt-dlp, ffmpeg, ffprobe) e avisos iguais à CLI, com a execução de `YtDlpUpdater` na inicialização da UI.
3. Revisar os catch vazios ou só comentados: `DownloadCoordinator.cs` (`StopAsync`, `PublishProgress`, `PublishJobFinished`) e `SystemExternalProcessRunner.cs:69-70`.
4. Configurar um hook local de pré-commit com build, format e testes; medir cobertura da lógica de negócio (meta ≥80%).
5. Ajustar a mensagem de falha do merge (`YtDlpYouTubeVideoSource.cs`) e investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido).
