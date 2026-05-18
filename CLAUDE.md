# Project notes for Claude

## Unity Editor — usar SIEMPRE el MCP

Este proyecto tiene conectado el MCP **`unityMCP`**. Es la única forma autorizada de crear o modificar contenido de escena.

**Regla obligatoria:** cualquier creación o modificación de **GameObjects, componentes, UI, prefabs, escenas, materiales, animaciones, assets** se hace **vía `unityMCP`**, materializándolo en la escena del editor. **Nunca** crear GameObjects ni UI por código en runtime (ej: `new GameObject(...)`, `AddComponent<...>()` en `Start/Awake` para armar la jerarquía, `Instantiate` para construir la escena base, etc.). Si el código necesita referenciar algo, ese algo debe existir antes en la escena como GameObject creado vía MCP y asignado por el inspector.

**Si el MCP no está conectado o falla**, parar y avisarme. No caer silenciosamente a edición manual de archivos `.unity`/`.prefab`/`.asset` ni a crear las cosas por código.

**Excepción explícita:** scripts `.cs`, archivos `.json` (incluidos los niveles), `.md`, y otros archivos de texto plano se editan con las tools normales (`Edit`/`Write`). El MCP es para el editor, no para reemplazar la edición de código.

La skill `unity-mcp-skill` documenta los patrones y schemas del `unityMCP` — úsala cuando corresponda.

## Mecánicas complejas — fraccionar en varios scripts

**Regla:** una mecánica compleja **no** vive en un solo script gigante. Se descompone en piezas con responsabilidad única y se organizan en una **subcarpeta propia** dentro de `Assets/Scripts/<Capa>/<NombreSistema>/`.

Patrón a seguir: un script **orquestador** corto + N scripts **especializados** (cada uno hace una sola cosa). Capas existentes en el proyecto: `Data/`, `Gameplay/`, `Managers/`, `Tweens/`, `UI/`, `Utils/`.

### Ejemplos reales del proyecto

**Sistema de Layout** ([Assets/Scripts/Managers/Layout/](Assets/Scripts/Managers/Layout/)):
- [LevelLayoutManager.cs](Assets/Scripts/Managers/Layout/LevelLayoutManager.cs) — orquestador. Solo coordina la secuencia.
- [BoardResizerManager.cs](Assets/Scripts/Managers/Layout/BoardResizerManager.cs) — escala el board para encajar las celdas.
- [CameraFitterManager.cs](Assets/Scripts/Managers/Layout/CameraFitterManager.cs) — encuadra la cámara.

Cada manager hace una sola cosa. El orquestador no sabe *cómo* se reescala el board, solo le dice "reescalá".

**Sistema de Belt** ([Assets/Scripts/Gameplay/Belt/](Assets/Scripts/Gameplay/Belt/)):
- [BeltPath.cs](Assets/Scripts/Gameplay/Belt/BeltPath.cs) — lógica del recorrido.
- [BeltVisuals.cs](Assets/Scripts/Gameplay/Belt/BeltVisuals.cs) — presentación visual.
- [BeltContainers.cs](Assets/Scripts/Gameplay/Belt/BeltContainers.cs) — estructuras de datos.
- [BeltPresetIO.cs](Assets/Scripts/Gameplay/Belt/BeltPresetIO.cs) — serialización.

Misma idea: lógica, visuales, datos y I/O separados, todo agrupado bajo `Belt/`.

**Sistema de LevelBuilder** ([Assets/Scripts/Managers/LevelBuilder/](Assets/Scripts/Managers/LevelBuilder/)):
- `LevelBuilderManager.cs` orquesta · `LevelStore.cs` persiste · `LevelValidator.cs` valida.

### Cuándo dividir

- Si un script supera ~300 líneas o mezcla 2+ responsabilidades claramente distintas (lógica + visuales, datos + persistencia, input + estado, etc.), **se parte**.
- Antes de empezar a escribir una mecánica nueva, proponer la descomposición: qué scripts, qué hace cada uno, en qué subcarpeta van.

## Construcción de niveles

Antes de crear, modificar o explicar un nivel, **leé** [`Assets/Resources/Levels/LEVELS_GUIDE.md`](Assets/Resources/Levels/LEVELS_GUIDE.md). Ese archivo es la fuente de verdad para:

- Formato y schema de `level_N.json`.
- Convenciones (origen en (0,0), direcciones, reglas de cadenas/`isEnd`).
- Colores en uso (con sus floats exactos).
- Workflow para crear un nivel a mano.

**Regla persistente:** cada vez que aprendas algo nuevo sobre construcción de niveles — un patrón, un gotcha, una decisión de diseño, un color nuevo, una corrección a una convención anterior — **agregalo a `LEVELS_GUIDE.md`** (en la sección "Lecciones aprendidas" o en la sección correspondiente). No lo dejes solo en memoria de sesión: el guide debe acumular el conocimiento del proyecto.
