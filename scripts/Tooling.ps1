Set-StrictMode -Version Latest

function Find-DotNet {
    param([string] $ExplicitPath)
    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) { throw "dotnet was not found at '$ExplicitPath'." }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $repository = Split-Path $PSScriptRoot -Parent
    $candidates = @(
        (Join-Path (Split-Path $repository -Parent) '.tools/dotnet/dotnet.exe'),
        (Join-Path $env:LOCALAPPDATA 'KeyBridgeBuild/dotnet/dotnet.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe'),
        (Join-Path $env:ProgramFiles 'dotnet/dotnet.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw 'Install the .NET 10 SDK, add dotnet to PATH, or supply -DotNetPath.'
}

function Find-WindowsSdkTool {
    param([Parameter(Mandatory)][string] $Name, [string] $ExplicitPath)
    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) { throw "$Name was not found at '$ExplicitPath'." }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $roots = @()
    foreach ($registryPath in @('HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots')) {
        $installed = Get-ItemProperty -LiteralPath $registryPath -Name KitsRoot10 -ErrorAction SilentlyContinue
        if ($installed) { $roots += $installed.KitsRoot10 }
    }
    $roots += Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
    $repository = Split-Path $PSScriptRoot -Parent
    $roots += Join-Path (Split-Path $repository -Parent) '.tools/windows-sdk-buildtools'
    foreach ($root in ($roots | Select-Object -Unique)) {
        $bin = Join-Path $root 'bin'
        if (-not (Test-Path -LiteralPath $bin -PathType Container)) { continue }
        $versions = Get-ChildItem -LiteralPath $bin -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Name } -Descending
        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName "x64/$Name"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
        $candidate = Join-Path $bin "x64/$Name"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw "Install the Windows SDK packaging tools, add $Name to PATH, or pass its explicit path."
}

function Invoke-Checked {
    param([Parameter(Mandatory)][string] $Executable, [Parameter(Mandatory)][string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "'$Executable' failed with exit code $LASTEXITCODE." }
}

function Get-ArtifactDirectory {
    param([Parameter(Mandatory)][string] $Repository, [Parameter(Mandatory)][string] $RelativePath)
    # All generated files stay in this checkout's artifacts directory.
    $artifacts = [IO.Path]::GetFullPath((Join-Path $Repository 'artifacts'))
    $target = [IO.Path]::GetFullPath((Join-Path $artifacts $RelativePath))
    if (-not $target.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The generated output path must stay inside the repository artifacts directory.'
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    return $target
}
