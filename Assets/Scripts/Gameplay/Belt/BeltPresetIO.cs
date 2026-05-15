using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Save / load de presets de cinta (BeltPreset ScriptableObject). Lee los
/// puntos del <see cref="BeltPath"/> y los serializa al asset, o aplica los
/// puntos del asset al path en escena. El <see cref="defaultPreset"/> se usa
/// si el nivel no especifica uno.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BeltPath))]
public class BeltPresetIO : MonoBehaviour
{
    [Tooltip("Preset usado si el nivel no especifica uno (campo beltPresetName vacio).")]
    [SerializeField] private BeltPreset defaultPreset;
    [Tooltip("Preset al que apuntan los menus Save/Load del editor.")]
    [SerializeField] private BeltPreset editorTargetPreset;

    public BeltPreset DefaultPreset => defaultPreset;
    public BeltPreset EditorTargetPreset => editorTargetPreset;

    /// <summary>
    /// Aplica el preset al belt. Si <paramref name="preset"/> es null, usa el
    /// defaultPreset. Regenera el path en escena (los visuales se regeneran
    /// automaticamente via BeltPath.OnPathChanged).
    /// </summary>
    public void ApplyPresetForLevel(BeltPreset preset)
    {
        var p = preset != null ? preset : defaultPreset;
        if (p == null) return;
        var path = GetComponent<BeltPath>();
        if (path == null) return;
        path.ApplyPointsFrom(p.localPoints);
    }

    [ContextMenu("Belt/Save As Preset")]
    public void EditorSaveAsPreset()
    {
        if (editorTargetPreset == null)
        {
            Debug.LogError("[BeltPresetIO] Asignar 'editorTargetPreset' antes de guardar.");
            return;
        }
        var path = GetComponent<BeltPath>();
        if (path == null) return;

        editorTargetPreset.localPoints = path.SnapshotLocalPoints();
#if UNITY_EDITOR
        EditorUtility.SetDirty(editorTargetPreset);
        AssetDatabase.SaveAssetIfDirty(editorTargetPreset);
        Debug.Log($"[BeltPresetIO] Preset '{editorTargetPreset.name}' guardado con {editorTargetPreset.localPoints.Count} puntos.");
#endif
    }

    [ContextMenu("Belt/Load Preset")]
    public void EditorLoadPreset()
    {
        if (editorTargetPreset == null)
        {
            Debug.LogError("[BeltPresetIO] Asignar 'editorTargetPreset' antes de cargar.");
            return;
        }
        var path = GetComponent<BeltPath>();
        if (path == null) return;
        path.ApplyPointsFrom(editorTargetPreset.localPoints);
    }
}
