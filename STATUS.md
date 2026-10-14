# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 137 testes passando localmente. Cobertura de linhas medida com coverlet: Application 92,4%, Infrastructure 82,1% (ambas acima da meta de 80% da lógica de negócio); Desktop 52,5% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso, cancelamento e erro de acompanhamento), histórico (paginação por cursor, abrir pasta) e ferramentas. A CLI e a interface usam a mesma composição (`ScrapsComposition`) e o mesmo `YtDlpUpdater`.

**Etapas do plano da interface (0 a 7):** concluídas. Revisão independente feita duas vezes; achados verificados e corrigidos. Validação visual das quatro abas e leitura do Smart App Control (`VerifiedAndReputablePolicyState = 0`, desligado) registradas em MEMORY.

**Backlog da revisão:** concluído, exceto os itens abaixo.
- Catches vazios removidos: observadores reportam por `WorkerFaulted`; runner de processo registra por `Trace`.
- Ordenação do histórico em UTC na gravação e no cursor.
- Pré-commit em `.githooks/pre-commit`, ativado por `git config core.hooksPath .githooks`.
- Job cuja reivindicação falha termina como Failed; se o banco continuar indisponível, a recuperação também falha e o job é marcado só na próxima inicialização (limitação conhecida).

**Pendências:**
- **403 do YouTube (depende do usuário):** não reproduzido nesta verificação. Com o yt-dlp empacotado (2026.08.19), metadados e URLs de formato saem e uma requisição HEAD responde 200. Para fechar: rodar um download real pelo app sem `SCRAPS_YTDLP` e registrar o resultado. Não é possível fechar sem esse teste.
- **Log persistente (fora do escopo, decisão do usuário):** `WorkerFaulted` e os `Trace` não gravam em arquivo porque o app não tem infraestrutura de log. Implementar exige decisão de onde gravar e por quanto tempo; registrado aqui conforme a regra de escopo fechado do CLAUDE.md §4.
- **Verificação automática de cobertura no pré-commit:** não incluída (lenta). A medição acima é manual.

**Próximos passos:**
1. Usuário: teste de download do YouTube pelo app sem `SCRAPS_YTDLP`; enviar o resultado.
2. Usuário: decidir sobre log persistente (CLI e interface).
3. Se quiser cobertura automatizada, criar comando separado com o limite de 80%.
