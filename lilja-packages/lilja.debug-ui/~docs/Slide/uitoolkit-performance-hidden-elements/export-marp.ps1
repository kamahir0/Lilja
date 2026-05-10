$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist = Join-Path $Root "dist"
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
    if (-not (Test-Path -LiteralPath $Dist)) {
        New-Item -ItemType Directory -Path $Dist | Out-Null
    }

    Invoke-Marp @(
        "slides.md",
        "--html",
        "--allow-local-files",
        "--theme",
        "theme.css",
        "-o",
        "dist/debug-menu-built-from-scratch.html"
    )

    Invoke-Marp @(
        "slides.md",
        "--html",
        "--allow-local-files",
        "--theme",
        "theme.css",
        "-o",
        "dist/debug-menu-built-from-scratch.pdf"
    )
}
finally {
    Pop-Location
}
