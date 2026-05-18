using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Maneja la carga/guardado/seleccion de niveles, el toggle del modo builder
/// y la transicion entre niveles. Las cosas mas especificas estan delegadas:
/// validacion -> LevelValidator, naming -> LevelNaming, persistencia -> LevelStore,
/// layout escena -> LevelLayoutManager, reset orders/reserve -> sus managers.
/// </summary>
public class LevelBuilderManager : MonoBehaviour
{
    [Header("UI / Spawner")]
    [SerializeField] private LevelEditorUI editorUI;
    [SerializeField] private CardsSpawnerManager spawner;
    [SerializeField] private LevelLayoutManager levelLayout;

    [Header("Datos")]
    [Tooltip("SO de proyecto usado como semilla; en runtime se clona para no mutar el asset.")]
    [FormerlySerializedAs("levelData")]
    [SerializeField] private LevelData levelDataAsset;
    [Tooltip("Paleta usada para resolver el nombre de cada color al validar el nivel.")]
    [SerializeField] private ColorPalette palette;

    [Header("Refs para reset al cambiar nivel")]
    [SerializeField] private OrdersManager ordersManager;
    [SerializeField] private ConveyorBelt reserveManager;

    private LevelData levelData;
    private LevelStore store;
    private LevelValidator validator;
    private bool inBuilderMode;
    private int currentLevelIndex;
    private int levelCount;
    // Cuantas "vueltas" enteras dio el jugador a la lista de niveles. Cada vez
    // que NextLevel pasa del ultimo, sumamos levelCount aca y volvemos al
    // index 0. El jugador ve "Nivel 4, 5, 6..." aunque internamente carguemos
    // de nuevo level_0.json. El builder y el panel de niveles siguen usando
    // currentLevelIndex (el real) para no romper la edicion.
    private int displayLoopOffset;

    public int CurrentLevelIndex => currentLevelIndex;
    public int LevelCount => levelCount;
    public LevelData LevelData => levelData;
    /// <summary>Numero de nivel mostrado al jugador (1-based, sigue creciendo al loopear).</summary>
    public int DisplayLevelNumber => currentLevelIndex + displayLoopOffset + 1;

    void Awake()
    {
        // Bump por defecto (200 tweeners / 50 sequences) es bajo cuando varios
        // chunks salen del board a la vez: cada carta dispara DOMove + DOJump +
        // DORotate, ademas de los tweens de cinta y feedback. Si se supera el
        // limite DOTween descarta tweens silenciosamente y aparecen cartas que
        // "no llegan" a destino.
        DOTween.SetTweensCapacity(1000, 200);

        levelData = Instantiate(levelDataAsset);
        levelData.cells = new List<CellEntry>();

        store = new LevelStore();
        validator = new LevelValidator(palette);
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
        ApplyLayoutAndSpawn();
    }

    void Update()
    {
        if (Keyboard.current.bKey.wasPressedThisFrame && !IsEditingTextField())
            ToggleBuilderMode();
    }

    static bool IsEditingTextField()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var sel = es.currentSelectedGameObject;
        if (sel == null) return false;
        var tmp = sel.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused) return true;
        var legacy = sel.GetComponent<InputField>();
        if (legacy != null && legacy.isFocused) return true;
        return false;
    }

    // --- Builder mode ---

    public void ToggleBuilderMode()
    {
        if (!inBuilderMode)
        {
            inBuilderMode = true;
            editorUI.Show();
        }
        else
        {
            if (!validator.Validate(levelData != null ? levelData.cells : null, out string error))
            {
                Debug.LogError($"[LevelBuilderManager] No se puede salir del builder:\n{error}");
                return;
            }
            inBuilderMode = false;
            SaveCurrentLevel();
            editorUI.Hide();
            ApplyLayoutAndSpawn();
        }
    }

    // --- Carga / guardado / navegacion ---

    public void SaveCurrentLevel()
    {
        if (levelData == null) return;
        levelData.levelName = LevelNaming.BuildName(currentLevelIndex, LevelNaming.ParseNote(levelData.levelName));
        store.Write(currentLevelIndex, JsonUtility.ToJson(levelData, true));
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
            note = LevelNaming.ParseNote(levelData.levelName);
        }
        else
        {
            levelData.cells = new List<CellEntry>();
        }
        levelData.levelName = LevelNaming.BuildName(index, note);
        currentLevelIndex = index;
    }

    public void PrevLevel()
    {
        if (currentLevelIndex <= 0) return;
        SaveCurrentLevel();
        LoadLevel(currentLevelIndex - 1);
        RefreshAfterLevelChange();
    }

    public void NextLevel()
    {
        if (levelCount <= 0) return;
        SaveCurrentLevel();
        if (currentLevelIndex >= levelCount - 1)
        {
            // Loop disimulado: volvemos al primer nivel pero seguimos contando
            // hacia arriba el numero mostrado al jugador.
            displayLoopOffset += levelCount;
            LoadLevel(0);
        }
        else
        {
            LoadLevel(currentLevelIndex + 1);
        }
        RefreshAfterLevelChange();
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index >= levelCount || index == currentLevelIndex) return;
        SaveCurrentLevel();
        // Eleccion manual desde el builder: cortamos el loop visual para no
        // confundir (el panel siempre muestra el indice real).
        displayLoopOffset = 0;
        LoadLevel(index);
        editorUI.Refresh();
    }

    public void CreateNewLevel()
    {
        SaveCurrentLevel();
        int newIndex = levelCount;
        CreateEmptyLevelFile(newIndex);
        levelCount++;
        displayLoopOffset = 0;
        LoadLevel(newIndex);
        editorUI.Refresh();
    }

    public void DeleteCurrentLevel() => DeleteLevel(currentLevelIndex);

    public void DeleteLevel(int index)
    {
        if (levelCount <= 1) return;
        if (index < 0 || index >= levelCount) return;
        if (index != currentLevelIndex) SaveCurrentLevel();
        displayLoopOffset = 0;
        store.Delete(index, levelCount);
        levelCount--;
        int next;
        if (index < currentLevelIndex) next = currentLevelIndex - 1;
        else if (index == currentLevelIndex) next = Mathf.Min(currentLevelIndex, levelCount - 1);
        else next = currentLevelIndex;
        LoadLevel(next);
        editorUI.Refresh();
    }

    public void MoveCurrentLevelUp()   => MoveLevelUp(currentLevelIndex);
    public void MoveCurrentLevelDown() => MoveLevelDown(currentLevelIndex);

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

    public void AutoSave() => SaveCurrentLevel();

    // --- Display de nombres ---

    public string GetLevelDisplayName(int index)
    {
        if (index == currentLevelIndex && levelData != null)
            return LevelNaming.BuildName(index, LevelNaming.ParseNote(levelData.levelName));
        string json = store.Read(index);
        if (string.IsNullOrEmpty(json)) return LevelNaming.BuildName(index, "");
        var peek = JsonUtility.FromJson<LevelNaming.LevelNamePeek>(json);
        return LevelNaming.BuildName(index, LevelNaming.ParseNote(peek != null ? peek.levelName : null));
    }

    public string GetCurrentNote() => levelData != null ? LevelNaming.ParseNote(levelData.levelName) : "";

    public void SetCurrentNote(string note)
    {
        if (levelData == null) return;
        levelData.levelName = LevelNaming.BuildName(currentLevelIndex, note);
        SaveCurrentLevel();
    }

    // --- Belt preset ---

    public string GetCurrentBeltPresetName()
    {
        if (levelData == null) return "";
        return levelData.beltPresetName ?? "";
    }

    /// <summary>Display que aparece en la UI: vacio = "Default".</summary>
    public string GetCurrentBeltPresetDisplayName()
    {
        var n = GetCurrentBeltPresetName();
        return string.IsNullOrEmpty(n) ? "Default" : n;
    }

    /// <summary>Devuelve los nombres de todos los BeltPreset en Resources/BeltPresets, mas "" como primer item (=default).</summary>
    public List<string> GetAvailableBeltPresetNames()
    {
        var names = new List<string> { "" };
        var all = Resources.LoadAll<BeltPreset>("BeltPresets");
        foreach (var p in all)
        {
            if (p == null) continue;
            // El nombre del asset es la clave para Resources.Load.
            names.Add(p.name);
        }
        return names;
    }

    public void SetCurrentBeltPresetName(string name)
    {
        if (levelData == null) return;
        levelData.beltPresetName = name ?? "";
        SaveCurrentLevel();
        // Aplicar de inmediato al belt en escena.
        if (reserveManager != null)
        {
            BeltPreset preset = null;
            if (!string.IsNullOrEmpty(levelData.beltPresetName))
                preset = Resources.Load<BeltPreset>("BeltPresets/" + levelData.beltPresetName);
            reserveManager.ApplyPresetForLevel(preset);
        }
    }

    public void CycleBeltPreset(int dir)
    {
        var names = GetAvailableBeltPresetNames();
        if (names.Count == 0) return;
        int cur = names.IndexOf(GetCurrentBeltPresetName());
        if (cur < 0) cur = 0;
        int next = ((cur + dir) % names.Count + names.Count) % names.Count;
        SetCurrentBeltPresetName(names[next]);
    }

    // --- Internos ---

    void CreateEmptyLevelFile(int index)
    {
        var tmp = ScriptableObject.CreateInstance<LevelData>();
        tmp.levelName = LevelNaming.BuildName(index, "");
        tmp.cells = new List<CellEntry>();
        store.Write(index, JsonUtility.ToJson(tmp, true));
        Destroy(tmp);
    }

    void ApplyLayoutAndSpawn()
    {
        // 1) Reset visual de orders y reserve antes del layout para que el
        //    encuadre de camara use el tamano real, no la escala-0 que pueden
        //    haber quedado del nivel anterior.
        if (ordersManager != null) ordersManager.ResetForLevel();
        if (reserveManager != null)
        {
            reserveManager.Clear();
            // Siempre aplicar el preset que le toca al nivel. Si el nivel no
            // especifica uno, ApplyPresetForLevel(null) cae al defaultPreset
            // del componente. De cualquier modo, regenera puntos y visuales
            // desde cero, dejando atras la cinta del nivel anterior.
            BeltPreset preset = null;
            if (levelData != null && !string.IsNullOrEmpty(levelData.beltPresetName))
                preset = Resources.Load<BeltPreset>("BeltPresets/" + levelData.beltPresetName);
            reserveManager.ApplyPresetForLevel(preset);
        }

        // 2) Layout: board scale, orders reposicion, camara fit.
        if (levelLayout != null) levelLayout.Layout(levelData != null ? levelData.cells : null);

        // 3) Spawnear cartas. Internamente levelFlow.Initialize asigna colores
        //    a las orders y oculta las que no se usan en este nivel.
        if (spawner != null) spawner.SpawnCards();
    }

    void RefreshAfterLevelChange()
    {
        if (inBuilderMode) editorUI.Refresh();
        else ApplyLayoutAndSpawn();
    }
}
