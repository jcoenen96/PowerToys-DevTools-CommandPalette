<#
.SYNOPSIS
    Builds the extension from this checkout, installs it into Windows and reloads Command Palette.

.DESCRIPTION
    The developer loop for the extension, without Visual Studio:

      1. builds src/DevTools.CommandPalette for this machine's platform;
      2. stops the running extension, so its files can be replaced;
      3. registers the build output as the DevTools.CommandPalette package (Add-AppxPackage -Register), the same
         loose-file deployment Visual Studio's Build > Deploy does. An earlier deployment from the same folder is
         updated in place, so the extension's settings and tools.json are kept. Any other copy (another
         configuration, platform or checkout, or a release .msix) is removed first, so only one stays installed;
      4. restarts Command Palette, so it lists the extension once. Reloading the extensions of a running Command
         Palette while it is also handling the package update can list the extension twice.

    Registering an unsigned package needs Windows Developer Mode (Settings > System > For developers).

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER Platform
    x64 or ARM64. Defaults to the platform of this machine.

.PARAMETER SkipBuild
    Deploy the last build without building again.

.PARAMETER NoReload
    Don't restart Command Palette; run "Reload" in Command Palette yourself.

.PARAMETER Clean
    Remove every installed copy of the extension first, including the one being updated, and install fresh. This
    resets the extension's settings and tools.json.

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

    [switch] $Clean,

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

# Every installed copy of the extension: all architectures, versions and install folders. PowerShell unrolls a
# returned array (one package comes back as a scalar), so callers wrap the call in @().
function Get-ExtensionPackage {
    return @(Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue)
}

function Remove-ExtensionPackage {
    [CmdletBinding(SupportsShouldProcess)]
    param($Packages, [string] $Reason)

    foreach ($package in $Packages) {
        if ($PSCmdlet.ShouldProcess($package.PackageFullName, 'Remove-AppxPackage')) {
            Write-Step "Removing $($package.PackageFullName) ($Reason)"
            Remove-AppxPackage -Package $package.PackageFullName
        }
    }
}

# Restarting rebuilds Command Palette's extension list from scratch; a reload of the running app can race with
# its own handling of the package update and list the extension twice.
function Restart-CommandPalette {
    $palette = Get-AppxPackage -Name 'Microsoft.CommandPalette*' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in 'Microsoft.CommandPalette', 'Microsoft.CommandPalette.Dev' } |
        Select-Object -First 1
    if ($null -eq $palette) {
        Write-Warning 'Command Palette is not installed (is PowerToys installed with Command Palette on?).'
        return
    }

    Write-Step 'Restarting Command Palette'
    Get-Process -Name 'Microsoft.CmdPal.UI' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    $appId = @((Get-AppxPackageManifest -Package $palette.PackageFullName).Package.Applications.Application)[0].Id
    try {
        Start-Process "shell:AppsFolder\$($palette.PackageFamilyName)!$appId"
    }
    catch {
        Write-Warning 'Could not start Command Palette again. Start it from PowerToys, or press its hotkey.'
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
    $installed = @(Get-ExtensionPackage)
    if ($installed.Count -eq 0) {
        Write-Host "$PackageName is not installed."
        return
    }

    Stop-Extension
    Remove-ExtensionPackage $installed 'uninstall'

    if (-not $NoReload) {
        Restart-CommandPalette
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

# Only one copy may stay: anything not installed from this build folder (another configuration, platform or
# checkout, a release .msix) is removed. The copy from this folder is updated in place, which keeps its settings.
$installed = @(Get-ExtensionPackage)
$others = @(
    $installed | Where-Object {
        $Clean -or -not [string]::Equals($_.InstallLocation, $manifest.DirectoryName, [StringComparison]::OrdinalIgnoreCase)
    }
)
if ($others.Count -gt 0) {
    Write-Warning "Removing $($others.Count) other installed copy(ies) of $PackageName. Their settings and tools.json are reset."
    Remove-ExtensionPackage $others 'replaced by this build'
}

Write-Step "Registering $($manifest.DirectoryName)"
if ($PSCmdlet.ShouldProcess($manifest.FullName, 'Add-AppxPackage -Register')) {
    Add-AppxPackage -Register $manifest.FullName -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

$packages = @(Get-ExtensionPackage)
foreach ($package in $packages) {
    Write-Host "Installed $($package.PackageFullName) from $($package.InstallLocation)" -ForegroundColor Green
}

if ($packages.Count -gt 1) {
    Write-Warning "$PackageName is installed $($packages.Count) times. Run this again with -Clean to keep only this build."
}

if ($NoReload) {
    Write-Host 'Run "Reload" in Command Palette to pick up the new build.'
}
else {
    Restart-CommandPalette
}
