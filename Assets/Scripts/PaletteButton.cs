using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PaletteButton : MonoBehaviour
{
    [SerializeField] private Image swatch;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    private Color paletteColor;
    private Action<Color> onPicked;

    void Reset()
    {
        button = GetComponent<Button>();
    }

    public void Setup(PaletteEntry entry, Action<Color> onPicked)
    {
        paletteColor = entry.color;
        this.onPicked = onPicked;

        if (label != null) label.text = entry.label;
        if (swatch != null)
        {
            // El "Empty" (alpha 0) se muestra como gris oscuro para que sea visible.
            swatch.color = entry.color.a < 0.01f
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(entry.color.r, entry.color.g, entry.color.b, 1f);
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onPicked?.Invoke(paletteColor));
        }
    }
}
