# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.0

<#
.SYNOPSIS
  Builds the JGS Wheel driver (jgswheel.sys + JgsWheelInterface.dll).

.DESCRIPTION
  Clones the upstream BrunnerInnovation/vJoy fork at a pinned tag, applies the
  patches under .\patches\ in numeric order, and builds with MSBuild + the
  Windows Driver Kit (WDK).

  This script is intended to run on a developer machine with the WDK and Visual
  Studio Build Tools installed. CI builds require a self-hosted Windows runner
  with the WDK provisioned.

.PARAMETER Configuration
  Build configuration: Debug or Release. Default: Release.

.PARAMETER TestSign
  When set, generates a self-signed developer certificate and uses signtool to
  test-sign jgswheel.sys + jgswheel.cat. Required for loading the driver on a
  machine running with `bcdedit /set testsigning on`.

.PARAMETER UpstreamTag
  Git tag of BrunnerInnovation/vJoy to fork from. Default: v2.2.2.

.EXAMPLE
  .\build.ps1 -Configuration Release -TestSign
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$TestSign,

    [string]$UpstreamTag = 'v2.2.2'
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot
$BuildDir = Join-Path $ScriptDir 'build'
$SrcDir = Join-Path $BuildDir 'vjoy-src'
$OutDir = Join-Path $ScriptDir 'out'
$PatchDir = Join-Path $ScriptDir 'patches'

function Assert-Tool {
    param([string]$Name, [string]$InstallHint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' was not found on PATH. $InstallHint"
    }
}

function Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---- preflight -------------------------------------------------------------
Step "Verifying build environment"
Assert-Tool git 'Install Git for Windows.'
Assert-Tool msbuild 'Open a Developer PowerShell from VS 2022 Build Tools, or add msbuild to PATH.'

if ($TestSign) {
    Assert-Tool signtool 'signtool ships with the Windows SDK; ensure your SDK\bin\<ver>\x64 folder is on PATH.'
    Assert-Tool inf2cat 'inf2cat ships with the WDK; ensure your WDK installation is on PATH.'
}

New-Item -ItemType Directory -Path $BuildDir, $OutDir -Force | Out-Null

# ---- clone upstream --------------------------------------------------------
if (-not (Test-Path $SrcDir)) {
    Step "Cloning BrunnerInnovation/vJoy@$UpstreamTag"
    git clone --depth 1 --branch $UpstreamTag https://github.com/BrunnerInnovation/vJoy.git $SrcDir
} else {
    Step "Reusing existing clone at $SrcDir (run 'Remove-Item -Recurse $SrcDir' to refresh)"
}

# ---- apply patches ---------------------------------------------------------
$patches = Get-ChildItem -Path $PatchDir -Filter '*.patch' | Sort-Object Name
if ($patches.Count -eq 0) {
    Write-Warning "No patches found in $PatchDir — fork will be byte-identical to upstream."
} else {
    Step "Applying $($patches.Count) patch(es)"
    Push-Location $SrcDir
    try {
        # Reset to clean state so re-runs are deterministic.
        git reset --hard HEAD | Out-Null
        git clean -fd | Out-Null
        foreach ($p in $patches) {
            Write-Host "  - $($p.Name)"
            git apply --whitespace=fix $p.FullName
        }
    } finally {
        Pop-Location
    }
}

# ---- build the driver + interface DLL --------------------------------------
Step "Building driver (Configuration=$Configuration, Platform=x64)"
$driverSln = Join-Path $SrcDir 'driver\vJoy.sln'
if (-not (Test-Path $driverSln)) {
    throw "Driver solution not found at $driverSln. Has the upstream layout changed?"
}

msbuild $driverSln `
    /p:Configuration=$Configuration `
    /p:Platform=x64 `
    /p:SignMode=Off `
    /m `
    /nologo

# ---- collect outputs -------------------------------------------------------
Step "Collecting build outputs"
$candidates = @(
    @{ From = "driver\sys\x64\$Configuration\jgswheel.sys"; To = 'jgswheel.sys' }
    @{ From = "driver\sys\x64\$Configuration\jgswheel.inf"; To = 'jgswheel.inf' }
    @{ From = "apps\common\vJoyInterface\x64\$Configuration\JgsWheelInterface.dll"; To = 'JgsWheelInterface.dll' }
)
foreach ($c in $candidates) {
    $src = Join-Path $SrcDir $c.From
    $dst = Join-Path $OutDir $c.To
    if (-not (Test-Path $src)) {
        Write-Warning "Missing expected output: $src (skipping)"
        continue
    }
    Copy-Item -Path $src -Destination $dst -Force
}

# ---- test-sign -------------------------------------------------------------
if ($TestSign) {
    Step "Generating self-signed test certificate"
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=JGS Wheel Test Cert' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddYears(2)
    $certPath = "Cert:\CurrentUser\My\$($cert.Thumbprint)"

    Export-Certificate -Cert $certPath -FilePath (Join-Path $OutDir 'jgswheel-test.cer') | Out-Null

    Step "Generating catalogue file"
    inf2cat /driver:$OutDir /os:10_x64

    Step "Signing driver + catalogue"
    $sysFile = Join-Path $OutDir 'jgswheel.sys'
    $catFile = Join-Path $OutDir 'jgswheel.cat'
    foreach ($f in @($sysFile, $catFile)) {
        if (Test-Path $f) {
            signtool sign /v `
                /fd SHA256 `
                /sha1 $cert.Thumbprint `
                /tr 'http://timestamp.digicert.com' /td SHA256 `
                $f
        }
    }
}

Step "Done. Artefacts in $OutDir"
Get-ChildItem -Path $OutDir | Format-Table Name, Length, LastWriteTime
