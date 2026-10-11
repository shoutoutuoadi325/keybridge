[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$')][string] $IdentityName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Publisher,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $PublisherDisplayName,
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string] $Version = '0.1.0.0',
    [string] $DotNetPath,
    [string] $MakeAppxPath,
    [string] $MakePriPath,
    [switch] $SkipTests
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tooling.ps1')
$repository = Split-Path $PSScriptRoot -Parent
$dotnet = Find-DotNet $DotNetPath
$makeAppx = Find-WindowsSdkTool 'MakeAppx.exe' $MakeAppxPath
$makePri = Find-WindowsSdkTool 'MakePri.exe' $MakePriPath
$parsedVersion = [version]$Version
foreach ($part in @($parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build, $parsedVersion.Revision)) {
    if ($part -gt 65535) { throw 'Every package version component must be at most 65535.' }
}
if ($parsedVersion.Major -eq 0 -and $parsedVersion.Minor -eq 0 -and $parsedVersion.Build -eq 0 -and $parsedVersion.Revision -eq 0) { throw 'The package version cannot be 0.0.0.0.' }
if ($parsedVersion.Revision -ne 0) { throw 'Use a zero revision component for Microsoft Store packages.' }
if ($Publisher -notmatch '^CN=') { throw 'Use the exact Publisher distinguished name from Partner Center, beginning with CN=.' }

& (Join-Path $PSScriptRoot 'Build.ps1') -DotNetPath $dotnet -Configuration Release -SkipTests:$SkipTests
& (Join-Path $PSScriptRoot 'New-Assets.ps1')
$token = [Guid]::NewGuid().ToString('N')
$staging = Get-ArtifactDirectory $repository "staging/$token"
$packages = Get-ArtifactDirectory $repository 'packages'
$logs = Get-ArtifactDirectory $repository 'logs'
# A fresh directory prevents stale publish files from entering a later package.
Invoke-Checked $dotnet @('publish', (Join-Path $repository 'src/KeyBridge.App/KeyBridge.App.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:DebugType=None', '-p:DebugSymbols=false', ('-p:Version=' + "$($parsedVersion.Major).$($parsedVersion.Minor).$($parsedVersion.Build)"), '--output', $staging)
if (-not (Test-Path -LiteralPath (Join-Path $staging 'KeyBridge.exe') -PathType Leaf)) { throw 'The publish output is missing KeyBridge.exe.' }
Copy-Item -LiteralPath (Join-Path $repository 'packaging/Assets') -Destination $staging -Recurse
Copy-Item -LiteralPath (Join-Path $repository 'docs/PRIVACY.md') -Destination (Join-Path $staging 'PRIVACY.md')
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination (Join-Path $staging 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repository 'docs/THIRD_PARTY_NOTICES.md') -Destination (Join-Path $staging 'THIRD_PARTY_NOTICES.md')
$runtimeConfig = Get-Content -LiteralPath (Join-Path $staging 'KeyBridge.runtimeconfig.json') -Raw | ConvertFrom-Json
$projectAssets = Get-Content -LiteralPath (Join-Path $repository 'src/KeyBridge.App/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageFolders = $projectAssets.packageFolders.PSObject.Properties.Name
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $noticeDefinitions = if ($framework.name -eq 'Microsoft.NETCore.App') {
        @(@{ Source = 'LICENSE.TXT'; Target = 'DOTNET_RUNTIME_LICENSE.txt' }, @{ Source = 'THIRD-PARTY-NOTICES.TXT'; Target = 'DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt' })
    } elseif ($framework.name -eq 'Microsoft.WindowsDesktop.App') {
        @(@{ Source = 'LICENSE'; Target = 'DOTNET_DESKTOP_LICENSE.txt' })
    } else { throw "Unreviewed runtime framework: $($framework.name)" }
    foreach ($notice in $noticeDefinitions) {
        $noticeSource = $null
        foreach ($folder in $packageFolders) {
            $candidate = Join-Path $folder "$packageName/$($framework.version)/$($notice.Source)"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $noticeSource = $candidate; break }
        }
        if (-not $noticeSource) { throw "The selected runtime pack '$packageName/$($framework.version)' is missing '$($notice.Source)'. Preserve its official notices before packaging." }
        Copy-Item -LiteralPath $noticeSource -Destination (Join-Path $staging $notice.Target)
    }
}
Copy-Item -LiteralPath (Join-Path $repository 'docs/third-party/WindowsSDKLicense.rtf') -Destination (Join-Path $staging 'WINDOWS_SDK_LICENSE.rtf')
$template = Get-Content -LiteralPath (Join-Path $repository 'packaging/AppxManifest.template.xml') -Raw
$manifest = $template.Replace('__IDENTITY_NAME__', [Security.SecurityElement]::Escape($IdentityName)).Replace('__PUBLISHER__', [Security.SecurityElement]::Escape($Publisher)).Replace('__PUBLISHER_DISPLAY_NAME__', [Security.SecurityElement]::Escape($PublisherDisplayName)).Replace('__VERSION__', $Version)
[xml]$validated = $manifest
if ($validated.Package.Capabilities.ChildNodes.Count -ne 1 -or $validated.Package.Capabilities.ChildNodes[0].Name -ne 'runFullTrust') { throw 'Only runFullTrust is allowed in this package.' }
[IO.File]::WriteAllText((Join-Path $staging 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))
$priConfigPath = Join-Path $logs "PriConfig-$token.xml"
$priLog = Join-Path $logs "MakePri-$token.log"
& $makePri 'createconfig' '/cf' $priConfigPath '/dq' 'en-US' '/o' *> $priLog
if ($LASTEXITCODE -ne 0) { throw "MakePri configuration generation failed. Full log: $priLog" }
[xml]$priConfig = Get-Content -LiteralPath $priConfigPath -Raw
# This is one complete package; do not split language/scale variants into separate resource packages.
$priConfig.resources.packaging.RemoveAll()
$priConfig.Save($priConfigPath)
& $makePri 'new' '/pr' $staging '/cf' $priConfigPath '/of' (Join-Path $staging 'resources.pri') '/in' $IdentityName '/o' *>> $priLog
if ($LASTEXITCODE -ne 0) { throw "MakePri resource indexing failed. Full log: $priLog" }
$package = Join-Path $packages "$IdentityName`_$Version`_x64.msix"
$makeAppxLog = Join-Path $logs "MakeAppx-$token.log"
& $makeAppx 'pack' '/d' $staging '/p' $package '/o' *> $makeAppxLog
if ($LASTEXITCODE -ne 0) {
    Get-Content -LiteralPath $makeAppxLog -Tail 40 | Write-Host
    throw "MakeAppx failed with exit code $LASTEXITCODE. Full log: $makeAppxLog"
}
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$package.sha256", "$hash  $([IO.Path]::GetFileName($package))`n", [Text.UTF8Encoding]::new($false))
Write-Host "Unsigned MSIX: $package"
Write-Host "MakeAppx validation log: $makeAppxLog"
Write-Host "MakePri indexing log: $priLog"
Write-Host 'Microsoft Store provides distribution signing for submitted MSIX packages.'
Write-Host 'For a direct local install, use a trusted certificate matching Publisher. See docs/STORE_RELEASE.md.'
