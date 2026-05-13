using UnityEngine;

public class CardsSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public GameObject cardPrefab;
    [SerializeField] public LevelData levelData;
    [SerializeField] public Transform boardTransform;

    [Header("Layout")]
    [Tooltip("Tamaño de cada slot en unidades del mundo")]
    [SerializeField] public float slotSize = 4f;
    [Tooltip("Espaciado entre centros de las 4 cartas dentro de un slot")]
    [SerializeField] public float cardSpacing = 1.2f;
    [Tooltip("Margen alrededor del grid en el Board")]
    [SerializeField] public float margin = 1f;

    // Las 4 posiciones relativas de las cartas dentro de un slot (escala por cardSpacing)
    private static readonly Vector2[] Offsets =
    {
        new Vector2(-0.5f,  0.5f),   // arriba-izquierda
        new Vector2( 0.5f,  0.5f),   // arriba-derecha
        new Vector2(-0.5f, -0.5f),   // abajo-izquierda
        new Vector2( 0.5f, -0.5f),   // abajo-derecha
    };

    public void SpawnCards()
    {
        ClearCards();
        if (levelData == null || cardPrefab == null || levelData.cells == null || levelData.cells.Count == 0)
            return;

        // Bounding box del nivel en coordenadas de grilla
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var cell in levelData.cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        float gridW = (maxX - minX + 1) * slotSize;
        float gridH = (maxY - minY + 1) * slotSize;

        // Centro del nivel en coordenadas del mundo (relativo a este transform)
        float centerX = (minX + maxX) * 0.5f * slotSize;
        float centerZ = (minY + maxY) * 0.5f * slotSize;

        // Escalar y reposicionar el Board para que contenga el grid + margen
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

        // Instanciar 4 cartas por cada celda pintada
        foreach (var cell in levelData.cells)
        {
            Color color = new Color(cell.r, cell.g, cell.b, 1f);

            // Centro del slot en espacio local (centrado en el grid)
            float sx = cell.x * slotSize - centerX;
            float sz = cell.y * slotSize - centerZ;

            foreach (var offset in Offsets)
            {
                Vector3 localPos = new Vector3(
                    sx + offset.x * cardSpacing,
                    0f,
                    sz + offset.y * cardSpacing
                );

                GameObject card = Instantiate(
                    cardPrefab,
                    transform.TransformPoint(localPos),
                    Quaternion.identity,
                    transform
                );

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
