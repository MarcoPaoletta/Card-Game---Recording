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
    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "Levels");
        Directory.CreateDirectory(savePath);
    }

    void Start()
    {
        editorUI.Hide();
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
            editorUI.Hide();
            SaveCurrentLevel();
            spawner.SpawnCards();
        }
    }

    public void SaveCurrentLevel()
    {
        // Serializamos directamente el ScriptableObject
        string json = JsonUtility.ToJson(levelData);
        File.WriteAllText(Path.Combine(savePath, $"level_{currentLevelIndex}.json"), json);
        Debug.Log($"[LevelBuilder] Nivel {currentLevelIndex} guardado. Celdas: {levelData.cells.Count}");
    }

    public void LoadLevel(int index)
    {
        string file = Path.Combine(savePath, $"level_{index}.json");
        if (File.Exists(file))
        {
            // Sobreescribe los campos del SO con los datos del JSON
            JsonUtility.FromJsonOverwrite(File.ReadAllText(file), levelData);
        }
        else
        {
            levelData.levelName = $"Nivel {index + 1}";
            levelData.cells = new List<CellEntry>();
        }
        currentLevelIndex = index;
        Debug.Log($"[LevelBuilder] Nivel {index} cargado. Celdas: {levelData.cells.Count}");
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
        SaveCurrentLevel();
        LoadLevel(currentLevelIndex + 1);
        editorUI.Refresh();
    }
}
