using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Genera los visuales de la cinta a lo largo del path: 2 portales (uno en
/// cada extremo, mirando hacia adentro) y N partes de cinta equiespaciadas
/// entre ellos. Se suscribe a <see cref="BeltPath.OnPathChanged"/> para
/// regenerar automaticamente cuando el path cambia.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BeltPath))]
public class BeltVisuals : MonoBehaviour
{
    [Tooltip("Modelo para las partes intermedias de la cinta. Se replica a lo largo del path.")]
    [SerializeField] private GameObject partPrefab;
    [Tooltip("Modelo para los extremos (portales). Se coloca uno al inicio y otro al final del path.")]
    [SerializeField] private GameObject portalPrefab;
    [Tooltip("Distancia en unidades de path entre partes consecutivas.")]
    [SerializeField] private float partSpacing = 0.5f;
    [Tooltip("Cuanto del path inicial/final queda libre cerca de los portales (no se ponen parts ahi).")]
    [SerializeField] private float portalEdgePadding = 0.4f;

    private const string VisualsContainerName = "Visuals";
    private Transform visualsContainer;
    private BeltPath path;

    void OnEnable()
    {
        EnsureRefs();
        if (path != null) path.OnPathChanged += Rebuild;
    }

    void OnDisable()
    {
        if (path != null) path.OnPathChanged -= Rebuild;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            EnsureRefs();
            Rebuild();
        };
    }
#endif

    void EnsureRefs()
    {
        if (path == null) path = GetComponent<BeltPath>();
        if (visualsContainer == null)
            visualsContainer = BeltContainers.EnsureChildContainer(transform, VisualsContainerName);
    }

    [ContextMenu("Belt/Rebuild Visuals")]
    public void Rebuild()
    {
        EnsureRefs();
        if (path == null) return;

        // Limpiar leftovers parentados directamente bajo cada Point (de versiones viejas).
        var pts = path.PointsContainer;
        if (pts != null)
        {
            for (int i = 0; i < pts.childCount; i++)
            {
                var pt = pts.GetChild(i);
                pt.name = $"Point_{i}";
                BeltContainers.ClearChildrenSafely(pt);
            }
        }

        BeltContainers.ClearChildrenSafely(visualsContainer);
        if (!path.HasArea) return;

        float length = path.GetPathLength();
        if (length <= 0f) return;

        // 1) Portales en los extremos del path. Los dos miran "hacia adentro"
        //    del camino. El tangente en distance=0 ya apunta hacia adentro;
        //    en distance=length apunta hacia afuera, asi que se invierte.
        SpawnVisualAtDistance(portalPrefab, 0f, "Portal_Start");
        path.SamplePath(length, out Vector3 endPos, out Vector3 endTan);
        SpawnVisualAt(portalPrefab, endPos, -endTan, "Portal_End");

        if (partPrefab == null || partSpacing <= 0.01f) return;

        // 2) Partes equiespaciadas a lo largo del path suavizado.
        float edge = Mathf.Min(portalEdgePadding, length * 0.49f);
        float start = edge;
        float end = length - edge;
        float available = end - start;
        if (available <= 0.0001f) return;
        int count = Mathf.Max(1, Mathf.RoundToInt(available / partSpacing));
        for (int k = 0; k <= count; k++)
        {
            float d = Mathf.Lerp(start, end, count > 0 ? (float)k / count : 0f);
            SpawnVisualAtDistance(partPrefab, d, $"Part_{k}");
        }
    }

    void SpawnVisualAtDistance(GameObject prefab, float distance, string name)
    {
        if (prefab == null) return;
        path.SamplePath(distance, out Vector3 pos, out Vector3 tangent);
        // Ventana de suavizado proporcional al spacing: cada part toma como
        // forward el promedio del tangente en una ventana del mismo tamano
        // que el step entre partes. Con spacing chico la ventana es chica
        // y casi no afecta (no ensancha la curva); con spacing grande
        // suaviza la transicion linea-arco.
        float window = partSpacing * 0.5f;
        if (window > 0.001f)
        {
            path.SamplePath(Mathf.Max(0f, distance - window), out _, out Vector3 tBack);
            path.SamplePath(distance + window, out _, out Vector3 tFwd);
            Vector3 avg = tBack + tFwd;
            if (avg.sqrMagnitude > 0.0001f) tangent = avg;
        }
        SpawnVisualAt(prefab, pos, tangent, name);
    }

    void SpawnVisualAt(GameObject prefab, Vector3 pos, Vector3 forward, string name)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, visualsContainer);
        go.name = name;
        go.transform.position = pos;
        if (forward.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
