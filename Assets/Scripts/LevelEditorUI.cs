using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class LevelEditorUI : MonoBehaviour
{
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelBuilderManager manager;

    // ── Viewport ─────────────────────────────────────────────────────────────
    // Cada celda es exactamente CELL_PX × CELL_PX → siempre cuadradas
    private const int CELL_PX    = 80;
    private const int CELL_GAP   = 2;
    private const int VIEWPORT_W = 17;
    private const int VIEWPORT_H = 12;

    // Coordenada (x,y) de la celda de la esquina inferior-izquierda del viewport
    private int viewOffsetX;
    private int viewOffsetY;

    // ── Estado ───────────────────────────────────────────────────────────────
    private Color selectedColor = Color.red;
    private Transform gridContainer;
    private Text levelNameText;
    private Text coordsText;
    private GameObject builderCanvas;

    // Repetición al mantener tecla
    private float panCooldown;
    private const float PanInitialDelay  = 0.30f;
    private const float PanRepeatInterval = 0.07f;

    // Pan con rueda del mouse (middle button drag)
    private Vector2 mousePanAccum;

    // Pintado por arrastre: 0 = nada, 1 = pintar, 2 = borrar
    private int dragOp;

    // ── Paleta ───────────────────────────────────────────────────────────────
    private static readonly Color[] Palette =
    {
        Color.clear,                    // Empty (borrar)
        Color.red,
        new Color(0.2f, 0.4f, 1f),     // Blue
        new Color(0.1f, 0.8f, 0.2f),   // Green
        Color.yellow,
        new Color(0.7f, 0.2f, 0.9f),   // Purple
        new Color(1f,   0.5f, 0f),      // Orange
        new Color(0f,   0.9f, 0.9f),    // Cyan
        Color.white
    };
    private static readonly string[] PaletteLabels =
        { "Empty", "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Cyan", "White" };

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Origen (0,0) en la esquina superior-izquierda del viewport
        viewOffsetX = 0;
        viewOffsetY = 0;
        BuildUI();
    }

    void Update()
    {
        if (builderCanvas == null || !builderCanvas.activeSelf) return;

        var kb = Keyboard.current;
        int dx = 0, dy = 0;
        if (kb.leftArrowKey.isPressed  || kb.aKey.isPressed)  dx = -1;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed)  dx =  1;
        if (kb.upArrowKey.isPressed    || kb.wKey.isPressed)  dy = -1;
        if (kb.downArrowKey.isPressed  || kb.sKey.isPressed)  dy =  1;

        bool anyJustPressed =
            kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame ||
            kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
            kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame ||
            kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame;

        if (anyJustPressed && (dx != 0 || dy != 0))
        {
            Pan(dx, dy);
            panCooldown = PanInitialDelay;
        }
        else if (dx != 0 || dy != 0)
        {
            panCooldown -= Time.deltaTime;
            if (panCooldown <= 0f)
            {
                panCooldown = PanRepeatInterval;
                Pan(dx, dy);
            }
        }

        HandleMousePan();
        HandleDragRelease();
    }

    void HandleMousePan()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!mouse.middleButton.isPressed)
        {
            mousePanAccum = Vector2.zero;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        float cellSize = CELL_PX + CELL_GAP;
        // Arrastrar a la derecha mueve el contenido a la derecha → viewOffsetX disminuye
        mousePanAccum.x -= delta.x / cellSize;
        mousePanAccum.y -= delta.y / cellSize;

        int panDx = 0, panDy = 0;
        while (mousePanAccum.x >=  1f) { panDx++; mousePanAccum.x -= 1f; }
        while (mousePanAccum.x <= -1f) { panDx--; mousePanAccum.x += 1f; }
        while (mousePanAccum.y >=  1f) { panDy++; mousePanAccum.y -= 1f; }
        while (mousePanAccum.y <= -1f) { panDy--; mousePanAccum.y += 1f; }
        if (panDx != 0 || panDy != 0) Pan(panDx, panDy);
    }

    void HandleDragRelease()
    {
        var mouse = Mouse.current;
        if (mouse == null) { dragOp = 0; return; }
        if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
            dragOp = 0;
    }

    // Llamado por las celdas al recibir un click
    public void OnCellPointerDown(int gx, int gy, bool isRight)
    {
        dragOp = isRight ? 2 : 1;
        ApplyPaint(gx, gy);
    }

    // Llamado por las celdas al pasar el cursor con un botón presionado
    public void OnCellPointerEnter(int gx, int gy)
    {
        if (dragOp != 0) ApplyPaint(gx, gy);
    }

    void ApplyPaint(int gx, int gy)
    {
        if (levelData == null) return;
        Color c = dragOp == 2 ? Color.clear : selectedColor;
        levelData.SetCell(gx, gy, c);
        RefreshGrid();
    }

    void Pan(int dx, int dy)
    {
        viewOffsetX += dx;
        viewOffsetY += dy;
        if (viewOffsetX < 0) viewOffsetX = 0;
        if (viewOffsetY < 0) viewOffsetY = 0;
        UpdateCoords();
        RefreshGrid();
    }

    // ── API pública ──────────────────────────────────────────────────────────

    public void Show()  { builderCanvas?.SetActive(true);  Refresh(); }
    public void Hide()  { builderCanvas?.SetActive(false); }

    public void Refresh()
    {
        if (levelNameText != null && levelData != null)
            levelNameText.text = levelData.levelName;
        UpdateCoords();
        RefreshGrid();
    }

    void UpdateCoords()
    {
        if (coordsText == null) return;
        int x0 = viewOffsetX, y0 = viewOffsetY;
        int x1 = viewOffsetX + VIEWPORT_W - 1;
        int y1 = viewOffsetY + VIEWPORT_H - 1;
        coordsText.text = $"TL ({x0},{y0})\nBR ({x1},{y1})";
    }

    // ── Construcción de la UI ────────────────────────────────────────────────

    void BuildUI()
    {
        // EventSystem compatible con New Input System
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // Canvas
        builderCanvas = new GameObject("LevelBuilderCanvas");
        var canvas = builderCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = builderCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        builderCanvas.AddComponent<GraphicRaycaster>();

        // Fondo oscuro
        var bg = MakePanel(builderCanvas.transform, "BG", new Color(0, 0, 0, 0.90f));
        Stretch(bg);

        // Panel contenedor principal
        var main = MakePanel(bg, "Main", new Color(0.10f, 0.10f, 0.12f));
        Anchors(main, .01f, .01f, .99f, .99f);

        // ── SIDEBAR IZQUIERDO ─────────────────────────────────────────────
        var side = MakePanel(main, "Sidebar", new Color(0.07f, 0.07f, 0.09f));
        Anchors(side, 0f, 0f, .185f, 1f);

        MakeLabel(side, "LEVEL\nBUILDER", 22, Color.white, 0, .91f, 1, 1f);

        levelNameText = MakeLabel(side, "", 16, Color.cyan, .04f, .84f, .96f, .91f);

        MakeBtn(side, "◀ Prev", 0, .77f, .47f, .83f,
            new Color(.15f, .25f, .45f), () => manager?.PrevLevel());
        MakeBtn(side, "Next ▶", .53f, .77f, 1f, .83f,
            new Color(.15f, .25f, .45f), () => manager?.NextLevel());

        MakeBtn(side, "💾 Save & Exit\n[ B ]", .02f, .69f, .98f, .77f,
            new Color(.07f, .43f, .16f), () => manager?.ToggleBuilderMode());

        // Paleta de colores (3 cols × 3 filas)
        MakeLabel(side, "─── COLOR ───", 11, Color.gray, 0, .64f, 1, .68f);
        BuildPalette(side);

        // Instrucciones de navegación
        MakeLabel(side, "NAVEGAR:\nWASD  /  ← ↑ → ↓", 11, new Color(.55f, .55f, .55f),
            0, .28f, 1, .36f);

        coordsText = MakeLabel(side, "", 10, new Color(.4f, .8f, .4f), 0, .22f, 1, .28f);

        // ── GRID PANEL (derecha) ──────────────────────────────────────────
        // Usamos GridLayoutGroup con cellSize fija para garantizar celdas cuadradas
        float totalW = VIEWPORT_W * (CELL_PX + CELL_GAP) + CELL_GAP;
        float totalH = VIEWPORT_H * (CELL_PX + CELL_GAP) + CELL_GAP;

        var gridRT = MakePanel(main, "GridPanel", new Color(.06f, .06f, .08f));
        // Anclamos al centro del área derecha
        gridRT.anchorMin = gridRT.anchorMax = new Vector2(.5f, .5f);
        gridRT.pivot     = new Vector2(.5f, .5f);
        // Desplazar hacia la derecha para no solapar el sidebar (~19% de 1920 ≈ 365px, más margen)
        gridRT.anchoredPosition = new Vector2(210f, 0f);
        gridRT.sizeDelta        = new Vector2(totalW, totalH);

        var glg = gridRT.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize       = new Vector2(CELL_PX, CELL_PX);
        glg.spacing        = new Vector2(CELL_GAP, CELL_GAP);
        glg.padding        = new RectOffset(CELL_GAP, CELL_GAP, CELL_GAP, CELL_GAP);
        glg.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis      = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = VIEWPORT_W;

        gridContainer = gridRT;

        Refresh();
    }

    void BuildPalette(Transform parent)
    {
        const int cols = 3;
        float pw = 1f / cols;
        const float cellH = 0.088f;
        const float startY = 0.625f;

        for (int i = 0; i < Palette.Length; i++)
        {
            int idx  = i;
            int row  = i / cols;
            int col  = i % cols;
            float yTop = startY - row * (cellH + 0.005f);
            float yBot = yTop - cellH;
            Color disp = Palette[i].a < 0.01f
                ? new Color(.22f, .22f, .22f)
                : new Color(Palette[i].r, Palette[i].g, Palette[i].b);

            MakeBtn(parent, PaletteLabels[i],
                pw * col + .01f, yBot,
                pw * (col + 1) - .01f, yTop,
                disp, () => selectedColor = Palette[idx]);
        }
    }

    void RefreshGrid()
    {
        if (gridContainer == null || levelData == null) return;

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        for (int row = 0; row < VIEWPORT_H; row++)
        for (int col = 0; col < VIEWPORT_W; col++)
        {
            // Fila 0 en pantalla = Y = 0 (origen arriba-izquierda)
            int gx = viewOffsetX + col;
            int gy = viewOffsetY + row;

            Color cellColor;
            bool painted = levelData.TryGetColor(gx, gy, out cellColor);
            Color disp   = painted ? cellColor : new Color(.14f, .14f, .16f);

            GridCell(disp, gx, gy);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void GridCell(Color bg, int gx, int gy)
    {
        var go  = new GameObject("cell");
        go.transform.SetParent(gridContainer, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var h = go.AddComponent<GridCellHandler>();
        h.ui = this; h.gx = gx; h.gy = gy;

        // Etiqueta de coordenadas en la esquina inferior-izquierda
        var lbl = new GameObject("coord");
        lbl.transform.SetParent(go.transform, false);
        var rt = lbl.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4, 2); rt.offsetMax = new Vector2(-2, -2);
        var t = lbl.AddComponent<Text>();
        t.text = $"{gx},{gy}";
        t.fontSize = 11;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.LowerLeft;
        // Color de texto que contraste con el fondo
        float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        t.color = new Color(lum > 0.5f ? 0f : 1f, lum > 0.5f ? 0f : 1f, lum > 0.5f ? 0f : 1f, 0.55f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;
    }

    RectTransform MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    Text MakeLabel(Transform parent, string text, int size, Color color,
                   float x0, float y0, float x1, float y1)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        Anchors(go.AddComponent<RectTransform>(), x0, y0, x1, y1);
        var t = go.AddComponent<Text>();
        t.text = text; t.fontSize = size; t.color = color;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    void MakeBtn(Transform parent, string label,
                 float x0, float y0, float x1, float y1,
                 Color bg, System.Action onClick)
    {
        var go = new GameObject(string.IsNullOrEmpty(label) ? "btn" : label);
        go.transform.SetParent(parent, false);
        Anchors(go.AddComponent<RectTransform>(), x0, y0, x1, y1);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb  = btn.colors;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.30f);
        cb.pressedColor     = Color.Lerp(bg, Color.black, 0.35f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        if (!string.IsNullOrEmpty(label))
        {
            var tgo = new GameObject("T");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<Text>();
            t.text = label; t.fontSize = 11; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    void Anchors(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

public class GridCellHandler : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    public LevelEditorUI ui;
    public int gx, gy;

    public void OnPointerDown(PointerEventData e)
    {
        if (ui == null) return;
        if (e.button == PointerEventData.InputButton.Right)
            ui.OnCellPointerDown(gx, gy, true);
        else if (e.button == PointerEventData.InputButton.Left)
            ui.OnCellPointerDown(gx, gy, false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (ui != null) ui.OnCellPointerEnter(gx, gy);
    }
}
