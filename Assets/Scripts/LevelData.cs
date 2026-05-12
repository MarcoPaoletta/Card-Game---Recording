using System;
using System.Collections.Generic;
using UnityEngine;

// Cada celda pintada del nivel (coordenadas enteras + color)
[Serializable]
public class CellEntry
{
    public int x, y;
    public float r, g, b;
}

// ScriptableObject que guarda los datos de un nivel
[CreateAssetMenu(fileName = "LevelData", menuName = "CardGame/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelName = "Nivel 1";
    public List<CellEntry> cells = new List<CellEntry>();

    // Devuelve true + color si la celda (x,y) está pintada
    public bool TryGetColor(int x, int y, out Color color)
    {
        foreach (var c in cells)
        {
            if (c.x == x && c.y == y)
            {
                color = new Color(c.r, c.g, c.b, 1f);
                return true;
            }
        }
        color = Color.clear;
        return false;
    }

    // Pinta o borra una celda (color con a < 0.01 = borrar)
    public void SetCell(int x, int y, Color color)
    {
        if (color.a < 0.01f)
        {
            cells.RemoveAll(c => c.x == x && c.y == y);
            return;
        }
        foreach (var c in cells)
        {
            if (c.x == x && c.y == y)
            {
                c.r = color.r; c.g = color.g; c.b = color.b;
                return;
            }
        }
        cells.Add(new CellEntry { x = x, y = y, r = color.r, g = color.g, b = color.b });
    }

    public void Clear() => cells.Clear();
}
