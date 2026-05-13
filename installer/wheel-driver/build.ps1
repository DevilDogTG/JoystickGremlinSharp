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

    [string]$UpstreamTag = 'v2.2.2.0',

    # Override the C++ platform toolset (e.g. 'v143' for VS2022, 'v180' for VS2026).
    # Leave empty to use the toolset baked into each .vcxproj (upstream vJoy uses v143).
    [string]$PlatformToolset = '',

    # Override the Windows SDK target. Use '10.0' to pick the latest installed.
    [string]$WindowsTargetPlatformVersion = '',

    # KMDF version to compile the driver against. Upstream pins to 1.9 which
    # is no longer present in WDK 10.0.22621+. Default to 1.15 (the version
    # bundled with current WDKs).
    [int]$KmdfMajor = 1,
    [int]$KmdfMinor = 15
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

# ---- detect VCTargetsPath candidates ---------------------------------------
# vJoy upstream targets v143 (VS2022) for user-mode helpers and the
# WindowsKernelModeDriver10.0 toolset for the kernel driver. These often live
# in *different* MSBuild VC dirs:
#   * VS2022:        v143 + WDK toolsets both under MSBuild\Microsoft\VC\v143\
#   * VS2026:        v143 lives under .\v170\ (sub-toolset),
#                    WDK toolsets live under .\v180\
# So we resolve two paths up-front and pass the right one per project.
$VcV143TargetsPath = ''
$VcWdkTargetsPath = ''
$vsRoot = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -property installationPath 2>$null
if ($vsRoot) {
    foreach ($name in @('v143', 'v170', 'v180')) {
        $p = "$vsRoot\MSBuild\Microsoft\VC\$name\"
        if (-not (Test-Path (Join-Path $p 'Microsoft.CppBuild.targets'))) { continue }
        if (-not $VcV143TargetsPath -and (Test-Path (Join-Path $p 'Platforms\x64\PlatformToolsets\v143'))) {
            $VcV143TargetsPath = $p
        }
        if (-not $VcWdkTargetsPath -and (Test-Path (Join-Path $p 'Platforms\x64\PlatformToolsets\WindowsKernelModeDriver10.0'))) {
            $VcWdkTargetsPath = $p
        }
    }
}
if ($VcV143TargetsPath) { Write-Host "    v143 toolset path:  $VcV143TargetsPath" -ForegroundColor DarkGray }
if ($VcWdkTargetsPath)  { Write-Host "    WDK  toolset path:  $VcWdkTargetsPath"  -ForegroundColor DarkGray }

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

function Invoke-MsBuild {
    param(
        [Parameter(Mandatory)] [string]$Project,
        [string[]]$ExtraArgs = @(),
        [string]$VCTargetsPath = ''
    )
    $args = @(
        $Project,
        "/p:Configuration=$Configuration",
        '/p:Platform=x64',
        '/m',
        '/nologo'
    ) + $ExtraArgs
    if ($PlatformToolset) { $args += "/p:PlatformToolset=$PlatformToolset" }
    if ($WindowsTargetPlatformVersion) { $args += "/p:WindowsTargetPlatformVersion=$WindowsTargetPlatformVersion" }
    $oldVcTargets = $env:VCTargetsPath
    if ($VCTargetsPath) { $env:VCTargetsPath = $VCTargetsPath }
    try {
        & msbuild @args
        if ($LASTEXITCODE -ne 0) {
            throw "msbuild failed (exit $LASTEXITCODE) for $Project"
        }
    } finally {
        $env:VCTargetsPath = $oldVcTargets
    }
}

# ---- build the driver + interface DLL --------------------------------------
# vJoyDriver.sln mixes a v143 user-mode helper (CreateVersion) with the
# WindowsKernelModeDriver10.0 driver projects. On VS2026 those toolsets live
# in different VC\ dirs, so we can't build the whole sln in one shot — we have
# to build CreateVersion first under the v143 targets path, then build the
# driver Package (which pulls in vJoy + hidkmdf) under the WDK targets path.
Step "Building CreateVersion (v143 user-mode helper)"
Invoke-MsBuild `
    -Project (Join-Path $SrcDir 'CreateVersion\CreateVersion.vcxproj') `
    -VCTargetsPath $VcV143TargetsPath

Step "Building vJoy driver package (KMDF $($KmdfMajor).$($KmdfMinor))"
Invoke-MsBuild `
    -Project (Join-Path $SrcDir 'driver\Package\Package.vcxproj') `
    -VCTargetsPath $VcWdkTargetsPath `
    -ExtraArgs @(
        '/p:SignMode=Off',
        "/p:KMDF_VERSION_MAJOR=$KmdfMajor",
        "/p:KMDF_VERSION_MINOR=$KmdfMinor"
    )

# Without rename patches the driver retains the upstream "vJoy" naming. The
# interface DLL lives in a separate solution under apps\common\vJoyInterface.
# Note: vJoyInterface.sln is the legacy VS2008 (.vcproj) layout; vJoyInterface2012.sln
# is the modern .vcxproj-based solution that MSBuild can actually consume.
$interfaceSln = Join-Path $SrcDir 'apps\common\vJoyInterface\vJoyInterface2012.sln'
if (Test-Path $interfaceSln) {
    Step "Building vJoyInterface DLL"
    Invoke-MsBuild -Project $interfaceSln -VCTargetsPath $VcV143TargetsPath
} else {
    Write-Warning "vJoyInterface solution not found at $interfaceSln (skipping DLL build)"
}

# ---- collect outputs -------------------------------------------------------
Step "Collecting build outputs"
$candidates = @(
    @{ From = "driver\Package\x64\$Configuration\Package\vJoy.sys"; To = 'vJoy.sys' }
    @{ From = "driver\Package\x64\$Configuration\Package\hidkmdf.sys"; To = 'hidkmdf.sys' }
    @{ From = "driver\Package\x64\$Configuration\Package\vjoy.inf"; To = 'vJoy.inf' }
    @{ From = "driver\Package\x64\$Configuration\Package\vjoy.cat"; To = 'vJoy.cat' }
    @{ From = "apps\common\vJoyInterface\x64\$Configuration\vJoyInterface.dll"; To = 'vJoyInterface.dll' }
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
    $signTargets = @('vJoy.sys', 'hidkmdf.sys', 'vJoy.cat') | ForEach-Object { Join-Path $OutDir $_ }
    foreach ($f in $signTargets) {
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
