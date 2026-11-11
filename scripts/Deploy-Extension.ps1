<#
.SYNOPSIS
    Builds the extension from this checkout, installs it into Windows and reloads Command Palette.

.DESCRIPTION
    The developer loop for the extension, without Visual Studio:

      1. builds src/DevTools.CommandPalette for this machine's platform;
      2. stops the running extension, so its files can be replaced;
      3. registers the build output as the DevTools.CommandPalette package (Add-AppxPackage -Register), the same
         loose-file deployment Visual Studio's Build > Deploy does. An earlier deployment is updated in place, so
         the extension's settings and tools.json are kept;
      4. tells Command Palette to reload its extensions (x-cmdpal://reload). The first time, Command Palette asks
         whether this may reload it.

    Registering an unsigned package needs Windows Developer Mode (Settings > System > For developers).

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER Platform
    x64 or ARM64. Defaults to the platform of this machine.

.PARAMETER SkipBuild
    Deploy the last build without building again.

.PARAMETER NoReload
    Don't ask Command Palette to reload; run "Reload" in Command Palette yourself.

.PARAMETER Uninstall
    Remove the extension instead. This also removes its settings and tools.json.

.EXAMPLE
    ./scripts/Deploy-Extension.ps1

    Builds Debug for this machine and deploys it.

.EXAMPLE
    ./scripts/Deploy-Extension.ps1 -Configuration Release -Platform ARM64
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform,

    [switch] $SkipBuild,

    [switch] $NoReload,

    [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PackageName = 'DevTools.CommandPalette'
$ProcessName = 'DevTools.CommandPalette'
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src/DevTools.CommandPalette/DevTools.CommandPalette.csproj'

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Stop-Extension {
    $running = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Step "Stopping the running extension ($($running.Count) process(es))"
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
}

function Test-DeveloperMode {
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
    $value = Get-ItemProperty -Path $key -Name 'AllowDevelopmentWithoutDevLicense' -ErrorAction SilentlyContinue
    return $null -ne $value -and $value.AllowDevelopmentWithoutDevLicense -eq 1
}

if ($env:OS -ne 'Windows_NT') {
    throw 'Command Palette extensions only run on Windows. On other systems, run: dotnet test DevTools.Core.slnf'
}

if ($Uninstall) {
    $installed = Get-AppxPackage -Name $PackageName
    if ($null -eq $installed) {
        Write-Host "$PackageName is not installed."
        return
    }

    Stop-Extension
    if ($PSCmdlet.ShouldProcess($installed.PackageFullName, 'Remove-AppxPackage')) {
        Write-Step "Removing $($installed.PackageFullName)"
        $installed | Remove-AppxPackage
    }

    if (-not $NoReload) {
        Start-Process 'x-cmdpal://reload' -ErrorAction SilentlyContinue
    }

    return
}

if (-not $Platform) {
    $Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }
}

if (-not (Test-DeveloperMode)) {
    Start-Process 'ms-settings:developers' -ErrorAction SilentlyContinue
    throw 'Turn on Developer Mode (Settings > System > For developers) to deploy an unsigned extension, then run this again.'
}

if (-not $SkipBuild) {
    if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK is not installed. Install the .NET 11 SDK from https://dot.net and run this again.'
    }

    # The running extension locks its files, which would fail the build's copy step.
    Stop-Extension

    Write-Step "Building $PackageName ($Configuration, $Platform)"
    & dotnet build $Project --configuration $Configuration "-p:Platform=$Platform" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "The build failed (exit code $LASTEXITCODE)."
    }
}

# Single-project MSIX writes the package layout, with its AppxManifest.xml, next to the build output.
$output = Join-Path $Root "src/DevTools.CommandPalette/bin/$Platform/$Configuration"
$manifest = Get-ChildItem -Path $output -Filter 'AppxManifest.xml' -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](AppPackages|publish)[\\/]' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $manifest) {
    throw "No AppxManifest.xml under $output. Build first (run without -SkipBuild) for $Configuration $Platform."
}

Stop-Extension

Write-Step "Registering $($manifest.DirectoryName)"
if ($PSCmdlet.ShouldProcess($manifest.FullName, 'Add-AppxPackage -Register')) {
    try {
        Add-AppxPackage -Register $manifest.FullName -ForceApplicationShutdown -ForceUpdateFromAnyVersion
    }
    catch {
        # A package installed from elsewhere (another checkout, a release .msix) can't be updated in place.
        $installed = Get-AppxPackage -Name $PackageName
        if ($null -eq $installed -or $installed.InstallLocation -eq $manifest.DirectoryName) {
            throw
        }

        Write-Warning "Replacing $PackageName installed from $($installed.InstallLocation). Its settings and tools.json are reset."
        $installed | Remove-AppxPackage
        Add-AppxPackage -Register $manifest.FullName -ForceApplicationShutdown
    }
}

$package = Get-AppxPackage -Name $PackageName
if ($null -ne $package) {
    Write-Host "Installed $($package.PackageFullName) from $($package.InstallLocation)" -ForegroundColor Green
}

if ($NoReload) {
    Write-Host 'Run "Reload" in Command Palette to pick up the new build.'
}
else {
    Write-Step 'Reloading Command Palette extensions'
    try {
        Start-Process 'x-cmdpal://reload'
    }
    catch {
        Write-Warning 'Could not ask Command Palette to reload (is PowerToys running?). Run "Reload" in Command Palette yourself.'
    }
}
