<#
.SYNOPSIS
  Builds the Mod Manager mod and lays it out in .\dist exactly as it goes into the game folder.
.EXAMPLE
  .\build.ps1                                   # build only
  .\build.ps1 -GameDir "E:\Games\Get To Work" -Install
  .\build.ps1 -Zip                              # also produce GTWM-ModManager-<version>.zip
#>
param(
    [string]$GameDir = $(if ($env:GTW_GAME_DIR) { $env:GTW_GAME_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Get To Work" }),
    [string]$Configuration = "Release",
    [switch]$Install,
    [switch]$Zip
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"

if (-not (Test-Path (Join-Path $root "external\GTWM\Directory.Build.props"))) {
    Write-Host "Fetching the GTWM loader submodule" -ForegroundColor Cyan
    git -C $root submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) { throw "Couldn't fetch external/GTWM. Clone with --recursive or run: git submodule update --init" }
}

Write-Host "Building ModManager" -ForegroundColor Cyan
dotnet build (Join-Path $root "src\ModManager\ModManager.csproj") -c $Configuration -p:GameDir="$GameDir" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
$out = New-Item -ItemType Directory -Force (Join-Path $dist "GTWM\mods\ModManager")
Copy-Item (Join-Path $root "build\ModManager\GTWM.ModManager.dll") $out
Write-Host "Packaged into $dist" -ForegroundColor Green

if ($Zip) {
    $version = (Get-Item (Join-Path $out "GTWM.ModManager.dll")).VersionInfo.ProductVersion -replace '\+.*$', ''
    $zipPath = Join-Path $root "GTWM-ModManager-$version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zipPath
    Write-Host "Zipped to $zipPath" -ForegroundColor Green
}

if ($Install) {
    if (-not (Test-Path (Join-Path $GameDir "GTWM\core\GTWModLoader.dll"))) {
        Write-Warning "The GTWM loader isn't installed in '$GameDir'. Install it first: https://github.com/BHXgg/GTWM"
    }
    $target = New-Item -ItemType Directory -Force (Join-Path $GameDir "GTWM\mods\ModManager")
    Copy-Item (Join-Path $out "GTWM.ModManager.dll") $target -Force
    Write-Host "Installed into $target (mods hot-reload, so the game can stay open)" -ForegroundColor Green
}
