[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [string] $DotNetPath,
    [switch] $SkipTests
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tooling.ps1')
$repository = Split-Path $PSScriptRoot -Parent
$dotnet = Find-DotNet $DotNetPath
Push-Location $repository
try {
    Invoke-Checked $dotnet @('build', 'KeyBridge.sln', '--configuration', $Configuration)
    if (-not $SkipTests) {
        Invoke-Checked $dotnet @('run', '--project', 'tests/KeyBridge.Core.Tests/KeyBridge.Core.Tests.csproj', '--configuration', $Configuration, '--no-build')
        Invoke-Checked $dotnet @('run', '--project', 'tests/KeyBridge.Platform.Tests/KeyBridge.Platform.Tests.csproj', '--configuration', $Configuration, '--no-build')
    }
} finally { Pop-Location }
