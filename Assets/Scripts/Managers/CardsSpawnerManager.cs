using UnityEngine;

public class CardsSpawnerManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public GameObject cardPrefab;
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

        foreach (var cell in levelData.cells)
        {
            Color color = new Color(cell.r, cell.g, cell.b, 1f);

            float sx = cell.x * SlotX - centerX;
            float sz = centerZ - cell.y * SlotZ;

            bool horizontal = (cell.dir == (int)CellDirection.Left || cell.dir == (int)CellDirection.Right);
            Quaternion rot = horizontal ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                float ox = horizontal ? order[i] * spacingZ : 0f;
                float oz = horizontal ? 0f : order[i] * spacingZ;

                Vector3 localPos = new Vector3(sx + ox, 0f, sz + oz);
                Vector3 worldPos = transform.TransformPoint(localPos);
                worldPos.y = 0.5f;

                GameObject card = Instantiate(cardPrefab, worldPos, rot, transform);

                MeshRenderer mr = card.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    Material mat = mr.materials[0];
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    else mat.color = color;
                }
            }
        }
    }

    public void ClearCards()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }
}
