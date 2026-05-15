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
        path.SamplePath(e.distance, out Vector3 pos, out Vector3 tangent);
        pos.y = cardY;
        e.slot.position = pos;

        if (tangent.sqrMagnitude > 0.0001f)
        {
            var baseRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            e.slot.rotation = baseRot * Quaternion.Euler(cardOnBeltEulerOffset);
        }

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

    // --- API publica ---

    public Transform AcquireSlot(Color color)
    {
        EnsureRefs();
        if (!HasArea) return null;

        // Empezar detras de la ultima carta en cola (la de menor distance),
        // respetando el spacing del board (CardsSpawnerManager.CardSpacing).
        // Si la cola pasa el inicio del path, el distance queda negativo y
        // la carta espera fisicamente en el portal hasta que avance.
        float spacing = cardsSpawner != null ? cardsSpawner.CardSpacing : 0.2f;
        float startDistance = 0f;
        if (entries.Count > 0)
        {
            float minDist = entries[0].distance;
            for (int i = 1; i < entries.Count; i++)
                if (entries[i].distance < minDist) minDist = entries[i].distance;
            startDistance = Mathf.Min(0f, minDist - spacing);
        }

        var slotGO = new GameObject("BeltSlot");
        slotGO.transform.SetParent(slotsContainer, worldPositionStays: false);

        path.SamplePath(Mathf.Max(0f, startDistance), out Vector3 pos, out _);
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

        // Reservamos slots del order y disparamos cada salida con stagger.
        // La entry permanece en `entries` (la carta sigue paseando por la cinta)
        // hasta que le toca su turno, asi visualmente no se "congela" en su lugar.
        int sentCount = 0;
        for (int i = entries.Count - 1; i >= 0 && !order.IsFull; i--)
        {
            var entry = entries[i];
            if (!ColorUtil.ApproximatelyEqual(entry.color, color)) continue;
            if (entry.card == null) continue;

            var orderSlot = order.AcquireNextSlot(color);
            if (orderSlot == null) break;

            float delay = sentCount * flushStagger;
            sentCount++;

            var capturedEntry = entry;
            var capturedOrderSlot = orderSlot;
            var capturedOrder = order;
            var jp = BuildJumpParams();

            DG.Tweening.DOVirtual.DelayedCall(delay, () =>
            {
                if (capturedEntry == null || capturedEntry.card == null) return;
                var card = capturedEntry.card;
                var sourceSlot = capturedEntry.slot;
                entries.Remove(capturedEntry);
                CardTransferTweens.BeltToOrder(card, sourceSlot, capturedOrderSlot, capturedOrder, jp);
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
}
