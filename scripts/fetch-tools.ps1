<#
.SYNOPSIS
Baixa e verifica as ferramentas externas usadas pelo Scraps (yt-dlp, ffmpeg e ffprobe).

.DESCRIPTION
Cada arquivo é conferido pelo SHA-256 do manifesto antes de qualquer cópia para
OutputDirectory. Se uma verificação falhar, nenhum arquivo é gravado.
#>
[CmdletBinding()]
param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'tools.manifest.json'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..' 'third-party'),
    # Copia os arquivos desta pasta em vez de baixar da internet (usado nos testes).
    [string]$LocalSourceDirectory
)

$ErrorActionPreference = 'Stop'

function Get-VerifiedArchive {
    param($Tool, [string]$StagingDirectory)

    $archive = Join-Path $StagingDirectory $Tool.fileName
    if ($LocalSourceDirectory) {
        Copy-Item -LiteralPath (Join-Path $LocalSourceDirectory $Tool.fileName) -Destination $archive
    }
    else {
        Invoke-WebRequest -Uri $Tool.url -OutFile $archive -UseBasicParsing
    }

    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    # Get-FileHash devolve em maiúsculas; -ine ignora a caixa do manifesto.
    if ($actual -ine $Tool.sha256) {
        throw "SHA-256 inválido para $($Tool.fileName): esperado $($Tool.sha256), obtido $actual."
    }
    return $archive
}

function Get-ToolFiles {
    param($Tool, [string]$Archive, [string]$StagingDirectory)

    if ($Tool.kind -eq 'binary') {
        return @(Get-Item -LiteralPath $Archive)
    }

    $extractDirectory = Join-Path $StagingDirectory "$($Tool.name)-extracted"
    Expand-Archive -LiteralPath $Archive -DestinationPath $extractDirectory
    foreach ($name in $Tool.outputs) {
        $found = Get-ChildItem -LiteralPath $extractDirectory -Recurse -File -Filter $name | Select-Object -First 1
        if (-not $found) { throw "$name não encontrado em $($Tool.fileName)." }
        $found
    }
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$stagingDirectory = Join-Path ([IO.Path]::GetTempPath()) "scraps-tools-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $stagingDirectory | Out-Null

try {
    # Verifica e extrai tudo antes de tocar em OutputDirectory.
    $prepared = foreach ($tool in $manifest.tools) {
        $archive = Get-VerifiedArchive -Tool $tool -StagingDirectory $stagingDirectory
        [pscustomobject]@{
            Tool  = $tool
            Files = @(Get-ToolFiles -Tool $tool -Archive $archive -StagingDirectory $stagingDirectory)
        }
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $versions = foreach ($item in $prepared) {
        $toolDirectory = Join-Path $OutputDirectory $item.Tool.name
        New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
        foreach ($file in $item.Files) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $toolDirectory $file.Name) -Force
        }
        "$($item.Tool.name) $($item.Tool.version) | origem: $($item.Tool.url) | sha256: $($item.Tool.sha256)"
    }
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'VERSIONS.txt') -Value $versions -Encoding UTF8
    Write-Output "Ferramentas instaladas em $((Resolve-Path $OutputDirectory).Path)."
}
finally {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
