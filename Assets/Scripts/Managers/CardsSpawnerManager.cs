using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardsSpawnerManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public Card cardPrefab;
    [SerializeField] public LevelData levelData;
    [SerializeField] public BoardResizerManager boardResizer;
    [SerializeField] public OrdersManager ordersManager;
    [SerializeField] public LevelFlowManager levelFlow;
    [SerializeField] public ConveyorBelt reserveManager;

    public void OverrideLevelData(LevelData runtime) { levelData = runtime; }

    [Header("Layout")]
    [Tooltip("Distancia entre centros de celdas vecinas (lado largo de la carta)")]
    [SerializeField] public float spacingX = 0.8f;
    [Tooltip("Distancia entre cartas apiladas dentro de un chunk (lado corto)")]
    [SerializeField] public float spacingZ = 0.2f;

    /// <summary>Distancia entre cartas en el board (eje corto). Reusada por ConveyorBelt.</summary>
    public float CardSpacing => spacingZ;

    [Header("Movimiento")]
    [Tooltip("Margen (en unidades de mundo) que se respeta del borde del board cuando un chunk sale.")]
    [SerializeField] private float boardEdgeMargin = 0.3f;
    [Tooltip("Padding extra para el BoxCollider de cada chunk (en cada eje, sumado a ambos lados).")]
    [SerializeField] private Vector3 chunkColliderPadding = new Vector3(0.4f, 1.2f, 0.4f);

    private float SlotX => spacingX;
    private float SlotZ => spacingX;
    private const int CentralCardIndex = 1;

    private readonly List<Chunk> chunks = new List<Chunk>();

    public void SpawnCards()
    {
        ClearCards();
        chunks.Clear();

        // Reset de orders, reserve y plan de flujo para el nuevo nivel.
        if (ordersManager != null) ordersManager.ResetForLevel();
        if (reserveManager != null) reserveManager.Clear();
        if (levelFlow != null) levelFlow.Initialize(levelData != null ? levelData.cells : null);

        if (levelData == null || cardPrefab == null || levelData.cells == null || levelData.cells.Count == 0)
            return;

        // Alinear el origen del spawner al centro del board en XZ para que las
        // cartas siempre caigan sobre el board, sin importar donde quedo el GO
        // del spawner en escena.
        if (boardResizer != null)
        {
            var c = boardResizer.GetBoardWorldBounds().center;
            var p = transform.position;
            p.x = c.x;
            p.z = c.z;
            transform.position = p;
        }

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

        var lookup = new Dictionary<Vector2Int, CellEntry>(levelData.cells.Count);
        foreach (var c in levelData.cells) lookup[new Vector2Int(c.x, c.y)] = c;

        int chunkIndex = 0;
        foreach (var chunkInfo in ExtractChunks(levelData.cells, lookup))
        {
            var chunkGO = new GameObject($"Chunk_{chunkIndex++}_{chunkInfo.direction}");
            chunkGO.transform.SetParent(transform, false);
            var chunk = chunkGO.AddComponent<Chunk>();

            var hostCell = chunkInfo.cells[chunkInfo.cells.Count / 2];
            var first = chunkInfo.cells[0];
            var last = chunkInfo.cells[chunkInfo.cells.Count - 1];
            float arrowGx = (first.x + last.x) * 0.5f;
            float arrowGy = (first.y + last.y) * 0.5f;
            Vector3 arrowWorldPos = transform.TransformPoint(new Vector3(
                arrowGx * SlotX - centerX, 0f, centerZ - arrowGy * SlotZ));
            arrowWorldPos.y = 0.5f;

            bool horizontal = (chunkInfo.direction == CellDirection.Left || chunkInfo.direction == CellDirection.Right);
            Quaternion rot = CardRotation(chunkInfo.direction);

            foreach (var cellPos in chunkInfo.cells)
            {
                var entry = lookup[cellPos];
                Color color = new Color(entry.r, entry.g, entry.b, 1f);

                float sx = entry.x * SlotX - centerX;
                float sz = centerZ - entry.y * SlotZ;

                for (int i = 0; i < 4; i++)
                {
                    float ox = horizontal ? order[i] * spacingZ : 0f;
                    float oz = horizontal ? 0f : order[i] * spacingZ;

                    Vector3 worldPos = transform.TransformPoint(new Vector3(sx + ox, 0f, sz + oz));
                    worldPos.y = 0.5f;

                    var card = Instantiate(cardPrefab, worldPos, rot, chunkGO.transform);
                    bool showArrow = (cellPos == hostCell && i == CentralCardIndex);
                    card.Setup(color, showArrow, arrowWorldPos);
                }
            }

            // Re-pivotear el transform del chunk al centroide de sus cartas.
            // Sin esto, scales como DOPunchScale (que se aplican al chunk
            // entero cuando hay un choque) escalan desde la posicion vieja
            // del chunk (lejana a las cartas), tirando los cards al origen
            // en vez de pulsarlos en el lugar.
            RePivotToChildrenCentroid(chunkGO.transform);

            AddChunkCollider(chunkGO);
            var firstEntry = lookup[chunkInfo.cells[0]];
            Color chunkColor = new Color(firstEntry.r, firstEntry.g, firstEntry.b, 1f);
            chunk.Init(this, ordersManager, reserveManager, chunkInfo.direction, chunkInfo.cells, chunkColor);
            chunks.Add(chunk);
        }
    }

    /// <summary>
    /// Mueve el transform del chunk a la centroide de sus cartas, preservando
    /// las posiciones/rotaciones en mundo de las cartas. Asi los scales sobre
    /// el chunk pivotean desde el centro visual y no desde una esquina arbitraria.
    /// </summary>
    static void RePivotToChildrenCentroid(Transform chunkRoot)
    {
        int n = chunkRoot.childCount;
        if (n == 0) return;

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++) centroid += chunkRoot.GetChild(i).position;
        centroid /= n;

        var wp = new Vector3[n];
        var wr = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            wp[i] = chunkRoot.GetChild(i).position;
            wr[i] = chunkRoot.GetChild(i).rotation;
        }

        chunkRoot.position = centroid;

        for (int i = 0; i < n; i++)
        {
            chunkRoot.GetChild(i).position = wp[i];
            chunkRoot.GetChild(i).rotation = wr[i];
        }
    }

    void AddChunkCollider(GameObject chunkGO)
    {
        int childCount = chunkGO.transform.childCount;
        if (childCount == 0) return;

        var bounds = new Bounds(chunkGO.transform.GetChild(0).position, Vector3.zero);
        for (int i = 1; i < childCount; i++)
            bounds.Encapsulate(chunkGO.transform.GetChild(i).position);
        bounds.Expand(chunkColliderPadding);

        var box = chunkGO.AddComponent<BoxCollider>();
        box.center = bounds.center - chunkGO.transform.position;
        box.size = bounds.size;
    }

    public void ClearCards()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = mouse.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 200f))
        {
            var chunk = hit.collider.GetComponentInParent<Chunk>();
            if (chunk != null) chunk.OnClicked();
        }
    }

    public void UnregisterChunk(Chunk c) { chunks.Remove(c); }

    public (Chunk blocker, float distance) CalculateChunkMove(Chunk chunk)
    {
        Vector2Int dirGrid = StepFromDir(chunk.Direction);
        Vector3 dirWorld = Chunk.WorldStep(chunk.Direction);

        float distanceToEdge = DistanceToBoardEdge(chunk, dirWorld);
        int maxSteps = Mathf.Max(1, Mathf.CeilToInt(distanceToEdge / SlotX) + 1);

        Vector2Int leading = LeadingCell(chunk, dirGrid);
        for (int step = 1; step <= maxSteps; step++)
        {
            var checkPos = leading + step * dirGrid;
            var blocker = FindChunkContaining(checkPos, chunk);
            if (blocker != null)
            {
                // Distancia que el chunk puede avanzar antes de chocar con el blocker:
                // (step - 1) celdas vacias entre el leading actual y la trailing card del blocker.
                float bumpDistance = Mathf.Max(0f, (step - 1) * SlotX);
                return (blocker, bumpDistance);
            }
        }

        return (null, Mathf.Max(0f, distanceToEdge - boardEdgeMargin));
    }

    Chunk FindChunkContaining(Vector2Int pos, Chunk except)
    {
        foreach (var c in chunks)
        {
            if (c == null || c == except) continue;
            if (c.Cells.Contains(pos)) return c;
        }
        return null;
    }

    float DistanceToBoardEdge(Chunk chunk, Vector3 dirWorld)
    {
        if (boardResizer == null) return 0f;
        Bounds b = boardResizer.GetBoardWorldBounds();

        Vector3[] corners = {
            new Vector3(b.min.x, 0f, b.min.z),
            new Vector3(b.min.x, 0f, b.max.z),
            new Vector3(b.max.x, 0f, b.min.z),
            new Vector3(b.max.x, 0f, b.max.z),
        };
        float boardEdgeInDir = float.NegativeInfinity;
        foreach (var c in corners)
            boardEdgeInDir = Mathf.Max(boardEdgeInDir, Vector3.Dot(c, dirWorld));

        float leadingInDir = float.NegativeInfinity;
        foreach (Transform card in chunk.transform)
            leadingInDir = Mathf.Max(leadingInDir, Vector3.Dot(card.position, dirWorld));

        return boardEdgeInDir - leadingInDir;
    }

    static Vector2Int LeadingCell(Chunk chunk, Vector2Int dirGrid)
    {
        Vector2Int best = chunk.Cells[0];
        int bestScore = best.x * dirGrid.x + best.y * dirGrid.y;
        foreach (var c in chunk.Cells)
        {
            int s = c.x * dirGrid.x + c.y * dirGrid.y;
            if (s > bestScore) { best = c; bestScore = s; }
        }
        return best;
    }

    struct ChunkInfo { public CellDirection direction; public List<Vector2Int> cells; }

    static IEnumerable<ChunkInfo> ExtractChunks(List<CellEntry> cells, Dictionary<Vector2Int, CellEntry> lookup)
    {
        foreach (var endCell in cells)
        {
            if (!endCell.isEnd) continue;
            var dir = (CellDirection)endCell.dir;
            Vector2Int back = -StepFromDir(dir);
            var chunkCells = new List<Vector2Int> { new Vector2Int(endCell.x, endCell.y) };
            var cur = chunkCells[0];
            while (true)
            {
                var prev = cur + back;
                if (lookup.TryGetValue(prev, out var p)
                    && p.dir == endCell.dir
                    && ColorUtil.ApproximatelyEqual(new Color(p.r, p.g, p.b), new Color(endCell.r, endCell.g, endCell.b))
                    && !p.isEnd)
                {
                    chunkCells.Add(prev);
                    cur = prev;
                }
                else break;
            }
            chunkCells.Reverse();
            yield return new ChunkInfo { direction = dir, cells = chunkCells };
        }
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
