using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Valida un nivel antes de salir del builder. Regla: cada color tiene que
/// tener un numero par de celdas (2 celdas = 8 cartas = 1 order).
/// </summary>
public class LevelValidator
{
    private readonly ColorPalette palette;

    public LevelValidator(ColorPalette palette)
    {
        this.palette = palette;
    }

    public bool Validate(List<CellEntry> cells, out string error)
    {
        error = null;
        if (cells == null || cells.Count == 0) return true;

        var counts = new Dictionary<Color, int>();
        var firstSeen = new List<Color>();
        foreach (var cell in cells)
        {
            Color key = ColorUtil.Quantize(new Color(cell.r, cell.g, cell.b, 1f));
            if (!counts.ContainsKey(key))
            {
                counts[key] = 0;
                firstSeen.Add(key);
            }
            counts[key]++;
        }

        var oddGroups = new List<string>();
        foreach (var c in firstSeen)
        {
            if (counts[c] % 2 != 0)
                oddGroups.Add($"  - {ColorNameFor(c)}: {counts[c]} celdas (impar)");
        }

        if (oddGroups.Count > 0)
        {
            error = "Cada color debe tener un nro par de celdas (2 celdas = 1 order de 8 cartas). Colores invalidos:\n"
                  + string.Join("\n", oddGroups);
            return false;
        }
        return true;
    }

    string ColorNameFor(Color c)
    {
        if (palette != null && palette.entries != null)
        {
            foreach (var entry in palette.entries)
            {
                if (ColorUtil.Quantize(entry.color) == c && !string.IsNullOrEmpty(entry.label))
                    return entry.label;
            }
        }
        return $"RGB({c.r:0.00},{c.g:0.00},{c.b:0.00})";
    }
}
