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
