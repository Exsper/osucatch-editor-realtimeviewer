# Build both EditorReader implementation variants (new / legacy).
#
# Background: EditorReader has two interchangeable implementations with identical public APIs,
# selected at build time via -p:UseLegacyEditorReader:
#   new     osucatch-editor-realtimeviewer/EditorReader/        (pointer width / cross-page / diagnostics)
#   legacy  osucatch-editor-realtimeviewer/EditorReaderLegacy/  (original 1564fdc, compatibility first)
#
# Use the new build on Windows and the legacy build on Wine.
# This script is intentionally ASCII-only so it parses correctly in any PowerShell host.
#
# Usage (run from the repository root, either Windows PowerShell 5.1 or PowerShell 7):
#   .\build-editor-reader-variants.ps1
#   .\build-editor-reader-variants.ps1 -Runtime win-x64
#   .\build-editor-reader-variants.ps1 -SkipHarness
#
# Outputs:
#   osucatch-editor-realtimeviewer/publish/<rid>-selfcontained            new viewer
#   osucatch-editor-realtimeviewer/publish/<rid>-selfcontained-legacy     legacy viewer
#   EditorReaderHarnessApp/<rid>-sc                                       new harness
#   EditorReaderHarnessApp/<rid>-sc-legacy                                legacy harness
#
# Note: dotnet publish does NOT copy StableCompatLib.dll (it is placed next to the output by an
# MSBuild Copy task, which publish does not run) and never ships GdiPlus.dll. Both are required at
# runtime on osu-winello/Wine prefixes, so this script copies them into every published folder --
# the same thing the release workflow does for the x86 self-contained build.

param(
    [string]$Runtime = "win-x86",
    [switch]$SkipHarness
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

function Invoke-Step {
    param(
        [string]$Label,
        [string[]]$Arguments
    )
    Write-Host ""
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed (exit $LASTEXITCODE)"
    }
}

# StableCompatLib.dll is per-architecture; GdiPlus.dll is architecture-neutral.
function Copy-RuntimeDependencies {
    param(
        [string]$Destination,
        [string]$Rid
    )
    if (-not (Test-Path $Destination)) {
        Write-Host "SKIP (no such directory): $Destination" -ForegroundColor Yellow
        return
    }

    $archDir = switch ($Rid) {
        "win-x64" { "x64" }
        "win-x86" { "x86" }
        default   { $null }
    }

    if ($archDir) {
        $stable = Join-Path $root "StableCompatLib\$archDir\StableCompatLib.dll"
        if (Test-Path $stable) {
            Copy-Item $stable $Destination -Force
            Write-Host "  + StableCompatLib.dll ($archDir)"
        } else {
            Write-Host "  ! StableCompatLib.dll not found at $stable" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ! unknown RID '$Rid'; skipped StableCompatLib.dll" -ForegroundColor Yellow
    }

    $gdiplus = Join-Path $root "osuwine-fix\GdiPlus.dll"
    if (Test-Path $gdiplus) {
        Copy-Item $gdiplus $Destination -Force
        Write-Host "  + GdiPlus.dll"
    } else {
        Write-Host "  ! GdiPlus.dll not found at $gdiplus" -ForegroundColor Yellow
    }
}

Push-Location $root
try {
    $viewerProj  = "osucatch-editor-realtimeviewer\OsuCatch-Editor-RealtimeViewer.csproj"
    $viewerOut   = "osucatch-editor-realtimeviewer\publish"
    $harnessProj = "EditorReaderHarness\EditorReaderHarness.csproj"
    $harnessOut  = "EditorReaderHarnessApp"

    $viewerNew    = "$viewerOut\$Runtime-selfcontained"
    $viewerLegacy = "$viewerOut\$Runtime-selfcontained-legacy"

    Invoke-Step "viewer NEW ($Runtime, self-contained)" @(
        "publish", $viewerProj, "-c", "Release", "-r", $Runtime, "--self-contained", "true",
        "-o", $viewerNew, "-v", "q", "-nologo")

    Invoke-Step "viewer LEGACY ($Runtime, self-contained)" @(
        "publish", $viewerProj, "-c", "Release", "-r", $Runtime, "--self-contained", "true",
        "-p:UseLegacyEditorReader=true",
        "-o", $viewerLegacy, "-v", "q", "-nologo")

    Write-Host ""
    Write-Host "=== Copy runtime dependencies (viewer) ===" -ForegroundColor Cyan
    Write-Host "viewer NEW:"
    Copy-RuntimeDependencies -Destination $viewerNew -Rid $Runtime
    Write-Host "viewer LEGACY:"
    Copy-RuntimeDependencies -Destination $viewerLegacy -Rid $Runtime

    if (-not $SkipHarness) {
        Invoke-Step "harness NEW ($Runtime, self-contained)" @(
            "publish", $harnessProj, "-c", "Release", "-r", $Runtime, "--self-contained", "true",
            "-o", "$harnessOut\$Runtime-sc", "-v", "q", "-nologo")

        Invoke-Step "harness LEGACY ($Runtime, self-contained)" @(
            "publish", $harnessProj, "-c", "Release", "-r", $Runtime, "--self-contained", "true",
            "-p:UseLegacyEditorReader=true",
            "-o", "$harnessOut\$Runtime-sc-legacy", "-v", "q", "-nologo")
    }

    Write-Host ""
    Write-Host "=== Artifacts ===" -ForegroundColor Green

    $targets = @(
        @{ Path = "$viewerNew\OsuCatch-Editor-RealtimeViewer.exe";    Label = "viewer  NEW   " },
        @{ Path = "$viewerLegacy\OsuCatch-Editor-RealtimeViewer.exe"; Label = "viewer  LEGACY" },
        @{ Path = "$harnessOut\$Runtime-sc\EditorReaderHarness.exe";        Label = "harness NEW   " },
        @{ Path = "$harnessOut\$Runtime-sc-legacy\EditorReaderHarness.exe"; Label = "harness LEGACY" }
    )

    foreach ($t in $targets) {
        if (Test-Path $t.Path) {
            $sizeMb = [Math]::Round((Get-Item $t.Path).Length / 1MB, 2)
            "{0}  {1}  ({2} MB)" -f $t.Label, $t.Path, $sizeMb
        } else {
            "{0}  MISSING: {1}" -f $t.Label, $t.Path
        }
    }

    Write-Host ""
    Write-Host "=== Runtime dependency check (viewer) ===" -ForegroundColor Green
    foreach ($dir in @($viewerNew, $viewerLegacy)) {
        foreach ($name in @("StableCompatLib.dll", "GdiPlus.dll")) {
            $ok = Test-Path (Join-Path $dir $name)
            "{0}  {1,-20} {2}" -f $(if ($ok) { "OK  " } else { "MISS" }), $name, $dir
        }
    }
}
finally {
    Pop-Location
}

