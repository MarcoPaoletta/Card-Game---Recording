using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GridCellView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text coordLabel;
    [SerializeField] private TMP_Text arrowLabel;

    private GridView grid;
    private int gx, gy;

    static readonly string[] Arrows = { "↑", "↓", "←", "→" };

    public void Setup(GridView grid, int gx, int gy, Color bg, bool showArrow, int dir)
    {
        this.grid = grid;
        this.gx = gx;
        this.gy = gy;

        if (background != null) background.color = bg;

        float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        float v = lum > 0.5f ? 0f : 1f;
        Color textCol = new Color(v, v, v, 0.55f);

        if (coordLabel != null)
        {
            coordLabel.text = $"{gx},{gy}";
            coordLabel.color = textCol;
        }

        if (arrowLabel != null)
        {
            if (showArrow && dir >= 0 && dir < Arrows.Length)
            {
                arrowLabel.gameObject.SetActive(true);
                arrowLabel.text = Arrows[dir];
                arrowLabel.color = new Color(v, v, v, 0.95f);
            }
            else
            {
                arrowLabel.gameObject.SetActive(false);
            }
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (grid == null) return;
        if (e.button == PointerEventData.InputButton.Right) grid.OnCellPointerDown(gx, gy, true);
        else if (e.button == PointerEventData.InputButton.Left) grid.OnCellPointerDown(gx, gy, false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (grid != null) grid.OnCellPointerEnter(gx, gy);
    }
}
