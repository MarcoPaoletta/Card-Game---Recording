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

## Colores en uso

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

## Workflow para crear un nivel a mano

1. Elegir el índice `N` libre → archivo `level_N.json`.
2. Bocetar el dibujo en grid **empezando en (0,0)** (top-left bbox).
3. Decidir cómo dividirlo en cadenas: agrupar celdas contiguas que comparten color+dir.
4. Para cada cadena, marcar `isEnd=true` en la última celda según la `dir`.
5. Escribir el JSON con pretty-print 4 espacios (`JsonUtility.ToJson(obj, true)` lo hace automático desde el código).
6. `levelName` = `"Level {N+1} - {nombre temático}"` (ej: `level_3.json` → `"Level 4 - Corazon"`).

---

## Lecciones aprendidas

> Agregar acá patrones, gotchas y decisiones de diseño a medida que aparecen.

- **2026-05-18**: El JSON debe persistirse pretty-printed (no en una línea). Se cambió `JsonUtility.ToJson(levelData)` → `JsonUtility.ToJson(levelData, true)` en `LevelBuilderManager.cs:133` y `:329`.
- **2026-05-18**: El dibujo arranca en (0,0). Padding inicial = board más grande de lo necesario.
