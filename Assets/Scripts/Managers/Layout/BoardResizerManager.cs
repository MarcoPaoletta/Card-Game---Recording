using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unica responsabilidad: ajustar el tamano (escala) del board para que entre
/// el grid de celdas con un margen dado, y mantenerlo centrado en el origen
/// del spawner. No sabe nada de orders ni camara: eso lo coordina
/// LevelLayoutManager.
/// </summary>
public class BoardResizerManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform boardTransform;
    [SerializeField] private Transform spawnerOrigin;

    [Header("Geometria del board")]
    [Tooltip("Distancia entre centros de celdas vecinas en X (mismo valor que CardsSpawnerManager.spacingX)")]
    [SerializeField] private float slotSize = 0.8f;
    [Tooltip("Escala minima del board")]
    [SerializeField] private float minSize = 0.05f;
    [Tooltip("Unidades de mundo que cubre el mesh del board con scale=1. Plane primitivo de Unity = 10")]
    [SerializeField] private float meshUnitsPerScale = 10f;

    public float SlotSize => slotSize;

    public Bounds GetBoardWorldBounds()
    {
        if (boardTransform == null) return new Bounds();
        float unit = meshUnitsPerScale <= 0.0001f ? 1f : meshUnitsPerScale;
        return new Bounds(
            boardTransform.position,
            new Vector3(boardTransform.localScale.x * unit, 0f, boardTransform.localScale.z * unit));
    }

    /// <summary>
    /// Reescala el board para que entren las celdas con <paramref name="gridMargin"/>
    /// de aire en cada lado, y lo centra sobre <c>spawnerOrigin</c>.
    /// </summary>
    public void ResizeToFit(List<CellEntry> cells, float gridMargin)
    {
        if (boardTransform == null) return;

        float unit = meshUnitsPerScale <= 0.0001f ? 1f : meshUnitsPerScale;
        Vector3 scale = boardTransform.localScale;

        if (cells == null || cells.Count == 0)
        {
            scale.x = minSize;
            scale.z = minSize;
        }
        else
        {
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
            scale.x = Mathf.Max(minSize, (gridW + gridMargin * 2f) / unit);
            scale.z = Mathf.Max(minSize, (gridH + gridMargin * 2f) / unit);
        }
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
