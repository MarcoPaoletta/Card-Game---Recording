using System;
using System.Collections.Generic;
using UnityEngine;

public class PaletteView : MonoBehaviour
{
    public enum PaletteSource { Default, Level }

    [SerializeField] private ColorPalette palette;
    [SerializeField] private RectTransform paletteContainer;
    [SerializeField] private PaletteButton paletteButtonPrefab;
    [SerializeField] private GridView gridView;

    private LevelData currentLevel;
    private PaletteSource source = PaletteSource.Default;

    /// <summary>Disparado cada vez que la paleta visible cambia (source o level).</summary>
    public event Action PaletteChanged;

    public PaletteSource Source => source;

    public bool HasLevelPalette =>
        currentLevel != null
        && currentLevel.levelPalette != null
        && currentLevel.levelPalette.Count > 0;

    void Awake()
    {
        Build();
    }

    public void SetCurrentLevel(LevelData level)
    {
        currentLevel = level;
        Build();
    }

    public void SetSource(PaletteSource s)
    {
        if (s == PaletteSource.Level && !HasLevelPalette) return;
        if (source == s) return;
        source = s;
        Build();
    }

    public void ToggleSource()
    {
        SetSource(source == PaletteSource.Default ? PaletteSource.Level : PaletteSource.Default);
    }

    public void Refresh()
    {
        // Si el nivel actual perdio su paleta, volver a Default automaticamente.
        if (source == PaletteSource.Level && !HasLevelPalette) source = PaletteSource.Default;
        Build();
    }

    void Build()
    {
        if (paletteContainer == null || paletteButtonPrefab == null) return;

        for (int i = paletteContainer.childCount - 1; i >= 0; i--)
            Destroy(paletteContainer.GetChild(i).gameObject);

        var entries = GetActiveEntries();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            var btn = Instantiate(paletteButtonPrefab, paletteContainer);
            btn.Setup(entry, OnColorSelected);
        }

        PaletteChanged?.Invoke();
    }

    IEnumerable<PaletteEntry> GetActiveEntries()
    {
        if (source == PaletteSource.Level && HasLevelPalette)
            return currentLevel.levelPalette;
        if (palette != null && palette.entries != null)
            return palette.entries;
        return null;
    }

    void OnColorSelected(Color c)
    {
        if (gridView != null && gridView.Painter != null) gridView.Painter.SelectedColor = c;
    }
}
