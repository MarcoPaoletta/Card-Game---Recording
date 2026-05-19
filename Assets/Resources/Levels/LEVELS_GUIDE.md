# Levels Guide

Guía para diseñar y editar niveles. **Si aprendes algo nuevo sobre construcción de niveles, agregalo a este archivo.** Convenciones, decisiones y "lecciones aprendidas" viven acá.

---

## Estructura de archivos

- Los niveles viven en `Assets/Resources/Levels/level_N.json` (0-indexed).
- El nombre visible se arma con `LevelNaming.BuildName(index, note)` → `"Level {index+1} - {note}"`. Es decir, `level_3.json` se muestra como **"Level 4 - ..."**.
- El código que escribe niveles (`LevelBuilderManager.SaveCurrentLevel` y `CreateEmptyLevelFile`) usa `JsonUtility.ToJson(..., true)` → **pretty-printed con indent de 4 espacios**. No reescribir en una sola línea.

## Schema (LevelData)

```jsonc
{
    "levelName": "Level N - <nota>",
    "beltPresetName": "Circle",    // vacío = default del componente
    "cells": [
        {
            "x": 0, "y": 0,         // int, coords en grid
            "r": 1.0, "g": 0.0, "b": 0.0,  // float 0-1 (Color)
            "dir": 3,               // 0=Up, 1=Down, 2=Left, 3=Right
            "isEnd": false          // true = última celda de la cadena
        }
    ]
}
```

Ver definición en [`Assets/Scripts/Data/LevelData.cs`](../../Scripts/Data/LevelData.cs).

## Convenciones de diseño

### Origen del dibujo
- **El dibujo siempre arranca en (0,0)**. El bounding box top-left del diseño debe tocar (0,0).
- No dejar padding/margen inicial — el board se reescala automáticamente para ajustar el contenido (`LevelLayoutManager` → `BoardResizerManager`). Si dejás margen en el JSON, el board queda más grande de lo necesario.

### Coordenadas
- `x` aumenta hacia la derecha.
- `y` aumenta hacia abajo (convención típica de grids 2D, no como Unity world-space).

### Direcciones
- `0 = Up` (y decrece)
- `1 = Down` (y crece)
- `2 = Left` (x decrece)
- `3 = Right` (x crece)

## Reglas de cadenas (chains)

Una **cadena** es una secuencia contigua de celdas con el mismo color **y** la misma `dir`.

- La última celda en el sentido de la `dir` lleva `isEnd=true`. Las demás `isEnd=false`.
  - dir=Right (3) → `isEnd` en la celda más a la derecha.
  - dir=Down (1) → `isEnd` en la celda más abajo.
  - dir=Left (2) → `isEnd` en la celda más a la izquierda.
  - dir=Up (0) → `isEnd` en la celda más arriba.
- Dos cadenas adyacentes del mismo color+dir son cadenas **distintas** si cada una tiene su propio `isEnd`. Esto permite poner dos commits del mismo color uno al lado del otro.
- Si te equivocás con `isEnd`, `LevelData.NormalizeAllChunks()` lo recalcula al cargar.

## Solvencia: las direcciones tienen que hacer el nivel jugable

**El nivel debe ser pasable.** No basta con que el dibujo se vea lindo: las `dir` de los chunks definen el flujo de las cartas, y si dos chunks empujan cartas hacia la misma zona, chocan y el nivel se vuelve **imposible**.

Regla práctica:

- Antes de cerrar el diseño, simular mentalmente el flujo: cada chunk emite/entrega cartas en el sentido de su `dir`. ¿Hay dos chunks que terminan apuntando al mismo lugar o se cruzan en el mismo recorrido? Si sí, ajustar `dir`.
- **Variar las direcciones** entre chunks vecinos para que el nivel sea entretenido. Todos los chunks con la misma `dir` suele ser aburrido y, peor, suele colisionar.
- **Variar también la orientación** (no solo Right/Left, ni solo Up/Down). Un buen nivel mezcla chunks horizontales y verticales. Si un dibujo lo permite, partirlo en columnas en algunas zonas y en filas en otras — no romperlo todo en una sola orientación.
- Caso típico de error: dos chunks en la **misma columna** ambos con `dir=Down` (1), o en la **misma fila** ambos con `dir=Right` (3). Casi siempre se pisan.
- Solución típica: invertir la `dir` de uno de los dos chunks (uno hacia abajo, el otro hacia arriba; uno a la derecha, el otro a la izquierda).

Ejemplo concreto del proyecto — Level 1 ("Cara Triste", [level_0.json](level_0.json)):

- Chunk **rojo** en columna 0 (filas 0-3) con `dir=Down` y chunk **azul** en columna 0 (filas 6-11) con `dir=Down`: el azul empuja hacia abajo en la misma columna donde el rojo ya envió cartas → **impasable**.
- Fix: el azul debe ir con `dir=Up` (0), `isEnd` en (0,6). Así rojo baja hacia (0,3) y azul sube hacia (0,6) — no se cruzan.

Si dudás de la solvencia de un nivel que estás diseñando, marcá el riesgo en la respuesta y pedí confirmación antes de cerrar el JSON.

## Paletas

Hay **dos** fuentes de color que conviven:

1. **DefaultPalette** ([Assets/Data/DefaultPalette.asset](../../Data/DefaultPalette.asset)): paleta global, 10 colores fijos (Empty, Red, Blue, Green, Yellow, Purple, Orange, Cyan, White, Brown). Esta paleta **no se modifica** automáticamente cuando se generan niveles desde imágenes.
2. **`levelPalette` embebida en el JSON del nivel**: lista opcional dentro de `LevelData`. Usada para niveles generados desde imágenes (que traen colores fuera de la default). Vive solo en el JSON del nivel, no contamina la default.

El builder permite cambiar entre ambas con el botón **"Paleta: Default / Nivel"** (componente `PaletteToggleButton` en `ControlsRow`). Si el nivel no tiene paleta propia, el toggle queda deshabilitado.

**Importante**: cuando Claude genera un nivel desde una imagen, agrega los colores como `levelPalette` del nivel — **no** los suma a la DefaultPalette.

## Colores en uso (DefaultPalette / niveles a mano)

Mantener consistencia con los colores ya establecidos en niveles previos. Los floats exactos son los que produce el round-trip de `Color` en Unity — pegarlos tal cual para que dos celdas del mismo "color lógico" comparen como iguales:

| Color       | r                      | g                      | b                      |
|-------------|------------------------|------------------------|------------------------|
| Rojo        | 1.0                    | 0.0                    | 0.0                    |
| Verde       | 0.10000000149011612    | 0.800000011920929      | 0.20000000298023225    |
| Azul        | 0.20000000298023225    | 0.4000000059604645     | 1.0                    |
| Amarillo    | 1.0                    | 0.9200000166893005     | 0.01600000075995922    |
| Violeta     | 0.699999988079071      | 0.20000000298023225    | 0.8999999761581421     |
| Rojo oscuro | 0.5031446218490601     | 0.06487081199884415    | 0.06487081199884415    |
| Naranja     | 1.0                    | 0.5                    | 0.0                    |

Si introducís un color nuevo, **registralo acá con el float exacto que termina en el JSON** (Unity reescribe el Color con precisión float, no el valor que tipeás).

## BeltPresets

- Default en uso: `"Circle"`.
- Vacío (`""`) = cae al `defaultPreset` del componente.
- Los assets están en `Resources/BeltPresets/`. Para listar los disponibles, mirar esa carpeta.

## Workflow: niveles desde imágenes (pixel art) — TRADUCCIÓN ESTRICTA

**No interpretar la imagen. Traducir pixel-por-pixel.**

**Regla de tamaño actualizada (2026-05-19): el layout ya NO impone un cap de 11×11.** El board reescala dinámicamente, y las orders + la cinta se reposicionan en cadena (`LevelLayoutManager` → `BoardResizer` → `OrdersManager.Reposition` → `BeltRepositionerManager` → `CameraFitter`). La cámara se abre sola via los anchors `OrdersOuterEdge` / `BeltOuterEdge`. Niveles arbitrariamente grandes son posibles desde el layout.

**Sigue valiendo el cap por calidad visual**: para imágenes auto-procesadas, el cap default sigue siendo 11×11 (auto-downsample falla con detalle fino, ver 2026-05-19 abajo). Para imágenes con grilla dibujada explícita o hand-design, podés ir más grande sin problemas de layout — el límite es estético (cuántas celdas tiene sentido visualmente). Si una imagen tiene grid natural mayor, avisá al usuario y elegí entre: subir `MaxGrid`, pedir imagen más chica, o hand-design.

**Regla de paridad actualizada (2026-05-19): NO auto-fix.** El pipeline reporta qué colores quedan con cantidad impar de celdas — el usuario los ajusta a mano en el builder cuando hace Save&Exit y el validador se queja. Tampoco se borran celdas para "limpiar". Fidelidad primero.

### Caso A — Imagen con grilla dibujada explícita (script `_gridded.ps1`)

Es el formato **preferido**: PNG donde la imagen viene con líneas de grilla light-gray visibles, cada cuadradito es una celda lógica.

Pipeline:

1. **Detecta el block size** scanneando una fila de la imagen y midiendo el spacing entre las líneas de grilla light-gray. Sin ambigüedad.
2. **Detecta background** del primer cell de la grilla (típicamente blanco).
3. **Para cada cell de la grilla**: samplea el centro (lejos de las líneas) → si está cerca del bg, skip; sino, agrega al cluster de paleta.
4. **Cluster** de colores con threshold euclidean (default 800 squared, más estricto que en auto-downsample porque no hay anti-alias).
5. **Auto-label** por heurística (Negro, Rojo, Amarillo, Verde, Azul, etc).
6. **Bbox del dibujo**: cells no-bg → shift a (0,0). Si bbox > 11×11, **abortar con warning**.
7. **Chunking H/V mezclado** + alternancia de dirs.
8. **Post-pass anti-singleton**: para chunks de 1 cell, intentar robar la celda final de un chunk vecino del mismo color (≥3 cells) para formar un par 2-cell. Los que no se pueden fusionar quedan como singletons con `isEnd=true` + dir (válidos).
9. **Report**: paridad por color (impares para que ajuste a mano), singletons restantes (informativo).
10. **Emit JSON**: `levelPalette` embebida + bbox top-left (0,0) + indent 4 spaces.

### Caso B — Imagen pixel-art upscaleado sin grilla visible (script `_translate.ps1`)

Usar solo si la imagen es **pixel art puro** (sin anti-aliasing, sin texto, sin marcas). Detecta el block size por análisis de uniformidad de bloques candidatos. Más frágil que el Caso A — preferir imágenes con grilla.

**Cuándo NO usar pipeline auto en absoluto:**
- Imagen sin pixel art puro (foto, ilustración pintada, JPG con anti-alias fuerte).
- Imagen mayor a 11×11 cells lógicos → pedir versión más chica o hand-design simplificado.
- El usuario explícitamente quiere una versión simplificada → hand-design (ver workflow más abajo).

### Chunks de 1 sola celda — sí son válidos

**Actualización 2026-05-19:** Un chunk de 1 sola celda **SÍ funciona en el juego**, siempre y cuando en el JSON tenga `isEnd: true` y un `dir` asignado. El game engine lo reconoce como una "cadena de longitud 1" con dirección.

- **Bug histórico**: el pipeline emitía singletons con `isEnd: false` por culpa del `switch` de PowerShell que desempaqueta arrays de 1 elemento (`$ordered.Count` daba mal). Fix: usar `if/elseif` con `@(...)` explícito en lugar de `switch` para el ordenamiento.
- Cuando un cell forma un chunk de 1 cell, el emisor debe asegurarse de marcar `isEnd: true`. Si el pipeline tira `WARNING: singletons sin head`, hay bug — chequear que la emisión esté seteando `isEnd` correctamente para chunks de 1 cell.
- **Diseño**: no hace falta evitar singletons en el diseño. Si una imagen tiene un pixel rojo aislado, el JSON puede tener un cell rojo con `isEnd: true` + `dir` y funciona.

### Cuándo funciona el auto-downsample vs. cuándo hace falta hand-design

- **Auto-downsample sirve** si la imagen es **pixel art puro**: cada pixel del source es un color sólido (sin anti-aliasing), sin gridlines auxiliares, sin marca de agua, fondo plano. En ese caso el snap a paleta es 1:1 y el resultado a 14×14 conserva la silueta.
- **Auto-downsample falla** con: imágenes renderizadas con anti-aliasing, JPEGs con compresión, PNGs con gridlines o texto de fondo, sprites a alta resolución con muchos colores intermedios. El promediado mezcla los bordes con el outline y cae en colores intermedios que no representan nada.
- **En caso de duda, hand-designear** capturando la silueta esencial. Se construye con loops/`SetValue` directos sobre un `[int[,]]` 14×14 — cada celda asignada explícitamente a un índice de paleta. Más confiable que string templates (errores de conteo de chars al typear).

Cuando el usuario pasa una imagen pixel-art (ej. la casita), seguir este pipeline manualmente:

1. **Inspeccionar imagen**: leer dimensiones y colores únicos. Tope de grid: 14×14. Para imágenes complejas, pensar la silueta esencial (techo, paredes, ventanas, puerta, etc) y dibujarla a mano respetando los colores del original.
2. **Detectar background**: el color del fondo (cielo, transparente) **no genera celdas**. Si la imagen tiene alfa, alpha=0 = empty; si no, el color dominante del borde suele ser el fondo.
3. **Cuantizar a paleta del nivel**: agrupar variantes cercanas (anti-aliasing, JPEG) al color dominante de cada cluster. Producir una `levelPalette` con labels semánticos (ej: `"Tejado"`, `"PuertaAzul"`, `"Cesped"`). Típicamente 5–15 colores.
4. **Mapear pixeles → celdas**: para cada pixel no-background, generar `CellEntry` con `x,y` directos del pixel y el color cuantizado correspondiente.
5. **Particionar en chunks**: para cada color, agrupar celdas contiguas. Romper cada blob en runs **horizontales y verticales mezclados** (regla "variar orientación" — ver sección "Solvencia").
6. **Asignar `dir` con solvencia**: alternar Down/Up entre verticales vecinos y Right/Left entre horizontales vecinos. Marcar `isEnd=true` en la última celda según `dir`.
7. **Chequeo de paridad**: contar celdas por color cuantizado. Si alguno es **impar**, **reportar al usuario** la lista de colores impares y dejarlo decidir qué celda agregar/quitar (y dónde). No tocar la imagen automáticamente.
8. **Emitir JSON**: `level_N.json` con `levelPalette` embebida + `cells` + indent 4 espacios (`JsonUtility.ToJson(..., true)`). El nivel **no contamina** DefaultPalette.

### Ejemplo de `levelPalette` en JSON

```jsonc
"levelPalette": [
    { "label": "Tejado", "color": { "r": 0.72, "g": 0.28, "b": 0.20, "a": 1.0 } },
    { "label": "Pared",  "color": { "r": 1.0,  "g": 1.0,  "b": 1.0,  "a": 1.0 } },
    { "label": "Puerta", "color": { "r": 0.35, "g": 0.45, "b": 0.55, "a": 1.0 } }
]
```

El builder muestra estos colores cuando el usuario clickea el toggle a "Paleta: Nivel".

## Workflow para crear un nivel a mano

1. Elegir el índice `N` libre → archivo `level_N.json`.
2. Bocetar el dibujo en grid **empezando en (0,0)** (top-left bbox).
3. Decidir cómo dividirlo en cadenas: agrupar celdas contiguas que comparten color+dir.
4. Para cada cadena, marcar `isEnd=true` en la última celda según la `dir`.
5. **Verificar solvencia**: revisar que ningún par de chunks empuje cartas a la misma zona (ver sección "Solvencia"). Variar `dir` entre chunks vecinos.
6. Escribir el JSON con pretty-print 4 espacios (`JsonUtility.ToJson(obj, true)` lo hace automático desde el código).
7. `levelName` = `"Level {N+1} - {nombre temático}"` (ej: `level_3.json` → `"Level 4 - Corazon"`).

---

## Lecciones aprendidas

> Agregar acá patrones, gotchas y decisiones de diseño a medida que aparecen.

- **2026-05-18**: El JSON debe persistirse pretty-printed (no en una línea). Se cambió `JsonUtility.ToJson(levelData)` → `JsonUtility.ToJson(levelData, true)` en `LevelBuilderManager.cs:133` y `:329`.
- **2026-05-18**: El dibujo arranca en (0,0). Padding inicial = board más grande de lo necesario.
- **2026-05-18**: Las `dir` definen jugabilidad, no sólo estética. Dos chunks que empujan cartas a la misma zona = nivel impasable. Variar direcciones entre chunks vecinos. Caso testigo: rojo y azul en columna 0 con `dir=Down` ambos → fix: azul con `dir=Up`. Ver sección "Solvencia".
- **2026-05-18**: Variar también la **orientación**, no solo la dirección. Un nivel con solo chunks horizontales (todos Right/Left) queda monótono incluso si las direcciones alternan. Mezclar verticales y horizontales según lo permita el dibujo. Caso testigo: primera versión del corazón (level_3) tenía 8 chunks horizontales → rehecho como 8 verticales (parte superior, columnas) + 3 horizontales (taper inferior).
- **2026-05-18**: Niveles basados en imágenes usan **paleta por nivel** (campo `levelPalette` en el JSON), no DefaultPalette. Los colores de la imagen no se agregan a la default. El builder muestra ambas paletas y el usuario cambia con el botón "Paleta: Default / Nivel". `LevelValidator` también reconoce la paleta del nivel para nombrar colores con labels semánticos en errores.
- **2026-05-18**: **Máximo grid 14×14**. 34×34 satura el board (tapa orders/reserves) y al auto-downsamplear a 14×14 se pierde la silueta. Para imágenes complejas, hand-designear la versión 14×14 capturando la silueta esencial con los colores del original. Caso testigo: casita (level_4) — primera versión auto a 34×34 ocupaba toda la pantalla; segunda auto a 14×14 quedó abstracta; tercera hand-designed a 14×14 = silueta clara (techo triangular, paredes, ventanas, puerta, arbustos).
- **2026-05-18**: **Auto-downsample solo funciona con pixel art puro** (sin anti-alias, sin gridlines, sin marca de agua). Imágenes con JPG/PNG renderizado de alta resolución producen "barro" al compresar a 14×14. Default para imágenes generales: hand-design.
- **2026-05-18**: **Construir el map 14×14 con loops/`SetValue` directos** sobre un `[int[,]]`, no con string templates. Los string templates ("..133..." etc) son muy propensos a errores de conteo de chars al typear. Patrón: `$m = [int[,]]::new(14,14); $m.SetValue($v,$x,$y)` por celda o `for ($x=...) { $m.SetValue(...) }` por run.
- **2026-05-18**: **Single-cell chunks no muestran flecha** porque la única celda es `isEnd=true`. Diseñar features como pares (1×2 vertical o 2×1 horizontal). Outlines preferentemente **rectangulares** (filas completas y columnas completas) en vez de curvas diagonales. Caso testigo: primer Pikachu tenía ojos como 1 pixel suelto (4,5) y (9,5) → singletons. Fix: ojos como 1×2 vertical (4,5)-(4,6) y (9,5)-(9,6). Tambien rediseñé head de Pikachu y AmongUs con outline rectangular (fila completa arriba/abajo, columnas completas izquierda/derecha) para evitar "step corners" diagonales que generaban outline cells aisladas en esquinas.
- **2026-05-19**: **Traducción estricta pixel-por-pixel es la default para imágenes**. El usuario explicitó: "no quiero que trates de interpretar la imagen, traducila en pixeles para el json". El detector de block-size automático (script `_translate.ps1`) prueba grids 8..20 con uniformity check (corners de cada bloque dentro de threshold 50 RGB-Euclidean), pickea el grid con mayor score con preferencia por grid menor (block mayor). Paleta se deriva de los colores reales del source via clustering (threshold ~1500 squared). El grid del nivel = grid natural del source (típicamente 14..20). Singletons se preservan por fidelidad — el pipeline avisa pero no los borra. Caso testigo: la "pizza_2_2_16_16.jpg" que yo interpreté mal como "Pikachu" → con traducción estricta queda como pizza (5 colores: blanco, negro, marrón, amarillo, rojo) y silueta correcta. Lección: NUNCA renombrar interpretando el subject; usar el filename o pedir nombre al usuario.
- **2026-05-19**: **Max grid 11×11** (downgrade desde 14×14 / 20×20). Si una imagen tiene block-size natural mayor (sale grid >11), avisar al usuario y NO forzar downsample silencioso — pedir imagen más chica o hand-design. **Update 2026-05-19 tarde**: el cap 11×11 se mantiene por *calidad del auto-downsample*, NO por layout. El layout ya soporta niveles arbitrariamente grandes (ver entrada siguiente).
- **2026-05-19**: **Layout dinámico: board + orders + belt + cámara se reposicionan en cadena**. Nuevo `BeltRepositionerManager` ([Assets/Scripts/Managers/Layout/BeltRepositionerManager.cs](Assets/Scripts/Managers/Layout/BeltRepositionerManager.cs)) + `OrdersManager.Reposition` ahora se llama desde `LevelLayoutManager.Layout()`. Cadena: `BoardResizer` reescala el board → `OrdersManager` traslada las orders a `BoardTop + ordersZGap` y mueve el anchor `OrdersOuterEdge` al borde exterior del grupo → `BeltRepositioner` traslada el `ConveyorBelt` root como grupo rígido a `OrdersOuterEdge + beltZGap` (Points/Slots/Visuals son hijos y siguen) → `CameraFitter` encuadra incluyendo anchors `OrdersOuterEdge` y `BeltOuterEdge`. Resultado: niveles 15×20 o más entran sin solaparse con orders ni belt, sin tocar más nada. Anchors viven en `--- WORLD ---/BoardAnchors/`. Tunables en `LevelLayoutManager`: `boardGridMargin`, `ordersZGap`, `beltZGap`, `cameraPadding`.
- **2026-05-19**: **Sin auto-parity-fix**. El pipeline NO borra cells para emparejar colores. Reporta cuáles colores quedan impares (`NOTE: colores IMPARES`); el usuario los ajusta a mano cuando hace Save&Exit en el builder y el validador del juego se queja. Fidelidad primero, paridad después.
- **2026-05-19**: **Imágenes con grilla dibujada explícita = formato preferido**. PNG donde el pixel-art viene con líneas light-gray entre celdas. El script `_gridded.ps1` detecta el spacing entre líneas (sin ambigüedad), samplea el centro de cada cuadradito, y emite el JSON 1:1. Mucho más confiable que `_translate.ps1` (que adivina block-size). Pedirle al usuario imágenes en este formato cuando sea posible.
- **2026-05-19**: **Bug crítico: el `switch` de PowerShell desempaqueta arrays de 1 elemento**. Resultado: `$ordered = switch ($c.dir) { 0 { @($cells | Sort-Object ...) } ... }` cuando `$cells` tiene 1 elemento → `$ordered` queda como hashtable suelto (no array), `$ordered.Count` da el número de keys del hashtable (2), y `isEnd = ($i -eq $nc - 1)` para single-cells da `false`. Fix: usar `if/elseif` en lugar de `switch`. Síntoma: cells de 1-cell chunks emitidas con `isEnd: false` → invisibles en el level builder.
- **2026-05-19**: **Single-cell chunks SÍ son válidos en el juego** si tienen `isEnd: true` + `dir`. El motor los reconoce como cadenas de longitud 1 con dirección. Pero hay que asegurarse que la emisión los marque correctamente (ver bug del switch arriba). Para minimizar singletons, el pipeline tiene un post-pass que intenta robar la cell-fin de chunks vecinos del mismo color (≥3 cells) y formar un par 2-cell. Los que no se pueden fusionar quedan como singletons válidos.
- **2026-05-19**: **No interpretar el subject de la imagen**. Yo etiqueté una imagen como "Pikachu" cuando era una pizza, y como "casita" cuando era hand-design diferente al source. Reglas: usar filename del usuario, o pedir nombre, o usar nombre genérico ("Imagen 1"). Nunca asumir qué representa el dibujo.
- **2026-05-19**: **`_gridded.ps1` ahora tiene fallback por uniformidad** cuando no detecta gridlines visibles. Si la imagen es pixel-art puro sin líneas dibujadas (típico de sprites pequeños), itera grids 8..maxGrid y elige el que da mayor uniformidad de bloques. Resultado típicamente OK pero menos confiable que cuando hay grilla explícita — preferí imágenes con grilla.
- **2026-05-19**: **`ClusterThresh` ajustable por imagen**. JPGs con compresión y noise generan muchas variantes de color cercanas (ej. imagen3 produjo 10 colores distintos: Marron, Marron2, Gris, Col6...). Pasar `-ClusterThresh 2500` o más alto (default 800) para mergear variantes. Caso testigo: imagen3 con ClusterThresh=800 → 10 colores y 13 singletons; con ClusterThresh=3500 → 5 colores y 5 singletons. Para PNGs limpios (mushroom, imagen1), default funciona.
- **2026-05-19**: **`MaxGrid` puede ajustarse por imagen como excepción** al cap 11×11 default. Sintaxis: `-MaxGrid 12`. Usar solo cuando el usuario explícitamente lo autoriza para una imagen específica (ej. unicornio 12×12). No relajar globalmente — la regla 11×11 sigue siendo el default.
- **2026-05-19**: **Niveles legacy con el bug del switch pueden quedar rotos en JSON** — cells con `isEnd=false` que no tienen "next cell" del mismo color+dir en la chain (huérfanas). Caso testigo: level_4 (Casita) tenía 10 cells rotas en las esquinas del techo (singletons emitidos con `isEnd=false` antes del fix). Script de detección: para cada cell con `isEnd=false`, computar la posición next según dir; si no existe cell del mismo color+dir ahí, es huérfana y debe pasar a `isEnd=true`. **Fix preservando formato JSON**: NO usar `ConvertTo-Json` (reformatea 2-space). Usar **regex sobre el raw string** para reemplazar `"isEnd":\s*false` por `"isEnd": true` solo en los blocks de las cells identificadas. Pattern: match el block completo `"x":N,"y":N,"r":..,"g":..,"b":..,"dir":N,"isEnd":false`, modificar solo el isEnd.
- **2026-05-19**: **Auto-label de colores ahora por HSV — siempre nombre válido, nunca "ColN"**. El heurístico anterior tenía gaps (rosa, cyan, violeta caían en `Col3`/`Col5` etc). Nuevo algoritmo: convierte RGB→HSV, clasifica por hue (Rojo, Naranja, Amarillo, Lima, Verde, Cyan, Azul, Violeta, Magenta, Rosa) con modifiers Oscuro/Pastel según V/S. Grises por V (Negro <0.12, GrisOscuro, Gris, GrisClaro, Blanco >0.88). Duplicados se sufijan (Rojo2, Rojo3...). Implementado en `_gridded.ps1` y en script standalone `_relabel.ps1` que re-labelea niveles existentes sin tocar las cells. **Bonus**: el relabel también agrega al `levelPalette` los colores que aparecen en cells pero faltaban como entry (caso testigo: unicornio tenía 34 cells blancos sin Blanco en la paleta — quedaron en celdas pero no se podían pintar desde "Paleta: Nivel" en el builder).
- **2026-05-19**: **Fix de paridad NO debe ser celda "random" — pensar el impacto visual**. Caso testigo unicornio: pink era 15 IMPAR, quité (5,3) automáticamente y dejó un hueco visible en la silueta del cuerpo. El usuario corrigió: "no saques una celda random, sino que sea algo a conciencia. Si necesitás cambiar levemente el shape, hacelo, o al menos preguntame". Reglas para elegir cell a remover: 1) priorizar cells **en el borde de la silueta** (con vecinos sky). 2) Si todas las cells del color son internas (sin vecinos sky), elegir la PUNTA/ÚLTIMA de la cadena en su dirección — el cambio queda en el borde exterior. 3) **Siempre presentar opciones al usuario** antes de fixear, no auto-decidir. Caso testigo: melena del unicornio enclavada (sky=0 todas), elegí (9,10) = punta inferior de la melena delantera (cambio en el borde, no hueco interno).
