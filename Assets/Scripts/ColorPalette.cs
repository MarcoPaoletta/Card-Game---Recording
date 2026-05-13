using System;
using UnityEngine;

[Serializable]
public class PaletteEntry
{
    public string label;
    public Color color = Color.white;
}

[CreateAssetMenu(fileName = "ColorPalette", menuName = "CardGame/Color Palette")]
public class ColorPalette : ScriptableObject
{
    public PaletteEntry[] entries;
}
