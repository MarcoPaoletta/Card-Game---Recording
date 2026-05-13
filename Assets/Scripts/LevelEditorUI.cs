using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelEditorUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelBuilderManager manager;
    [SerializeField] private ColorPalette palette;

    [Header("Scene refs")]
    [SerializeField] private GameObject builderCanvas;
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private TMP_Text coordsText;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private RectTransform paletteContainer;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button saveExitButton;

    [Header("Prefabs")]
    [SerializeField] private PaletteButton paletteButtonPrefab;
    [SerializeField] private GridCellView gridCellPrefab;

    [Header("Viewport")]
    [SerializeField] private int viewportW = 17;
    [SerializeField] private int viewportH = 12;

    private int viewOffsetX;
    private int viewOffsetY;

    private Color selectedColor = Color.red;

    // Repetición al mantener tecla
    private float panCooldown;
    private const float PanInitialDelay  = 0.30f;
    private const float PanRepeatInterval = 0.07f;

    // Pan con rueda del mouse (middle button drag)
    private Vector2 mousePanAccum;

    // Pintado por arrastre: 0 = nada, 1 = pintar, 2 = borrar
    private int dragOp;

    void Awake()
    {
        viewOffsetX = 0;
        viewOffsetY = 0;

        if (prevButton      != null) prevButton.onClick.AddListener(()      => manager?.PrevLevel());
        if (nextButton      != null) nextButton.onClick.AddListener(()      => manager?.NextLevel());
        if (saveExitButton  != null) saveExitButton.onClick.AddListener(()  => manager?.ToggleBuilderMode());

        BuildPalette();
    }

    void Update()
    {
        if (builderCanvas == null || !builderCanvas.activeSelf) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        int dx = 0, dy = 0;
        if (kb.leftArrowKey.isPressed  || kb.aKey.isPressed)  dx = -1;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed)  dx =  1;
        if (kb.upArrowKey.isPressed    || kb.wKey.isPressed)  dy =  1;
        if (kb.downArrowKey.isPressed  || kb.sKey.isPressed)  dy = -1;

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

        // Convertir delta de pantalla a celdas usando el tamaño real del GridLayoutGroup
        float cellSize = 80f;
        var glg = gridContainer != null ? gridContainer.GetComponent<GridLayoutGroup>() : null;
        if (glg != null) cellSize = glg.cellSize.x + glg.spacing.x;

        Vector2 delta = mouse.delta.ReadValue();
        mousePanAccum.x -= delta.x / cellSize;
        mousePanAccum.y += delta.y / cellSize;

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

    public void OnCellPointerDown(int gx, int gy, bool isRight)
    {
        dragOp = isRight ? 2 : 1;
        ApplyPaint(gx, gy);
    }

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
        int x1 = viewOffsetX + viewportW - 1;
        int y1 = viewOffsetY + viewportH - 1;
        coordsText.text = $"TL ({viewOffsetX},{viewOffsetY})\nBR ({x1},{y1})";
    }

    void BuildPalette()
    {
        if (paletteContainer == null || paletteButtonPrefab == null || palette == null || palette.entries == null)
            return;

        for (int i = paletteContainer.childCount - 1; i >= 0; i--)
            Destroy(paletteContainer.GetChild(i).gameObject);

        foreach (var entry in palette.entries)
        {
            var btn = Instantiate(paletteButtonPrefab, paletteContainer);
            btn.Setup(entry, c => selectedColor = c);
        }
    }

    void RefreshGrid()
    {
        if (gridContainer == null || gridCellPrefab == null || levelData == null) return;

        for (int i = gridContainer.childCount - 1; i >= 0; i--)
            Destroy(gridContainer.GetChild(i).gameObject);

        for (int row = 0; row < viewportH; row++)
        for (int col = 0; col < viewportW; col++)
        {
            int gx = viewOffsetX + col;
            int gy = viewOffsetY + row;

            Color cellColor;
            bool painted = levelData.TryGetColor(gx, gy, out cellColor);
            Color disp   = painted ? cellColor : new Color(.14f, .14f, .16f);

            var cell = Instantiate(gridCellPrefab, gridContainer);
            cell.Setup(this, gx, gy, disp);
        }
    }
}
