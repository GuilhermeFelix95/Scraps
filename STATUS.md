# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 137 testes passando localmente. Cobertura de linhas medida com coverlet: Application 92,4%, Infrastructure 82,1% (acima da meta de 80% da lógica de negócio); Desktop 52,5% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila, histórico e ferramentas. A CLI e a interface usam a mesma composição (`ScrapsComposition`) e o mesmo `YtDlpUpdater`.

**Etapas do plano da interface (0 a 7):** concluídas, com revisão independente feita duas vezes e achados corrigidos. Validação visual das quatro abas e leitura do Smart App Control (`VerifiedAndReputablePolicyState = 0`, desligado) registradas em MEMORY.

**Backlog original (catches vazios, ordenação UTC, pré-commit, 403, mensagem de merge):** concluído, exceto o 403.
- Pré-commit em `.githooks/pre-commit`, ativado por `git config core.hooksPath .githooks`.
- Catches vazios substituídos por relato de falha (`WorkerFaulted`) ou `Trace`.
- Ordenação do histórico em UTC na gravação e no cursor.
- Mensagem de merge do yt-dlp cita o caminho do ffmpeg configurado.
- Job cuja reivindicação falha termina como Failed. Se o banco continuar indisponível, o job só é marcado na próxima inicialização (limitação conhecida).

**Aberto:**
- **403 do YouTube (único item do backlog pendente):** não reproduzido. Com o yt-dlp empacotado (2026.08.19), metadados e URLs de formato saem e um HEAD responde 200. Fechar exige um download real pelo app sem `SCRAPS_YTDLP`, que depende de decisão do usuário.

**Propostas fora do escopo (não são backlog; registradas conforme CLAUDE.md §4):**
- Log persistente para `WorkerFaulted` e `Trace`. Não há infraestrutura de log; destino e retenção são decisão do usuário.
- Verificação automática de cobertura no pré-commit (lenta). A medição é manual.
