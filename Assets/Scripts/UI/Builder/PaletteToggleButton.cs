using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boton del builder que cambia la fuente de paleta entre Default y Level.
/// Mantiene el label sincronizado con el estado actual de PaletteView, y se
/// deshabilita cuando el nivel no tiene paleta propia (no hay nada a lo que
/// switchear).
/// </summary>
public class PaletteToggleButton : MonoBehaviour
{
    [SerializeField] private PaletteView paletteView;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string defaultText = "Paleta: Default";
    [SerializeField] private string levelText = "Paleta: Nivel";

    void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
        if (paletteView != null) paletteView.PaletteChanged += Refresh;
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
        if (paletteView != null) paletteView.PaletteChanged -= Refresh;
    }

    void OnEnable() => Refresh();

    void OnClicked()
    {
        if (paletteView != null) paletteView.ToggleSource();
    }

    void Refresh()
    {
        if (label != null && paletteView != null)
            label.text = paletteView.Source == PaletteView.PaletteSource.Level ? levelText : defaultText;

        if (button != null && paletteView != null)
        {
            // Si el nivel no tiene paleta y estamos en Default, el toggle no tiene a donde ir.
            bool isLevel = paletteView.Source == PaletteView.PaletteSource.Level;
            button.interactable = isLevel || paletteView.HasLevelPalette;
        }
    }
}
