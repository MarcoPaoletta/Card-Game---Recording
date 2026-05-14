using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GridView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelBuilderManager manager;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private GridCellView gridCellPrefab;
    [SerializeField] private TMP_Text coordsText;

    [Header("Viewport")]
    [SerializeField] private int viewportW = 17;
    [SerializeField] private int viewportH = 12;

    private LevelData levelData;
    private GridPainter painter;
    private int viewOffsetX;
    private int viewOffsetY;
    private bool wasDragging;

    private float panCooldown;
    private const float PanInitialDelay  = 0.30f;
    private const float PanRepeatInterval = 0.07f;
    private Vector2 mousePanAccum;

    private GameObject directionPickerGO;

    public GridPainter Painter => painter;

    public void OverrideLevelData(LevelData runtime)
    {
        levelData = runtime;
        if (painter == null) painter = new GridPainter(levelData);
        else painter.SetData(levelData);
    }

    void Awake()
    {
        if (painter == null) painter = new GridPainter(levelData);
    }

    void Update()
    {
        if (!isActiveAndEnabled) return;
        HandleKeyboardPan();
        HandleMousePan();
        HandleDragLifecycle();
    }

    void HandleKeyboardPan()
    {
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

    void HandleDragLifecycle()
    {
        var mouse = Mouse.current;
        bool isDragging = painter != null && painter.IsActive && mouse != null && (
            (painter.Mode == GridPainter.PaintMode.Paint && mouse.leftButton.isPressed) ||
            (painter.Mode == GridPainter.PaintMode.Erase && mouse.rightButton.isPressed));

        if (wasDragging && !isDragging)
        {
            var result = painter.Commit();
            if (result == GridPainter.CommitResult.PendingDirection)
            {
                var cell = painter.PendingCell.Value;
                ShowDirectionPicker(cell.x, cell.y);
                // No autosave hasta que el usuario elija direccion.
                Refresh();
            }
            else
            {
                if (result == GridPainter.CommitResult.Committed) manager?.AutoSave();
                Refresh();
            }
        }
        wasDragging = isDragging;
    }

    public void OnCellPointerDown(int gx, int gy, bool isRight)
    {
        if (painter == null) return;
        // Si habia un picker abierto, lo cancelamos al iniciar otra accion.
        if (directionPickerGO != null)
        {
            painter.CancelPending();
            HideDirectionPicker();
        }
        painter.Begin(gx, gy, isRight);
        wasDragging = true;
        Refresh();
    }

    public void OnCellPointerEnter(int gx, int gy)
    {
        if (painter == null || !painter.IsActive) return;
        painter.DragTo(gx, gy);
        Refresh();
    }

    void Pan(int dx, int dy)
    {
        viewOffsetX += dx;
        viewOffsetY += dy;
        if (viewOffsetX < 0) viewOffsetX = 0;
        if (viewOffsetY < 0) viewOffsetY = 0;
        UpdateCoords();
        Refresh();
    }

    void UpdateCoords()
    {
        if (coordsText == null) return;
        int x1 = viewOffsetX + viewportW - 1;
        int y1 = viewOffsetY + viewportH - 1;
        coordsText.text = $"TL ({viewOffsetX},{viewOffsetY})\nBR ({x1},{y1})";
    }

    public void Refresh()
    {
        UpdateCoords();
        RebuildCells();
    }

    void RebuildCells()
    {
        if (gridContainer == null || gridCellPrefab == null || levelData == null || painter == null) return;

        for (int i = gridContainer.childCount - 1; i >= 0; i--)
            Destroy(gridContainer.GetChild(i).gameObject);

        var preview = painter.Preview;
        Vector2Int previewEnd = preview.Count > 0 ? preview[preview.Count - 1] : new Vector2Int(int.MinValue, int.MinValue);
        var previewSet = new HashSet<Vector2Int>(preview);
        int previewDir = preview.Count > 0 ? (int)painter.PreviewDir() : 0;
        Color previewColor = painter.Mode == GridPainter.PaintMode.Erase
            ? new Color(.14f, .14f, .16f)
            : painter.SelectedColor;

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
                showArrow = (key == previewEnd) && painter.Mode == GridPainter.PaintMode.Paint;
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

    void ShowDirectionPicker(int gx, int gy)
    {
        HideDirectionPicker();
        if (gridContainer == null) return;

        int col = gx - viewOffsetX;
        int row = gy - viewOffsetY;
        if (col < 0 || col >= viewportW || row < 0 || row >= viewportH) return;
        int idx = row * viewportW + col;
        if (idx < 0 || idx >= gridContainer.childCount) return;

        var cellRT = gridContainer.GetChild(idx) as RectTransform;
        if (cellRT == null) return;

        var glg = gridContainer.GetComponent<GridLayoutGroup>();
        float cellSize = glg != null ? glg.cellSize.x : 80f;
        float btnSize = cellSize * 0.9f;

        var parent = gridContainer.parent as RectTransform;
        if (parent == null) parent = gridContainer;

        directionPickerGO = new GameObject("DirectionPicker", typeof(RectTransform));
        var rootRT = directionPickerGO.GetComponent<RectTransform>();
        rootRT.SetParent(parent, worldPositionStays: false);
        rootRT.position = cellRT.position;
        rootRT.sizeDelta = Vector2.zero;
        rootRT.localScale = Vector3.one;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null && gridCellPrefab != null)
        {
            var arrowField = gridCellPrefab.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (arrowField != null) font = arrowField.font;
        }

        CreatePickerButton(rootRT, "↑", new Vector2(0f,  cellSize), CellDirection.Up,    btnSize, font);
        CreatePickerButton(rootRT, "↓", new Vector2(0f, -cellSize), CellDirection.Down,  btnSize, font);
        CreatePickerButton(rootRT, "←", new Vector2(-cellSize, 0f), CellDirection.Left,  btnSize, font);
        CreatePickerButton(rootRT, "→", new Vector2( cellSize, 0f), CellDirection.Right, btnSize, font);
    }

    void CreatePickerButton(RectTransform parent, string arrow, Vector2 offset, CellDirection dir, float size, TMP_FontAsset font)
    {
        var go = new GameObject($"Btn_{dir}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.55f, 0.95f, 1f);
        colors.pressedColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        btn.colors = colors;

        var txtGO = new GameObject("Label", typeof(RectTransform));
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.SetParent(rt, false);
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = arrow;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.fontSize = size * 0.6f;
        txt.raycastTarget = false;

        btn.onClick.AddListener(() => OnDirectionPicked(dir));
    }

    void OnDirectionPicked(CellDirection dir)
    {
        if (painter == null) return;
        painter.CommitPendingDirection(dir);
        HideDirectionPicker();
        manager?.AutoSave();
        Refresh();
    }

    void HideDirectionPicker()
    {
        if (directionPickerGO != null)
        {
            Destroy(directionPickerGO);
            directionPickerGO = null;
        }
    }
}
