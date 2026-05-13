using UnityEngine;

public class CardsSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public GameObject cardPrefab;
    [SerializeField] public LevelData levelData;
    [SerializeField] public Transform boardTransform;

    [Header("Layout")]
    [Tooltip("Espaciado horizontal (X) entre centros de columnas de cartas")]
    [SerializeField] public float spacingX = 0.8f;
    [Tooltip("Espaciado vertical (Z) entre centros de cartas")]
    [SerializeField] public float spacingZ = 0.2f;
    [Tooltip("Margen alrededor del grid en el Board")]
    [SerializeField] public float margin = 1f;

    // Cada celda = 1 columna de 4 cartas alineadas en Z.
    // SlotX = spacingX (1 carta de ancho por celda).
    // SlotZ = 4 * spacingZ para que las cartas entre celdas verticales conserven spacingZ.
    private float SlotX => spacingX;
    private float SlotZ => spacingZ * 4f;

    // 4 cartas en columna a lo largo del eje Z, todas con la misma X
    private static readonly Vector2[] Offsets =
    {
        new Vector2(0f, -1.5f),
        new Vector2(0f, -0.5f),
        new Vector2(0f,  0.5f),
        new Vector2(0f,  1.5f),
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

        float gridW = (maxX - minX + 1) * SlotX;
        float gridH = (maxY - minY + 1) * SlotZ;

        // Centro del nivel en coordenadas del mundo (relativo a este transform)
        float centerX = (minX + maxX) * 0.5f * SlotX;
        float centerZ = (minY + maxY) * 0.5f * SlotZ;

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

            // Centro del slot en espacio local (centrado en el grid).
            // Z invertido: editor fila 0 (arriba) → +Z mundo (arriba en pantalla con la cámara actual).
            float sx = cell.x * SlotX - centerX;
            float sz = centerZ - cell.y * SlotZ;

            foreach (var offset in Offsets)
            {
                Vector3 localPos = new Vector3(
                    sx + offset.x * spacingX,
                    0f,
                    sz + offset.y * spacingZ
                );
                Vector3 worldPos = transform.TransformPoint(localPos);
                worldPos.y = 0.5f;

                GameObject card = Instantiate(
                    cardPrefab,
                    worldPos,
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
