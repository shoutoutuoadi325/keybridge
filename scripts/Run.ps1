[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [string] $DotNetPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tooling.ps1')
$repository = Split-Path $PSScriptRoot -Parent
$dotnet = Find-DotNet $DotNetPath
Push-Location $repository
try {
    Invoke-Checked $dotnet @('run', '--project', 'src/KeyBridge.App/KeyBridge.App.csproj', '--configuration', $Configuration)
} finally { Pop-Location }
