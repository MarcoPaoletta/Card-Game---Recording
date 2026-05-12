// Necesitamos esto para usar las clases de Unity como MonoBehaviour, Vector3, etc.
using UnityEngine;

// CardsSpawner es un componente que se puede adjuntar a cualquier GameObject en la escena.
// MonoBehaviour es la clase base de todos los scripts de Unity.
public class CardsSpawner : MonoBehaviour
{
    // [SerializeField] hace que una variable privada aparezca en el Inspector de Unity,
    // así podés asignarla arrastrando un asset sin necesidad de hacerla pública.

    // El prefab de la carta que vamos a instanciar (crear copias en la escena).
    [SerializeField] private GameObject cardPrefab;

    // Separación entre cartas en el eje X (horizontal).
    [SerializeField] private float spacingX = 2f;

    // Separación entre cartas en el eje Z (profundidad, hacia/desde la cámara en 3D).
    [SerializeField] private float spacingZ = 3f;

    // Start() es un método especial de Unity. Se ejecuta una sola vez,
    // justo antes del primer frame cuando el objeto se activa en la escena.
    void Start()
    {
        // Llamamos al método que genera el grid de cartas.
        SpawnGrid();
    }

    // Este método crea las 9 cartas organizadas en una grilla 3x3.
void SpawnGrid()
    {
        int cols = 3;
        int rows = 3;

        float totalWidth = (cols - 1) * spacingX;
        float totalDepth = (rows - 1) * spacingZ;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float x = col * spacingX - totalWidth / 2f;
                float z = row * spacingZ - totalDepth / 2f;
                Vector3 localPos = new Vector3(x, 0f, z);

                GameObject card = Instantiate(cardPrefab, transform.TransformPoint(localPos), Quaternion.identity, transform);

                // Asignamos un color random al MeshRenderer de la carta
                MeshRenderer renderer = card.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
                }
            }
        }
    }
}
