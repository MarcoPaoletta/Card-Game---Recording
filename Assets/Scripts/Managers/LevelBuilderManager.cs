using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class LevelBuilderManager : MonoBehaviour
{
    [SerializeField] private LevelEditorUI editorUI;
    [SerializeField] private CardsSpawnerManager spawner;
    [Tooltip("SO de proyecto usado como semilla; en runtime se clona para no mutar el asset.")]
    [FormerlySerializedAs("levelData")]
    [SerializeField] private LevelData levelDataAsset;
    [Tooltip("Paleta usada para resolver el nombre de cada color al validar el nivel.")]
    [SerializeField] private ColorPalette palette;

    private LevelData levelData;
    private LevelStore store;
    private bool inBuilderMode;
    private int currentLevelIndex;
    private int levelCount;

    public int CurrentLevelIndex => currentLevelIndex;
    public int LevelCount => levelCount;
    public LevelData LevelData => levelData;

    void Awake()
    {
        levelData = Instantiate(levelDataAsset);
        levelData.cells = new List<CellEntry>();

        store = new LevelStore();
        levelCount = store.Count();

        if (spawner != null) spawner.OverrideLevelData(levelData);
        if (editorUI != null) editorUI.OverrideLevelData(levelData);
    }

    void Start()
    {
        editorUI.Hide();
        if (levelCount == 0)
        {
            CreateEmptyLevelFile(0);
            levelCount = 1;
        }
        LoadLevel(0);
        spawner.SpawnCards();
    }

    void Update()
    {
        if (Keyboard.current.bKey.wasPressedThisFrame)
            ToggleBuilderMode();
    }

    public void ToggleBuilderMode()
    {
        if (!inBuilderMode)
        {
            inBuilderMode = true;
            editorUI.Show();
        }
        else
        {
            if (!ValidateLevel(out string error))
            {
                Debug.LogError($"[LevelBuilderManager] No se puede salir del builder:\n{error}");
                return;
            }
            inBuilderMode = false;
            SaveCurrentLevel();
            editorUI.Hide();
            spawner.SpawnCards();
        }
    }

    bool ValidateLevel(out string error)
    {
        error = null;
        if (levelData == null || levelData.cells == null || levelData.cells.Count == 0)
            return true;

        var counts = new Dictionary<Color, int>();
        var firstSeen = new List<Color>();
        foreach (var cell in levelData.cells)
        {
            Color key = QuantizeColor(new Color(cell.r, cell.g, cell.b, 1f));
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
                oddGroups.Add($"  - {ColorName(c)}: {counts[c]} celdas (impar)");
        }

        if (oddGroups.Count > 0)
        {
            error = "Cada color debe tener un nro par de celdas (2 celdas = 1 orden de 8 cartas). Colores invalidos:\n"
                  + string.Join("\n", oddGroups);
            return false;
        }
        return true;
    }

    static Color QuantizeColor(Color c)
    {
        return new Color(
            Mathf.Round(c.r * 100f) / 100f,
            Mathf.Round(c.g * 100f) / 100f,
            Mathf.Round(c.b * 100f) / 100f,
            1f);
    }

    string ColorName(Color c)
    {
        if (palette != null && palette.entries != null)
        {
            foreach (var entry in palette.entries)
            {
                if (QuantizeColor(entry.color) == c && !string.IsNullOrEmpty(entry.label))
                    return entry.label;
            }
        }
        return $"RGB({c.r:0.00},{c.g:0.00},{c.b:0.00})";
    }

    private const string Separator = " - ";

    static string BuildName(int index, string note)
    {
        if (note == null) note = "";
        return $"Level {index + 1}{Separator}{note}";
    }

    static string ParseNote(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        int sep = fullName.IndexOf(Separator);
        if (sep >= 0) return fullName.Substring(sep + Separator.Length);
        if (fullName.StartsWith("Level ")) return "";
        return fullName;
    }

    [System.Serializable] class LevelNamePeek { public string levelName; }

    public string GetLevelDisplayName(int index)
    {
        if (index == currentLevelIndex && levelData != null)
            return BuildName(index, ParseNote(levelData.levelName));
        string json = store.Read(index);
        if (string.IsNullOrEmpty(json)) return BuildName(index, "");
        var peek = JsonUtility.FromJson<LevelNamePeek>(json);
        return BuildName(index, ParseNote(peek != null ? peek.levelName : null));
    }

    public string GetCurrentNote() => levelData != null ? ParseNote(levelData.levelName) : "";

    public void SetCurrentNote(string note)
    {
        if (levelData == null) return;
        levelData.levelName = BuildName(currentLevelIndex, note);
        SaveCurrentLevel();
    }

    void CreateEmptyLevelFile(int index)
    {
        var tmp = ScriptableObject.CreateInstance<LevelData>();
        tmp.levelName = BuildName(index, "");
        tmp.cells = new List<CellEntry>();
        store.Write(index, JsonUtility.ToJson(tmp));
        Destroy(tmp);
    }

    public void SaveCurrentLevel()
    {
        if (levelData == null) return;
        levelData.levelName = BuildName(currentLevelIndex, ParseNote(levelData.levelName));
        store.Write(currentLevelIndex, JsonUtility.ToJson(levelData));
    }

    public void LoadLevel(int index)
    {
        if (index < 0) index = 0;
        if (index >= levelCount) index = Mathf.Max(0, levelCount - 1);

        string json = store.Read(index);
        string note = "";
        if (!string.IsNullOrEmpty(json))
        {
            JsonUtility.FromJsonOverwrite(json, levelData);
            note = ParseNote(levelData.levelName);
        }
        else
        {
            levelData.cells = new List<CellEntry>();
        }
        levelData.levelName = BuildName(index, note);
        currentLevelIndex = index;
    }

    public void PrevLevel()
    {
        if (currentLevelIndex <= 0) return;
        SaveCurrentLevel();
        LoadLevel(currentLevelIndex - 1);
        editorUI.Refresh();
    }

    public void NextLevel()
    {
        if (currentLevelIndex >= levelCount - 1) return;
        SaveCurrentLevel();
        LoadLevel(currentLevelIndex + 1);
        editorUI.Refresh();
    }

    public void CreateNewLevel()
    {
        SaveCurrentLevel();
        int newIndex = levelCount;
        CreateEmptyLevelFile(newIndex);
        levelCount++;
        LoadLevel(newIndex);
        editorUI.Refresh();
    }

    public void DeleteCurrentLevel()
    {
        if (levelCount <= 1) return;
        store.Delete(currentLevelIndex, levelCount);
        levelCount--;
        int next = Mathf.Min(currentLevelIndex, levelCount - 1);
        LoadLevel(next);
        editorUI.Refresh();
    }

    public void MoveLevelUp(int index)
    {
        if (index <= 0 || index >= levelCount) return;
        SaveCurrentLevel();
        store.Swap(index, index - 1);
        if (currentLevelIndex == index) LoadLevel(index - 1);
        else if (currentLevelIndex == index - 1) LoadLevel(index);
        editorUI.Refresh();
    }

    public void MoveLevelDown(int index)
    {
        if (index < 0 || index >= levelCount - 1) return;
        SaveCurrentLevel();
        store.Swap(index, index + 1);
        if (currentLevelIndex == index) LoadLevel(index + 1);
        else if (currentLevelIndex == index + 1) LoadLevel(index);
        editorUI.Refresh();
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index >= levelCount || index == currentLevelIndex) return;
        SaveCurrentLevel();
        LoadLevel(index);
        editorUI.Refresh();
    }

    public void AutoSave() => SaveCurrentLevel();
}
