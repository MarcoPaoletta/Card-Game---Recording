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
        // Cantidad de columnas y filas del grid.
        int cols = 3;
        int rows = 3;

        // Calculamos el ancho y la profundidad total del grid.
        // Usamos (cols - 1) porque entre 3 columnas hay 2 espacios, no 3.
        // Ejemplo con spacingX=2: totalWidth = 2 * 2 = 4 unidades de ancho total.
        float totalWidth = (cols - 1) * spacingX;
        float totalDepth = (rows - 1) * spacingZ;

        // Recorremos cada fila del grid (0, 1, 2).
        for (int row = 0; row < rows; row++)
        {
            // Dentro de cada fila, recorremos cada columna (0, 1, 2).
            for (int col = 0; col < cols; col++)
            {
                // Calculamos la posición X de esta carta.
                // Restamos totalWidth/2 para centrar el grid en el origen del Board.
                // Ejemplo: col=0 → x=-2, col=1 → x=0, col=2 → x=2
                float x = col * spacingX - totalWidth / 2f;

                // Mismo cálculo pero para el eje Z (profundidad).
                float z = row * spacingZ - totalDepth / 2f;

                // Creamos un Vector3 con la posición local de esta carta.
                // Y=0 para que quede a la misma altura que el Board.
                Vector3 localPos = new Vector3(x, 0f, z);

                // Instantiate crea una copia del prefab en la escena.
                // - cardPrefab: el objeto a copiar.
                // - transform.TransformPoint(localPos): convierte la posición local
                //   (relativa al Board) a posición global en el mundo.
                // - Quaternion.identity: sin rotación (rotación "neutral").
                // - transform: hace que la carta creada sea hija del Board,
                //   así queda organizada dentro de él en la jerarquía.
                Instantiate(cardPrefab, transform.TransformPoint(localPos), Quaternion.identity, transform);
            }
        }
    }
}
