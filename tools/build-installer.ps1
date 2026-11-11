<#
  Builds the OnTop installer: publishes the app (Release, framework-dependent,
  win-x64) into installer/publish, then compiles installer/OnTop.iss with Inno
  Setup 6. Used by CI and by hand (see docs/BUILD.md).

    pwsh tools/build-installer.ps1 [-OutDir <folder>]

  Output: <OutDir>/OnTopSetup-<version>.exe (default OutDir: installer/Output).
#>
param([string]$OutDir = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $root 'installer'
$publish = Join-Path $installer 'publish'
if (-not $OutDir) { $OutDir = Join-Path $installer 'Output' }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'OnTop/OnTop.csproj') -c Release -r win-x64 --self-contained false -o $publish -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source }
if (-not $iscc -and $env:CI) {
    choco install innosetup -y --no-progress | Out-Null
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}
if (-not $iscc -or -not (Test-Path $iscc)) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
& $iscc /Q "/O$OutDir" (Join-Path $installer 'OnTop.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed ($LASTEXITCODE)" }
Get-ChildItem $OutDir -Filter 'OnTopSetup-*.exe' | ForEach-Object { Write-Host "Installer: $($_.FullName)" }
