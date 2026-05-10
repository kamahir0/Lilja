$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist = Join-Path $Root "dist"
$LocalSlidev = Join-Path $Root "node_modules/.bin/slidev.cmd"

function Invoke-Slidev {
    param([string[]]$Arguments)

    if (Test-Path -LiteralPath $LocalSlidev) {
        & $LocalSlidev @Arguments
    }
    else {
        npx @slidev/cli@52.15.2 @Arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Slidev failed with exit code $LASTEXITCODE"
    }
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

Push-Location $Root
try {
    Ensure-Directory $Dist

    Invoke-Slidev @(
        "build",
        "slides.md",
        "--out",
        "dist/html"
    )

    Invoke-Slidev @(
        "export",
        "slides.md",
        "--format",
        "pdf",
        "--per-slide",
        "--output",
        "dist/debug-menu-built-from-scratch.pdf"
    )

    Invoke-Slidev @(
        "export",
        "slides.md",
        "--format",
        "pptx",
        "--with-clicks",
        "false",
        "--per-slide",
        "--output",
        "dist/debug-menu-built-from-scratch.pptx"
    )
}
finally {
    Pop-Location
}
