using System.IO;
using UnityEngine;

public class LevelStore
{
    private const string ResourcesSubPath = "Levels";

    private readonly string editorRoot;

    public LevelStore()
    {
        editorRoot = Path.Combine(Application.dataPath, "Resources", ResourcesSubPath);
#if UNITY_EDITOR
        Directory.CreateDirectory(editorRoot);
#endif
        TryMigrateFromPersistentData();
    }

    public int Count()
    {
        int c = 0;
        while (Exists(c)) c++;
        return c;
    }

    public bool Exists(int index)
    {
#if UNITY_EDITOR
        if (File.Exists(EditorPath(index))) return true;
#endif
        return Resources.Load<TextAsset>($"{ResourcesSubPath}/level_{index}") != null;
    }

    public string Read(int index)
    {
#if UNITY_EDITOR
        string p = EditorPath(index);
        if (File.Exists(p)) return File.ReadAllText(p);
#endif
        var ta = Resources.Load<TextAsset>($"{ResourcesSubPath}/level_{index}");
        return ta != null ? ta.text : null;
    }

    public void Write(int index, string json)
    {
#if UNITY_EDITOR
        File.WriteAllText(EditorPath(index), json);
        UnityEditor.AssetDatabase.Refresh();
#else
        Debug.LogWarning("[LevelStore] Write is editor-only; levels cannot be saved in runtime builds.");
#endif
    }

    public void Swap(int a, int b)
    {
#if UNITY_EDITOR
        string ta = Read(a) ?? "";
        string tb = Read(b) ?? "";
        File.WriteAllText(EditorPath(a), tb);
        File.WriteAllText(EditorPath(b), ta);
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void Delete(int index, int totalCount)
    {
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.DeleteAsset(AssetPath(index));
        for (int i = index + 1; i < totalCount; i++)
            UnityEditor.AssetDatabase.MoveAsset(AssetPath(i), AssetPath(i - 1));
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    string EditorPath(int index) => Path.Combine(editorRoot, $"level_{index}.json");
    string AssetPath(int index) => $"Assets/Resources/{ResourcesSubPath}/level_{index}.json";

    void TryMigrateFromPersistentData()
    {
#if UNITY_EDITOR
        string legacy = Path.Combine(Application.persistentDataPath, "Levels");
        if (!Directory.Exists(legacy)) return;
        if (Directory.GetFiles(editorRoot, "level_*.json").Length > 0) return;

        int i = 0;
        while (true)
        {
            string src = Path.Combine(legacy, $"level_{i}.json");
            if (!File.Exists(src)) break;
            File.WriteAllText(EditorPath(i), File.ReadAllText(src));
            i++;
        }
        if (i > 0)
        {
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"[LevelStore] Migrated {i} level(s) from persistentDataPath to Assets/Resources/Levels.");
        }
#endif
    }
}
