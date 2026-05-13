using System.Collections.Generic;
using UnityEngine;

public class GridPainter
{
    public enum PaintMode { None = 0, Paint = 1, Erase = 2 }

    public Color SelectedColor = Color.red;

    private LevelData data;
    private PaintMode mode;
    private Vector2Int start, current;
    private readonly List<Vector2Int> preview = new List<Vector2Int>();

    public GridPainter(LevelData data) { this.data = data; }
    public void SetData(LevelData d) { this.data = d; }

    public PaintMode Mode => mode;
    public bool IsActive => mode != PaintMode.None;
    public Vector2Int Current => current;
    public Vector2Int Start => start;
    public IReadOnlyList<Vector2Int> Preview => preview;

    public void Begin(int x, int y, bool isRight)
    {
        mode = isRight ? PaintMode.Erase : PaintMode.Paint;
        start = current = new Vector2Int(x, y);
        preview.Clear();
        preview.Add(start);
    }

    public void DragTo(int x, int y)
    {
        if (mode == PaintMode.None) return;
        var nc = new Vector2Int(x, y);
        if (nc == current) return;
        current = nc;
        Recompute();
    }

    public CellDirection PreviewDir()
    {
        int dx = current.x - start.x;
        int dy = current.y - start.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx >= 0 ? CellDirection.Right : CellDirection.Left;
        return dy > 0 ? CellDirection.Down : CellDirection.Up;
    }

    public bool Commit()
    {
        if (data == null || preview.Count == 0) { Cancel(); return false; }

        if (mode == PaintMode.Erase)
        {
            foreach (var p in preview) data.EraseCell(p.x, p.y);
        }
        else
        {
            var dir = PreviewDir();
            var end = preview[preview.Count - 1];
            foreach (var p in preview)
                data.SetCell(p.x, p.y, SelectedColor, dir, p == end);
        }

        Cancel();
        return true;
    }

    public void Cancel()
    {
        mode = PaintMode.None;
        preview.Clear();
    }

    void Recompute()
    {
        preview.Clear();
        int dx = current.x - start.x;
        int dy = current.y - start.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
        {
            int sign = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int len = Mathf.Abs(dx);
            for (int i = 0; i <= len; i++) preview.Add(new Vector2Int(start.x + sign * i, start.y));
        }
        else
        {
            int sign = dy > 0 ? 1 : -1;
            int len = Mathf.Abs(dy);
            for (int i = 0; i <= len; i++) preview.Add(new Vector2Int(start.x, start.y + sign * i));
        }
    }
}
