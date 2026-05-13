using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GridCellView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text coordLabel;

    private LevelEditorUI ui;
    private int gx, gy;

    public void Setup(LevelEditorUI ui, int gx, int gy, Color bg)
    {
        this.ui = ui;
        this.gx = gx;
        this.gy = gy;

        if (background != null) background.color = bg;
        if (coordLabel != null)
        {
            coordLabel.text = $"{gx},{gy}";
            float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            float v = lum > 0.5f ? 0f : 1f;
            coordLabel.color = new Color(v, v, v, 0.55f);
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (ui == null) return;
        if (e.button == PointerEventData.InputButton.Right) ui.OnCellPointerDown(gx, gy, true);
        else if (e.button == PointerEventData.InputButton.Left) ui.OnCellPointerDown(gx, gy, false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (ui != null) ui.OnCellPointerEnter(gx, gy);
    }
}
