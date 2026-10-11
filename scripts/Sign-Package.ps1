[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackagePath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string] $CertificateThumbprint,
    [string] $SignToolPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tooling.ps1')
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) { throw "Package not found: $PackagePath" }
$package = (Resolve-Path -LiteralPath $PackagePath).Path
if ([IO.Path]::GetExtension($package) -ne '.msix') { throw 'Supply a .msix package.' }
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
if (-not $certificate.HasPrivateKey) { throw 'The selected certificate has no private signing key.' }
if ($certificate.NotAfter -lt [DateTime]::Now -or $certificate.NotBefore -gt [DateTime]::Now) { throw 'The selected certificate is outside its validity period.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $archive.GetEntry('AppxManifest.xml')
    if (-not $entry) { throw 'The package has no AppxManifest.xml.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.Package.Identity.Publisher -cne $certificate.Subject) { throw 'Certificate subject must exactly match the package Publisher.' }
} finally { $archive.Dispose() }
$signTool = Find-WindowsSdkTool 'SignTool.exe' $SignToolPath
Invoke-Checked $signTool @('sign', '/fd', 'SHA256', '/sha1', $CertificateThumbprint, '/s', 'My', $package)
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$package.sha256", "$hash  $([IO.Path]::GetFileName($package))`n", [Text.UTF8Encoding]::new($false))
Invoke-Checked $signTool @('verify', '/pa', '/v', $package)
Write-Host "Signed test package: $package"
Write-Host 'Install only after the tester deliberately trusts the signing certificate. This script does not change certificate trust.'
