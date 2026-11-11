<#
  Public privacy check (CI runs it on every PR). Holds no private data, so it
  looks for generic tells of a personal identifier instead:
    - a real user-profile path (C:\Users\<name>\, /Users/<name>/, /home/<name>/)
    - an e-mail address that isn't a GitHub noreply / example address
    - a commit (in -Range) authored or committed with a non-noreply address
  The private, name-specific patterns live only in each developer's
  .git/info/identity-patterns and are enforced by local git hooks.

    pwsh tools/check-privacy.ps1 [-Range origin/main..HEAD]
#>
param([string]$Range = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $problems = @()
    $allowedMail = '(@users\.noreply\.github\.com|^noreply@github\.com|@noreply\.anthropic\.com|^noreply@anthropic\.com|@example\.(com|org)|^you@|^user@)'
    $pathRx = '(?i)([A-Z]:[\\/]+Users[\\/]+(?!(Public|Default|<|\{|%|\$|\.\.\.|you|user|name)\b)[A-Za-z0-9._-]+)|(/Users/(?!(<|\$|you|user|name|Shared)\b)[A-Za-z0-9._-]+/)|(/home/(?!(<|\$|you|user|name|runner)\b)[A-Za-z0-9._-]+/)'
    $mailRx = '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b'

    $files = git ls-files | Where-Object {
        $_ -notmatch '\.(ico|png|jpg|gif|zip|exe|dll|pdb)$' -and $_ -ne 'LICENSE' -and $_ -ne 'tools/check-privacy.ps1'
    }
    foreach ($f in $files) {
        $n = 0
        foreach ($line in [IO.File]::ReadLines((Join-Path $root $f))) {
            $n++
            if ($line -match $pathRx) { $problems += "${f}:${n}: user-profile path '$($Matches[0])'" }
            foreach ($m in [regex]::Matches($line, $mailRx)) {
                if ($m.Value -notmatch $allowedMail) { $problems += "${f}:${n}: e-mail address '$($m.Value)'" }
            }
        }
    }

    if ($Range) {
        $ids = git log --format='%ae%n%ce' $Range | Sort-Object -Unique
        foreach ($id in $ids) {
            if ($id -and $id -notmatch '(@users\.noreply\.github\.com|^noreply@github\.com)$') {
                $problems += "commit identity '$id' in $Range is not a GitHub noreply address"
            }
        }
    }

    if ($problems) {
        $problems | ForEach-Object { Write-Host "::error::$_"; Write-Host "FAIL: $_" }
        exit 1
    }
    Write-Host "Privacy OK: $($files.Count) files$(if ($Range) { ", commits $Range" })"
}
finally { Pop-Location }
