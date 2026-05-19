$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function FmtFloat($v){$s=([double]$v).ToString("R",[System.Globalization.CultureInfo]::InvariantCulture); if($s -notmatch '[.eE]'){$s="$s.0"}; return $s}
function ColorDist($a, $b) {
    return ($a[0]-$b[0])*($a[0]-$b[0]) + ($a[1]-$b[1])*($a[1]-$b[1]) + ($a[2]-$b[2])*($a[2]-$b[2])
}

# Detect cell size by scanning a row for gray grid line pixels
function Detect-GridSpacing($img, $rowFrac = 0.05) {
    $w = $img.Width; $h = $img.Height
    $rowY = [int]($h * $rowFrac)
    $grayPositions = New-Object System.Collections.Generic.List[int]
    for ($x = 0; $x -lt $w; $x++) {
        $p = $img.GetPixel($x, $rowY)
        $avg = ($p.R + $p.G + $p.B) / 3.0
        $maxCh = [math]::Max([math]::Max($p.R, $p.G), $p.B)
        $minCh = [math]::Min([math]::Min($p.R, $p.G), $p.B)
        if ($avg -lt 230 -and $avg -gt 80 -and ($maxCh - $minCh) -lt 20) {
            $grayPositions.Add($x)
        }
    }
    if ($grayPositions.Count -lt 4) { return $null }
    $lines = @()
    $prev = -10; $start = -1
    foreach ($p in $grayPositions) {
        if ($p - $prev -gt 2) {
            if ($start -ne -1) { $lines += [int](($start + $prev) / 2) }
            $start = $p
        }
        $prev = $p
    }
    if ($start -ne -1) { $lines += [int](($start + $prev) / 2) }
    if ($lines.Count -lt 3) { return $null }
    $diffs = @()
    for ($i=1; $i -lt $lines.Count; $i++) { $diffs += ($lines[$i] - $lines[$i-1]) }
    $cellSize = ($diffs | Measure-Object -Average).Average
    # First line position (origin of grid in image coords)
    return @{ cellSize = $cellSize; gridOriginX = $lines[0]; gridOriginY = $null }
}

# ===== Process gridded image =====
function Translate-Gridded {
    param(
        [string]$ImagePath, [int]$LevelIndex, [string]$LevelName,
        [int]$ClusterThresh = 800,
        [int]$BgThresh = 25,
        [int]$MaxGrid = 11
    )
    $img = [System.Drawing.Bitmap]::new($ImagePath)
    $w = $img.Width; $h = $img.Height
    Write-Output "Processing $ImagePath ($w x $h)"

    $info = Detect-GridSpacing $img -rowFrac 0.05
    if ($null -eq $info) { Write-Output "  ERROR: no gridlines detected"; $img.Dispose(); return }
    $cellSize = $info.cellSize

    # Detect grid Y origin by scanning a vertical column for gray gridline pixels
    $colX = [int]($w * 0.05)
    $grayY = New-Object System.Collections.Generic.List[int]
    for ($y = 0; $y -lt $h; $y++) {
        $p = $img.GetPixel($colX, $y)
        $avg = ($p.R + $p.G + $p.B) / 3.0
        $maxCh = [math]::Max([math]::Max($p.R, $p.G), $p.B)
        $minCh = [math]::Min([math]::Min($p.R, $p.G), $p.B)
        if ($avg -lt 230 -and $avg -gt 80 -and ($maxCh - $minCh) -lt 20) { $grayY.Add($y) }
    }
    $linesY = @(); $prev = -10; $start = -1
    foreach ($p in $grayY) {
        if ($p - $prev -gt 2) {
            if ($start -ne -1) { $linesY += [int](($start + $prev) / 2) }
            $start = $p
        }
        $prev = $p
    }
    if ($start -ne -1) { $linesY += [int](($start + $prev) / 2) }
    $gridOY = if ($linesY.Count -gt 0) { $linesY[0] } else { 0 }
    $gridOX = $info.gridOriginX

    $cellsW = [int][math]::Floor(($w - $gridOX) / $cellSize)
    $cellsH = [int][math]::Floor(($h - $gridOY) / $cellSize)
    Write-Output ("  Grid: cellSize={0:F1}px, origin=({1},{2}), {3} x {4} cells visible" -f $cellSize, $gridOX, $gridOY, $cellsW, $cellsH)

    # Detect bg color (corner cell, presumably empty)
    $bgSampleX = [int]($gridOX + $cellSize/2)
    $bgSampleY = [int]($gridOY + $cellSize/2)
    $bgPix = $img.GetPixel($bgSampleX, $bgSampleY)
    $bg = @($bgPix.R, $bgPix.G, $bgPix.B)
    Write-Output ("  Bg from corner cell: #{0:X2}{1:X2}{2:X2}" -f $bg[0],$bg[1],$bg[2])

    # Sample each cell at center
    $samples = New-Object 'object[,]' $cellsW, $cellsH
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            $sx = [int]($gridOX + $cx * $cellSize + $cellSize/2)
            $sy = [int]($gridOY + $cy * $cellSize + $cellSize/2)
            # 3x3 sample around center
            $rs=0;$gs=0;$bs=0;$n=0
            $step = [math]::Max(1, [int]($cellSize/6))
            foreach ($dy in -$step,0,$step) {
                foreach ($dx in -$step,0,$step) {
                    $px = [math]::Max(0,[math]::Min($w-1,$sx+$dx))
                    $py = [math]::Max(0,[math]::Min($h-1,$sy+$dy))
                    $p = $img.GetPixel($px,$py)
                    $rs+=$p.R; $gs+=$p.G; $bs+=$p.B; $n++
                }
            }
            $samples[$cx,$cy] = @([int]($rs/$n),[int]($gs/$n),[int]($bs/$n))
        }
    }
    $img.Dispose()

    # Classify cells: bg vs drawn
    $isBg = [bool[,]]::new($cellsW, $cellsH)
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            $rgb = $samples[$cx,$cy]
            if ((ColorDist $rgb $bg) -lt ($BgThresh*$BgThresh)) { $isBg[$cx,$cy] = $true }
        }
    }

    # Build palette from drawn cells
    $palette = @()
    $cellPal = [int[,]]::new($cellsW, $cellsH)
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            if ($isBg.GetValue($cx,$cy)) { $cellPal[$cx,$cy] = 0; continue }
            $rgb = $samples[$cx,$cy]
            $idx = -1
            for ($i=0;$i -lt $palette.Count;$i++) {
                if ((ColorDist $rgb $palette[$i]) -lt $ClusterThresh) { $idx = $i; break }
            }
            if ($idx -eq -1) { $palette += ,$rgb; $idx = $palette.Count - 1 }
            $cellPal[$cx,$cy] = $idx + 1
        }
    }
    Write-Output ("  Palette ({0} colors):" -f $palette.Count)
    for ($i=0;$i -lt $palette.Count;$i++) {
        Write-Output ("    [{0}] #{1:X2}{2:X2}{3:X2}" -f ($i+1), $palette[$i][0],$palette[$i][1],$palette[$i][2])
    }

    # Auto-label
    $labels = @("Sky")
    foreach ($rgb in $palette) {
        $r=$rgb[0]; $g=$rgb[1]; $b=$rgb[2]
        $name = "Col$(($labels.Count))"
        if ($r -lt 40 -and $g -lt 40 -and $b -lt 40) { $name = "Negro" }
        elseif ($r -gt 215 -and $g -gt 215 -and $b -gt 215) { $name = "Blanco" }
        elseif ($r -gt 180 -and $g -lt 80 -and $b -lt 80) { $name = "Rojo" }
        elseif ($r -gt 180 -and $g -gt 180 -and $b -lt 180) { $name = "Amarillo" }
        elseif ($r -lt 100 -and $g -gt 130 -and $b -lt 130) { $name = "Verde" }
        elseif ($r -lt 130 -and $g -lt 160 -and $b -gt 160) { $name = "Azul" }
        elseif ($r -gt 200 -and $g -gt 100 -and $g -lt 180 -and $b -lt 100) { $name = "Naranja" }
        elseif ($r -gt 100 -and $g -lt 80 -and $b -lt 60) { $name = "Marron" }
        elseif ($r -gt 140 -and $g -gt 140 -and $b -gt 140) { $name = "Gris" }
        $labels += $name
    }
    $counts = @{}
    for ($i=1;$i -lt $labels.Count;$i++) {
        $base = $labels[$i]
        if (-not $counts.ContainsKey($base)) { $counts[$base] = 1 }
        else { $counts[$base]++; $labels[$i] = "$base$($counts[$base])" }
    }

    # Find bbox of drawn cells
    $minCx = $cellsW; $minCy = $cellsH; $maxCx = -1; $maxCy = -1
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            if ($cellPal.GetValue($cx,$cy) -ne 0) {
                if ($cx -lt $minCx) { $minCx = $cx }
                if ($cy -lt $minCy) { $minCy = $cy }
                if ($cx -gt $maxCx) { $maxCx = $cx }
                if ($cy -gt $maxCy) { $maxCy = $cy }
            }
        }
    }
    $bbW = $maxCx - $minCx + 1
    $bbH = $maxCy - $minCy + 1
    Write-Output ("  Drawn bbox: ({0},{1})..({2},{3}) = {4}x{5}" -f $minCx,$minCy,$maxCx,$maxCy,$bbW,$bbH)

    if ($bbW -gt $MaxGrid -or $bbH -gt $MaxGrid) {
        Write-Output ("  WARNING: drawing is {0}x{1}, exceeds max {2}x{2}. Aborting." -f $bbW,$bbH,$MaxGrid)
        return
    }

    # Per-color counts and parity report
    $colorCounts = New-Object int[] ($palette.Count + 1)
    for ($cy=0;$cy -lt $cellsH;$cy++) { for ($cx=0;$cx -lt $cellsW;$cx++) { $colorCounts[$cellPal.GetValue($cx,$cy)]++ } }
    Write-Output "  Counts (no auto-fix):"
    $totalNonSky = 0
    $impar = @()
    for ($i=0;$i -lt $colorCounts.Count;$i++) {
        $par = if ($colorCounts[$i] % 2 -eq 0) {"par"} else {"IMPAR"}
        Write-Output ("    {0,-12} {1,4} {2}" -f $labels[$i], $colorCounts[$i], $par)
        if ($i -ne 0) {
            $totalNonSky += $colorCounts[$i]
            if ($colorCounts[$i] % 2 -ne 0) { $impar += $labels[$i] }
        }
    }
    if ($impar.Count -gt 0) {
        Write-Output ("  NOTE: colores IMPARES (vas a tener que ajustar a mano en builder): {0}" -f ($impar -join ", "))
    }

    # Chunk + emit JSON
    $assigned = [bool[,]]::new($cellsW, $cellsH)
    $chunks = New-Object System.Collections.Generic.List[object]
    $toggle = $true
    for ($y=0;$y -lt $cellsH;$y++) {
        for ($x=0;$x -lt $cellsW;$x++) {
            if ($assigned.GetValue($x,$y)) { continue }
            $col = $cellPal.GetValue($x,$y)
            if ($col -eq 0) { $assigned.SetValue($true, $x, $y); continue }
            $hLen=0; $cx=$x
            while ($cx -lt $cellsW -and -not $assigned.GetValue($cx,$y) -and $cellPal.GetValue($cx,$y) -eq $col) { $hLen++; $cx++ }
            $vLen=0; $cy=$y
            while ($cy -lt $cellsH -and -not $assigned.GetValue($x,$cy) -and $cellPal.GetValue($x,$cy) -eq $col) { $vLen++; $cy++ }
            $orient = if ($hLen -gt $vLen) {"H"} elseif ($vLen -gt $hLen) {"V"} else { if ($toggle) {"H"} else {"V"} }
            $toggle = -not $toggle
            $cells = New-Object System.Collections.Generic.List[object]
            if ($orient -eq "H") {
                for ($i=0;$i -lt $hLen;$i++) { $cells.Add(@{x=$x+$i; y=$y}); $assigned.SetValue($true, ($x+$i), $y) }
            } else {
                for ($i=0;$i -lt $vLen;$i++) { $cells.Add(@{x=$x; y=$y+$i}); $assigned.SetValue($true, $x, ($y+$i)) }
            }
            $chunks.Add(@{ color=$col; orient=$orient; cells=$cells })
        }
    }
    $pcH=@{}; $pcV=@{}
    foreach ($c in $chunks) {
        if ($c.orient -eq "H") {
            if (-not $pcH.ContainsKey($c.color)) { $pcH[$c.color]=0 }
            $i=$pcH[$c.color]; $pcH[$c.color]++
            $c.dir = if ($i % 2 -eq 0) {3} else {2}
        } else {
            if (-not $pcV.ContainsKey($c.color)) { $pcV[$c.color]=0 }
            $i=$pcV[$c.color]; $pcV[$c.color]++
            $c.dir = if ($i % 2 -eq 0) {1} else {0}
        }
    }

    $pFloat = New-Object System.Collections.Generic.List[object]
    $pFloat.Add(@(0.0, 0.0, 0.0))
    foreach ($rgb in $palette) {
        $pFloat.Add(@([math]::Round($rgb[0]/255.0,6),[math]::Round($rgb[1]/255.0,6),[math]::Round($rgb[2]/255.0,6)))
    }
    $cellsJson = New-Object System.Collections.Generic.List[string]
    foreach ($c in $chunks) {
        $ordered = switch ($c.dir) {
            0 { $c.cells | Sort-Object { -$_.y } }
            1 { $c.cells | Sort-Object { $_.y } }
            2 { $c.cells | Sort-Object { -$_.x } }
            3 { $c.cells | Sort-Object { $_.x } }
        }
        $i=0; $nc=$ordered.Count
        foreach ($cell in $ordered) {
            $isEnd = ($i -eq $nc - 1)
            $rgb = $pFloat[$c.color]
            $r=FmtFloat $rgb[0]; $g=FmtFloat $rgb[1]; $b=FmtFloat $rgb[2]
            $e = if ($isEnd) {"true"} else {"false"}
            $cx = $cell.x - $minCx
            $cy = $cell.y - $minCy
            $cellsJson.Add(@"
        {
            "x": $cx,
            "y": $cy,
            "r": $r,
            "g": $g,
            "b": $b,
            "dir": $($c.dir),
            "isEnd": $e
        }
"@)
            $i++
        }
    }
    $palJson = New-Object System.Collections.Generic.List[string]
    for ($i=1;$i -lt $labels.Count;$i++) {
        $r=FmtFloat $pFloat[$i][0]; $g=FmtFloat $pFloat[$i][1]; $b=FmtFloat $pFloat[$i][2]
        $palJson.Add(@"
        {
            "label": "$($labels[$i])",
            "color": {
                "r": $r,
                "g": $g,
                "b": $b,
                "a": 1.0
            }
        }
"@)
    }
    $json = @"
{
    "levelName": "$LevelName",
    "beltPresetName": "Circle",
    "levelPalette": [
$($palJson -join ",`n")
    ],
    "cells": [
$($cellsJson -join ",`n")
    ]
}
"@
    [System.IO.File]::WriteAllText("Assets\Resources\Levels\level_$LevelIndex.json", $json)
    Write-Output ("  -> level_{0}.json ({1} cells, {2} chunks)" -f $LevelIndex, $cellsJson.Count, $chunks.Count)
    $singletons = $chunks | Where-Object { $_.cells.Count -eq 1 }
    if ($singletons.Count -gt 0) {
        Write-Output ("  Note: {0} single-cell chunks" -f $singletons.Count)
    }
}

Translate-Gridded -ImagePath "Assets\_LevelSources\honguito.png" -LevelIndex 5 -LevelName "Level 6 - Honguito"