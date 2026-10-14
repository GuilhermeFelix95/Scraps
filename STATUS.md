# Status

**Estado atual (2026-10-14):** código sob licença MIT (`LICENSE`). Backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 137 testes passando localmente; cobertura de linhas: Application 90,7%, Infrastructure 82%, Desktop 49% (apresentação, fora da meta). Interface Avalonia em `src/Scraps.Desktop` com abas: nova mídia, fila (progresso, cancelamento e erro de acompanhamento), histórico (paginação por cursor, abrir pasta) e ferramentas. A CLI e a interface usam a mesma composição (`ScrapsComposition`) e o mesmo `YtDlpUpdater`.

**Etapas do plano da interface (0 a 7):** concluídas. A revisão independente foi feita duas vezes; os achados da segunda foram verificados e corrigidos, inclusive os de baixa severidade (5 e 6) e o separador do histórico. Validação visual das quatro abas e leitura do Smart App Control (`VerifiedAndReputablePolicyState = 0`, desligado) registradas em MEMORY.

**Backlog da revisão:** catch vazios removidos (observadores e runner de processo agora reportam a falha pelo evento `WorkerFaulted` ou por `Trace`); ordenação do histórico em UTC na gravação e no cursor; hook de pré-commit em `.githooks/pre-commit` (formatação, build com avisos como erro e testes) ativado por `git config core.hooksPath .githooks`.

**Pendências que dependem de você ou de uma decisão:**
- **403 do YouTube:** não reproduzido nesta verificação. Com o yt-dlp empacotado (2026.08.19), os metadados e as URLs de formato saem e uma requisição HEAD responde 200. Para fechar: rode o app sem `SCRAPS_YTDLP` e baixe o mesmo vídeo; se o 403 voltar, a investigação segue com o log dessa execução.
- Um job cuja gravação falha durante a reivindicação fica como Queued até a próxima inicialização, e o acompanhamento dele não conclui antes disso. Aceito como limitação; a correção completa exige um estado de falha de worker no histórico.
- `WorkerFaulted` e os `Trace` não têm log persistente: não há infraestrutura de log no app. Decidir se a CLI e a interface devem gravar em arquivo.
- Dados antigos: linhas gravadas antes desta mudança mantêm o offset original. Como `DownloadJob.Create` usa UTC, o esperado é que sejam UTC, mas não verifiquei um banco real de produção.

**Próximos passos:**
1. Você: testar o download do YouTube pelo app sem `SCRAPS_YTDLP` e me enviar o erro, se houver.
2. Decidir sobre log persistente (CLI e interface) antes de qualquer implementação.
3. Medir a cobertura da lógica de negócio no pré-commit ou em comando separado, se quiser o limite de 80% automatizado.
4. Migrar o histórico para um estado de falha de worker, para que nenhum job fique sem conclusão.
