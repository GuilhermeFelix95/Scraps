# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 125 testes passando localmente. O fluxo de download é um caso de uso da Application (`MediaImportService`), reaproveitável pela interface; a CLI é só um adaptador de terminal, e a composição das dependências fica em `ScrapsComposition`. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/`; o app resolve essas ferramentas antes do PATH e atualiza o yt-dlp embutido ao iniciar (CLI e interface usam o mesmo `YtDlpUpdater`). Avisos de ferramentas ausentes ficam em `ToolchainWarnings` e são iguais na CLI e na interface. O yt-dlp recebe a localização do ffmpeg embutido (`--ffmpeg-location`). Avisos de licença em THIRD-PARTY-NOTICES.md. Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso e cancelamento), histórico (paginação por cursor, abrir pasta) e ferramentas. Etapas 3 a 6 concluídas. Próxima etapa: fechamento (etapa 7).

**Próximos passos:**
1. Etapa 7: revisão da etapa, cobertura ≥80% da lógica de negócio e fechamento de STATUS/MEMORY.
2. Revisar os catch vazios ou só comentados: `DownloadCoordinator.cs` (`StopAsync`, `PublishProgress`, `PublishJobFinished`) e `SystemExternalProcessRunner.cs:69-70`.
3. Ordenação do histórico é textual sobre `created_at` gravado com o offset local: se o fuso mudar, linhas antigas podem sair fora de ordem. Corrigir gravando em UTC com migração.
4. Hook de pré-commit (build, format, testes).
5. Investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido) e ajustar a mensagem de falha do merge (`YtDlpYouTubeVideoSource.cs`).
