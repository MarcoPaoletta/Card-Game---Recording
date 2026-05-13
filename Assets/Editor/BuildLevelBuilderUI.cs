using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public static class BuildLevelBuilderUI
{
    const string PrefabsFolder = "Assets/Prefabs/UI";
    const string PaletteButtonPath = "Assets/Prefabs/UI/PaletteButton.prefab";
    const string GridCellPath = "Assets/Prefabs/UI/GridCell.prefab";
    const string PaletteAssetPath = "Assets/Data/DefaultPalette.asset";

    [MenuItem("Tools/Build Level Builder UI")]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabsFolder);

        CreatePaletteButtonPrefab();
        CreateGridCellPrefab();
        BuildSceneUI();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildLevelBuilderUI] OK");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void CreatePaletteButtonPrefab()
    {
        var go = new GameObject("PaletteButton", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(140, 60);

        var img = go.AddComponent<Image>();
        img.color = Color.gray;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var script = go.AddComponent<PaletteButton>();

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = (RectTransform)lblGO.transform;
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Label";
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var so = new SerializedObject(script);
        so.FindProperty("swatch").objectReferenceValue = img;
        so.FindProperty("label").objectReferenceValue = tmp;
        so.FindProperty("button").objectReferenceValue = btn;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, PaletteButtonPath);
        Object.DestroyImmediate(go);
    }

    static void CreateGridCellPrefab()
    {
        var go = new GameObject("GridCell", typeof(RectTransform));
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.14f, 0.16f);
        var script = go.AddComponent<GridCellView>();

        var coordGO = new GameObject("Coord", typeof(RectTransform));
        coordGO.transform.SetParent(go.transform, false);
        var coordRT = (RectTransform)coordGO.transform;
        coordRT.anchorMin = Vector2.zero; coordRT.anchorMax = Vector2.one;
        coordRT.offsetMin = new Vector2(4, 2); coordRT.offsetMax = new Vector2(-2, -2);
        var coordTmp = coordGO.AddComponent<TextMeshProUGUI>();
        coordTmp.text = "0,0";
        coordTmp.fontSize = 18;
        coordTmp.fontStyle = FontStyles.Bold;
        coordTmp.alignment = TextAlignmentOptions.BottomLeft;
        coordTmp.color = new Color(1, 1, 1, 0.55f);
        coordTmp.raycastTarget = false;

        // Flecha de dirección (sólo visible en celdas marcadas como end del chunk)
        var arrowGO = new GameObject("Arrow", typeof(RectTransform));
        arrowGO.transform.SetParent(go.transform, false);
        var arrowRT = (RectTransform)arrowGO.transform;
        arrowRT.anchorMin = Vector2.zero; arrowRT.anchorMax = Vector2.one;
        arrowRT.offsetMin = Vector2.zero; arrowRT.offsetMax = Vector2.zero;
        var arrowTmp = arrowGO.AddComponent<TextMeshProUGUI>();
        arrowTmp.text = "→";
        arrowTmp.fontSize = 48;
        arrowTmp.fontStyle = FontStyles.Bold;
        arrowTmp.alignment = TextAlignmentOptions.Center;
        arrowTmp.color = Color.white;
        arrowTmp.raycastTarget = false;
        arrowGO.SetActive(false);

        var so = new SerializedObject(script);
        so.FindProperty("background").objectReferenceValue = img;
        so.FindProperty("coordLabel").objectReferenceValue = coordTmp;
        so.FindProperty("arrowLabel").objectReferenceValue = arrowTmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, GridCellPath);
        Object.DestroyImmediate(go);
    }

    static void BuildSceneUI()
    {
        var editorUI = Object.FindAnyObjectByType<LevelEditorUI>();
        if (editorUI == null)
        {
            Debug.LogError("LevelEditorUI not in scene.");
            return;
        }

        // EventSystem con InputSystemUIInputModule (necesario para click derecho con New Input System)
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // Limpiar hijos previos del GO de LevelEditorUI
        var hostT = editorUI.transform;
        for (int i = hostT.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(hostT.GetChild(i).gameObject);

        // ── Canvas raíz ──
        var canvasGO = new GameObject("BuilderCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(hostT, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // BG
        var bg = MakePanel(canvasGO.transform, "BG", new Color(0, 0, 0, 0.90f));
        Stretch(bg);

        // Main
        var main = MakePanel(bg, "Main", new Color(0.10f, 0.10f, 0.12f));
        Anchors(main, .01f, .01f, .99f, .99f);

        // ── Sidebar ──
        var side = MakePanel(main, "Sidebar", new Color(0.07f, 0.07f, 0.09f));
        Anchors(side, 0f, 0f, .22f, 1f);

        MakeTMP(side, "TitleLabel", "LEVEL\nBUILDER", 36, Color.white,
            0, .90f, 1, 1f, TextAlignmentOptions.Center);

        var levelNameTMP = MakeTMP(side, "LevelName", "", 24, new Color(0.4f, 0.95f, 1f),
            .04f, .83f, .96f, .90f, TextAlignmentOptions.Center);

        var prevBtn = MakeButton(side, "PrevButton", "◀ Prev", new Color(.15f, .25f, .45f),
            0.02f, .76f, .48f, .82f);
        var nextBtn = MakeButton(side, "NextButton", "Next ▶", new Color(.15f, .25f, .45f),
            0.52f, .76f, .98f, .82f);

        var saveBtn = MakeButton(side, "SaveExitButton", "💾 Save & Exit [ B ]",
            new Color(.07f, .43f, .16f),
            0.02f, .68f, .98f, .75f);

        MakeTMP(side, "ColorHeader", "─── COLOR ───", 18, Color.gray,
            0, .63f, 1, .67f, TextAlignmentOptions.Center);

        // Palette container (3 cols × 3 filas)
        var paletteGO = new GameObject("Palette", typeof(RectTransform));
        paletteGO.transform.SetParent(side.transform, false);
        var paletteRT = (RectTransform)paletteGO.transform;
        Anchors(paletteRT, 0.02f, .36f, .98f, .63f);
        var glg = paletteGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(95, 55);
        glg.spacing = new Vector2(6, 6);
        glg.padding = new RectOffset(4, 4, 4, 4);
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperCenter;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;

        MakeTMP(side, "NavLabel", "NAVEGAR:\nWASD / ← ↑ → ↓\nRueda: arrastrar\nDerecho: borrar",
            16, new Color(.6f, .6f, .6f), 0.02f, .15f, .98f, .33f, TextAlignmentOptions.TopLeft);

        var coordsTMP = MakeTMP(side, "CoordsLabel", "", 16, new Color(.45f, .9f, .45f),
            0.02f, .04f, .98f, .14f, TextAlignmentOptions.Center);

        // ── Grid Panel ──
        const int VIEWPORT_W = 17, VIEWPORT_H = 12;
        const int CELL_PX = 80, CELL_GAP = 2;
        float totalW = VIEWPORT_W * (CELL_PX + CELL_GAP) + CELL_GAP;
        float totalH = VIEWPORT_H * (CELL_PX + CELL_GAP) + CELL_GAP;

        var gridPanel = MakePanel(main, "GridPanel", new Color(.06f, .06f, .08f));
        gridPanel.anchorMin = gridPanel.anchorMax = new Vector2(0.5f, 0.5f);
        gridPanel.pivot = new Vector2(0.5f, 0.5f);
        gridPanel.anchoredPosition = new Vector2(180f, 0f);
        gridPanel.sizeDelta = new Vector2(totalW, totalH);

        var gridGLG = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
        gridGLG.cellSize = new Vector2(CELL_PX, CELL_PX);
        gridGLG.spacing = new Vector2(CELL_GAP, CELL_GAP);
        gridGLG.padding = new RectOffset(CELL_GAP, CELL_GAP, CELL_GAP, CELL_GAP);
        gridGLG.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridGLG.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridGLG.childAlignment = TextAnchor.UpperLeft;
        gridGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridGLG.constraintCount = VIEWPORT_W;

        // ── Wire LevelEditorUI references ──
        var so = new SerializedObject(editorUI);
        so.FindProperty("builderCanvas").objectReferenceValue = canvasGO;
        so.FindProperty("levelNameText").objectReferenceValue = levelNameTMP;
        so.FindProperty("coordsText").objectReferenceValue = coordsTMP;
        so.FindProperty("gridContainer").objectReferenceValue = gridPanel;
        so.FindProperty("paletteContainer").objectReferenceValue = paletteRT;
        so.FindProperty("prevButton").objectReferenceValue = prevBtn;
        so.FindProperty("nextButton").objectReferenceValue = nextBtn;
        so.FindProperty("saveExitButton").objectReferenceValue = saveBtn;

        var paletteBtnPrefab = AssetDatabase.LoadAssetAtPath<PaletteButton>(PaletteButtonPath);
        var gridCellPrefab = AssetDatabase.LoadAssetAtPath<GridCellView>(GridCellPath);
        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(PaletteAssetPath);
        so.FindProperty("paletteButtonPrefab").objectReferenceValue = paletteBtnPrefab;
        so.FindProperty("gridCellPrefab").objectReferenceValue = gridCellPrefab;
        so.FindProperty("palette").objectReferenceValue = palette;
        so.ApplyModifiedPropertiesWithoutUndo();

        canvasGO.SetActive(false); // builder oculto al inicio
    }

    static RectTransform MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI MakeTMP(Transform parent, string name, string text, int size, Color color,
                                    float x0, float y0, float x1, float y1, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Anchors((RectTransform)go.transform, x0, y0, x1, y1);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label, Color color,
                             float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Anchors((RectTransform)go.transform, x0, y0, x1, y1);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var tmpGO = new GameObject("Label", typeof(RectTransform));
        tmpGO.transform.SetParent(go.transform, false);
        var rt = (RectTransform)tmpGO.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = tmpGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }

    static void Anchors(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
