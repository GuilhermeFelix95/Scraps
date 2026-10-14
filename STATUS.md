# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 140 testes passando localmente. Cobertura de linhas medida pelo pré-commit: Application 91,9%, Infrastructure 83,2% (mínimo 80%); Desktop 52,5% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila, histórico e ferramentas.

**Etapas do plano da interface (0 a 7):** concluídas, com revisão independente feita duas vezes e achados corrigidos. Validação visual das quatro abas e leitura do Smart App Control (`VerifiedAndReputablePolicyState = 0`, desligado) registradas em MEMORY.

**Backlog original:** concluído. Pré-commit (formatação, build com avisos como erro, testes e cobertura mínima), catches vazios substituídos, ordenação UTC, mensagem de merge do ffmpeg, falha de reivindicação do worker, log em arquivo diário.

**Propostas executadas nesta etapa:**
- Log em arquivo diário em `%LOCALAPPDATA%\Scraps\logs`, retenção de 7 dias. Registra falhas de worker (`WorkerFaulted`) e falhas reportadas via `Trace`. Limite de retenção e pasta são os padrões; mudar exige decisão do usuário.
- Cobertura automática no pré-commit com limite de 80% (`scripts/check-coverage.sh`), para Application e Infrastructure. O limite é o da regra do CLAUDE.md §6.

**403 do YouTube: resolvido.** Reproduzido pelo app no download completo (fluxo Consultar, Enfileirar e download completo), sem `SCRAPS_YTDLP`: o usuário confirmou conclusão em 2026-10-14. A causa exata do 403 com `SCRAPS_YTDLP` definida não foi identificada; o yt-dlp empacotado (2026.08.19) não reproduziu o erro nos testes.

**Limitações conhecidas:**
- Job cuja reivindicação falha termina como Failed; se o banco continuar indisponível, o job só é marcado na próxima inicialização.
- O log não rotaciona dentro do mesmo dia; com o app aberto por muito tempo, um dia pode crescer.
- `SCRAPS_YTDLP` continua definida na variável de usuário do Windows. Remover de vez é decisão do usuário; o app funciona sem ela.

**Próximos passos:**
1. Apagar a branch local `fix/yt-dlp-ffmpeg-location` (já incluída na `main` via #9), com OK do usuário.
2. Decidir se a variável `SCRAPS_YTDLP` do usuário será removida de vez (decisão do usuário).
3. Usar o app e acompanhar `%LOCALAPPDATA%\Scraps\logs` para novos erros.
4. Se quiser outro destino ou retenção para o log, ajustar `AppLog.RetentionDays` e `AppLog.DefaultDirectory`.
