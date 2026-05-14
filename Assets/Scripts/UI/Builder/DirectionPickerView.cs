using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup runtime con 4 botones (arriba/abajo/izq/der) que se construye sobre una
/// celda del grid del builder cuando el usuario pinta una sola celda y tiene que
/// elegirle direccion. Cero prefabs: se ensambla por codigo.
/// </summary>
public class DirectionPickerView
{
    private readonly RectTransform host;
    private readonly TMP_FontAsset font;
    private GameObject root;
    private Action<CellDirection> onPicked;

    public DirectionPickerView(RectTransform host, TMP_FontAsset font)
    {
        this.host = host;
        this.font = font;
    }

    public bool IsOpen => root != null;

    public void Show(RectTransform cellRT, float cellSize, Action<CellDirection> onPicked)
    {
        Hide();
        if (cellRT == null || host == null) return;

        this.onPicked = onPicked;

        root = new GameObject("DirectionPicker", typeof(RectTransform));
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.SetParent(host, worldPositionStays: false);
        rootRT.position = cellRT.position;
        rootRT.sizeDelta = Vector2.zero;
        rootRT.localScale = Vector3.one;

        float btnSize = cellSize * 0.9f;
        CreateButton(rootRT, "↑", new Vector2(0f,  cellSize), CellDirection.Up,    btnSize);
        CreateButton(rootRT, "↓", new Vector2(0f, -cellSize), CellDirection.Down,  btnSize);
        CreateButton(rootRT, "←", new Vector2(-cellSize, 0f), CellDirection.Left,  btnSize);
        CreateButton(rootRT, "→", new Vector2( cellSize, 0f), CellDirection.Right, btnSize);
    }

    public void Hide()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
        }
        onPicked = null;
    }

    void CreateButton(RectTransform parent, string arrow, Vector2 offset, CellDirection dir, float size)
    {
        var go = new GameObject($"Btn_{dir}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.55f, 0.95f, 1f);
        colors.pressedColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        btn.colors = colors;

        var txtGO = new GameObject("Label", typeof(RectTransform));
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.SetParent(rt, false);
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = arrow;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.fontSize = size * 0.6f;
        txt.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            var cb = onPicked;
            Hide();
            cb?.Invoke(dir);
        });
    }
}
