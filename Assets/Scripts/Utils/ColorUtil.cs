using UnityEngine;

/// <summary>
/// Helpers de color compartidos: cuantizado a 2 decimales (para tratar floats
/// guardados en JSON como claves estables de diccionario) y comparacion con
/// epsilon. Reemplaza copias dispersas en LevelBuilderManager / LevelFlowManager
/// / LevelData / Order / ReserveManager / CardsSpawnerManager.
/// </summary>
public static class ColorUtil
{
    public const float Epsilon = 0.01f;

    public static Color Quantize(Color c)
    {
        return new Color(
            Mathf.Round(c.r * 100f) / 100f,
            Mathf.Round(c.g * 100f) / 100f,
            Mathf.Round(c.b * 100f) / 100f,
            1f);
    }

    public static bool ApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < Epsilon
            && Mathf.Abs(a.g - b.g) < Epsilon
            && Mathf.Abs(a.b - b.b) < Epsilon;
    }
}
