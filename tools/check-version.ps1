<#
  Checks that OnTop's version is consistent. Run by CI on every build; run it
  locally before a PR:  pwsh tools/check-version.ps1   (or powershell -File ...)

  - Directory.Build.props <OnTopVersion> is the only version source (x.y.z).
  - OnTop.csproj and installer/OnTop.iss must not hard-code a version.
  - The newest CHANGELOG.md entry ("## [x.y.z]") names the same version.
  - When run for a tag (-Tag, or GITHUB_REF_TYPE=tag), the tag is v<version>.
#>
param([string]$Tag = $(if ($env:GITHUB_REF_TYPE -eq 'tag') { $env:GITHUB_REF_NAME } else { '' }))

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$problems = @()

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.OnTopVersion } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version -or $version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Directory.Build.props: <OnTopVersion> missing or not x.y.z (got '$version')"
}

$csproj = Get-Content (Join-Path $root 'OnTop/OnTop.csproj') -Raw
if ($csproj -match '<(Version|AssemblyVersion|FileVersion)>') {
    $problems += 'OnTop/OnTop.csproj sets its own <Version>/<AssemblyVersion>/<FileVersion>; remove it (Directory.Build.props is the source).'
}

$iss = Get-Content (Join-Path $root 'installer/OnTop.iss') -Raw
if ($iss -match '(?m)^\s*#define\s+MyAppVersion\s+"') {
    $problems += 'installer/OnTop.iss hard-codes MyAppVersion; it must read the version from the published exe.'
}

$top = Select-String -Path (Join-Path $root 'CHANGELOG.md') -Pattern '^## \[(\d+\.\d+\.\d+)\]' | Select-Object -First 1
if (-not $top) { $problems += 'CHANGELOG.md has no "## [x.y.z]" entry.' }
elseif ($top.Matches[0].Groups[1].Value -ne $version) {
    $problems += "CHANGELOG.md newest entry is $($top.Matches[0].Groups[1].Value), but Directory.Build.props says $version."
}

if ($Tag -and $Tag -ne "v$version") {
    $problems += "Tag $Tag does not match version v$version."
}

if ($problems) {
    $problems | ForEach-Object { Write-Host "::error::$_"; Write-Host "FAIL: $_" }
    exit 1
}
Write-Host "Version OK: $version$(if ($Tag) { " (tag $Tag)" })"
