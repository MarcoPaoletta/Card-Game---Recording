using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelBuilderManager : MonoBehaviour
{
    [SerializeField] private LevelEditorUI editorUI;
    [SerializeField] private CardsSpawner spawner;
    [SerializeField] private LevelData levelData;

    private bool inBuilderMode;
    private int currentLevelIndex;
    private int levelCount;
    private string savePath;

    public int CurrentLevelIndex => currentLevelIndex;
    public int LevelCount => levelCount;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "Levels");
        Directory.CreateDirectory(savePath);
        RefreshLevelCount();
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
        inBuilderMode = !inBuilderMode;
        if (inBuilderMode)
        {
            editorUI.Show();
        }
        else
        {
            SaveCurrentLevel();
            editorUI.Hide();
            spawner.SpawnCards();
        }
    }

    void RefreshLevelCount()
    {
        levelCount = 0;
        while (File.Exists(LevelFile(levelCount))) levelCount++;
    }

    string LevelFile(int index) => Path.Combine(savePath, $"level_{index}.json");

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
        string f = LevelFile(index);
        if (!File.Exists(f)) return BuildName(index, "");
        var peek = JsonUtility.FromJson<LevelNamePeek>(File.ReadAllText(f));
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
        File.WriteAllText(LevelFile(index), JsonUtility.ToJson(tmp));
        Object.Destroy(tmp);
    }

    public void SaveCurrentLevel()
    {
        if (levelData == null) return;
        levelData.levelName = BuildName(currentLevelIndex, ParseNote(levelData.levelName));
        File.WriteAllText(LevelFile(currentLevelIndex), JsonUtility.ToJson(levelData));
    }

    public void LoadLevel(int index)
    {
        if (index < 0) index = 0;
        if (index >= levelCount) index = Mathf.Max(0, levelCount - 1);

        string file = LevelFile(index);
        string note = "";
        if (File.Exists(file))
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(file), levelData);
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

        for (int i = currentLevelIndex; i < levelCount - 1; i++)
        {
            string src = LevelFile(i + 1);
            string dst = LevelFile(i);
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(src, dst);
        }
        levelCount--;
        int next = Mathf.Min(currentLevelIndex, levelCount - 1);
        LoadLevel(next);
        editorUI.Refresh();
    }

    public void MoveCurrentLevelUp() => MoveLevelUp(currentLevelIndex);
    public void MoveCurrentLevelDown() => MoveLevelDown(currentLevelIndex);

    public void MoveLevelUp(int index)
    {
        if (index <= 0 || index >= levelCount) return;
        SaveCurrentLevel();
        SwapLevels(index, index - 1);
        if (currentLevelIndex == index) LoadLevel(index - 1);
        else if (currentLevelIndex == index - 1) LoadLevel(index);
        editorUI.Refresh();
    }

    public void MoveLevelDown(int index)
    {
        if (index < 0 || index >= levelCount - 1) return;
        SaveCurrentLevel();
        SwapLevels(index, index + 1);
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

    void SwapLevels(int a, int b)
    {
        string fa = LevelFile(a);
        string fb = LevelFile(b);
        string ta = File.Exists(fa) ? File.ReadAllText(fa) : "";
        string tb = File.Exists(fb) ? File.ReadAllText(fb) : "";
        File.WriteAllText(fa, tb);
        File.WriteAllText(fb, ta);
    }

    public void AutoSave()
    {
        SaveCurrentLevel();
    }
}
