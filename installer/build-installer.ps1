<#
.SYNOPSIS
    Builds a Velopack installer for JoystickGremlinSharp (local development use).

.DESCRIPTION
    1. Publishes the application as a self-contained win-x64 binary.
    2. Packs the output into a Velopack installer using vpk CLI.
    The resulting installer and delta packages are placed in installer/out/.

.EXAMPLE
    # From the repository root:
    .\installer\build-installer.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ──────────────────────────────────────────────────────────────
$repoRoot   = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $repoRoot 'publish'
$outputDir  = Join-Path $PSScriptRoot 'out'
$appCsproj  = Join-Path $repoRoot 'src\JoystickGremlin.App\JoystickGremlin.App.csproj'

# ── Read version from version.json ─────────────────────────────────────────────
$versionJson = Get-Content (Join-Path $repoRoot 'version.json') | ConvertFrom-Json
$version     = $versionJson.version
Write-Host "Building installer for version $version" -ForegroundColor Cyan

# ── Publish self-contained win-x64 ─────────────────────────────────────────────
Write-Host "`nPublishing application..." -ForegroundColor Cyan
dotnet publish $appCsproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# ── Ensure vpk CLI is available ────────────────────────────────────────────────
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "`nInstalling Velopack CLI (vpk)..." -ForegroundColor Cyan
    dotnet tool install --global vpk
    if ($LASTEXITCODE -ne 0) { throw "Failed to install vpk" }
}

# Allow vpk (built against .NET 9) to run on .NET 10 via roll-forward.
$env:DOTNET_ROLL_FORWARD = 'Major'

# ── Pack installer ─────────────────────────────────────────────────────────────
Write-Host "`nPacking installer..." -ForegroundColor Cyan
vpk pack `
    --packId   JoystickGremlinSharp `
    --packVersion $version `
    --packDir  $publishDir `
    --mainExe  JoystickGremlin.App.exe `
    --icon     (Join-Path $repoRoot 'src\JoystickGremlin.App\Assets\icon.ico') `
    --outputDir $outputDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

# ── Rename outputs to include version suffix ────────────────────────────────────
$renameMap = @{
    'JoystickGremlinSharp-win-Setup.exe'    = "JoystickGremlinSharp-$version-win-Setup.exe"
    'JoystickGremlinSharp-win-Portable.zip' = "JoystickGremlinSharp-$version-win-Portable.zip"
}
foreach ($entry in $renameMap.GetEnumerator()) {
    $src = Join-Path $outputDir $entry.Key
    $dst = Join-Path $outputDir $entry.Value
    if (Test-Path $src) {
        Move-Item -Path $src -Destination $dst -Force
        Write-Host "Renamed: $($entry.Key) → $($entry.Value)" -ForegroundColor DarkGray
    }
}

# ── Remove nupkg files ─────────────────────────────────────────────────────────
Get-ChildItem -Path $outputDir -Filter '*.nupkg' | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "Removed: $($_.Name)" -ForegroundColor DarkGray
}

Write-Host "`nInstaller ready in: $outputDir" -ForegroundColor Green
