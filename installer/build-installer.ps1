<#
.SYNOPSIS
    Builds a WiX v6 MSI installer for JoystickGremlinSharp (local development use).

.DESCRIPTION
    1. Publishes the application as a self-contained win-x64 binary.
    2. Installs the WiX v6 dotnet global tool if not present.
    3. Builds the MSI using the WiX SDK project (installer/JoystickGremlinSharp.wixproj).
    The resulting MSI is placed in installer/out/.

.EXAMPLE
    # From the repository root:
    .\installer\build-installer.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ──────────────────────────────────────────────────────────────
$repoRoot      = Split-Path $PSScriptRoot -Parent
$publishDir    = Join-Path $repoRoot 'publish'
$outputDir     = Join-Path $PSScriptRoot 'out'
$appCsproj     = Join-Path $repoRoot 'src\JoystickGremlin.App\JoystickGremlin.App.csproj'
$wixProj       = Join-Path $PSScriptRoot 'JoystickGremlinSharp.wixproj'

# Trailing backslash required for WiX HarvestDirectory
$publishDirSlash = $publishDir.TrimEnd('\') + '\'

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

# ── Ensure wix global tool is available at the correct version ────────────────
$requiredWixVersion = '6.0.2'
$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
$wixVersionOk = $wixCmd -and ((wix --version 2>&1) -match [regex]::Escape($requiredWixVersion))

if (-not $wixVersionOk) {
    Write-Host "`nInstalling WiX CLI v$requiredWixVersion..." -ForegroundColor Cyan
    dotnet tool install --global wix --version $requiredWixVersion 2>$null
    if ($LASTEXITCODE -ne 0) {
        dotnet tool update --global wix --version $requiredWixVersion
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed to install/update wix tool to v$requiredWixVersion" }
}

# ── Build MSI ─────────────────────────────────────────────────────────────────
Write-Host "`nBuilding MSI..." -ForegroundColor Cyan

if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

dotnet build $wixProj `
    --configuration Release `
    -p:AppVersion=$version `
    -p:PublishDir=$publishDirSlash `
    -p:OutputPath=$outputDir

if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }

# ── Rename MSI to include version suffix ──────────────────────────────────────
$msi = Get-ChildItem -Path $outputDir -Filter '*.msi' | Select-Object -First 1
if ($null -eq $msi) { throw "No .msi found in $outputDir — build may have failed." }

$desiredName = "JoystickGremlinSharp-$version-Setup.msi"
if ($msi.Name -ne $desiredName) {
    $dest = Join-Path $outputDir $desiredName
    Move-Item -Path $msi.FullName -Destination $dest -Force
    Write-Host "Renamed: $($msi.Name) → $desiredName" -ForegroundColor DarkGray
} else {
    Write-Host "MSI already named: $desiredName" -ForegroundColor DarkGray
}

Write-Host "`nInstaller ready in: $outputDir" -ForegroundColor Green
