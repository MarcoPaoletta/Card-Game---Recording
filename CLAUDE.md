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

### Auto-check obligatorio del guide (al final de cada respuesta sobre niveles)

**Si en una respuesta toqué cualquiera de estos archivos:**
- `Assets/Resources/Levels/level_*.json`
- `Assets/_LevelSources/*` (imágenes fuente, scripts de pipeline)
- Cualquier `.cs` relacionado a `LevelData`, `LevelBuilder`, `LevelStore`, `LevelValidator`, paleta, chunks
- Cualquier `.md` que documente niveles

**Antes de cerrar la respuesta DEBO**:

1. Preguntarme: ¿hubo alguna **regla nueva**, **bug encontrado**, **decisión que contradice lo que dice el guide hoy**, **color nuevo**, **workflow que funcionó/no funcionó con un tipo de imagen**, o **el usuario me corrigió una asunción**?

2. Si la respuesta es **sí**, actualizar `LEVELS_GUIDE.md` con `Edit` **en ESTE MISMO turno** (no en el próximo, no "después"). Si introduje un cambio que contradice lo viejo, **CORREGIR** lo viejo en el guide, no solo apendear al final.

3. Terminar la respuesta con una línea de cierre:

   > **Guide check**: actualizado — [resumen 1-línea del cambio] · *o* — sin cambios (regla X ya documentada)

**Eventos que SIEMPRE requieren update del guide (no negociable)**:

- Bug nuevo del pipeline (Unity, PowerShell, JsonUtility, etc).
- Cambio en una regla numérica (max grid, paridad, etc).
- Cambio de comportamiento del motor del juego (qué hace válido/inválido un chunk, una celda, etc).
- Descubrimiento sobre un tipo de imagen (qué funciona, qué no).
- Convención de naming (auto-label de colores, nombre de niveles).
- El usuario me corrigió ("eso no es así") → la corrección va al guide.

**En caso de duda, actualizá**. Es más barato sobre-documentar que sub-documentar — el guide es la única manera de que la próxima sesión arranque con el conocimiento acumulado.

**Fallaste 5+ veces en esta regla durante una sola sesión** (mayo 2026): cada nueva lección requirió que el usuario te lo recordara. La regla está acá porque tu disciplina sola no alcanza. **No la salteés.**
