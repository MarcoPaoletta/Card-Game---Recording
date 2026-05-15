using UnityEngine;

public class LevelEditorUI : MonoBehaviour
{
    [SerializeField] private GameObject builderCanvas;

    [Header("Sub-views")]
    [SerializeField] private GridView gridView;
    [SerializeField] private PaletteView paletteView;
    [SerializeField] private BuilderNameDisplay nameDisplay;
    [SerializeField] private BuilderNoteInput noteInput;
    [SerializeField] private LevelsPanelView levelsPanel;
    [SerializeField] private BuilderBeltPresetSelector beltPresetSelector;

    public void OverrideLevelData(LevelData runtime)
    {
        if (gridView != null) gridView.OverrideLevelData(runtime);
    }

    public void Show() { builderCanvas?.SetActive(true); Refresh(); }
    public void Hide() { builderCanvas?.SetActive(false); }

    public void Refresh()
    {
        if (nameDisplay != null) nameDisplay.Refresh();
        if (noteInput  != null) noteInput.Refresh();
        if (gridView   != null) gridView.Refresh();
        if (levelsPanel != null && levelsPanel.IsOpen) levelsPanel.Refresh();
        if (beltPresetSelector != null) beltPresetSelector.Refresh();
    }
}
