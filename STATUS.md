# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 132 testes passando localmente; cobertura de linhas: Application 90,7%, Infrastructure 82%, Desktop 49% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso e cancelamento), histórico (paginação por cursor, abrir pasta) e ferramentas. A CLI e a interface usam a mesma composição (`ScrapsComposition`) e o mesmo `YtDlpUpdater`.

**Etapa 7 do plano:** revisão independente (subagente Revisor) feita duas vezes. A segunda, sobre o código já corrigido, apontou 8 achados; os 4 bloqueantes, o teste instável e o texto de teste superlativo foram corrigidos neste branch (ver MEMORY). Validação visual feita por captura da janela Release das quatro abas: layout e textos corretos, com um defeito cosmético pendente. Smart App Control: `VerifiedAndReputablePolicyState = 0` (desligado) em leitura; não alterado.

**Pendências dos achados da revisão (não corrigidas nesta etapa):**
- `DownloadCoordinator.CancelAsync` pode devolver `true` para um job que termina como Completed entre a gravação final e a remoção de `_active` (achado 6, baixa severidade).
- `QueueViewModel.Track` dispara `FollowAsync` sem tratar exceção; se a leitura falhar, o item fica com o último status e o botão Cancelar continua habilitado (achado 5).
- Um job cuja gravação falha durante a reivindicação fica como Queued no histórico e o `WaitForFinishedAsync` dele não conclui até a próxima inicialização (limitação da correção de worker).
- Histórico: quando não há erro, aparece `Concluído · ` com separador vazio (`HistoryView.axaml:18`).
- Achados de revisão sem tratamento: o `WorkerFaulted` ainda não tem consumidor nem log.

**Próximos passos:**
1. Corrigir o separador vazio do histórico e tratar `FollowAsync` (achado 5) com teste antes da correção.
2. Revisar os catch vazios ou só comentados: `SystemExternalProcessRunner.cs:69-70` e `Publish`/`PublishProgress` (aceitáveis como isolamento de observadores, mas sem log).
3. Ordenação do histórico é textual sobre `created_at`. O revisor leu que `DownloadJob.Create` usa UTC, o que tornaria a ordenação correta; não testei isso contra um banco real com dados antigos. Verificar e, se necessário, migrar para UTC.
4. Hook de pré-commit (build, format, testes) e checagem automática de cobertura.
5. Investigar o 403 do download completo do YouTube (depende de testar com `SCRAPS_YTDLP` removido) e ajustar a mensagem de falha do merge (`YtDlpYouTubeVideoSource.cs`).
