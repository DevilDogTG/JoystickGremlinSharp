<#
.SYNOPSIS
    Creates a self-signed code-signing certificate and exports it for use in GitHub Actions.

.DESCRIPTION
    Generates a self-signed certificate for signing JoystickGremlinSharp binaries.
    After creation, prints the base64-encoded PFX value to add as a GitHub Secret.

    NOTE: A self-signed certificate will NOT remove Windows SmartScreen warnings for
    end users. It will, however, show "JoystickGremlinSharp" as the publisher in UAC
    prompts instead of "Unknown Publisher". To fully remove SmartScreen warnings, a
    commercially purchased code-signing certificate is required.

.PARAMETER OutputPath
    Where to write the .pfx file. Defaults to .\JoystickGremlinSharp-signing.pfx

.PARAMETER Password
    Password to protect the PFX file. If not specified, you will be prompted.

.EXAMPLE
    .\installer\create-cert.ps1
    .\installer\create-cert.ps1 -OutputPath C:\certs\my.pfx -Password (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'JoystickGremlinSharp-signing.pfx'),
    [SecureString]$Password
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($null -eq $Password) {
    Write-Host 'Enter a password to protect the certificate PFX file.' -ForegroundColor Cyan
    Write-Host '(Save this password — you will need it as the SIGNING_CERT_PASSWORD GitHub secret.)' -ForegroundColor Yellow
    $Password = Read-Host -AsSecureString -Prompt 'Certificate password'
    $Confirm  = Read-Host -AsSecureString -Prompt 'Confirm password'

    $plain1 = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                  [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
    $plain2 = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                  [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Confirm))

    if ($plain1 -ne $plain2) {
        throw 'Passwords do not match.'
    }
}

# ── Create self-signed certificate ─────────────────────────────────────────────
Write-Host "`nCreating self-signed code-signing certificate..." -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
    -Subject            'CN=JoystickGremlinSharp' `
    -Type               CodeSigningCert `
    -CertStoreLocation  'Cert:\CurrentUser\My' `
    -HashAlgorithm      SHA256 `
    -KeyLength          2048 `
    -NotAfter           (Get-Date).AddYears(10)

Write-Host "  Thumbprint : $($cert.Thumbprint)" -ForegroundColor DarkGray
Write-Host "  Subject    : $($cert.Subject)" -ForegroundColor DarkGray
Write-Host "  Expires    : $($cert.NotAfter.ToString('yyyy-MM-dd'))" -ForegroundColor DarkGray

# ── Export to PFX ──────────────────────────────────────────────────────────────
Write-Host "`nExporting certificate to: $OutputPath" -ForegroundColor Cyan

$OutputPath = [IO.Path]::GetFullPath($OutputPath)
Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $Password | Out-Null

Write-Host 'Certificate exported.' -ForegroundColor Green

# ── Print base64 for GitHub Secrets ───────────────────────────────────────────
$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($OutputPath))

Write-Host ''
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor Yellow
Write-Host ' GitHub Secrets to add (Settings → Secrets and variables → Actions)' -ForegroundColor Yellow
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor Yellow
Write-Host ''
Write-Host '  Secret name : SIGNING_CERT_BASE64' -ForegroundColor Cyan
Write-Host '  Secret value:' -ForegroundColor Cyan
Write-Host $base64
Write-Host ''
Write-Host '  Secret name : SIGNING_CERT_PASSWORD' -ForegroundColor Cyan
Write-Host '  Secret value: (the password you just entered)' -ForegroundColor Cyan
Write-Host ''
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor Yellow
Write-Host ''
Write-Host 'IMPORTANT: Keep the .pfx file safe and do NOT commit it to git.' -ForegroundColor Red
Write-Host "           Path: $OutputPath" -ForegroundColor Red
Write-Host ''
