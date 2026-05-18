using UnityEngine;

public class LevelsPanelView : MonoBehaviour
{
    [SerializeField] private LevelBuilderManager manager;
    [SerializeField] private RectTransform listContainer;
    [SerializeField] private LevelRow rowTemplate;
    [Tooltip("Si esta activo, el panel queda visible siempre (no se auto-oculta en Awake). Util para layouts donde la lista es parte del HUD principal del builder.")]
    [SerializeField] private bool keepOpen;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (!keepOpen) gameObject.SetActive(false);
        else Refresh();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (keepOpen) return;
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (listContainer == null || rowTemplate == null || manager == null) return;

        for (int i = listContainer.childCount - 1; i >= 0; i--)
        {
            var child = listContainer.GetChild(i).gameObject;
            if (child == rowTemplate.gameObject) continue;
            Destroy(child);
        }

        int count = manager.LevelCount;
        int current = manager.CurrentLevelIndex;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            row.Setup(
                label: manager.GetLevelDisplayName(idx),
                isCurrent: idx == current,
                canMoveUp: idx > 0,
                canMoveDown: idx < count - 1,
                canDelete: count > 1,
                onSelect: () => manager.SelectLevel(idx),
                onUp:     () => manager.MoveLevelUp(idx),
                onDown:   () => manager.MoveLevelDown(idx),
                onDelete: () => manager.DeleteLevel(idx));
        }
    }
}
