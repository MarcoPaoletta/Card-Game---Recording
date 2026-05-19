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
    return @{ cellSize = $cellSize; gridOriginX = $lines[0]; gridOriginY = $null }
}

# Fallback: detect block size by uniformity analysis (8..maxGrid candidate grids)
function Detect-BlockByUniformity($img, $maxGrid = 11, $thresh = 50) {
    $w = $img.Width; $h = $img.Height
    $minDim = [math]::Min($w, $h)
    $bestGrid = 0; $bestScore = -1.0
    for ($grid = 8; $grid -le $maxGrid; $grid++) {
        $bs = $minDim / $grid
        if ($bs -lt 6) { break }
        $uniform = 0; $total = 0
        for ($cy = 0; $cy -lt $grid; $cy++) {
            for ($cx = 0; $cx -lt $grid; $cx++) {
                $x0 = [int]($cx * $bs); $y0 = [int]($cy * $bs)
                $x1 = [int](($cx+1) * $bs) - 1; $y1 = [int](($cy+1) * $bs) - 1
                if ($x1 -ge $w -or $y1 -ge $h) { continue }
                $pad = [math]::Max(2, [int]($bs * 0.3))
                $xa = $x0+$pad; $xb = $x1-$pad; $ya = $y0+$pad; $yb = $y1-$pad
                if ($xa -ge $xb -or $ya -ge $yb) { continue }
                $c1 = $img.GetPixel($xa,$ya); $c2 = $img.GetPixel($xb,$ya)
                $c3 = $img.GetPixel($xa,$yb); $c4 = $img.GetPixel($xb,$yb)
                $cs = @($c1,$c2,$c3,$c4); $maxD = 0
                for ($i=0;$i -lt 4;$i++) { for ($j=$i+1;$j -lt 4;$j++) {
                    $d = [math]::Sqrt(($cs[$i].R-$cs[$j].R)*($cs[$i].R-$cs[$j].R)+($cs[$i].G-$cs[$j].G)*($cs[$i].G-$cs[$j].G)+($cs[$i].B-$cs[$j].B)*($cs[$i].B-$cs[$j].B))
                    if ($d -gt $maxD) { $maxD = $d }
                } }
                if ($maxD -lt $thresh) { $uniform++ }
                $total++
            }
        }
        if ($total -eq 0) { continue }
        $score = [double]$uniform / $total
        if ($score -gt $bestScore) { $bestScore = $score; $bestGrid = $grid }
    }
    if ($bestGrid -eq 0) { return $null }
    return @{ cellSize = $minDim / $bestGrid; gridOriginX = 0; gridOriginY = 0; score = $bestScore }
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
    if ($null -eq $info) {
        Write-Output "  No gridlines detected, falling back to uniformity analysis..."
        $info = Detect-BlockByUniformity $img -maxGrid $MaxGrid
        if ($null -eq $info) { Write-Output "  ERROR: cannot determine block size"; $img.Dispose(); return }
        Write-Output ("  Uniformity-based: cellSize={0:F1}px score={1:P0}" -f $info.cellSize, $info.score)
    }
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

    # Classify cells: bg color (matches background sample) vs drawn
    $isBg = [bool[,]]::new($cellsW, $cellsH)
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            $rgb = $samples[$cx,$cy]
            if ((ColorDist $rgb $bg) -lt ($BgThresh*$BgThresh)) { $isBg[$cx,$cy] = $true }
        }
    }

    # Flood-fill desde los bordes del grid usando SOLO celdas bg. Las visitadas
    # son fondo "real" (afuera de la silueta). Las celdas con color bg NO
    # visitadas estan encerradas por el outline (ej: blanco dentro del cuerpo
    # de Sonic, los ojos del perro, la panza del zorro) y se emiten como
    # celdas normales con el color bg.
    $outsideBg = [bool[,]]::new($cellsW, $cellsH)
    $queue = New-Object System.Collections.Generic.Queue[object]
    $cw1 = $cellsW - 1; $ch1 = $cellsH - 1
    for ($cx=0; $cx -lt $cellsW; $cx++) {
        if ($isBg[$cx,0])    { $outsideBg[$cx,0] = $true;  $queue.Enqueue(@{x=$cx; y=0}) }
        if ($isBg[$cx,$ch1]) { $outsideBg[$cx,$ch1] = $true; $queue.Enqueue(@{x=$cx; y=$ch1}) }
    }
    for ($cy=0; $cy -lt $cellsH; $cy++) {
        if ($isBg[0,$cy])    { $outsideBg[0,$cy] = $true;  $queue.Enqueue(@{x=0; y=$cy}) }
        if ($isBg[$cw1,$cy]) { $outsideBg[$cw1,$cy] = $true; $queue.Enqueue(@{x=$cw1; y=$cy}) }
    }
    $deltas = @(@{dx=1;dy=0}, @{dx=-1;dy=0}, @{dx=0;dy=1}, @{dx=0;dy=-1})
    while ($queue.Count -gt 0) {
        $p = $queue.Dequeue(); $x = $p.x; $y = $p.y
        foreach ($d in $deltas) {
            $nx = $x + $d.dx; $ny = $y + $d.dy
            if ($nx -lt 0 -or $nx -ge $cellsW -or $ny -lt 0 -or $ny -ge $cellsH) { continue }
            if (-not $isBg[$nx,$ny]) { continue }
            if ($outsideBg[$nx,$ny]) { continue }
            $outsideBg[$nx,$ny] = $true
            $queue.Enqueue(@{x=$nx; y=$ny})
        }
    }

    # Build palette: enclosed bg cells se incluyen usando el color bg como sample.
    $palette = @()
    $cellPal = [int[,]]::new($cellsW, $cellsH)
    $enclosedBgCount = 0
    for ($cy=0;$cy -lt $cellsH;$cy++) {
        for ($cx=0;$cx -lt $cellsW;$cx++) {
            $cellIsBg = $isBg[$cx,$cy]
            # Solo skipear el bg REAL (afuera del outline).
            if ($cellIsBg -and $outsideBg[$cx,$cy]) { $cellPal[$cx,$cy] = 0; continue }
            $rgb = $samples[$cx,$cy]
            if ($cellIsBg) { $rgb = $bg; $enclosedBgCount++ }
            $idx = -1
            for ($i=0;$i -lt $palette.Count;$i++) {
                if ((ColorDist $rgb $palette[$i]) -lt $ClusterThresh) { $idx = $i; break }
            }
            if ($idx -eq -1) { $palette += ,$rgb; $idx = $palette.Count - 1 }
            $cellPal[$cx,$cy] = $idx + 1
        }
    }
    if ($enclosedBgCount -gt 0) {
        Write-Output ("  Enclosed bg cells (interior, no skipeadas): {0}" -f $enclosedBgCount)
    }
    Write-Output ("  Palette ({0} colors):" -f $palette.Count)
    for ($i=0;$i -lt $palette.Count;$i++) {
        Write-Output ("    [{0}] #{1:X2}{2:X2}{3:X2}" -f ($i+1), $palette[$i][0],$palette[$i][1],$palette[$i][2])
    }

    # Auto-label via HSV (always produces a valid name, never "ColN")
    $labels = @("Sky")
    foreach ($rgb in $palette) {
        $r=$rgb[0]; $g=$rgb[1]; $b=$rgb[2]
        $r1 = $r / 255.0; $g1 = $g / 255.0; $b1 = $b / 255.0
        $mx = [math]::Max($r1, [math]::Max($g1, $b1))
        $mn = [math]::Min($r1, [math]::Min($g1, $b1))
        $v = $mx
        $s = if ($mx -eq 0) { 0 } else { ($mx - $mn) / $mx }
        $h = 0.0
        if ($mx -ne $mn) {
            $d = $mx - $mn
            if ($mx -eq $r1)     { $h = (($g1 - $b1) / $d) }
            elseif ($mx -eq $g1) { $h = (($b1 - $r1) / $d) + 2 }
            else                 { $h = (($r1 - $g1) / $d) + 4 }
            $h = $h * 60.0
            while ($h -lt 0) { $h += 360 }
            while ($h -ge 360) { $h -= 360 }
        }
        $name = "Negro"
        if ($v -lt 0.12) { $name = "Negro" }
        elseif ($s -lt 0.12) {
            if ($v -gt 0.88)     { $name = "Blanco" }
            elseif ($v -gt 0.6)  { $name = "GrisClaro" }
            elseif ($v -gt 0.3)  { $name = "Gris" }
            else                 { $name = "GrisOscuro" }
        }
        else {
            $name = if ($h -lt 15)       { "Rojo" }
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
            if ($v -lt 0.35) { $name = $name + "Oscuro" }
            elseif ($s -lt 0.4 -and $v -gt 0.75) { $name = $name + "Pastel" }
            elseif ($name -eq "Naranja" -and $v -lt 0.55) { $name = "Marron" }
            elseif ($name -eq "Rojo" -and $v -lt 0.5) { $name = "RojoOscuro" }
        }
        $labels += $name
    }
    $counts = @{}
    for ($i=1;$i -lt $labels.Count;$i++) {
        $base = $labels[$i]
        if (-not $counts.ContainsKey($base)) { $counts[$base] = 1 }
        else { $counts[$base]++; $labels[$i] = "$base$($counts[$base])" }
    }

    # ===== Paridad auto-fix: remover una celda EXTERIOR por color impar =====
    # El juego necesita conteos pares (los chunks se forman de a pares de cartas).
    # Antes el pipeline solo reportaba imparidad y dejaba al user fixearlo a mano.
    # Ahora: para cada color con count impar, removemos la celda con MAS vecinos
    # bg (la mas exterior — quitar una "punta" del borde casi no afecta la
    # silueta). Si todas las celdas estan internas (sin vecinos bg), elegimos
    # la mas lejos del centroide del color (PUNTA mas alejada del grueso).
    # Reportamos al user que celdas se removieron para que pueda revisar.
    $parityDeltas = @(@{dx=1;dy=0}, @{dx=-1;dy=0}, @{dx=0;dy=1}, @{dx=0;dy=-1})
    $paletteCount0 = $palette.Count + 1
    $colorCounts0 = New-Object int[] $paletteCount0
    for ($cy=0;$cy -lt $cellsH;$cy++) { for ($cx=0;$cx -lt $cellsW;$cx++) { $colorCounts0[$cellPal.GetValue($cx,$cy)]++ } }
    $autoOps = @()
    for ($colorIdx=1; $colorIdx -lt $paletteCount0; $colorIdx++) {
        if ($colorCounts0[$colorIdx] % 2 -eq 0) { continue }
        $count = $colorCounts0[$colorIdx]
        # CASO 1: count == 1 (singleton, ej: lengua del perro). REMOVER eliminaria
        # el feature entero → AGREGAR un vecino bg para hacer 2 cells.
        if ($count -eq 1) {
            $scx = -1; $scy = -1
            for ($cy=0;$cy -lt $cellsH;$cy++) { for ($cx=0;$cx -lt $cellsW;$cx++) {
                if ($cellPal.GetValue($cx,$cy) -eq $colorIdx) { $scx = $cx; $scy = $cy; break }
            }; if ($scx -ge 0) { break } }
            # Buscar vecino bg para extender. Preferir el de mas vecinos bg (mas exterior).
            $bestNx = -1; $bestNy = -1; $bestNbg = -1
            foreach ($d in $parityDeltas) {
                $nx = $scx + $d.dx; $ny = $scy + $d.dy
                if ($nx -lt 0 -or $nx -ge $cellsW -or $ny -lt 0 -or $ny -ge $cellsH) { continue }
                if ($cellPal.GetValue($nx,$ny) -ne 0) { continue }
                $nbg = 0
                foreach ($d2 in $parityDeltas) {
                    $mx = $nx + $d2.dx; $my = $ny + $d2.dy
                    if ($mx -lt 0 -or $mx -ge $cellsW -or $my -lt 0 -or $my -ge $cellsH) { $nbg++ }
                    elseif ($cellPal.GetValue($mx,$my) -eq 0) { $nbg++ }
                }
                if ($nbg -gt $bestNbg) { $bestNbg = $nbg; $bestNx = $nx; $bestNy = $ny }
            }
            if ($bestNx -ge 0) {
                $cellPal.SetValue($colorIdx, $bestNx, $bestNy)
                $autoOps += ("+({0},{1}) {2} (singleton extendido)" -f $bestNx, $bestNy, $labels[$colorIdx])
                continue
            }
            # Sin vecinos bg disponibles (singleton enclavado). Cae al remove default abajo.
        }
        # CASO 2: count >= 3 impar, o singleton sin vecinos bg. REMOVER la mas exterior.
        $sumX = 0.0; $sumY = 0.0; $n = 0
        for ($cy=0;$cy -lt $cellsH;$cy++) {
            for ($cx=0;$cx -lt $cellsW;$cx++) {
                if ($cellPal.GetValue($cx,$cy) -eq $colorIdx) { $sumX += $cx; $sumY += $cy; $n++ }
            }
        }
        $cenX = if ($n -gt 0) { $sumX / $n } else { 0.0 }
        $cenY = if ($n -gt 0) { $sumY / $n } else { 0.0 }
        $bestCx = -1; $bestCy = -1; $bestBg = -1; $bestDist2 = -1.0
        for ($cy=0;$cy -lt $cellsH;$cy++) {
            for ($cx=0;$cx -lt $cellsW;$cx++) {
                if ($cellPal.GetValue($cx,$cy) -ne $colorIdx) { continue }
                $bgN = 0
                foreach ($d in $parityDeltas) {
                    $nx = $cx + $d.dx; $ny = $cy + $d.dy
                    if ($nx -lt 0 -or $nx -ge $cellsW -or $ny -lt 0 -or $ny -ge $cellsH) { $bgN++ }
                    elseif ($cellPal.GetValue($nx,$ny) -eq 0) { $bgN++ }
                }
                $dx2 = ($cx - $cenX); $dy2 = ($cy - $cenY); $dist2 = $dx2*$dx2 + $dy2*$dy2
                if ($bgN -gt $bestBg -or ($bgN -eq $bestBg -and $dist2 -gt $bestDist2)) {
                    $bestBg = $bgN; $bestDist2 = $dist2; $bestCx = $cx; $bestCy = $cy
                }
            }
        }
        if ($bestCx -ge 0) {
            $cellPal.SetValue(0, $bestCx, $bestCy)
            $autoOps += ("-({0},{1}) {2} [bg-vecinos={3}]" -f $bestCx, $bestCy, $labels[$colorIdx], $bestBg)
        }
    }
    if ($autoOps.Count -gt 0) {
        Write-Output ("  Paridad auto-fix ({0} ops): {1}" -f $autoOps.Count, ($autoOps -join "; "))
    }

    # Find bbox of drawn cells (re-calculado despues del parity-fix)
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
    # ===== Post-process: eliminate singletons by stealing end-cells from same-color neighbor chunks =====
    $unresolvedSingletons = @()
    $singletons = @($chunks | Where-Object { $_.cells.Count -eq 1 })
    foreach ($sc in $singletons) {
        $cell = $sc.cells[0]
        $x = $cell.x; $y = $cell.y; $col = $sc.color
        $fixed = $false
        $xp1 = $x + 1; $xm1 = $x - 1; $yp1 = $y + 1; $ym1 = $y - 1
        $neighs = @( ,@($xp1,$y,3) )
        $neighs += ,@($xm1,$y,2)
        $neighs += ,@($x,$yp1,1)
        $neighs += ,@($x,$ym1,0)
        foreach ($n in $neighs) {
            $nx = $n[0]; $ny = $n[1]; $newDir = $n[2]
            # Find another chunk of same color containing (nx,ny), with >=3 cells
            $target = $null
            foreach ($c in $chunks) {
                if ($c -eq $sc) { continue }
                if ($c.color -ne $col) { continue }
                if ($c.cells.Count -lt 3) { continue }
                $first = $c.cells[0]; $last = $c.cells[$c.cells.Count - 1]
                $isFirst = ($first.x -eq $nx -and $first.y -eq $ny)
                $isLast  = ($last.x -eq $nx  -and $last.y -eq $ny)
                if ($isFirst -or $isLast) { $target = $c; break }
            }
            if ($null -eq $target) { continue }
            # Steal (nx,ny) from target
            $newList = New-Object System.Collections.Generic.List[object]
            foreach ($cc in $target.cells) {
                if (-not ($cc.x -eq $nx -and $cc.y -eq $ny)) { $newList.Add($cc) }
            }
            $target.cells = $newList
            # Convert singleton chunk into a 2-cell chunk (x,y) -> (nx,ny)
            $newCells = New-Object System.Collections.Generic.List[object]
            $newCells.Add(@{x=$x; y=$y})
            $newCells.Add(@{x=$nx; y=$ny})
            $sc.cells = $newCells
            if ($x -eq $nx) { $sc.orient = "V" } else { $sc.orient = "H" }
            $sc.dir = $newDir
            $sc.stolen = $true
            $fixed = $true
            break
        }
        if (-not $fixed) { $unresolvedSingletons += $sc }
    }
    # Assign dirs by "nearest exit" heuristic — cada chunk apunta al borde mas
    # cercano del bbox. Evita el patron impasable mas comun: dos chunks en la
    # misma fila/columna apuntandose entre si (A dir=Right + B dir=Left con A
    # a la izquierda de B → ambos se bloquean mutuamente, ninguno sale).
    # Con nearest-exit:
    #   - Chunks en mitad izquierda del bbox: dir=Left (salen por la izquierda).
    #   - Chunks en mitad derecha: dir=Right (salen por la derecha).
    #   Resultado: misma fila con dos chunks → o ambos del mismo lado (mismo
    #   dir, solvable por orden downstream-first), o uno de cada lado (apuntan
    #   AWAY entre si, no se bloquean).
    # Excepcion: chunks "stolen" por el anti-singleton ya tienen dir asignado
    # por el vecino de quien robaron — no tocar.
    $bbCenterX = ($minCx + $maxCx) / 2.0
    $bbCenterY = ($minCy + $maxCy) / 2.0
    # Aplicar nearest-exit a TODOS los chunks (incluidos los stolen). Los
    # stolen quedaron orient=H/V segun cómo crecieron, pero su dir del robo
    # puede crear pares face-to-face con otros chunks vecinos del mismo color.
    # La emision JSON re-ordena las cells por dir y pone isEnd al final, asi
    # que sobrescribir dir aca no rompe la chain.
    foreach ($c in $chunks) {
        $avgX = 0.0; $avgY = 0.0
        foreach ($cell in $c.cells) { $avgX += $cell.x; $avgY += $cell.y }
        $avgX /= $c.cells.Count; $avgY /= $c.cells.Count
        if ($c.orient -eq "H") {
            $c.dir = if ($avgX -lt $bbCenterX) { 2 } else { 3 }
        } else {
            $c.dir = if ($avgY -lt $bbCenterY) { 0 } else { 1 }
        }
    }

    # ===== Solvencia: detector de pares face-to-face (impasable) =====
    # Para cada par de chunks (A, B), si A apunta hacia B y B apunta hacia A
    # en el camino de salida → impasable (cycle de longitud 2). El criterio
    # exit-mas-cercano deberia eliminar estos pares por diseno, pero verificamos
    # post-hoc por las dudas (chunks stolen, asimetrias raras, etc.).
    $facingPairs = @()
    for ($i=0; $i -lt $chunks.Count; $i++) {
        for ($j=$i+1; $j -lt $chunks.Count; $j++) {
            $a = $chunks[$i]; $b = $chunks[$j]
            $aMinX = ($a.cells | ForEach-Object x | Measure-Object -Min).Minimum
            $aMaxX = ($a.cells | ForEach-Object x | Measure-Object -Max).Maximum
            $aMinY = ($a.cells | ForEach-Object y | Measure-Object -Min).Minimum
            $aMaxY = ($a.cells | ForEach-Object y | Measure-Object -Max).Maximum
            $bMinX = ($b.cells | ForEach-Object x | Measure-Object -Min).Minimum
            $bMaxX = ($b.cells | ForEach-Object x | Measure-Object -Max).Maximum
            $bMinY = ($b.cells | ForEach-Object y | Measure-Object -Min).Minimum
            $bMaxY = ($b.cells | ForEach-Object y | Measure-Object -Max).Maximum
            # H-H face-off: misma fila (rectangulares 1xN) Y solapadas en y, A a izq con dir=R, B a der con dir=L
            if ($a.orient -eq "H" -and $b.orient -eq "H" -and $aMinY -eq $bMinY) {
                if ($a.dir -eq 3 -and $b.dir -eq 2 -and $aMaxX -lt $bMinX) { $facingPairs += "row ${aMinY}: H@$aMinX-$aMaxX R vs H@$bMinX-$bMaxX L" }
                if ($b.dir -eq 3 -and $a.dir -eq 2 -and $bMaxX -lt $aMinX) { $facingPairs += "row ${aMinY}: H@$bMinX-$bMaxX R vs H@$aMinX-$aMaxX L" }
            }
            # V-V face-off: misma columna
            if ($a.orient -eq "V" -and $b.orient -eq "V" -and $aMinX -eq $bMinX) {
                if ($a.dir -eq 1 -and $b.dir -eq 0 -and $aMaxY -lt $bMinY) { $facingPairs += "col ${aMinX}: V@$aMinY-$aMaxY D vs V@$bMinY-$bMaxY U" }
                if ($b.dir -eq 1 -and $a.dir -eq 0 -and $bMaxY -lt $aMinY) { $facingPairs += "col ${aMinX}: V@$bMinY-$bMaxY D vs V@$aMinY-$aMaxY U" }
            }
        }
    }
    if ($facingPairs.Count -gt 0) {
        Write-Output ("  WARNING: {0} pares face-to-face (impasable, revisar a mano):" -f $facingPairs.Count)
        foreach ($f in $facingPairs) { Write-Output "    $f" }
    }

    $pFloat = New-Object System.Collections.Generic.List[object]
    $pFloat.Add(@(0.0, 0.0, 0.0))
    foreach ($rgb in $palette) {
        $pFloat.Add(@([math]::Round($rgb[0]/255.0,6),[math]::Round($rgb[1]/255.0,6),[math]::Round($rgb[2]/255.0,6)))
    }
    $cellsJson = New-Object System.Collections.Generic.List[string]
    foreach ($c in $chunks) {
        $ordered = $null
        if ($c.dir -eq 0)     { $ordered = @($c.cells | Sort-Object { -$_.y }) }
        elseif ($c.dir -eq 1) { $ordered = @($c.cells | Sort-Object { $_.y }) }
        elseif ($c.dir -eq 2) { $ordered = @($c.cells | Sort-Object { -$_.x }) }
        elseif ($c.dir -eq 3) { $ordered = @($c.cells | Sort-Object { $_.x }) }
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
    $remaining = @($chunks | Where-Object { $_.cells.Count -eq 1 })
    if ($remaining.Count -gt 0) {
        Write-Output ("  Info: {0} single-cell chunks (con isEnd=true + direction, validos en juego):" -f $remaining.Count)
        foreach ($sc in $remaining) {
            $cell = $sc.cells[0]
            $cx = $cell.x - $minCx
            $cy = $cell.y - $minCy
            Write-Output ("    ({0},{1}) {2} dir={3}" -f $cx, $cy, $Labels[$sc.color], $sc.dir)
        }
    }
}

Translate-Gridded -ImagePath "Assets\_LevelSources\honguito.png" -LevelIndex 5 -LevelName "Level 6 - Honguito"
Translate-Gridded -ImagePath "Assets\_LevelSources\673fa4f744f3285ea575d3fa0c295fa8.jpg" -LevelIndex 6 -LevelName "Level 7 - Imagen1"
Translate-Gridded -ImagePath "Assets\_LevelSources\Pixel-art-dune-licorne-magique-en-couleurs-vives.jpeg" -LevelIndex 7 -LevelName "Level 8 - Unicornio" -MaxGrid 12
Translate-Gridded -ImagePath "Assets\_LevelSources\d2jncfn-5b1976d1-764e-4dcb-8e00-9d83331e027f.jpg" -LevelIndex 8 -LevelName "Level 9 - Imagen3" -ClusterThresh 3500

Translate-Gridded -ImagePath "Assets\_LevelSources\Sonic.png" -LevelIndex 9 -LevelName "Level 10 - Sonic" -MaxGrid 30
Translate-Gridded -ImagePath "Assets\_LevelSources\water-melon.png" -LevelIndex 10 -LevelName "Level 11 - Sandia" -MaxGrid 20
Translate-Gridded -ImagePath "Assets\_LevelSources\Pixel-art-Creez-un-adorable-cochon-en-quelques-pixels.png" -LevelIndex 11 -LevelName "Level 12 - Chancho" -MaxGrid 20
Translate-Gridded -ImagePath "Assets\_LevelSources\dog_cropped.png" -LevelIndex 12 -LevelName "Level 13 - Perro" -MaxGrid 25
Translate-Gridded -ImagePath "Assets\_LevelSources\fox_cropped.png" -LevelIndex 13 -LevelName "Level 14 - Zorro" -MaxGrid 28