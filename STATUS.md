# Status

**Estado atual (2026-10-14):** backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 86 testes passando localmente. `scripts/fetch-tools.ps1` baixa e verifica yt-dlp e ffmpeg em `third-party/` (etapa 1 do modelo MediaDownloader); o app resolve essas ferramentas antes do PATH (etapa 2). Sem interface Avalonia ainda.

**Próximos passos (etapa 3 do modelo MediaDownloader: atualização automática do yt-dlp ao iniciar, com aviso se falhar; etapa 4: THIRD-PARTY-NOTICES.md):**
1. Revisar os quatro blocos `catch` vazios (`DownloadCoordinator.cs:88`, `ConsoleWorkflow.cs:180`, `SystemExternalProcessRunner.cs:69-70`).
2. Adicionar licença ao repositório, se quiser que o código seja reutilizável.
3. Iniciar a interface Avalonia consumindo a composição descrita no README.
4. Configurar um hook local de pré-commit com build, format e testes.
5. Medir cobertura da lógica de negócio (meta ≥80%).
