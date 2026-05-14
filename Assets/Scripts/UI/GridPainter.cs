using System.Collections.Generic;
using UnityEngine;

public class GridPainter
{
    public enum PaintMode { None = 0, Paint = 1, Erase = 2 }
    public enum CommitResult { None, Committed, PendingDirection }

    public Color SelectedColor = Color.red;

    private LevelData data;
    private PaintMode mode;
    private Vector2Int start, current;
    private readonly List<Vector2Int> preview = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> eraseSet = new HashSet<Vector2Int>();

    // Single-cell paint queda pendiente hasta que el usuario elige direccion.
    public Vector2Int? PendingCell { get; private set; }
    public Color PendingColor { get; private set; }
    public bool HasPending => PendingCell.HasValue;

    public GridPainter(LevelData data) { this.data = data; }
    public void SetData(LevelData d) { this.data = d; }

    public PaintMode Mode => mode;
    public bool IsActive => mode != PaintMode.None;
    public Vector2Int Current => current;
    public Vector2Int Start => start;
    public IReadOnlyList<Vector2Int> Preview => preview;

    public void Begin(int x, int y, bool isRight)
    {
        // Cualquier accion nueva cancela un pending previo.
        PendingCell = null;

        mode = isRight ? PaintMode.Erase : PaintMode.Paint;
        start = current = new Vector2Int(x, y);
        preview.Clear();
        preview.Add(start);
        if (mode == PaintMode.Erase)
        {
            eraseSet.Clear();
            eraseSet.Add(start);
        }
    }

    public void DragTo(int x, int y)
    {
        if (mode == PaintMode.None) return;
        var nc = new Vector2Int(x, y);
        if (nc == current) return;
        current = nc;

        if (mode == PaintMode.Erase)
        {
            // Free-form: cada celda tocada se agrega al preview.
            if (eraseSet.Add(nc)) preview.Add(nc);
        }
        else
        {
            Recompute();
        }
    }

    public CellDirection PreviewDir()
    {
        int dx = current.x - start.x;
        int dy = current.y - start.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx >= 0 ? CellDirection.Right : CellDirection.Left;
        return dy > 0 ? CellDirection.Down : CellDirection.Up;
    }

    public CommitResult Commit()
    {
        if (data == null || preview.Count == 0) { Cancel(); return CommitResult.None; }

        if (mode == PaintMode.Erase)
        {
            // Borrar la cadena entera de cada celda tocada (chain integrity).
            foreach (var p in preview) data.EraseChunkAt(p.x, p.y);
            Cancel();
            return CommitResult.Committed;
        }

        // Paint
        if (preview.Count == 1)
        {
            // Una sola celda: queda pendiente hasta que el usuario elija direccion.
            var p = preview[0];
            // Chain integrity: borrar la cadena previa en la que estaba esta celda.
            data.EraseChunkAt(p.x, p.y);
            PendingCell = p;
            PendingColor = SelectedColor;
            mode = PaintMode.None;
            preview.Clear();
            return CommitResult.PendingDirection;
        }

        var dir = PreviewDir();
        // Chain integrity: borrar cadenas viejas que solapan con el nuevo preview.
        foreach (var p in preview) data.EraseChunkAt(p.x, p.y);

        // Cada commit es UNA cadena: solo la ultima celda lleva isEnd.
        var end = preview[preview.Count - 1];
        foreach (var p in preview)
            data.SetCell(p.x, p.y, SelectedColor, dir, p == end);

        Cancel();
        return CommitResult.Committed;
    }

    public void CommitPendingDirection(CellDirection dir)
    {
        if (data == null || !PendingCell.HasValue) return;
        var p = PendingCell.Value;
        // Single-cell tambien es su propia cadena: isEnd=true en si misma.
        data.SetCell(p.x, p.y, PendingColor, dir, true);
        PendingCell = null;
    }

    public void CancelPending()
    {
        if (!PendingCell.HasValue) return;
        var p = PendingCell.Value;
        // No quedo escrito, asi que no hace falta borrar nada de data.
        PendingCell = null;
    }

    public void Cancel()
    {
        mode = PaintMode.None;
        preview.Clear();
        eraseSet.Clear();
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
