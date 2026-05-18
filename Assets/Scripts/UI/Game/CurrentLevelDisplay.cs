using TMPro;
using UnityEngine;

/// <summary>
/// Muestra el numero de nivel actual. Refresca cada Update consultando al
/// LevelBuilderManager (es O(1) y el LBM ya tiene el dato cacheado).
/// </summary>
public class CurrentLevelDisplay : MonoBehaviour
{
    [SerializeField] private LevelBuilderManager builder;
    [SerializeField] private TMP_Text label;

    void LateUpdate()
    {
        if (builder == null || label == null) return;
        label.text = $"Nivel {builder.DisplayLevelNumber}";
    }
}
