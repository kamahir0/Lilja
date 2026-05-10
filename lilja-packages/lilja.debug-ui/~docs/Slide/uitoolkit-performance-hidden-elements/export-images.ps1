$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$PngDir = Join-Path $Root "dist/png"
$LocalMarp = Join-Path $Root "node_modules/.bin/marp.cmd"

function Invoke-Marp {
    param([string[]]$Arguments)

    if (Test-Path -LiteralPath $LocalMarp) {
        & $LocalMarp @Arguments
        return
    }

    npx @marp-team/marp-cli@4.4.0 @Arguments
}

Push-Location $Root
try {
    if (-not (Test-Path -LiteralPath $PngDir)) {
        New-Item -ItemType Directory -Path $PngDir | Out-Null
    }

    $ResolvedPngDir = (Resolve-Path -LiteralPath $PngDir).Path
    if (-not $ResolvedPngDir.StartsWith($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside deck root: $ResolvedPngDir"
    }

    Get-ChildItem -Path $ResolvedPngDir -File -Filter "*.png" | Remove-Item -Force

    Invoke-Marp @(
        "slides.md",
        "--html",
        "--allow-local-files",
        "--theme",
        "theme.css",
        "--images",
        "png",
        "-o",
        "dist/png/debug-menu-built-from-scratch.png"
    )
}
finally {
    Pop-Location
}
