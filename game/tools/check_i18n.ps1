# Checks game/Godot/i18n/*.json: no duplicate keys (one duplicate makes the whole language fall back to English)
# and every translation keeps its key's {0} {1:N0} placeholders. Run: powershell -File game/tools/check_i18n.ps1
$bad = 0
foreach ($f in Get-ChildItem "$PSScriptRoot\..\Godot\i18n\*.json" | ? Name -notlike 'cards.*') {
  $pairs = [regex]::Matches((Get-Content $f.FullName -Raw -Encoding utf8), '(?m)^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"')
  $pairs | % { $_.Groups[1].Value } | Group-Object -CaseSensitive | ? Count -gt 1 | % { "$($f.Name) duplicate: $($_.Name)"; $bad++ }
  foreach ($p in $pairs) {
    $a = ([regex]::Matches($p.Groups[1].Value, '\{\d[^}]*\}') | % Value | Sort-Object) -join ','
    $b = ([regex]::Matches($p.Groups[2].Value, '\{\d[^}]*\}') | % Value | Sort-Object) -join ','
    if ($a -ne $b) { "$($f.Name) placeholders differ: $($p.Groups[1].Value)"; $bad++ }
  }
}
"problems: $bad"
exit [int]($bad -gt 0)
