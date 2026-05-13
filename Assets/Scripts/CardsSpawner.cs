using UnityEngine;

public class CardsSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public GameObject cardPrefab;
    [SerializeField] public LevelData levelData;
    [SerializeField] public Transform boardTransform;

    [Header("Layout")]
    [Tooltip("Distancia entre centros de celdas vecinas (lado largo de la carta)")]
    [SerializeField] public float spacingX = 0.8f;
    [Tooltip("Distancia entre cartas apiladas dentro de un chunk (lado corto)")]
    [SerializeField] public float spacingZ = 0.2f;
    [Tooltip("Margen alrededor del grid en el Board")]
    [SerializeField] public float margin = 1f;

    // Slots cuadrados: chunks vecinos en X o Z quedan a la misma distancia.
    private float SlotX => spacingX;
    private float SlotZ => spacingX;

    public void SpawnCards()
    {
        ClearCards();
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

        float gridW = (maxX - minX + 1) * SlotX;
        float gridH = (maxY - minY + 1) * SlotZ;

        float centerX = (minX + maxX) * 0.5f * SlotX;
        float centerZ = (minY + maxY) * 0.5f * SlotZ;

        if (boardTransform != null)
        {
            Vector3 s = boardTransform.localScale;
            s.x = gridW + margin * 2f;
            s.z = gridH + margin * 2f;
            boardTransform.localScale = s;

            Vector3 p = boardTransform.position;
            p.x = transform.position.x + centerX;
            p.z = transform.position.z + centerZ;
            boardTransform.position = p;
        }

        // 4 offsets relativos dentro de un slot (centrados en 0)
        float[] order = { -1.5f, -0.5f, 0.5f, 1.5f };

        foreach (var cell in levelData.cells)
        {
            Color color = new Color(cell.r, cell.g, cell.b, 1f);

            // Editor coords → mundo (Y top-left, Z arriba en pantalla)
            float sx = cell.x * SlotX - centerX;
            float sz = centerZ - cell.y * SlotZ;

            bool horizontal = (cell.dir == (int)CellDirection.Left || cell.dir == (int)CellDirection.Right);
            // Chunks horizontales: rotar 90° en Y para que el lado largo siga
            // la dirección del chunk. El stacking interno usa siempre spacingZ.
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
