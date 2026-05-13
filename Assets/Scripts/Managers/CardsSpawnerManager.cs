using System.Collections.Generic;
using UnityEngine;

public class CardsSpawnerManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public Card cardPrefab;
    [SerializeField] public LevelData levelData;
    [SerializeField] public BoardResizerManager boardResizer;

    public void OverrideLevelData(LevelData runtime) { levelData = runtime; }

    [Header("Layout")]
    [Tooltip("Distancia entre centros de celdas vecinas (lado largo de la carta)")]
    [SerializeField] public float spacingX = 0.8f;
    [Tooltip("Distancia entre cartas apiladas dentro de un chunk (lado corto)")]
    [SerializeField] public float spacingZ = 0.2f;

    private float SlotX => spacingX;
    private float SlotZ => spacingX;

    // i-index dentro del stack de 4 cartas que recibe la flecha (carta central del chunk).
    private const int CentralCardIndex = 1;

    public void SpawnCards()
    {
        ClearCards();

        if (boardResizer != null && levelData != null)
            boardResizer.ResizeToFit(levelData.cells);

        if (levelData == null || cardPrefab == null || levelData.cells == null || levelData.cells.Count == 0)
            return;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var cell in levelData.cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        float centerX = (minX + maxX) * 0.5f * SlotX;
        float centerZ = (minY + maxY) * 0.5f * SlotZ;

        float[] order = { -1.5f, -0.5f, 0.5f, 1.5f };

        var arrowHosts = ComputeArrowHosts(levelData.cells, centerX, centerZ);

        foreach (var cell in levelData.cells)
        {
            Color color = new Color(cell.r, cell.g, cell.b, 1f);
            CellDirection direction = (CellDirection)cell.dir;

            float sx = cell.x * SlotX - centerX;
            float sz = centerZ - cell.y * SlotZ;

            bool horizontal = (direction == CellDirection.Left || direction == CellDirection.Right);
            Quaternion rot = CardRotation(direction);

            for (int i = 0; i < 4; i++)
            {
                float ox = horizontal ? order[i] * spacingZ : 0f;
                float oz = horizontal ? 0f : order[i] * spacingZ;

                Vector3 localPos = new Vector3(sx + ox, 0f, sz + oz);
                Vector3 worldPos = transform.TransformPoint(localPos);
                worldPos.y = 0.5f;

                var card = Instantiate(cardPrefab, worldPos, rot, transform);
                bool showArrow = arrowHosts.TryGetValue((cell.x, cell.y, i), out Vector3 arrowWorldPos);
                card.Setup(color, showArrow, arrowWorldPos);
            }
        }
    }

    public void ClearCards()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    // Para cada chunk: elige una carta host (cellPos + i) y devuelve la posición world donde el arrow
    // debe quedar (el centro geométrico real del chunk, que puede caer entre celdas o entre cartas).
    Dictionary<(int, int, int), Vector3> ComputeArrowHosts(List<CellEntry> cells, float centerX, float centerZ)
    {
        var hosts = new Dictionary<(int, int, int), Vector3>();
        var lookup = new Dictionary<Vector2Int, CellEntry>(cells.Count);
        foreach (var c in cells) lookup[new Vector2Int(c.x, c.y)] = c;

        foreach (var endCell in cells)
        {
            if (!endCell.isEnd) continue;
            Vector2Int back = -StepFromDir((CellDirection)endCell.dir);
            var chunk = new List<Vector2Int> { new Vector2Int(endCell.x, endCell.y) };
            var cur = chunk[0];
            while (true)
            {
                var prev = cur + back;
                if (lookup.TryGetValue(prev, out var p) && p.dir == endCell.dir)
                {
                    chunk.Add(prev);
                    cur = prev;
                }
                else break;
            }
            chunk.Reverse();

            var hostCell = chunk[chunk.Count / 2];
            int hostIndex = CentralCardIndex;

            var first = chunk[0];
            var last = chunk[chunk.Count - 1];
            float gx = (first.x + last.x) * 0.5f;
            float gy = (first.y + last.y) * 0.5f;
            float sx = gx * SlotX - centerX;
            float sz = centerZ - gy * SlotZ;
            Vector3 worldPos = transform.TransformPoint(new Vector3(sx, 0f, sz));
            worldPos.y = 0.5f;

            hosts[(hostCell.x, hostCell.y, hostIndex)] = worldPos;
        }
        return hosts;
    }

    static Quaternion CardRotation(CellDirection d)
    {
        switch (d)
        {
            case CellDirection.Right: return Quaternion.Euler(0f,  90f, 0f);
            case CellDirection.Left:  return Quaternion.Euler(0f, -90f, 0f);
            case CellDirection.Up:    return Quaternion.identity;
            case CellDirection.Down:  return Quaternion.Euler(0f, 180f, 0f);
        }
        return Quaternion.identity;
    }

    static Vector2Int StepFromDir(CellDirection d)
    {
        switch (d)
        {
            case CellDirection.Right: return new Vector2Int(+1,  0);
            case CellDirection.Left:  return new Vector2Int(-1,  0);
            case CellDirection.Down:  return new Vector2Int( 0, +1);
            case CellDirection.Up:    return new Vector2Int( 0, -1);
        }
        return Vector2Int.zero;
    }
}
