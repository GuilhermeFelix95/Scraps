# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 106 testes passando localmente. O fluxo de download é um caso de uso da Application (`MediaImportService`), reaproveitável pela interface; a CLI é só um adaptador de terminal, e a composição das dependências fica em `ScrapsComposition`. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/`; o app resolve essas ferramentas antes do PATH e atualiza o yt-dlp embutido ao iniciar. O yt-dlp recebe a localização do ffmpeg embutido (`--ffmpeg-location`). Avisos de licença em THIRD-PARTY-NOTICES.md. Interface Avalonia em `src/Scraps.Desktop`: etapa 3 concluída (tela de nova mídia: consulta, escolha de qualidade e enfileiramento). Próxima etapa: fila e progresso.

**Próximos passos:**
1. Etapa 4 da interface Avalonia: fila e progresso, levando os eventos do coordenador para a thread da UI.
2. Revisar os catch vazios ou só comentados: `DownloadCoordinator.cs` (`StopAsync`, `PublishProgress`, `PublishJobFinished`) e `SystemExternalProcessRunner.cs:69-70`.
3. Configurar um hook local de pré-commit com build, format e testes.
4. Medir cobertura da lógica de negócio (meta ≥80%).
5. Ajustar a mensagem de falha do merge: ela manda verificar o PATH mesmo quando o ffmpeg é o embutido (`YtDlpYouTubeVideoSource.cs`). Investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido).
