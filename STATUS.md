# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 140 testes passando localmente. Cobertura de linhas medida pelo pré-commit: Application 91,9%, Infrastructure 83,2% (mínimo 80%); Desktop 52,5% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila, histórico e ferramentas.

**Etapas do plano da interface (0 a 7):** concluídas, com revisão independente feita duas vezes e achados corrigidos. Validação visual das quatro abas e leitura do Smart App Control (`VerifiedAndReputablePolicyState = 0`, desligado) registradas em MEMORY.

**Backlog original:** concluído. Pré-commit (formatação, build com avisos como erro, testes e cobertura mínima), catches vazios substituídos, ordenação UTC, mensagem de merge do ffmpeg, falha de reivindicação do worker. O 403 do YouTube continua aberto (ver Limitações).

**Propostas executadas nesta etapa:**
- Log em arquivo diário em `%LOCALAPPDATA%\Scraps\logs`, retenção de 7 dias. Registra falhas de worker (`WorkerFaulted`) e falhas reportadas via `Trace`. Limite de retenção e pasta são os padrões; mudar exige decisão do usuário.
- Cobertura automática no pré-commit com limite de 80% (`scripts/check-coverage.sh`), para Application e Infrastructure. O limite é o da regra do CLAUDE.md §6.

**Limitações conhecidas:**
- **403 do YouTube: aberto.** Verificado só com um download de 10 s fora do app (2026-10-14), sem 403. Falta reproduzir pelo próprio app (fluxo Consultar, Enfileirar e download completo), ou o usuário aceitar explicitamente o teste externo como encerramento.
- Job cuja reivindicação falha termina como Failed; se o banco continuar indisponível, o job só é marcado na próxima inicialização.
- O log não rotaciona dentro do mesmo dia; com o app aberto por muito tempo, um dia pode crescer.

**Próximos passos:**
1. Usar o app e acompanhar `%LOCALAPPDATA%\Scraps\logs` para novos erros.
2. Se quiser outro destino ou retenção para o log, ajustar `AppLog.RetentionDays` e `AppLog.DefaultDirectory`.
