using UnityEngine;

public class PaletteView : MonoBehaviour
{
    [SerializeField] private ColorPalette palette;
    [SerializeField] private RectTransform paletteContainer;
    [SerializeField] private PaletteButton paletteButtonPrefab;
    [SerializeField] private GridView gridView;

    void Awake()
    {
        BuildPalette();
    }

    void BuildPalette()
    {
        if (paletteContainer == null || paletteButtonPrefab == null || palette == null || palette.entries == null)
            return;

        for (int i = paletteContainer.childCount - 1; i >= 0; i--)
            Destroy(paletteContainer.GetChild(i).gameObject);

        foreach (var entry in palette.entries)
        {
            var btn = Instantiate(paletteButtonPrefab, paletteContainer);
            btn.Setup(entry, OnColorSelected);
        }
    }

    void OnColorSelected(Color c)
    {
        if (gridView != null && gridView.Painter != null) gridView.Painter.SelectedColor = c;
    }
}
