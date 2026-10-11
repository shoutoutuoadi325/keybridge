[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string] $Version,
    [string] $DotNetPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tooling.ps1')
$repository = Split-Path $PSScriptRoot -Parent
$dotnet = Find-DotNet $DotNetPath
$staging = Get-ArtifactDirectory $repository ('portable-staging/' + [Guid]::NewGuid().ToString('N'))
$packages = Get-ArtifactDirectory $repository 'packages'

Invoke-Checked $dotnet @('publish', (Join-Path $repository 'src/KeyBridge.App/KeyBridge.App.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:DebugType=None', '-p:DebugSymbols=false', ('-p:Version=' + $Version), '--output', $staging)
if (-not (Test-Path -LiteralPath (Join-Path $staging 'KeyBridge.exe') -PathType Leaf)) { throw 'KeyBridge.exe is missing from the publish output.' }
Copy-Item -LiteralPath (Join-Path $repository 'docs/PRIVACY.md') -Destination (Join-Path $staging 'PRIVACY.md')
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination (Join-Path $staging 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repository 'docs/THIRD_PARTY_NOTICES.md') -Destination (Join-Path $staging 'THIRD_PARTY_NOTICES.md')
Copy-Item -LiteralPath (Join-Path $repository 'docs/third-party/WindowsSDKLicense.rtf') -Destination (Join-Path $staging 'WINDOWS_SDK_LICENSE.rtf')

$runtime = Get-Content -LiteralPath (Join-Path $staging 'KeyBridge.runtimeconfig.json') -Raw | ConvertFrom-Json
$assets = Get-Content -LiteralPath (Join-Path $repository 'src/KeyBridge.App/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $notices = if ($framework.name -eq 'Microsoft.NETCore.App') {
        @(@{ Source = 'LICENSE.TXT'; Target = 'DOTNET_RUNTIME_LICENSE.txt' }, @{ Source = 'THIRD-PARTY-NOTICES.TXT'; Target = 'DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt' })
    } elseif ($framework.name -eq 'Microsoft.WindowsDesktop.App') {
        @(@{ Source = 'LICENSE'; Target = 'DOTNET_DESKTOP_LICENSE.txt' })
    } else { throw "Unrecognized runtime framework: $($framework.name)" }
    foreach ($notice in $notices) {
        $noticePath = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder "$packageName/$($framework.version)/$($notice.Source)"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $noticePath = $candidate; break }
        }
        if (-not $noticePath) { throw "Missing runtime notice: $packageName/$($framework.version)/$($notice.Source)" }
        Copy-Item -LiteralPath $noticePath -Destination (Join-Path $staging $notice.Target)
    }
}

$instructions = @"
KeyBridge $Version for Windows x64

Extract all files together, then open KeyBridge.exe.
Requires Windows 11 x64 and a physical keyboard. The .NET runtime is included.

Complete setup on Home, choose your Command key and confirm the emergency pause shortcut.
Use the ENG US layout. Preview your application shortcuts, confirm their settings and enable mapping.
Pause is available from the window, notification area and your emergency shortcut.
Use Exit KeyBridge to close the application completely.

Website: https://github.com/shoutoutuoadi325/keybridge
Support: zhiqizhangg@gmail.com
License: GNU GPL v3; see LICENSE.txt and bundled runtime notices.
"@
[IO.File]::WriteAllText((Join-Path $staging 'START_HERE.txt'), $instructions, [Text.UTF8Encoding]::new($false))
$package = Join-Path $packages "KeyBridge-$Version-win-x64.zip"
if (Test-Path -LiteralPath $package) { throw "Package already exists: $package. Choose a new version or move the existing ZIP before building." }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $package, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$package.sha256", "$hash  $([IO.Path]::GetFileName($package))`n", [Text.UTF8Encoding]::new($false))
Write-Host "Download package: $package"
Write-Host "SHA-256: $hash"
Write-Host "Published application files: $staging"
