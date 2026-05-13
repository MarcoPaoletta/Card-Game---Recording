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
}
