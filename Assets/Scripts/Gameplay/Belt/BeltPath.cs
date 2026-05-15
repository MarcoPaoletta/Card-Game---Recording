using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Geometria del path de la cinta. Posee el container "Points" con los control
/// points y construye una spline Catmull-Rom suavizada entre ellos. No sabe
/// nada de visuales, presets, ni cartas: solo expone <see cref="SamplePath"/>,
/// <see cref="GetPathLength"/> y un evento <see cref="OnPathChanged"/> al que
/// se suscriben los componentes interesados (visuales) para regenerar.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class BeltPath : MonoBehaviour
{
    [Tooltip("Tension de la spline (Catmull-Rom). 0 = curvas suaves casi rectas; 0.5 = clasico; cerca de 1 = se hace loopy.")]
    [Range(0f, 1f)]
    [SerializeField] private float splineTension = 0.5f;
    [Tooltip("Cantidad de sub-segmentos por cada par de control points. Mas alto = curva mas suave pero mas calculo.")]
    [Range(2, 64)]
    [SerializeField] private int splineSubdivisions = 16;

    private const string PointsContainerName = "Points";
    private Transform pointsContainer;

    public event Action OnPathChanged;

    public bool HasArea => GetPointCount() >= 2;

    void OnEnable() { EnsureContainer(); }
    void Reset() { EnsureContainer(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            EnsureContainer();
            RaisePathChanged();
        };
    }
#endif

    void Update()
    {
        if (Application.isPlaying) return;
        TryAutoRebuildOnPointMove();
    }

    // En editor: detecta si un Point fue movido. Solo dispara el evento cuando
    // el usuario solto el drag (hotControl == 0), no en cada frame del drag —
    // asi no parpadea ni se vuelve pesado mientras arrastras.
    void TryAutoRebuildOnPointMove()
    {
        if (pointsContainer == null) pointsContainer = transform.Find(PointsContainerName);
        if (pointsContainer == null) return;

        bool anyChanged = false;
        for (int i = 0; i < pointsContainer.childCount; i++)
        {
            if (pointsContainer.GetChild(i).hasChanged) { anyChanged = true; break; }
        }
        if (!anyChanged) return;

#if UNITY_EDITOR
        if (GUIUtility.hotControl != 0) return;
#endif

        for (int i = 0; i < pointsContainer.childCount; i++)
            pointsContainer.GetChild(i).hasChanged = false;
        RaisePathChanged();
    }

    public Transform PointsContainer
    {
        get { EnsureContainer(); return pointsContainer; }
    }

    public int GetPointCount()
    {
        EnsureContainer();
        return pointsContainer != null ? pointsContainer.childCount : 0;
    }

    public Transform GetPoint(int i) => pointsContainer != null ? pointsContainer.GetChild(i) : null;

    struct PathSegment
    {
        public Vector3 a, b;
        public float length;
    }

    /// <summary>
    /// Construye el path como una polilinea fina: para cada par de control
    /// points adyacentes, subdividimos un Catmull-Rom (con tangentes a partir
    /// de los vecinos) en N micro-segmentos. La spline pasa exactamente por
    /// cada control point y suaviza las curvas. En los extremos se duplica
    /// el control point como ghost para no perder forma.
    /// </summary>
    List<PathSegment> BuildSegments()
    {
        var segs = new List<PathSegment>();
        int n = GetPointCount();
        if (n < 2) return segs;

        int subs = Mathf.Max(1, splineSubdivisions);

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 P0 = (i > 0) ? GetPoint(i - 1).position : GetPoint(i).position;
            Vector3 P1 = GetPoint(i).position;
            Vector3 P2 = GetPoint(i + 1).position;
            Vector3 P3 = (i + 2 < n) ? GetPoint(i + 2).position : GetPoint(i + 1).position;

            Vector3 prev = P1;
            for (int s = 1; s <= subs; s++)
            {
                float t = s / (float)subs;
                Vector3 cur = CatmullRom(P0, P1, P2, P3, t, splineTension);
                float len = Vector3.Distance(prev, cur);
                if (len > 0.0001f)
                    segs.Add(new PathSegment { a = prev, b = cur, length = len });
                prev = cur;
            }
        }

        return segs;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float tension)
    {
        Vector3 m1 = tension * (p2 - p0);
        Vector3 m2 = tension * (p3 - p1);
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * p1
             + (t3 - 2f * t2 + t) * m1
             + (-2f * t3 + 3f * t2) * p2
             + (t3 - t2) * m2;
    }

    public float GetPathLength()
    {
        var segs = BuildSegments();
        float total = 0f;
        for (int i = 0; i < segs.Count; i++) total += segs[i].length;
        return total;
    }

    public void SamplePath(float distance, out Vector3 position, out Vector3 tangent)
    {
        position = transform.position;
        tangent = Vector3.right;
        var segs = BuildSegments();
        if (segs.Count == 0) return;

        float remaining = Mathf.Max(0f, distance);
        for (int i = 0; i < segs.Count; i++)
        {
            var seg = segs[i];
            bool last = i == segs.Count - 1;
            if (remaining <= seg.length || last)
            {
                float t = seg.length > 0.0001f ? Mathf.Clamp01(remaining / seg.length) : 0f;
                position = Vector3.Lerp(seg.a, seg.b, t);
                tangent = (seg.b - seg.a).normalized;
                return;
            }
            remaining -= seg.length;
        }
    }

    // --- Edicion ---

    void EnsureContainer()
    {
        if (pointsContainer != null) return;
        var t = transform.Find(PointsContainerName);
        if (t == null)
        {
            var go = new GameObject(PointsContainerName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        pointsContainer = t;
    }

    public void RaisePathChanged() => OnPathChanged?.Invoke();

    [ContextMenu("Belt/Add Point")]
    public void EditorAddPoint()
    {
        EnsureContainer();
        int idx = pointsContainer.childCount;
        var go = new GameObject($"Point_{idx}");
        go.transform.SetParent(pointsContainer, false);
        if (idx == 0) go.transform.localPosition = Vector3.zero;
        else if (idx == 1) go.transform.localPosition = new Vector3(2f, 0f, 0f);
        else
        {
            var prev = pointsContainer.GetChild(idx - 1).localPosition;
            var prev2 = pointsContainer.GetChild(idx - 2).localPosition;
            go.transform.localPosition = prev + (prev - prev2);
        }
        RaisePathChanged();
    }

    [ContextMenu("Belt/Remove Last Point")]
    public void EditorRemoveLastPoint()
    {
        EnsureContainer();
        int n = pointsContainer.childCount;
        if (n <= 2) { Debug.LogWarning("[BeltPath] Minimo 2 puntos."); return; }
        var last = pointsContainer.GetChild(n - 1);
        if (Application.isPlaying) Destroy(last.gameObject);
        else DestroyImmediate(last.gameObject);
        RaisePathChanged();
    }

    public void RenamePointsSequential()
    {
        EnsureContainer();
        for (int i = 0; i < pointsContainer.childCount; i++)
            pointsContainer.GetChild(i).name = $"Point_{i}";
    }

    /// <summary>Reemplaza los control points por la lista provista (espacio local).</summary>
    public void ApplyPointsFrom(List<Vector3> localPoints)
    {
        EnsureContainer();
        BeltContainers.ClearChildrenSafely(pointsContainer);
        if (localPoints == null) return;
        for (int i = 0; i < localPoints.Count; i++)
        {
            var go = new GameObject($"Point_{i}");
            go.transform.SetParent(pointsContainer, false);
            go.transform.localPosition = localPoints[i];
        }
        RaisePathChanged();
    }

    /// <summary>Snapshot de los control points en espacio local.</summary>
    public List<Vector3> SnapshotLocalPoints()
    {
        EnsureContainer();
        var list = new List<Vector3>(pointsContainer.childCount);
        for (int i = 0; i < pointsContainer.childCount; i++)
            list.Add(pointsContainer.GetChild(i).localPosition);
        return list;
    }

    void OnDrawGizmos()
    {
        if (pointsContainer == null) pointsContainer = transform.Find(PointsContainerName);
        if (pointsContainer == null) return;
        int n = pointsContainer.childCount;
        if (n < 2) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        float length = GetPathLength();
        const int samples = 64;
        if (length > 0f)
        {
            Vector3 prev;
            SamplePath(0f, out prev, out _);
            for (int s = 1; s <= samples; s++)
            {
                SamplePath((s / (float)samples) * length, out Vector3 cur, out _);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        for (int i = 0; i < n; i++)
            Gizmos.DrawWireSphere(pointsContainer.GetChild(i).position, 0.18f);
    }
}
