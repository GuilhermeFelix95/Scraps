# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 129 testes passando localmente; cobertura de linhas: Application 90,7%, Infrastructure 82%, Desktop 49% (apresentação, fora da meta). O fluxo de download é um caso de uso da Application (`MediaImportService`), reaproveitável pela interface; a CLI e a interface usam a mesma composição (`ScrapsComposition`) e o mesmo `YtDlpUpdater`. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/`; o app resolve essas ferramentas antes do PATH. Avisos de ferramentas ausentes ficam em `ToolchainWarnings`, iguais na CLI e na interface. Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso e cancelamento), histórico (paginação por cursor, abrir pasta) e ferramentas. Etapas 0 a 7 do plano concluídas, com revisão feita pelo assistente (sem Revisor independente) e janela verificada só por execução de 15 s, sem inspeção visual.

**Próximos passos:**
1. Inspecionar visualmente a janela (layout, abas e textos) e checar se o Smart App Control bloqueia as DLLs do Avalonia em execução real.
2. Revisar os catch vazios ou só comentados: `DownloadCoordinator.cs` (`StopAsync`, `PublishProgress`, `Publish`) e `SystemExternalProcessRunner.cs:69-70`. Também `QueueViewModel.Track` e `HistoryViewModel`, que disparam tarefas sem observar exceções (revisão independente: risco baixo).
3. Ordenação do histórico é textual sobre `created_at` gravado com o offset local: se o fuso mudar, linhas antigas podem sair fora de ordem. Corrigir gravando em UTC com migração.
4. Hook de pré-commit (build, format, testes) e checagem automática de cobertura.
5. Investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido) e ajustar a mensagem de falha do merge (`YtDlpYouTubeVideoSource.cs`).
