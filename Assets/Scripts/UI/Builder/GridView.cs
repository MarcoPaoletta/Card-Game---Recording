using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Render del grid del builder. Coordina:
/// - panning (teclado y mouse medio)
/// - pintado (paint/erase) via GridPainter
/// - popup de direccion para single-cell via DirectionPickerView
/// </summary>
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
    private DirectionPickerView directionPicker;
    private int viewOffsetX;
    private int viewOffsetY;
    private bool wasDragging;

    private float panCooldown;
    private const float PanInitialDelay  = 0.30f;
    private const float PanRepeatInterval = 0.07f;
    private Vector2 mousePanAccum;

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
        directionPicker = new DirectionPickerView(GetPickerHost(), ResolveFont());
    }

    void Update()
    {
        if (!isActiveAndEnabled) return;
        HandleKeyboardPan();
        HandleMousePan();
        HandleDragLifecycle();
    }

    // --- Input ---

    public void OnCellPointerDown(int gx, int gy, bool isRight)
    {
        if (painter == null) return;
        if (directionPicker != null && directionPicker.IsOpen)
        {
            painter.CancelPending();
            directionPicker.Hide();
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
                OpenDirectionPicker(cell.x, cell.y);
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

    // --- Pan ---

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

    void Pan(int dx, int dy)
    {
        viewOffsetX += dx;
        viewOffsetY += dy;
        if (viewOffsetX < 0) viewOffsetX = 0;
        if (viewOffsetY < 0) viewOffsetY = 0;
        UpdateCoords();
        Refresh();
    }

    // --- Render ---

    public void Refresh()
    {
        UpdateCoords();
        RebuildCells();
    }

    void UpdateCoords()
    {
        if (coordsText == null) return;
        int x1 = viewOffsetX + viewportW - 1;
        int y1 = viewOffsetY + viewportH - 1;
        coordsText.text = $"TL ({viewOffsetX},{viewOffsetY})\nBR ({x1},{y1})";
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

    // --- Direction picker ---

    void OpenDirectionPicker(int gx, int gy)
    {
        if (directionPicker == null || gridContainer == null) return;

        int col = gx - viewOffsetX;
        int row = gy - viewOffsetY;
        if (col < 0 || col >= viewportW || row < 0 || row >= viewportH) return;
        int idx = row * viewportW + col;
        if (idx < 0 || idx >= gridContainer.childCount) return;

        var cellRT = gridContainer.GetChild(idx) as RectTransform;
        if (cellRT == null) return;

        var glg = gridContainer.GetComponent<GridLayoutGroup>();
        float cellSize = glg != null ? glg.cellSize.x : 80f;

        directionPicker.Show(cellRT, cellSize, OnDirectionPicked);
    }

    void OnDirectionPicked(CellDirection dir)
    {
        if (painter == null) return;
        painter.CommitPendingDirection(dir);
        manager?.AutoSave();
        Refresh();
    }

    RectTransform GetPickerHost()
    {
        return (gridContainer != null ? gridContainer.parent as RectTransform : null) ?? gridContainer;
    }

    TMP_FontAsset ResolveFont()
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null && gridCellPrefab != null)
        {
            var arrowField = gridCellPrefab.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (arrowField != null) font = arrowField.font;
        }
        return font;
    }
}
