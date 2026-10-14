# Scraps

Aplicativo desktop em Avalonia (.NET 10), com importação de arquivos autorizados, conversão por qualidade, fila sequencial, cancelamento, progresso e histórico SQLite. A interface (`src/Scraps.Desktop`) tem abas para nova mídia, fila com progresso e cancelamento, histórico paginado e status das ferramentas. A CLI (`src/Scraps.Cli`) usa a mesma lógica no terminal.

> **Aviso de responsabilidade.** O Scraps não autoriza, incentiva nem verifica se você pode baixar um vídeo ou outra mídia. **Baixar, armazenar ou processar conteúdo sem autorização do titular dos direitos é de responsabilidade exclusiva do usuário**, em qualquer jurisdição, e pode violar os termos de uso de plataformas como o YouTube e a legislação de direitos autorais. Use a ferramenta apenas com conteúdo que você tem permissão para baixar. O software é fornecido "como está", sem garantias, conforme a [licença MIT](LICENSE). Este aviso não é aconselhamento jurídico.

## YouTube e uso autorizado

- A CLI aceita uma URL de **um vídeo** do YouTube, consulta metadados com `yt-dlp`, pede confirmação explícita e, após a escolha de qualidade, baixa o vídeo para uma pasta temporária isolada antes de usar a fila e o fluxo de conversão existentes.
- A integração é uma tentativa para vídeos que `yt-dlp` consiga acessar naquele momento; **não há garantia de baixar todos os vídeos**. Playlists/canais em lote não são suportados.
- Scraps não fornece login/cookies, não desbloqueia vídeos privados ou inacessíveis, não contorna DRM nem outros controles de acesso. A confirmação do usuário não concede direitos nem transforma uma fonte restrita em acessível.
- Cabe ao usuário ter autorização aplicável para baixar e processar a mídia. A ferramenta não determina direitos autorais, permissões ou termos aplicáveis.
- URLs diretas continuam limitadas a HTTP/HTTPS, com validação de DNS/IP e revalidação dos redirecionamentos para bloquear redes locais/privadas. A URL direta é baixada temporariamente para inspeção e o arquivo preparado segue para a fila sem uma segunda transferência.

## Ferramentas embutidas (opcional)

Para não depender do PATH, rode o script de preparo. Ele baixa `yt-dlp` e `ffmpeg` das fontes oficiais, confere o SHA-256 de cada arquivo e grava em `third-party/`:

```powershell
pwsh -File scripts/fetch-tools.ps1
```

O build copia `third-party/` para junto do executável. A ordem de resolução é: variável de ambiente (`SCRAPS_YTDLP`, `SCRAPS_FFMPEG`), depois `third-party/`, e por último o PATH. Os binários não são versionados no git. Licenças e links do código-fonte das ferramentas estão em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Ao iniciar, o app roda `yt-dlp -U` no binário embutido, com limite de 30 s. Se a atualização falhar, ele avisa e segue com a versão instalada. Instalações externas (PATH ou `SCRAPS_YTDLP`) não são atualizadas pelo app.

## Requisitos

- .NET 10 SDK.
- `ffmpeg` e `ffprobe`, em `third-party/` (ver [Ferramentas embutidas](#ferramentas-embutidas-opcional)) ou acessíveis pelo `PATH`.
- Build do FFmpeg com encoder `libx264` para os presets que convertem.
- `yt-dlp` atualizado e instalado separadamente; o Scraps não baixa nem instala executáveis automaticamente. No Windows, a distribuição oficial `yt-dlp.exe` já inclui os scripts EJS. Se não estiver no `PATH`, configure `SCRAPS_YTDLP` com o caminho do executável.
- Para extração completa do YouTube, `yt-dlp` precisa de um runtime JavaScript compatível e dos scripts EJS. Deno 2.3+ é habilitado por padrão. Como alternativa, Node.js 22+ pode ser selecionado com `SCRAPS_YTDLP_JS_RUNTIME=node` (ou `node:C:/caminho/node.exe`). Com a instalação Python, use `python -m pip install -U "yt-dlp[default]"`, que inclui `yt-dlp-ejs`; pacotes de terceiros podem exigir instalação compatível do componente.

Referências oficiais: [yt-dlp](https://github.com/yt-dlp/yt-dlp), [guia EJS e runtimes](https://github.com/yt-dlp/yt-dlp/wiki/EJS) e [yt-dlp-ejs](https://github.com/yt-dlp/ejs).

Verifique os requisitos básicos com PowerShell:

```powershell
ffmpeg -version
ffprobe -version
ffmpeg -hide_banner -encoders | Select-String -Pattern libx264
yt-dlp --version
node --version
# ou, se usar Deno:
deno --version
```

Quando `yt-dlp` estiver instalado fora do `PATH`, configure-o antes de iniciar a CLI:

```powershell
$env:SCRAPS_YTDLP = 'C:\caminho\para\yt-dlp.exe'
# Opcional: selecione Node se Deno não estiver instalado/for desejado.
$env:SCRAPS_YTDLP_JS_RUNTIME = 'node'
```

Se usar Deno, deixe `SCRAPS_YTDLP_JS_RUNTIME` sem definir: o `yt-dlp` habilita Deno por padrão. Para uma instalação Python do `yt-dlp`, consulte a documentação oficial sobre `yt-dlp-ejs`; a instalação manual do componente é `python -m pip install -U yt-dlp-ejs` no mesmo ambiente Python.

## Executar

Terminal:

```powershell
dotnet run --project src/Scraps.Cli
```

## Interface desktop

```powershell
dotnet run --project src/Scraps.Desktop
```

A interface usa as mesmas variáveis `SCRAPS_*` e a mesma pasta padrão e histórico da CLI. A aba de ferramentas mostra a disponibilidade de ffmpeg e ffprobe e os mesmos avisos da CLI.

## Fluxo da CLI

1. Cole uma URL de vídeo individual do YouTube, caminho local ou URL direta.
2. Confirme que tem autorização para baixar e processar a mídia.
3. Para YouTube, Scraps consulta os metadados e mostra as qualidades disponíveis.
4. Escolha um preset e aguarde o processamento.

A pasta padrão é `%USERPROFILE%\Downloads\Scraps`; nomes existentes recebem sufixo em vez de serem sobrescritos. O histórico fica em `%LOCALAPPDATA%\Scraps\history.db`.

- **Original:** mantém o contêiner baixado, sem recompressão.
- **1080p / 720p / 480p / 360p:** só aparecem quando não aumentam a altura da fonte; saída MP4 H.264/AAC, proporção preservada.

## Estrutura

- `src/Scraps.Application`: pedidos e estados da fila, parser do ID do YouTube, modelos de mídia, presets e contratos.
- `src/Scraps.Infrastructure`: importação/transferência segura, adaptador `yt-dlp`, SQLite, probe via ffprobe, transcodificação via FFmpeg e workflow da CLI.
- `src/Scraps.Cli`: ponto de entrada do terminal, sobre a composição compartilhada (`ScrapsComposition`).
- `src/Scraps.Desktop`: interface Avalonia (MVVM com CommunityToolkit.Mvvm). Usa a mesma composição da CLI (`ScrapsComposition`) e as mesmas variáveis `SCRAPS_*`.
- `tests/Scraps.Tests`: testes unitários e de integração com temporários, SQLite, HTTP simulado e processos locais.

## Validar

```powershell
dotnet restore Scraps.sln
dotnet test Scraps.sln
dotnet build Scraps.sln -c Release
dotnet format Scraps.sln --verify-no-changes --no-restore
```

Antes de commitar, ative o portão local (uma vez por clone), que roda formatação, build sem avisos, testes e cobertura mínima de 80% na Application e na Infrastructure (`scripts/check-coverage.sh`):

```bash
git config core.hooksPath .githooks
```

Os testes automatizados não acessam YouTube nem fazem chamadas externas à internet; o adaptador é exercitado com um executor de processo simulado. Para testar apenas a conversão, cole o caminho de um arquivo local próprio e escolha uma qualidade compatível. Para testar o YouTube de verdade, configure `yt-dlp` e o runtime conforme os requisitos e use uma URL de vídeo à qual tenha autorização aplicável.

## Composição e trabalhos interrompidos

`ScrapsComposition` monta os serviços (histórico SQLite, executores, ferramentas) e é usada pela CLI e pela interface. Trabalhos interrompidos (fechamento do app durante um download) são marcados como falhos na próxima inicialização e não retomam automaticamente. Migrações adicionam campos ao histórico existente sem descartar linhas.

## Licença

Scraps é distribuído sob a [licença MIT](LICENSE). As ferramentas externas seguem suas próprias licenças, listadas em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Logs

As falhas do processamento (worker de downloads) e os erros reportados via `Trace` são gravados em `%LOCALAPPDATA%\Scraps\logs\scraps-AAAA-MM-DD.log`, um arquivo por dia. Arquivos com mais de 7 dias são removidos na inicialização. A CLI e a interface gravam no mesmo lugar.
