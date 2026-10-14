# Status

**Estado atual (2026-10-14):** backend CLI em C# (.NET 10) com fila de downloads, conversão via FFmpeg, histórico SQLite e adaptador `yt-dlp`. 78 testes passando localmente. A CLI verifica `ffmpeg` e `ffprobe` na inicialização: sem `ffmpeg`, o YouTube usa formato único e só a opção Original fica disponível. Sem interface Avalonia ainda.

**Próximos passos:**
1. Revisar os quatro blocos `catch` vazios (`DownloadCoordinator.cs:88`, `ConsoleWorkflow.cs:180`, `SystemExternalProcessRunner.cs:69-70`).
2. Adicionar licença ao repositório, se quiser que o código seja reutilizável.
3. Iniciar a interface Avalonia consumindo a composição descrita no README.
4. Configurar um hook local de pré-commit com build, format e testes.
5. Medir cobertura da lógica de negócio (meta ≥80%).
