using System;
using System.Collections.Generic;
using UnityEngine;

public enum CellDirection { Up = 0, Down = 1, Left = 2, Right = 3 }

[Serializable]
public class CellEntry
{
    public int x, y;
    public float r, g, b;
    public int dir;     // CellDirection as int (sirve para JsonUtility)
    public bool isEnd;  // true si esta celda es el final del chunk (recibe la flecha)
}

[CreateAssetMenu(fileName = "LevelData", menuName = "CardGame/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelName = "Nivel 1";
    public List<CellEntry> cells = new List<CellEntry>();

    public bool TryGet(int x, int y, out CellEntry entry)
    {
        foreach (var c in cells)
        {
            if (c.x == x && c.y == y) { entry = c; return true; }
        }
        entry = null;
        return false;
    }

    public bool TryGetColor(int x, int y, out Color color)
    {
        if (TryGet(x, y, out var c))
        {
            color = new Color(c.r, c.g, c.b, 1f);
            return true;
        }
        color = Color.clear;
        return false;
    }

    public void SetCell(int x, int y, Color color, CellDirection dir = CellDirection.Right, bool isEnd = false)
    {
        if (color.a < 0.01f)
        {
            cells.RemoveAll(c => c.x == x && c.y == y);
            return;
        }
        if (TryGet(x, y, out var existing))
        {
            existing.r = color.r; existing.g = color.g; existing.b = color.b;
            existing.dir = (int)dir;
            existing.isEnd = isEnd;
            return;
        }
        cells.Add(new CellEntry
        {
            x = x, y = y,
            r = color.r, g = color.g, b = color.b,
            dir = (int)dir,
            isEnd = isEnd,
        });
    }

    public void EraseCell(int x, int y)
    {
        cells.RemoveAll(c => c.x == x && c.y == y);
    }

    public void Clear() => cells.Clear();

    /// <summary>
    /// Devuelve todas las celdas del chunk (cadena) que contiene (x,y). Una cadena
    /// es la secuencia contigua de celdas con misma dir y color a lo largo del eje
    /// de la dir, delimitada por las celdas con isEnd=true (que marcan el final
    /// de cada cadena pintada). Es decir, dos commits adyacentes del mismo color+dir
    /// son cadenas distintas porque cada uno tiene su propio isEnd.
    /// Orden: de "atras" (contra dir) a "adelante" (a favor de dir, terminando en isEnd).
    /// </summary>
    public List<Vector2Int> GetChunkAt(int x, int y)
    {
        if (!TryGet(x, y, out var entry)) return null;
        Vector2Int step = StepFromDir((CellDirection)entry.dir);
        Vector2Int back = -step;
        int targetDir = entry.dir;

        // 1) Buscar el isEnd de la cadena: caminamos hacia adelante desde (x,y).
        //    Si entry ya es isEnd, ese es el end. Si no, avanzamos hasta encontrar uno
        //    con isEnd o cortar la cadena.
        Vector2Int endPos = new Vector2Int(x, y);
        if (!entry.isEnd)
        {
            var cur = endPos;
            while (true)
            {
                var next = cur + step;
                if (TryGet(next.x, next.y, out var n) && n.dir == targetDir && SameColor(n, entry))
                {
                    cur = next;
                    if (n.isEnd) { endPos = cur; break; }
                }
                else break;
            }
            // Si no encontramos isEnd hacia adelante, la celda es orfana: usamos cur como ancla.
            if (!TryGet(endPos.x, endPos.y, out var e0) || !e0.isEnd)
                endPos = cur;
        }

        // 2) Caminar hacia atras desde endPos. El walk se corta al chocar con
        //    otro isEnd (eso es el final de una cadena previa, no parte de esta).
        var chain = new List<Vector2Int> { endPos };
        var c = endPos;
        while (true)
        {
            var prev = c + back;
            if (TryGet(prev.x, prev.y, out var p) && p.dir == targetDir && SameColor(p, entry) && !p.isEnd)
            {
                chain.Insert(0, prev);
                c = prev;
            }
            else break;
        }

        return chain;
    }

    public void EraseChunkAt(int x, int y)
    {
        var chunk = GetChunkAt(x, y);
        if (chunk == null) return;
        foreach (var p in chunk)
            cells.RemoveAll(c => c.x == p.x && c.y == p.y);
    }

    /// <summary>
    /// Para cada celda semilla, recalcula la cadena (misma dir + color) y deja
    /// isEnd=true solamente en la celda mas "adelantada" (a favor de la dir).
    /// </summary>
    public void NormalizeChunksAt(IEnumerable<Vector2Int> seeds)
    {
        var visited = new HashSet<Vector2Int>();
        foreach (var seed in seeds)
        {
            if (visited.Contains(seed)) continue;
            var chain = GetChunkAt(seed.x, seed.y);
            if (chain == null) continue;
            for (int i = 0; i < chain.Count; i++)
            {
                visited.Add(chain[i]);
                if (TryGet(chain[i].x, chain[i].y, out var e))
                    e.isEnd = (i == chain.Count - 1);
            }
        }
    }

    /// <summary>Normaliza todas las cadenas del nivel (util al cargar).</summary>
    public void NormalizeAllChunks()
    {
        var seeds = new List<Vector2Int>(cells.Count);
        foreach (var c in cells) seeds.Add(new Vector2Int(c.x, c.y));
        NormalizeChunksAt(seeds);
    }

    static bool SameColor(CellEntry a, CellEntry b)
    {
        return ColorUtil.ApproximatelyEqual(new Color(a.r, a.g, a.b), new Color(b.r, b.g, b.b));
    }

    static Vector2Int StepFromDir(CellDirection d)
    {
        switch (d)
        {
            case CellDirection.Right: return new Vector2Int(+1,  0);
            case CellDirection.Left:  return new Vector2Int(-1,  0);
            case CellDirection.Down:  return new Vector2Int( 0, +1);
            case CellDirection.Up:    return new Vector2Int( 0, -1);
        }
        return Vector2Int.zero;
    }
}
