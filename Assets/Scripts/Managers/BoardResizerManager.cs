using System.Collections.Generic;
using UnityEngine;

public class BoardResizerManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform boardTransform;
    [SerializeField] private Transform spawnerOrigin;

    [Header("Layout")]
    [Tooltip("Distancia entre centros de celdas vecinas en X (mismo valor que CardsSpawnerManager.spacingX)")]
    [SerializeField] private float slotSize = 0.8f;
    [Tooltip("Margen extra alrededor del grid (en unidades de mundo)")]
    [SerializeField] private float margin = 0.4f;
    [Tooltip("Escala mínima del board")]
    [SerializeField] private float minSize = 0.05f;
    [Tooltip("Unidades de mundo que cubre el mesh del board con scale=1. Plane primitivo de Unity = 10")]
    [SerializeField] private float meshUnitsPerScale = 10f;

    public float SlotSize => slotSize;
    public float Margin => margin;

    public void ResizeToFit(List<CellEntry> cells)
    {
        if (boardTransform == null) return;

        if (cells == null || cells.Count == 0)
        {
            Vector3 s = boardTransform.localScale;
            s.x = minSize;
            s.z = minSize;
            boardTransform.localScale = s;

            if (spawnerOrigin != null)
            {
                Vector3 op = boardTransform.position;
                op.x = spawnerOrigin.position.x;
                op.z = spawnerOrigin.position.z;
                boardTransform.position = op;
            }
            return;
        }

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        float gridW = (maxX - minX + 1) * slotSize;
        float gridH = (maxY - minY + 1) * slotSize;

        float unit = meshUnitsPerScale <= 0.0001f ? 1f : meshUnitsPerScale;
        Vector3 scale = boardTransform.localScale;
        scale.x = Mathf.Max(minSize, (gridW + margin * 2f) / unit);
        scale.z = Mathf.Max(minSize, (gridH + margin * 2f) / unit);
        boardTransform.localScale = scale;

        if (spawnerOrigin != null)
        {
            Vector3 p = boardTransform.position;
            p.x = spawnerOrigin.position.x;
            p.z = spawnerOrigin.position.z;
            boardTransform.position = p;
        }
    }
}
