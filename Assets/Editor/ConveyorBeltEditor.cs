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
            "Cada 'Point' es un hijo bajo 'Points'. Movelos con la herramienta de transform. " +
            "Primer y ultimo son portales; los del medio son partes. Rebuild Visuals regenera modelos y orientacion.",
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
}
