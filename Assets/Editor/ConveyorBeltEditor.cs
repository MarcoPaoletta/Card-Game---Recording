using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConveyorBelt))]
public class ConveyorBeltEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var belt = (ConveyorBelt)target;

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
                Undo.RegisterFullObjectHierarchyUndo(belt.gameObject, "Add belt point");
                belt.EditorAddPoint();
                EditorUtility.SetDirty(belt);
            }
            if (GUILayout.Button("- Remove Last"))
            {
                Undo.RegisterFullObjectHierarchyUndo(belt.gameObject, "Remove belt point");
                belt.EditorRemoveLastPoint();
                EditorUtility.SetDirty(belt);
            }
            if (GUILayout.Button("Rebuild Visuals"))
            {
                Undo.RegisterFullObjectHierarchyUndo(belt.gameObject, "Rebuild belt visuals");
                belt.RebuildVisuals();
                EditorUtility.SetDirty(belt);
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Preset (Editor Target)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save As Preset"))
            {
                belt.EditorSaveAsPreset();
            }
            if (GUILayout.Button("Load Preset"))
            {
                Undo.RegisterFullObjectHierarchyUndo(belt.gameObject, "Load belt preset");
                belt.EditorLoadPreset();
                EditorUtility.SetDirty(belt);
            }
        }
    }

    void OnSceneGUI()
    {
        var belt = (ConveyorBelt)target;
        var points = belt.transform.Find("Points");
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

            // Boton clickeable: al apretar selecciona el GameObject del Point
            // y los handles de transform aparecen automaticamente.
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
