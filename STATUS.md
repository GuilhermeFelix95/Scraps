# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 119 testes passando localmente. O fluxo de download é um caso de uso da Application (`MediaImportService`), reaproveitável pela interface; a CLI é só um adaptador de terminal, e a composição das dependências fica em `ScrapsComposition`. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/`; o app resolve essas ferramentas antes do PATH e atualiza o yt-dlp embutido ao iniciar. O yt-dlp recebe a localização do ffmpeg embutido (`--ffmpeg-location`). Avisos de licença em THIRD-PARTY-NOTICES.md. Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso e cancelamento) e histórico (paginação por cursor, abrir pasta). Etapas 3, 4 e 5 concluídas. Próxima etapa: ferramentas e avisos (etapa 6).

**Próximos passos:**
1. Etapa 6 da interface Avalonia: aba de ferramentas (yt-dlp, ffmpeg, ffprobe com `MediaToolchainStatus`), avisos iguais à CLI e `YtDlpUpdater` na inicialização da UI.
2. Etapa 7: revisão independente, cobertura ≥80% da lógica de negócio, e fechamento de STATUS/MEMORY.
3. Revisar os catch vazios ou só comentados: `DownloadCoordinator.cs` (`StopAsync`, `PublishProgress`, `PublishJobFinished`) e `SystemExternalProcessRunner.cs:69-70`.
4. Ordenação do histórico é textual sobre `created_at` gravado com o offset local: se o fuso mudar, linhas antigas podem sair fora de ordem. Corrigir gravando em UTC com migração.
5. Hook de pré-commit (build, format, testes); investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido) e ajustar a mensagem de falha do merge (`YtDlpYouTubeVideoSource.cs`).
