using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control del level builder para ver y cambiar el preset de cinta del nivel
/// actual. Muestra el nombre del preset y dos botones para ciclar entre los
/// presets disponibles en Resources/BeltPresets/. El primer item es siempre
/// el default (cinta tal como esta autorada en el componente ConveyorBelt
/// con su defaultPreset).
/// </summary>
public class BuilderBeltPresetSelector : MonoBehaviour
{
    [SerializeField] private LevelBuilderManager manager;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(() => Cycle(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Cycle(+1));
    }

    void Cycle(int dir)
    {
        if (manager == null) return;
        manager.CycleBeltPreset(dir);
        Refresh();
    }

    public void Refresh()
    {
        if (manager == null || label == null) return;
        label.text = manager.GetCurrentBeltPresetDisplayName();
    }
}
