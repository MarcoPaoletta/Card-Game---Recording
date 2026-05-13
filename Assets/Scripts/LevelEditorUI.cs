using System.Collections.Generic;
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

    // ── Estado de drag para line-paint ────────────────────────────────────
    // dragMode: 0 = nada, 1 = pintar (left), 2 = borrar (right)
    private int dragMode;
    private Vector2Int dragStart;
    private Vector2Int dragCurrent;
    private List<Vector2Int> previewCells = new List<Vector2Int>();
    private bool wasDragging;

    void Awake()
    {
        viewOffsetX = 0;
        viewOffsetY = 0;

        if (prevButton     != null) prevButton.onClick.AddListener(()     => manager?.PrevLevel());
        if (nextButton     != null) nextButton.onClick.AddListener(()     => manager?.NextLevel());
        if (saveExitButton != null) saveExitButton.onClick.AddListener(() => manager?.ToggleBuilderMode());

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
        HandleDragLifecycle();
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

    // Detecta release y commit del chunk
    void HandleDragLifecycle()
    {
        var mouse = Mouse.current;
        bool isDragging = dragMode != 0 && mouse != null && (
            (dragMode == 1 && mouse.leftButton.isPressed) ||
            (dragMode == 2 && mouse.rightButton.isPressed));

        if (wasDragging && !isDragging) CommitChunk();
        wasDragging = isDragging;
    }

    public void OnCellPointerDown(int gx, int gy, bool isRight)
    {
        dragMode = isRight ? 2 : 1;
        dragStart = new Vector2Int(gx, gy);
        dragCurrent = dragStart;
        previewCells.Clear();
        previewCells.Add(dragStart);
        wasDragging = true;
        RefreshGrid();
    }

    public void OnCellPointerEnter(int gx, int gy)
    {
        if (dragMode == 0) return;
        var newCurrent = new Vector2Int(gx, gy);
        if (newCurrent == dragCurrent) return;
        dragCurrent = newCurrent;
        RecomputePreview();
        RefreshGrid();
    }

    void RecomputePreview()
    {
        previewCells.Clear();
        int dx = dragCurrent.x - dragStart.x;
        int dy = dragCurrent.y - dragStart.y;
        // Snap a eje dominante
        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
        {
            int sign = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int len = Mathf.Abs(dx);
            for (int i = 0; i <= len; i++) previewCells.Add(new Vector2Int(dragStart.x + sign * i, dragStart.y));
        }
        else
        {
            int sign = dy > 0 ? 1 : -1;
            int len = Mathf.Abs(dy);
            for (int i = 0; i <= len; i++) previewCells.Add(new Vector2Int(dragStart.x, dragStart.y + sign * i));
        }
    }

    void CommitChunk()
    {
        if (levelData == null || previewCells.Count == 0) { ResetDrag(); return; }

        if (dragMode == 2)
        {
            // Borrar
            foreach (var p in previewCells) levelData.EraseCell(p.x, p.y);
        }
        else
        {
            // Pintar con dirección
            int dx = dragCurrent.x - dragStart.x;
            int dy = dragCurrent.y - dragStart.y;
            CellDirection dir;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                dir = dx >= 0 ? CellDirection.Right : CellDirection.Left;
            else
                dir = dy > 0 ? CellDirection.Down : CellDirection.Up;
            // Nota: con origen top-left, dy > 0 = hacia abajo = "Down"

            var endCell = previewCells[previewCells.Count - 1];
            foreach (var p in previewCells)
            {
                bool isEnd = (p == endCell);
                levelData.SetCell(p.x, p.y, selectedColor, dir, isEnd);
            }
        }

        ResetDrag();
        RefreshGrid();
    }

    void ResetDrag()
    {
        dragMode = 0;
        previewCells.Clear();
        wasDragging = false;
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

    // Calcula la dirección provisional del preview durante el drag
    int PreviewDir()
    {
        int dx = dragCurrent.x - dragStart.x;
        int dy = dragCurrent.y - dragStart.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx >= 0 ? (int)CellDirection.Right : (int)CellDirection.Left;
        return dy > 0 ? (int)CellDirection.Down : (int)CellDirection.Up;
    }

    void RefreshGrid()
    {
        if (gridContainer == null || gridCellPrefab == null || levelData == null) return;

        for (int i = gridContainer.childCount - 1; i >= 0; i--)
            Destroy(gridContainer.GetChild(i).gameObject);

        // Set de celdas en preview para lookup rápido
        Vector2Int previewEnd = previewCells.Count > 0 ? previewCells[previewCells.Count - 1] : new Vector2Int(int.MinValue, int.MinValue);
        var previewSet = new HashSet<Vector2Int>(previewCells);
        int previewDir = previewCells.Count > 0 ? PreviewDir() : 0;
        Color previewColor = dragMode == 2 ? new Color(.14f, .14f, .16f) : selectedColor;

        for (int row = 0; row < viewportH; row++)
        for (int col = 0; col < viewportW; col++)
        {
            int gx = viewOffsetX + col;
            int gy = viewOffsetY + row;
            var key = new Vector2Int(gx, gy);

            Color disp;
            bool showArrow;
            int dir;

            if (previewSet.Contains(key))
            {
                disp = previewColor;
                showArrow = (key == previewEnd) && dragMode == 1;
                dir = previewDir;
            }
            else if (levelData.TryGet(gx, gy, out var entry))
            {
                disp = new Color(entry.r, entry.g, entry.b, 1f);
                showArrow = entry.isEnd;
                dir = entry.dir;
            }
            else
            {
                disp = new Color(.14f, .14f, .16f);
                showArrow = false;
                dir = 0;
            }

            var cell = Instantiate(gridCellPrefab, gridContainer);
            cell.Setup(this, gx, gy, disp, showArrow, dir);
        }
    }
}
