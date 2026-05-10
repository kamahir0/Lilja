$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$PngDir = Join-Path $Root "dist/png"
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

Push-Location $Root
try {
    if (-not (Test-Path -LiteralPath $PngDir)) {
        New-Item -ItemType Directory -Path $PngDir | Out-Null
    }

    $ResolvedPngDir = (Resolve-Path -LiteralPath $PngDir).Path
    if (-not $ResolvedPngDir.StartsWith($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside deck root: $ResolvedPngDir"
    }

    Get-ChildItem -Path $ResolvedPngDir -Force | Remove-Item -Recurse -Force

    Invoke-Slidev @(
        "export",
        "slides.md",
        "--format",
        "png",
        "--per-slide",
        "--output",
        "dist/png"
    )
}
finally {
    Pop-Location
}
