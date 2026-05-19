$ErrorActionPreference = "Stop"

# HSV-based color naming. Input: r,g,b in 0..255 range.
function Get-ColorName {
    param([int]$r, [int]$g, [int]$b)
    $r1 = $r / 255.0; $g1 = $g / 255.0; $b1 = $b / 255.0
    $max = [math]::Max($r1, [math]::Max($g1, $b1))
    $min = [math]::Min($r1, [math]::Min($g1, $b1))
    $v = $max
    $s = if ($max -eq 0) { 0 } else { ($max - $min) / $max }
    $h = 0.0
    if ($max -ne $min) {
        $d = $max - $min
        if ($max -eq $r1)     { $h = (($g1 - $b1) / $d) }
        elseif ($max -eq $g1) { $h = (($b1 - $r1) / $d) + 2 }
        else                  { $h = (($r1 - $g1) / $d) + 4 }
        $h = $h * 60.0
        while ($h -lt 0) { $h += 360 }
        while ($h -ge 360) { $h -= 360 }
    }

    # Grayscale tier
    if ($v -lt 0.12) { return "Negro" }
    if ($s -lt 0.12) {
        if ($v -gt 0.88) { return "Blanco" }
        if ($v -gt 0.6)  { return "GrisClaro" }
        if ($v -gt 0.3)  { return "Gris" }
        return "GrisOscuro"
    }
    # Hue base
    $base = if ($h -lt 15)       { "Rojo" }
            elseif ($h -lt 45)   { "Naranja" }
            elseif ($h -lt 65)   { "Amarillo" }
            elseif ($h -lt 90)   { "Lima" }
            elseif ($h -lt 150)  { "Verde" }
            elseif ($h -lt 200)  { "Cyan" }
            elseif ($h -lt 250)  { "Azul" }
            elseif ($h -lt 290)  { "Violeta" }
            elseif ($h -lt 330)  { "Magenta" }
            elseif ($h -lt 348)  { "Rosa" }
            else                 { "Rojo" }
    # Shade modifiers
    if ($v -lt 0.35) { return ($base + "Oscuro") }
    if ($s -lt 0.40 -and $v -gt 0.75) { return ($base + "Pastel") }
    if ($base -eq "Naranja" -and $v -lt 0.55) { return "Marron" }
    if ($base -eq "Rojo" -and $v -lt 0.5) { return "RojoOscuro" }
    return $base
}

function Relabel-Level {
    param([string]$Path)
    $raw = [System.IO.File]::ReadAllText($Path)
    $json = $raw | ConvertFrom-Json

    # Collect distinct colors from cells (rounded)
    $key = { param($c) "{0:F4},{1:F4},{2:F4}" -f $c.r,$c.g,$c.b }
    $distinctColors = @{}
    foreach ($c in $json.cells) {
        $k = & $key $c
        if (-not $distinctColors.ContainsKey($k)) {
            $distinctColors[$k] = @{ r=$c.r; g=$c.g; b=$c.b }
        }
    }
    Write-Output "$Path : $($distinctColors.Count) distinct cell colors"

    # Compute name per color
    $newPalette = @()
    $usedNames = @{}
    foreach ($k in $distinctColors.Keys) {
        $col = $distinctColors[$k]
        $name = Get-ColorName ([int]([math]::Round($col.r*255))) ([int]([math]::Round($col.g*255))) ([int]([math]::Round($col.b*255)))
        # Disambiguate duplicates
        if ($usedNames.ContainsKey($name)) {
            $usedNames[$name]++
            $finalName = "$name$($usedNames[$name])"
        } else {
            $usedNames[$name] = 1
            $finalName = $name
        }
        $newPalette += @{ label=$finalName; r=$col.r; g=$col.g; b=$col.b }
    }

    # Build new palette JSON (preserve formatting style)
    function FmtFloat($v){
        $s=([double]$v).ToString("R",[System.Globalization.CultureInfo]::InvariantCulture)
        if($s -notmatch '[.eE]'){$s="$s.0"}
        return $s
    }
    $palLines = New-Object System.Collections.Generic.List[string]
    foreach ($p in $newPalette) {
        $r = FmtFloat $p.r; $g = FmtFloat $p.g; $b = FmtFloat $p.b
        $palLines.Add(@"
        {
            "label": "$($p.label)",
            "color": {
                "r": $r,
                "g": $g,
                "b": $b,
                "a": 1.0
            }
        }
"@)
    }
    $newPalText = $palLines -join ",`n"

    # Replace the existing levelPalette block in the raw text
    # Pattern: "levelPalette": [...] — non-greedy, multiline
    $pattern = '"levelPalette":\s*\[[\s\S]*?\n\s*\]'
    $replacement = "`"levelPalette`": [`n$newPalText`n    ]"
    if ($raw -match $pattern) {
        $newRaw = $raw -replace $pattern, $replacement
        [System.IO.File]::WriteAllText($Path, $newRaw)
        Write-Output "  Updated palette ($($newPalette.Count) entries):"
        foreach ($p in $newPalette) {
            Write-Output ("    {0,-15} #{1:X2}{2:X2}{3:X2}" -f $p.label, [int]([math]::Round($p.r*255)), [int]([math]::Round($p.g*255)), [int]([math]::Round($p.b*255)))
        }
    } else {
        Write-Output "  ERROR: levelPalette block not found"
    }
    Write-Output ""
}

foreach ($i in 5,6,7,8) {
    Relabel-Level -Path "Assets\Resources\Levels\level_$i.json"
}
