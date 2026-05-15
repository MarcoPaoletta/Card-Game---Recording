using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor que agrupa los controles de los 3 componentes del belt
/// (<see cref="BeltPath"/>, <see cref="BeltVisuals"/>, <see cref="BeltPresetIO"/>)
/// en un solo inspector cuando seleccionas el GameObject ConveyorBelt.
/// </summary>
[CustomEditor(typeof(BeltPath))]
public class ConveyorBeltEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var path = (BeltPath)target;
        var visuals = path.GetComponent<BeltVisuals>();
        var presetIO = path.GetComponent<BeltPresetIO>();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Tip: en scene view ves un circulo en cada Point. Hace click sobre el circulo " +
            "para seleccionar ese punto y moverlo con la tool de transform. Cada vez que sueltes " +
            "el drag se regenera la cinta sola.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Add Point"))
            {
                Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Add belt point");
                path.EditorAddPoint();
                EditorUtility.SetDirty(path);
            }
            if (GUILayout.Button("- Remove Last"))
            {
                Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Remove belt point");
                path.EditorRemoveLastPoint();
                EditorUtility.SetDirty(path);
            }
            if (GUILayout.Button("Rebuild Visuals"))
            {
                Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Rebuild belt visuals");
                if (visuals != null) visuals.Rebuild();
                EditorUtility.SetDirty(path);
            }
        }

        if (presetIO != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preset (Editor Target)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save As Preset"))
                {
                    presetIO.EditorSaveAsPreset();
                }
                if (GUILayout.Button("Load Preset"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Load belt preset");
                    presetIO.EditorLoadPreset();
                    EditorUtility.SetDirty(path);
                }
            }
        }
    }

    void OnSceneGUI()
    {
        var path = (BeltPath)target;
        var points = path.PointsContainer;
        if (points == null) return;

        int n = points.childCount;
        for (int i = 0; i < n; i++)
        {
            var pt = points.GetChild(i);
            float size = HandleUtility.GetHandleSize(pt.position) * 0.18f;

            bool isPortal = (i == 0 || i == n - 1);
            Handles.color = isPortal
                ? new Color(0.3f, 0.85f, 1f, 0.95f)
                : new Color(1f, 0.85f, 0.2f, 0.95f);

            if (Handles.Button(pt.position, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
            {
                Selection.activeGameObject = pt.gameObject;
                Event.current.Use();
            }

            Handles.color = Color.white;
            Handles.Label(pt.position + Vector3.up * size * 1.5f, $"P{i}{(isPortal ? " (portal)" : "")}");
        }
    }
}
