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

**No interpretar la imagen. Traducir pixel-por-pixel.** El usuario pasa pixel-art upscaleado (ej. 1200×1200 con cada "pixel lógico" como un bloque NxN). El pipeline:

1. **Detecta el block size natural** de la imagen analizando uniformidad de bloques candidatos (grids 8..20). Pick = grid con mayor uniformity con preferencia por grid chico (block grande).
2. **Detecta background** del corner sample(s). Celdas con color cercano al bg se skipean.
3. **Bbox del contenido**: snap a múltiplos del block size.
4. **Sample center color** de cada pixel lógico → cluster en paleta del nivel (threshold euclidiana ajustable, default ~1500 squared).
5. **Auto-label** colores (Rojo, Negro, Blanco, Verde, etc) por heurística HSV/RGB.
6. **Auto-fix parity** removiendo celdas con menor `sameN` de colores impares.
7. **Chunkear** con orientaciones mezcladas + alternancia de dirs.
8. **Emit JSON** con `levelPalette` embebida y bbox top-left en (0,0).

El tamaño de la grilla **lo define la imagen** (típicamente 14×14 a 20×20). La regla anterior de "max 14×14 obligatorio" se relaja a "max 20×20" en el detector — si la imagen tiene más fidelidad, se respeta hasta ese tope. Para imágenes con block size natural mayor (más de 20 lógicos), la salida cae a 20 con menor fidelidad.

**Cuándo NO usar el pipeline auto:**
- Imagen sin pixel art puro (foto, ilustración pintada, etc.).
- Block size detectado da uniformidad <85% → la imagen tiene anti-alias fuerte. Subir threshold o pre-procesar (posterize en GIMP).
- El usuario explícitamente quiere una versión simplificada → hand-design.

### Evitar chunks de 1 sola celda

**Un chunk de 1 sola celda se renderiza sin flecha** porque la única celda es `isEnd=true` (destino) y no hay celda "anterior" donde dibujar la dirección. Visualmente queda como una celda muda.

Regla: **toda celda de un color X debe tener al menos 1 vecino 4-conectado del mismo color X** que no esté ya asignado a otro chunk al momento de procesarla. En la práctica eso significa:

- Diseñar features (ojos, narices, manchas) como pares de celdas (1×2 vertical o 2×1 horizontal) en lugar de pixeles aislados.
- Outlines mejor **rectangulares** que en curva diagonal: una curva tipo step (un pixel al lado del otro en diagonal) genera celdas aisladas porque no son 4-conectadas.
- Si un outline tiene que cambiar de columna entre dos filas, asegurarse que el pixel "corner" esté conectado vertical u horizontalmente con un pixel del mismo color (no solo diagonal).

El pipeline detecta y avisa con `WARNING: N single-cell chunks (sin flecha)`. Si el output trae singletons, rediseñar.

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
