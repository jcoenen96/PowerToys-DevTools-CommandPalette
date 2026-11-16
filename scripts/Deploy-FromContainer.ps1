<#
.SYNOPSIS
    Builds the extension in the dev container and installs the build into Command Palette on this Windows machine.

.DESCRIPTION
    The same developer loop as Deploy-Extension.ps1, but nothing has to be installed on Windows except Docker
    Desktop (with Linux containers) and PowerToys: the .NET SDK and every build tool come from the dev container
    in .devcontainer/.

      1. builds the dev container image (cached after the first run);
      2. stops the running extension, so its files can be replaced;
      3. builds src/DevTools.CommandPalette in the container, on this checkout, for this machine's platform. The
         NuGet packages are kept in the same Docker volume the dev container uses;
      4. makes resources.pri on Windows: makepri.exe, the one build tool that only runs on Windows, is copied out
         of the container by the build together with its arguments (see build/ContainerBuild.props);
      5. installs the build with Deploy-Extension.ps1 -SkipBuild: registers it with Windows, removes any other
         installed copy, and restarts Command Palette.

    Registering an unsigned package needs Windows Developer Mode (Settings > System > For developers).

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER Platform
    x64 or ARM64. Defaults to the platform of this machine.

.PARAMETER SkipBuild
    Don't build in the container; finish and deploy the last container build.

.PARAMETER NoReload
    Don't restart Command Palette; run "Reload" in Command Palette yourself.

.PARAMETER Clean
    Remove every installed copy of the extension first and install fresh. This resets the extension's settings
    and tools.json.

.EXAMPLE
    ./scripts/Deploy-FromContainer.ps1

    Builds Debug for this machine in the dev container and deploys it.

.EXAMPLE
    ./scripts/Deploy-FromContainer.ps1 -Configuration Release -Platform ARM64
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform,

    [switch] $SkipBuild,

    [switch] $NoReload,

    [switch] $Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Image = 'devtools-commandpalette-dev'
# The dev container's NuGet volume (.devcontainer/devcontainer.json), so packages are downloaded once.
$NuGetVolume = 'devtools-commandpalette-nuget'
$Workspace = "/workspaces/$(Split-Path -Leaf $Root)"

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

if ($env:OS -ne 'Windows_NT') {
    throw 'This installs the extension into Command Palette on Windows. To only build it, open the dev container and run: dotnet build src/DevTools.CommandPalette -p:Platform=x64'
}

$hostArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
if (-not $Platform) {
    $Platform = if ($hostArchitecture -eq 'arm64') { 'ARM64' } else { 'x64' }
}

if (-not $SkipBuild) {
    if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker is not installed. Install Docker Desktop (https://www.docker.com/products/docker-desktop/) and run this again.'
    }

    $osType = & docker info --format '{{.OSType}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker is not running. Start Docker Desktop and run this again.'
    }
    if ($osType -ne 'linux') {
        throw 'Docker is running Windows containers. Switch Docker Desktop to Linux containers and run this again.'
    }

    Write-Step 'Building the dev container image'
    & docker build --tag $Image (Join-Path $Root '.devcontainer')
    if ($LASTEXITCODE -ne 0) {
        throw "Building the dev container image failed (exit code $LASTEXITCODE)."
    }

    # The running extension locks its files, which would fail the build's copy step.
    $running = @(Get-Process -Name 'DevTools.CommandPalette' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Step "Stopping the running extension ($($running.Count) process(es))"
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }

    Write-Step "Building DevTools.CommandPalette ($Configuration, $Platform) in the dev container"
    & docker run --rm `
        --volume "${Root}:$Workspace" `
        --volume "${NuGetVolume}:/home/vscode/.nuget/packages" `
        --workdir $Workspace `
        $Image `
        dotnet build src/DevTools.CommandPalette/DevTools.CommandPalette.csproj `
        --configuration $Configuration "-p:Platform=$Platform" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "The build failed (exit code $LASTEXITCODE)."
    }
}

# What the container build left for Windows (build/ContainerBuild.Msix.targets).
$hostDir = Join-Path $Root "artifacts/container/$Platform/$Configuration"
$specFile = Join-Path $hostDir 'makepri.json'
if (-not (Test-Path $specFile)) {
    throw "No container build for $Configuration $Platform ($specFile is missing). Run this again without -SkipBuild."
}

function Get-FullPath([string] $RelativePath) {
    return [System.IO.Path]::GetFullPath((Join-Path $Root $RelativePath)).TrimEnd('\', '/')
}

$spec = Get-Content -Raw $specFile | ConvertFrom-Json
$makePri = Join-Path $hostDir "$hostArchitecture/makepri.exe"
$priFile = Get-FullPath $spec.outputFile

Write-Step 'Making resources.pri'
# The same command the build runs on Windows (WinAppSdkGenerateProjectPriFile).
& $makePri New `
    -ProjectRoot (Get-FullPath $spec.projectRoot) `
    -ConfigXml (Get-FullPath $spec.configXml) `
    -OutputFile $priFile `
    -IndexName $spec.indexName `
    -Overwrite | Out-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $priFile)) {
    throw "makepri.exe failed (exit code $LASTEXITCODE)."
}

$deploy = @{
    Configuration = $Configuration
    Platform      = $Platform
    SkipBuild     = $true
    NoReload      = $NoReload
    Clean         = $Clean
    WhatIf        = $WhatIfPreference
}
& (Join-Path $PSScriptRoot 'Deploy-Extension.ps1') @deploy
