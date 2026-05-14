/// <summary>
/// Convenciones para el nombre de un nivel: "Level N - nota".
/// Centraliza build/parse del nombre y el struct usado para "peek" del nombre
/// sin deserializar el resto del JSON.
/// </summary>
public static class LevelNaming
{
    public const string Separator = " - ";

    public static string BuildName(int index, string note)
    {
        if (note == null) note = "";
        return $"Level {index + 1}{Separator}{note}";
    }

    public static string ParseNote(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        int sep = fullName.IndexOf(Separator);
        if (sep >= 0) return fullName.Substring(sep + Separator.Length);
        if (fullName.StartsWith("Level ")) return "";
        return fullName;
    }

    /// <summary>Para leer solo el campo levelName de un JSON sin deserializar todo.</summary>
    [System.Serializable]
    public class LevelNamePeek { public string levelName; }
}
