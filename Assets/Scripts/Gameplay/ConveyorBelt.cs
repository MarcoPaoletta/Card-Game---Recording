using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Cinta transportadora que reemplaza al ReserveManager. Las cartas que no
/// pudieron entrar a una order viajan en loop sobre la cinta entre dos portales:
/// al llegar al portal final hacen scale down, teletransportan al portal inicial
/// y hacen scale up. Cuando una order del color matcheante esta disponible, la
/// carta se despega de la cinta y vuela hacia el order.
///
/// Los puntos bajo "Points" son los puntos de CONTROL del path (no se les
/// asocia un modelo). Se mueven con las herramientas del editor. El sistema
/// rellena automaticamente "Visuals" con: 2 portales (al principio y al final
/// del path) y N partes de cinta espaciadas por `partSpacing` a lo largo del
/// camino.
///
/// Soporta presets (BeltPreset SO) para guardar/cargar configuraciones y asignar
/// una distinta por nivel.
/// </summary>
[ExecuteAlways]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Modelo para las partes intermedias de la cinta. Se replica a lo largo del path.")]
    [SerializeField] private GameObject partPrefab;
    [Tooltip("Modelo para los extremos (portales). Se coloca uno al inicio y otro al final del path.")]
    [SerializeField] private GameObject portalPrefab;
    [Tooltip("Distancia en unidades de path entre partes consecutivas.")]
    [SerializeField] private float partSpacing = 0.5f;
    [Tooltip("Cuanto del path inicial/final queda libre cerca de los portales (no se ponen parts ahi).")]
    [SerializeField] private float portalEdgePadding = 0.4f;
    [Tooltip("Tension de la spline (Catmull-Rom). 0 = curvas suaves casi rectas; 0.5 = clasico; cerca de 1 = se hace loopy.")]
    [Range(0f, 1f)]
    [SerializeField] private float splineTension = 0.5f;
    [Tooltip("Cantidad de sub-segmentos por cada par de control points. Mas alto = curva mas suave pero mas calculo.")]
    [Range(2, 64)]
    [SerializeField] private int splineSubdivisions = 16;

    [Header("Presets")]
    [Tooltip("Preset usado si el nivel no especifica uno (campo beltPresetName vacio).")]
    [SerializeField] private BeltPreset defaultPreset;
    [Tooltip("Preset al que apuntan los menus Save/Load del editor.")]
    [SerializeField] private BeltPreset editorTargetPreset;

    [Header("Refs")]
    [SerializeField] private OrdersManager ordersManager;

    [Header("Movimiento")]
    [Tooltip("Velocidad de avance de las cartas a lo largo del path (units/seg).")]
    [SerializeField] private float beltSpeed = 0.6f;
    [Tooltip("Longitud (en units del path) cerca de cada portal donde la carta hace scale 0<->1.")]
    [SerializeField] private float portalScaleZoneLength = 0.5f;
    [Tooltip("Altura Y a la que se posicionan las cartas en la cinta.")]
    [SerializeField] private float cardY = 0.5f;

    [Header("Animaciones de salida (hacia orders)")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float jumpPower = 1.0f;

    private const string PointsContainerName = "Points";
    private const string VisualsContainerName = "Visuals";
    private const string SlotsContainerName = "Slots";

    private Transform pointsContainer;
    private Transform visualsContainer;
    private Transform slotsContainer;

    enum State { InFlightToBelt, Riding }

    class Entry
    {
        public Color color;
        public Transform card;
        public Transform slot;
        public float distance;
        public bool hasWrapped;
        public State state;
    }

    private readonly List<Entry> entries = new List<Entry>();

    public bool HasArea => GetPointCount() >= 2;

    void OnEnable() { EnsureContainers(); }

    void Reset() { EnsureContainers(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            EnsureContainers();
            RebuildVisuals();
        };
    }
#endif

    void Update()
    {
        if (!Application.isPlaying)
        {
            TryAutoRebuildOnPointMove();
            return;
        }
        if (entries.Count == 0) return;
        float length = GetPathLength();
        if (length <= 0f) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.state != State.Riding || e.slot == null) continue;
            e.distance += beltSpeed * Time.deltaTime;
            if (e.distance >= length) { e.distance -= length; e.hasWrapped = true; }
            UpdateSlotTransform(e, length);
        }
    }

    // En editor: detecta si un Point fue movido. Solo regenera los visuales
    // cuando el usuario solto el drag (hotControl == 0), no en cada frame del
    // drag — asi no parpadea ni se vuelve pesado mientras arrastras.
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
        if (GUIUtility.hotControl != 0) return; // drag activo: esperar a que suelte
#endif

        for (int i = 0; i < pointsContainer.childCount; i++)
            pointsContainer.GetChild(i).hasChanged = false;
        RebuildVisuals();
    }

    void UpdateSlotTransform(Entry e, float length)
    {
        SamplePath(e.distance, out Vector3 pos, out _);
        pos.y = cardY;
        e.slot.position = pos;

        float zone = Mathf.Max(0.001f, portalScaleZoneLength);
        float t;
        if (e.distance > length - zone)
            t = Mathf.Clamp01((length - e.distance) / zone);
        else if (e.hasWrapped && e.distance < zone)
            t = Mathf.Clamp01(e.distance / zone);
        else
            t = 1f;

        e.slot.localScale = new Vector3(t, t, t);
    }

    // --- API publica (reemplaza ReserveManager) ---

    public Transform AcquireSlot(Color color)
    {
        if (!HasArea) return null;
        EnsureContainers();
        var slotGO = new GameObject("BeltSlot");
        slotGO.transform.SetParent(slotsContainer, worldPositionStays: false);

        SamplePath(0f, out Vector3 pos, out _);
        pos.y = cardY;
        slotGO.transform.position = pos;
        slotGO.transform.localScale = Vector3.one;

        var entry = new Entry
        {
            color = color,
            slot = slotGO.transform,
            distance = 0f,
            hasWrapped = false,
            state = State.InFlightToBelt,
        };
        entries.Add(entry);
        return slotGO.transform;
    }

    public void AttachCardToSlot(Transform slot, Transform card)
    {
        if (slot == null || card == null) return;
        var entry = FindBySlot(slot);
        if (entry == null) { Destroy(card.gameObject); return; }

        entry.card = card;
        entry.state = State.Riding;
        card.SetParent(slot, worldPositionStays: true);
        card.localPosition = Vector3.zero;

        TryForwardEntry(entry);
    }

    void TryForwardEntry(Entry entry)
    {
        if (entry == null || entry.card == null || ordersManager == null) return;
        var assign = ordersManager.AcquireNextSlot(entry.color);
        if (assign.slot == null) return;

        var card = entry.card;
        var sourceSlot = entry.slot;
        var capturedOrder = assign.order;
        var capturedSlot = assign.slot;

        entries.Remove(entry);
        card.SetParent(null, worldPositionStays: true);
        card.localScale = Vector3.one;
        if (sourceSlot != null) Destroy(sourceSlot.gameObject);

        card.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(card.DOJump(capturedSlot.position, jumpPower, 1, flyDuration).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            if (card == null || capturedSlot == null) return;
            card.SetParent(capturedSlot, worldPositionStays: true);
            card.localPosition = Vector3.zero;
            card.localRotation = Quaternion.identity;
            capturedOrder.NotifyDelivered();
        });
    }

    public void TryFlushTo(Order order)
    {
        if (order == null) return;
        Color color = order.Color;

        for (int i = entries.Count - 1; i >= 0 && !order.IsFull; i--)
        {
            var e = entries[i];
            if (!ColorUtil.ApproximatelyEqual(e.color, color)) continue;
            if (e.card == null) continue;

            var orderSlot = order.AcquireNextSlot(color);
            if (orderSlot == null) break;

            var card = e.card;
            var sourceSlot = e.slot;
            entries.RemoveAt(i);

            card.SetParent(null, worldPositionStays: true);
            card.localScale = Vector3.one;
            if (sourceSlot != null) Destroy(sourceSlot.gameObject);

            var capturedOrder = order;
            card.DOKill();
            var seq = DOTween.Sequence();
            seq.Append(card.DOJump(orderSlot.position, jumpPower, 1, flyDuration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                if (card == null || orderSlot == null) return;
                card.SetParent(orderSlot, worldPositionStays: true);
                card.localPosition = Vector3.zero;
                card.localRotation = Quaternion.identity;
                capturedOrder.NotifyDelivered();
            });
        }
    }

    public void Clear()
    {
        foreach (var e in entries)
        {
            if (e.card != null) Destroy(e.card.gameObject);
            if (e.slot != null) Destroy(e.slot.gameObject);
        }
        entries.Clear();
    }

    /// <summary>
    /// Aplica el preset al belt. Si preset es null, usa el defaultPreset.
    /// Regenera puntos y visuales en escena.
    /// </summary>
    public void ApplyPresetForLevel(BeltPreset preset)
    {
        var p = preset != null ? preset : defaultPreset;
        if (p == null) return;
        ApplyPointsFrom(p.localPoints);
        RebuildVisuals();
    }

    // --- Path helpers ---

    int GetPointCount()
    {
        EnsureContainers();
        return pointsContainer != null ? pointsContainer.childCount : 0;
    }

    Transform GetPoint(int i) => pointsContainer != null ? pointsContainer.GetChild(i) : null;

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

    Entry FindBySlot(Transform slot)
    {
        foreach (var e in entries) if (e.slot == slot) return e;
        return null;
    }

    // --- Edicion ---

    void EnsureContainers()
    {
        pointsContainer = EnsureChildContainer(PointsContainerName);
        visualsContainer = EnsureChildContainer(VisualsContainerName);
        slotsContainer = EnsureChildContainer(SlotsContainerName);
    }

    Transform EnsureChildContainer(string name)
    {
        var t = transform.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        return t;
    }

    [ContextMenu("Belt/Add Point")]
    public void EditorAddPoint()
    {
        EnsureContainers();
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
        RebuildVisuals();
    }

    [ContextMenu("Belt/Remove Last Point")]
    public void EditorRemoveLastPoint()
    {
        EnsureContainers();
        int n = pointsContainer.childCount;
        if (n <= 2) { Debug.LogWarning("[ConveyorBelt] Minimo 2 puntos."); return; }
        var last = pointsContainer.GetChild(n - 1);
        if (Application.isPlaying) Destroy(last.gameObject);
        else DestroyImmediate(last.gameObject);
        RebuildVisuals();
    }

    [ContextMenu("Belt/Rebuild Visuals")]
    public void RebuildVisuals()
    {
        EnsureContainers();

        // Limpiar leftovers de la version anterior: visuales que pudieran estar
        // parentados directamente bajo cada Point.
        for (int i = 0; i < pointsContainer.childCount; i++)
        {
            var pt = pointsContainer.GetChild(i);
            pt.name = $"Point_{i}";
            ClearChildrenSafely(pt);
        }

        // Limpiar el container de Visuals.
        ClearChildrenSafely(visualsContainer);

        if (pointsContainer.childCount < 2) return;

        float length = GetPathLength();
        if (length <= 0f) return;

        // 1) Portales en los extremos del path. Los dos miran "hacia adentro"
        //    del camino. El tangente en distance=0 ya apunta hacia adentro;
        //    en distance=length apunta hacia afuera, asi que se invierte.
        SpawnVisualAtDistance(portalPrefab, 0f, "Portal_Start");
        SamplePath(length, out Vector3 endPos, out Vector3 endTan);
        SpawnVisualAt(portalPrefab, endPos, -endTan, "Portal_End");

        if (partPrefab == null || partSpacing <= 0.01f) return;

        // 2) Partes equiespaciadas a lo largo del path suavizado.
        //    Como el path tiene fillets en las esquinas, no necesitamos forzar
        //    parts en los vertices: el muestreo a distancia constante recorre
        //    la curva sin saltos.
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
        SamplePath(distance, out Vector3 pos, out Vector3 tangent);
        // Ventana de suavizado proporcional al spacing: cada part toma como
        // forward el promedio del tangente en una ventana del mismo tamano
        // que el step entre partes. Con spacing chico la ventana es chica
        // y casi no afecta (no ensancha la curva); con spacing grande
        // suaviza la transicion linea-arco.
        float window = partSpacing * 0.5f;
        if (window > 0.001f)
        {
            SamplePath(Mathf.Max(0f, distance - window), out _, out Vector3 tBack);
            SamplePath(distance + window, out _, out Vector3 tFwd);
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

    void OnDrawGizmos()
    {
        if (pointsContainer == null) pointsContainer = transform.Find(PointsContainerName);
        if (pointsContainer == null) return;
        int n = pointsContainer.childCount;
        if (n < 2) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        // Dibujar el path real (con fillets) muestreado.
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
        // Marcar los control points.
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        for (int i = 0; i < n; i++)
            Gizmos.DrawWireSphere(pointsContainer.GetChild(i).position, 0.18f);
    }

    [ContextMenu("Belt/Save As Preset")]
    public void EditorSaveAsPreset()
    {
        if (editorTargetPreset == null)
        {
            Debug.LogError("[ConveyorBelt] Asignar 'editorTargetPreset' antes de guardar.");
            return;
        }
        EnsureContainers();
        editorTargetPreset.localPoints.Clear();
        for (int i = 0; i < pointsContainer.childCount; i++)
            editorTargetPreset.localPoints.Add(pointsContainer.GetChild(i).localPosition);
#if UNITY_EDITOR
        EditorUtility.SetDirty(editorTargetPreset);
        AssetDatabase.SaveAssetIfDirty(editorTargetPreset);
        Debug.Log($"[ConveyorBelt] Preset '{editorTargetPreset.name}' guardado con {editorTargetPreset.localPoints.Count} puntos.");
#endif
    }

    [ContextMenu("Belt/Load Preset")]
    public void EditorLoadPreset()
    {
        if (editorTargetPreset == null)
        {
            Debug.LogError("[ConveyorBelt] Asignar 'editorTargetPreset' antes de cargar.");
            return;
        }
        ApplyPointsFrom(editorTargetPreset.localPoints);
        RebuildVisuals();
    }

    void ApplyPointsFrom(List<Vector3> localPoints)
    {
        EnsureContainers();
        ClearChildrenSafely(pointsContainer);
        if (localPoints == null) return;
        for (int i = 0; i < localPoints.Count; i++)
        {
            var go = new GameObject($"Point_{i}");
            go.transform.SetParent(pointsContainer, false);
            go.transform.localPosition = localPoints[i];
        }
    }

    // Detach + destroy. En play mode Destroy() es diferido al final del frame,
    // asi que sin detach los children siguen contando en childCount y rompen
    // los rebuilds que corren en el mismo frame.
    static void ClearChildrenSafely(Transform parent)
    {
        if (parent == null) return;
        var snapshot = new List<Transform>(parent.childCount);
        for (int i = 0; i < parent.childCount; i++) snapshot.Add(parent.GetChild(i));
        foreach (var c in snapshot)
        {
            if (c == null) continue;
            c.SetParent(null, worldPositionStays: false);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }
}
