using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cinta transportadora: gestiona las cartas que estan "viajando en loop" entre
/// dos portales mientras esperan que se libere un order del color matcheante.
///
/// Esta clase solo se encarga del runtime: lleva el registro de los slots
/// activos, los avanza por el path, y dispara los tweens de transferencia
/// cuando aparece un order disponible.
///
/// La geometria del path vive en <see cref="BeltPath"/>; los visuales
/// (portales + partes) en <see cref="BeltVisuals"/>; la I/O de presets en
/// <see cref="BeltPresetIO"/>. Esta fachada delgada mantiene la API publica
/// historica (<see cref="AcquireSlot"/>, <see cref="AttachCardToSlot"/>,
/// <see cref="TryFlushTo"/>, <see cref="Clear"/>, <see cref="ApplyPresetForLevel"/>)
/// para no romper a los callers (Chunk, OrdersManager, CardsSpawnerManager,
/// LevelBuilderManager).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BeltPath))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private OrdersManager ordersManager;
    [Tooltip("Spawner del board. Se usa para leer su card spacing y mantenerlo igual en la cinta.")]
    [SerializeField] private CardsSpawnerManager cardsSpawner;

    [Header("Movimiento")]
    [Tooltip("Velocidad de avance de las cartas a lo largo del path (units/seg).")]
    [SerializeField] private float beltSpeed = 0.6f;
    [Tooltip("Longitud (en units del path) cerca de cada portal donde la carta hace scale 0<->1.")]
    [SerializeField] private float portalScaleZoneLength = 0.5f;
    [Tooltip("Distancia adelantada en el path donde aterriza la primera carta del board (frente de cola). Subir este valor evita que el target del jump caiga justo en la boca del portal y haga clipping con el portal o con cartas previas.")]
    [SerializeField] private float entryPathOffset = 0.4f;
    [Tooltip("Espaciado entre cartas en la cola de entrada. Si es <= 0 se usa CardsSpawnerManager.CardSpacing (default 0.2). Subir este valor separa mas las cartas que aterrizan en rapida sucesion para que no se solapen.")]
    [SerializeField] private float entrySpacing = 0f;
    [Tooltip("Solo editor: dibuja un gizmo en el punto donde aterriza la primera carta y las posiciones que tomarian las siguientes en la cola, para tunear entryPathOffset / entrySpacing sin entrar a play.")]
    [SerializeField] private bool drawEntryGizmo = true;
    [Tooltip("Solo editor: cuantas posiciones de cola dibuja el gizmo de entrada.")]
    [Range(1, 32)]
    [SerializeField] private int entryGizmoPreviewCount = 8;
    [Tooltip("Altura Y a la que se posicionan las cartas en la cinta.")]
    [SerializeField] private float cardY = 0.5f;

    [Header("Animaciones de salida (hacia orders)")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float jumpPower = 1.0f;
    [Tooltip("Stagger entre cartas cuando se vacia la cinta hacia un order recien aparecido. Cada carta sale carta-a-carta, no todas en bloque.")]
    [SerializeField] private float flushStagger = 0.12f;

    [Header("Rotacion de la carta en cinta")]
    [Tooltip("Offset Euler aplicado a la rotacion de la carta cuando viaja en la cinta, despues del LookRotation por tangente. " +
             "Usar si el modelo del cartas no tiene su 'frente' alineado con +Z.")]
    [SerializeField] private Vector3 cardOnBeltEulerOffset = new Vector3(0f, -90f, 0f);

    private const string SlotsContainerName = "Slots";
    private Transform slotsContainer;
    private BeltPath path;
    private BeltPresetIO presetIO;

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

    public bool HasArea => path != null && path.HasArea;

    void OnEnable()
    {
        EnsureRefs();
    }

    void EnsureRefs()
    {
        if (path == null) path = GetComponent<BeltPath>();
        if (presetIO == null) presetIO = GetComponent<BeltPresetIO>();
        if (slotsContainer == null)
            slotsContainer = BeltContainers.EnsureChildContainer(transform, SlotsContainerName);
    }

    CardTransferTweens.JumpParams BuildJumpParams() => new CardTransferTweens.JumpParams
    {
        jumpPower = jumpPower,
        jumpCount = 1,
        duration = flyDuration,
        ease = DG.Tweening.Ease.OutQuad,
    };

    void Update()
    {
        if (!Application.isPlaying) return;
        if (entries.Count == 0) return;
        if (path == null) return;

        float length = path.GetPathLength();
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

    void UpdateSlotTransform(Entry e, float length)
    {
        SampleBeltSlot(e.distance, out Vector3 pos, out Vector3 tangent);
        pos.y = cardY;
        e.slot.position = pos;

        if (tangent.sqrMagnitude > 0.0001f)
        {
            var baseRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            e.slot.rotation = baseRot * Quaternion.Euler(cardOnBeltEulerOffset);
        }

        float zone = Mathf.Max(0.001f, portalScaleZoneLength);
        float t;
        if (e.distance < 0f)
            t = 0f;
        else if (e.distance > length - zone)
            t = Mathf.Clamp01((length - e.distance) / zone);
        else if (e.hasWrapped && e.distance < zone)
            t = Mathf.Clamp01(e.distance / zone);
        else
            t = 1f;

        e.slot.localScale = new Vector3(t, t, t);
    }

    /// <summary>
    /// Sample del path con extrapolacion lineal hacia atras del portal de entrada
    /// (distance &lt; 0). Sin esto, distancias negativas todas caen sobre la boca
    /// del portal (SamplePath internamente las clamp ea 0) y las cartas en cola
    /// se solapan visualmente en un solo punto. La extrapolacion usa el tangente
    /// del primer segmento para que la cola se extienda fuera del path en linea
    /// recta hacia atras.
    /// </summary>
    void SampleBeltSlot(float distance, out Vector3 pos, out Vector3 tangent)
    {
        if (distance >= 0f)
        {
            path.SamplePath(distance, out pos, out tangent);
            return;
        }
        path.SamplePath(0f, out Vector3 pos0, out tangent);
        Vector3 dir = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.right;
        pos = pos0 + dir * distance; // distance < 0 => mueve hacia atras
    }

    float EffectiveEntrySpacing()
    {
        if (entrySpacing > 0f) return entrySpacing;
        return cardsSpawner != null ? cardsSpawner.CardSpacing : 0.2f;
    }

    // --- API publica ---

    public Transform AcquireSlot(Color color)
    {
        EnsureRefs();
        if (!HasArea) return null;

        // La cola se extiende HACIA ADELANTE en el path: la primera carta
        // aterriza en entryPathOffset y cada nueva carta se ubica delante de la
        // mas adelantada de la cola por un spacing. Las cartas avanzan todas
        // juntas via belt; el orden de consumo (TryForwardEntry/TryFlushTo) se
        // sigue manteniendo por orden de llegada porque iteran la lista
        // 'entries', no por distancia.
        float spacing = EffectiveEntrySpacing();
        float length = path.GetPathLength();
        float startDistance = entryPathOffset;
        if (entries.Count > 0)
        {
            float maxDist = entries[0].distance;
            for (int i = 1; i < entries.Count; i++)
                if (entries[i].distance > maxDist) maxDist = entries[i].distance;
            startDistance = Mathf.Max(entryPathOffset, maxDist + spacing);
        }
        if (length > 0f && startDistance >= length)
            startDistance = length - spacing * 0.5f;

        var slotGO = new GameObject("BeltSlot");
        slotGO.transform.SetParent(slotsContainer, worldPositionStays: false);

        SampleBeltSlot(startDistance, out Vector3 pos, out _);
        pos.y = cardY;
        slotGO.transform.position = pos;
        slotGO.transform.localScale = Vector3.one;

        var entry = new Entry
        {
            color = color,
            slot = slotGO.transform,
            distance = startDistance,
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
        // Reset rotacion local: la carta hereda la orientacion del slot,
        // que sigue el tangente del path (+ offset configurable).
        card.localRotation = Quaternion.identity;

        TryForwardEntry(entry);
    }

    void TryForwardEntry(Entry entry)
    {
        if (entry == null || entry.card == null || ordersManager == null) return;
        var assign = ordersManager.AcquireNextSlot(entry.color);
        if (assign.slot == null) return;

        var card = entry.card;
        var sourceSlot = entry.slot;
        entries.Remove(entry);

        CardTransferTweens.BeltToOrder(card, sourceSlot, assign.slot, assign.order, BuildJumpParams());
    }

    public void TryFlushTo(Order order)
    {
        if (order == null) return;
        Color color = order.Color;

        // Iterar de mas vieja a mas nueva (FIFO): la primera carta que entro a la
        // cinta sale primero. Reservamos el slot del order RECIEN en el delay para
        // que, si otra ruta (TryForwardEntry o un flush concurrente sobre otra
        // order del mismo color) ya tomo la entry o lleno la order, no quede una
        // reserva "fantasma" que impida que la order se vuelva a llenar.
        var candidates = new List<Entry>();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.card == null) continue;
            if (entry.state != State.Riding) continue;
            if (!ColorUtil.ApproximatelyEqual(entry.color, color)) continue;
            candidates.Add(entry);
        }

        int cap = order.SlotCount;
        int sent = 0;
        foreach (var entry in candidates)
        {
            if (sent >= cap) break;
            float delay = sent * flushStagger;
            sent++;

            var capturedEntry = entry;
            var capturedOrder = order;
            var jp = BuildJumpParams();

            DG.Tweening.DOVirtual.DelayedCall(delay, () =>
            {
                if (capturedEntry == null || capturedEntry.card == null) return;
                if (!entries.Contains(capturedEntry)) return;
                if (capturedOrder == null || !ColorUtil.ApproximatelyEqual(capturedEntry.color, capturedOrder.Color)) return;

                var orderSlot = capturedOrder.AcquireNextSlot(capturedEntry.color);
                if (orderSlot == null) return;

                var card = capturedEntry.card;
                var sourceSlot = capturedEntry.slot;
                entries.Remove(capturedEntry);
                CardTransferTweens.BeltToOrder(card, sourceSlot, orderSlot, capturedOrder, jp);
            });
        }
    }

    /// <summary>
    /// Despues de que una order se refilea o se va abajo, intenta drenar el
    /// resto de la cinta a CUALQUIER order disponible (no solo la que disparo
    /// el evento). Cubre el caso en el que varias orders del mismo color
    /// abren capacidad a la vez y la pasada original solo flusheo a una.
    /// </summary>
    public void TryFlushToAny()
    {
        if (ordersManager == null) return;
        var snapshot = new List<Entry>(entries);
        foreach (var entry in snapshot)
        {
            if (entry == null || entry.card == null) continue;
            if (entry.state != State.Riding) continue;
            if (!entries.Contains(entry)) continue;
            TryForwardEntry(entry);
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
    /// Aplica el preset al belt. Si preset es null, usa el defaultPreset. La
    /// regeneracion de visuales se dispara via <see cref="BeltPath.OnPathChanged"/>.
    /// </summary>
    public void ApplyPresetForLevel(BeltPreset preset)
    {
        EnsureRefs();
        if (presetIO != null) presetIO.ApplyPresetForLevel(preset);
    }

    Entry FindBySlot(Transform slot)
    {
        foreach (var e in entries) if (e.slot == slot) return e;
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Edit-mode preview: pinta el punto donde aterriza la primera carta (frente
    /// de cola = entryPathOffset) y las posiciones que tomarian las siguientes
    /// si llegaran a la cinta sin que la primera haya avanzado todavia. Las
    /// posiciones detras del portal (distance &lt; 0) se extrapolan en linea recta
    /// segun el tangente del primer segmento.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!drawEntryGizmo) return;
        if (path == null) path = GetComponent<BeltPath>();
        if (path == null || !path.HasArea) return;

        float spacing = EffectiveEntrySpacing();
        float length = path.GetPathLength();
        int n = Mathf.Max(1, entryGizmoPreviewCount);

        // Frente de cola: donde aterriza la 1ra carta. Esfera verde grande.
        SampleBeltSlot(entryPathOffset, out Vector3 frontPos, out _);
        Vector3 front = new Vector3(frontPos.x, cardY, frontPos.z);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.95f);
        Gizmos.DrawSphere(front, 0.18f);
        Gizmos.color = new Color(0.05f, 0.5f, 0.15f, 1f);
        Gizmos.DrawWireSphere(front, 0.22f);

        // Posiciones de cola: las siguientes cartas aterrizan ADELANTE en el
        // path, no detras. Naranja si el calculo cae mas alla del exit portal
        // (caso raro: la cinta esta saturada).
        Vector3 prev = front;
        for (int i = 1; i < n; i++)
        {
            float d = entryPathOffset + i * spacing;
            bool pastExit = length > 0f && d >= length;
            if (pastExit) d = length - spacing * 0.5f;
            SampleBeltSlot(d, out Vector3 p, out _);
            p.y = cardY;
            float fade = 1f - (i / (float)n) * 0.7f;
            Gizmos.color = pastExit
                ? new Color(1f, 0.55f, 0.1f, fade * 0.85f)
                : new Color(0.4f, 0.9f, 0.5f, fade * 0.85f);
            Gizmos.DrawSphere(p, 0.12f);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.4f);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // Etiqueta opcional sobre el frente.
        UnityEditor.Handles.color = new Color(0.7f, 1f, 0.8f, 1f);
        UnityEditor.Handles.Label(front + Vector3.up * 0.35f,
            $"Entry @ {entryPathOffset:0.00}\nspacing {spacing:0.00}");
    }
#endif
}
