using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Cinta transportadora con modelo de slot pool: el preset define una
/// capacidad (cuantas cartas caben a la vez) y al aplicarse construimos un
/// arreglo fijo de slot Transforms equiespaciados por entrySpacing a lo largo
/// del path, empezando en entryPathOffset. Cada slot acepta UNA carta a la
/// vez; cuando una carta sale a un order, su slot queda libre y la proxima
/// carta entrante lo reutiliza. Asi nunca dos cartas comparten posicion.
///
/// La geometria del path vive en <see cref="BeltPath"/>; los visuales
/// (portales + partes) en <see cref="BeltVisuals"/>; la I/O de presets en
/// <see cref="BeltPresetIO"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BeltPath))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private OrdersManager ordersManager;
    [Tooltip("Spawner del board. Se usa para leer su card spacing y mantenerlo igual en la cinta.")]
    [SerializeField] private CardsSpawnerManager cardsSpawner;

    [Header("Slot pool")]
    [Tooltip("Distancia desde el portal de entrada (path = 0) donde se ubica el slot 0 cuando phase=0. Cada slot siguiente esta a entrySpacing del anterior.")]
    [SerializeField] private float entryPathOffset = 0.4f;
    [Tooltip("Espaciado entre slots a lo largo del path. Si es <= 0 se usa CardsSpawnerManager.CardSpacing (default 0.2).")]
    [SerializeField] private float entrySpacing = 0f;
    [Tooltip("Altura Y a la que se posicionan los slots/cartas.")]
    [SerializeField] private float cardY = 0.5f;
    [Tooltip("Velocidad de rotacion del pool a lo largo del path (units/seg). Todos los slots avanzan juntos manteniendo entrySpacing entre si y vuelven al inicio al pasar el portal de salida. Las cartas viajan pegadas a su slot.")]
    [SerializeField] private float beltSpeed = 0.6f;
    [Tooltip("Solo editor: dibuja un gizmo con todos los slots del pool. Usa el preset al que apunta editorTargetPreset (el que estas editando con Save/Load); si no hay, cae a defaultPreset; si no, auto.")]
    [SerializeField] private bool drawSlotsGizmo = true;

    [Header("Animaciones de salida (hacia orders)")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float jumpPower = 1.0f;
    [Tooltip("Stagger global de salidas belt->order. Siempre se respeta entre cualquier par de cartas que salen, sin importar la ruta (TryForwardEntry, TryFlushTo, TryFlushToAny).")]
    [SerializeField] private float flushStagger = 0.12f;

    [Header("Rotacion de la carta en cinta")]
    [Tooltip("Offset Euler aplicado a la rotacion del slot, despues del LookRotation por tangente. Usar si el modelo de la carta no tiene su 'frente' alineado con +Z.")]
    [SerializeField] private Vector3 cardOnBeltEulerOffset = new Vector3(0f, -90f, 0f);

    private const string SlotsContainerName = "Slots";
    private Transform slotsContainer;
    private BeltPath path;
    private BeltPresetIO presetIO;
    private BeltPreset lastAppliedPreset;

    class SlotInfo
    {
        public int index;
        public Transform transform;
        public float baseOffset; // posicion del slot en el pool cuando phase = 0
        public Entry occupant;
    }

    class Entry
    {
        public Color color;
        public Transform card;
        public SlotInfo slotInfo;
        public bool scheduled;
    }

    private readonly List<SlotInfo> slots = new List<SlotInfo>();
    // Lista paralela de entries (cartas que ocupan slots) en orden de llegada.
    // Sirve para que TryFlushTo / TryFlushToAny iteren FIFO y para el throttle
    // global de forwards (ScheduleForward).
    private readonly List<Entry> entries = new List<Entry>();
    private float nextForwardTime;
    // Avance global del pool. Todos los slots tienen distance = (baseOffset + phase) % length.
    private float phase;
    // Indice del proximo slot del pool a usar para una carta entrante. Se incrementa
    // ciclicamente cada AcquireSlot; si esta ocupado, salta al siguiente libre.
    // Asi el llenado SIEMPRE sigue el orden del pool (carta N entra en el slot que
    // viene despues del slot de carta N-1), incluso aunque al rotar otros slots
    // queden mas cerca del portal de entrada. Los huecos solo aparecen cuando
    // un slot del medio se libera por un forward a un order.
    private int nextFillIndex;

    public bool HasArea => path != null && path.HasArea;
    public int SlotCount => slots.Count;
    public int FreeSlotCount
    {
        get { int n = 0; for (int i = 0; i < slots.Count; i++) if (slots[i].occupant == null) n++; return n; }
    }

    void OnEnable() { EnsureRefs(); }

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

    float EffectiveEntrySpacing()
    {
        if (entrySpacing > 0f) return entrySpacing;
        return cardsSpawner != null ? cardsSpawner.CardSpacing : 0.2f;
    }

    int ResolveCapacity()
    {
        var preset = ResolveActivePreset();
        int presetCap = preset != null ? preset.capacity : 0;
        if (presetCap > 0) return presetCap;

        if (path == null || !path.HasArea) return 0;
        float spacing = EffectiveEntrySpacing();
        if (spacing <= 0f) return 1;
        float length = path.GetPathLength();
        return Mathf.Max(1, Mathf.FloorToInt((length - entryPathOffset) / spacing) + 1);
    }

    /// <summary>
    /// Preset al que el componente le esta haciendo caso EN ESTE MOMENTO:
    /// - En play: el ultimo que se aplico via ApplyPresetForLevel (= el del nivel cargado).
    /// - En edit: el editorTargetPreset (el que el dev abre/guarda con los context menu),
    ///   y si no esta seteado, el defaultPreset como fallback.
    /// El gizmo usa este resultado para leer capacity, asi cambiar el capacity
    /// del preset que estas editando refleja en el gizmo, sin tener que
    /// reasignar el defaultPreset.
    /// </summary>
    BeltPreset ResolveActivePreset()
    {
        if (Application.isPlaying) return lastAppliedPreset;
        if (presetIO == null) return null;
        return presetIO.EditorTargetPreset != null ? presetIO.EditorTargetPreset : presetIO.DefaultPreset;
    }

    /// <summary>
    /// Preset activo expuesto publicamente. Permite que sistemas externos (ej.
    /// BeltRepositionerManager) lean configuracion del preset actual (anchor
    /// mode, capacity, etc.) tanto en play como en edit mode.
    /// </summary>
    public BeltPreset ActivePreset => ResolveActivePreset();

    // --- Slot pool ---

    void BuildSlotPool()
    {
        DestroySlotPool();
        EnsureRefs();
        if (!HasArea) return;

        int cap = ResolveCapacity();
        float spacing = EffectiveEntrySpacing();
        float length = path.GetPathLength();

        for (int i = 0; i < cap; i++)
        {
            float d = entryPathOffset + i * spacing;
            if (d > length) break;

            var go = new GameObject($"Slot_{i}");
            go.transform.SetParent(slotsContainer, false);
            path.SamplePath(d, out Vector3 pos, out Vector3 tan);
            pos.y = cardY;
            go.transform.position = pos;
            if (tan.sqrMagnitude > 0.0001f)
                go.transform.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up)
                                      * Quaternion.Euler(cardOnBeltEulerOffset);

            slots.Add(new SlotInfo { index = i, transform = go.transform, baseOffset = d, occupant = null });
        }
        phase = 0f;
        nextFillIndex = 0;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (slots.Count == 0 || path == null || !HasArea) return;
        float length = path.GetPathLength();
        if (length <= 0f) return;

        // Si la cinta esta totalmente vacia, congelamos la rotacion y rebobinamos
        // a phase=0. Asi los slots quedan en su posicion inicial (slot 0 al
        // principio del path) y la proxima carta que entre lo va a hacer
        // siempre desde el portal de entrada, no desde un punto cualquiera
        // segun donde haya quedado el pool girando solo.
        if (entries.Count > 0)
        {
            phase += beltSpeed * Time.deltaTime;
            if (phase >= length) phase -= length;
        }
        else
        {
            if (phase != 0f) phase = 0f;
            if (nextFillIndex != 0) nextFillIndex = 0;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.transform == null) continue;
            float d = (s.baseOffset + phase) % length;
            path.SamplePath(d, out Vector3 pos, out Vector3 tan);
            pos.y = cardY;
            s.transform.position = pos;
            if (tan.sqrMagnitude > 0.0001f)
                s.transform.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up)
                                     * Quaternion.Euler(cardOnBeltEulerOffset);
        }
    }

    float CurrentSlotDistance(SlotInfo s, float length)
    {
        if (length <= 0f) return s.baseOffset;
        return (s.baseOffset + phase) % length;
    }

    void DestroySlotPool()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.transform == null) continue;
            if (Application.isPlaying) Destroy(s.transform.gameObject);
            else DestroyImmediate(s.transform.gameObject);
        }
        slots.Clear();
    }

    // --- API publica ---

    /// <summary>
    /// Reserva el primer slot libre (mas cercano al portal de entrada) para
    /// una carta que viene del board. Devuelve null si la cinta esta llena.
    /// </summary>
    public Transform AcquireSlot(Color color)
    {
        EnsureRefs();
        if (slots.Count == 0) BuildSlotPool();
        if (slots.Count == 0) return null;

        // Llenado en orden de pool: empezamos en nextFillIndex y avanzamos
        // ciclicamente hasta encontrar el primer slot libre. Esto garantiza que
        // las cartas entren siempre adyacentes a la ultima que entro, sin
        // formar huecos por la rotacion del pool. Los slots que se liberen en
        // el medio (por forward a un order) quedan como hueco hasta que el
        // ciclo de fill vuelva a pasarles por arriba.
        int found = -1;
        for (int k = 0; k < slots.Count; k++)
        {
            int idx = (nextFillIndex + k) % slots.Count;
            if (slots[idx].occupant == null) { found = idx; break; }
        }
        if (found < 0) return null;
        nextFillIndex = (found + 1) % slots.Count;
        var best = slots[found];

        var entry = new Entry { color = color, slotInfo = best };
        best.occupant = entry;
        entries.Add(entry);
        return best.transform;
    }

    public void AttachCardToSlot(Transform slot, Transform card)
    {
        if (slot == null || card == null) return;
        var entry = FindBySlot(slot);
        if (entry == null) { Destroy(card.gameObject); return; }

        entry.card = card;
        // Pegamos la carta al slot pero suavizamos el desfasaje que se acumulo
        // mientras volaba: el target del DOJump fue la posicion del slot en el
        // momento del click, y como el pool rota, el slot se movio un poco.
        // Sin esto, hay un pop visible al aterrizar.
        card.SetParent(slot, worldPositionStays: true);
        card.DOKill();
        card.DOLocalMove(Vector3.zero, 0.12f).SetEase(DG.Tweening.Ease.OutQuad);
        card.DOLocalRotateQuaternion(Quaternion.identity, 0.12f).SetEase(DG.Tweening.Ease.OutQuad);

        TryForwardEntry(entry);
    }

    void TryForwardEntry(Entry entry)
    {
        if (entry == null || entry.card == null || ordersManager == null) return;
        if (!HasAnyMatchingOrder(entry.color)) return;
        ScheduleForward(entry);
    }

    bool HasAnyMatchingOrder(Color color)
    {
        if (ordersManager == null) return false;
        foreach (var o in ordersManager.Orders)
        {
            if (o == null || o.IsFull) continue;
            if (ColorUtil.ApproximatelyEqual(o.Color, color)) return true;
        }
        return false;
    }

    public void TryFlushTo(Order order)
    {
        if (order == null) return;
        Color color = order.Color;
        int cap = order.SlotCount;
        int considered = 0;
        // Iteramos de la mas reciente a la mas vieja: las cartas que entraron
        // ultimas (slots con indice mas alto, mas adelantadas en el path) salen
        // primero hacia el order. Asi el belt se va vaciando "desde el frente"
        // y las que estan recien entrando, en los slots de atras, se quedan.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (considered >= cap) break;
            var entry = entries[i];
            if (entry.card == null) continue;
            if (!ColorUtil.ApproximatelyEqual(entry.color, color)) continue;
            considered++;
            ScheduleForward(entry);
        }
    }

    /// <summary>
    /// Despues de que una order se refilea o se va abajo, intenta drenar el
    /// resto de la cinta a CUALQUIER order disponible.
    /// </summary>
    public void TryFlushToAny()
    {
        if (ordersManager == null) return;
        var snapshot = new List<Entry>(entries);
        // Mismo criterio que TryFlushTo: sacar primero las cartas mas recientes
        // (slots mas adelantados en el pool).
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            var entry = snapshot[i];
            if (entry == null || entry.card == null) continue;
            if (!entries.Contains(entry)) continue;
            if (!HasAnyMatchingOrder(entry.color)) continue;
            ScheduleForward(entry);
        }
    }

    /// <summary>
    /// Throttler global: encola un forward para que arranque en el proximo
    /// hueco de la cadena, separando cada salida de la anterior por
    /// flushStagger. Sin esto, varias cartas que llegan en el mismo frame se
    /// ven como "un bloque" en lugar de carta-a-carta.
    /// </summary>
    void ScheduleForward(Entry entry)
    {
        if (entry == null) return;
        if (entry.scheduled) return;
        entry.scheduled = true;

        float now = Time.time;
        float scheduledAt = Mathf.Max(now, nextForwardTime);
        float delay = scheduledAt - now;
        nextForwardTime = scheduledAt + flushStagger;

        var capturedEntry = entry;
        if (delay <= 0.0001f) ExecuteForward(capturedEntry);
        else DG.Tweening.DOVirtual.DelayedCall(delay, () => ExecuteForward(capturedEntry));
    }

    void ExecuteForward(Entry entry)
    {
        if (entry == null) return;
        entry.scheduled = false;
        if (entry.card == null || ordersManager == null) return;
        if (!entries.Contains(entry)) return;

        var assign = ordersManager.AcquireNextSlot(entry.color);
        if (assign.slot == null) return;

        var card = entry.card;
        var sourceSlot = entry.slotInfo != null ? entry.slotInfo.transform : null;
        // Liberar el slot del pool: la carta sale del belt, el slot vuelve a
        // estar disponible para la proxima carta entrante.
        if (entry.slotInfo != null) entry.slotInfo.occupant = null;
        entries.Remove(entry);

        CardTransferTweens.BeltToOrder(card, sourceSlot, assign.slot, assign.order, BuildJumpParams());
    }

    public void Clear()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (e.card != null) Destroy(e.card.gameObject);
            if (e.slotInfo != null) e.slotInfo.occupant = null;
        }
        entries.Clear();
        nextForwardTime = 0f;
        nextFillIndex = 0;
    }

    /// <summary>
    /// Aplica el preset al belt y reconstruye el slot pool segun la capacidad
    /// del preset (o auto si capacity == 0).
    /// </summary>
    public void ApplyPresetForLevel(BeltPreset preset)
    {
        EnsureRefs();
        Clear();
        if (presetIO != null) presetIO.ApplyPresetForLevel(preset);
        lastAppliedPreset = preset != null ? preset : (presetIO != null ? presetIO.DefaultPreset : null);
        BuildSlotPool();
    }

    Entry FindBySlot(Transform slot)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.slotInfo != null && e.slotInfo.transform == slot) return e;
        }
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Edit-mode preview del slot pool: dibuja una esfera por cada slot que
    /// se generaria con el preset por defecto (o el ultimo aplicado en runtime)
    /// y la capacity actual. Sirve para tunear entryPathOffset / entrySpacing /
    /// BeltPreset.capacity sin entrar a play y ver donde van a aterrizar las
    /// cartas.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!drawSlotsGizmo) return;
        if (path == null) path = GetComponent<BeltPath>();
        if (presetIO == null) presetIO = GetComponent<BeltPresetIO>();
        if (path == null || !path.HasArea) return;

        int cap = ResolveCapacity();
        if (cap <= 0) return;
        float spacing = EffectiveEntrySpacing();
        float length = path.GetPathLength();

        Vector3 prev = Vector3.zero;
        bool first = true;
        int drawn = 0;
        for (int i = 0; i < cap; i++)
        {
            float d = entryPathOffset + i * spacing;
            if (d > length) break;
            path.SamplePath(d, out Vector3 pos, out _);
            pos.y = cardY;

            if (i == 0)
            {
                // Slot 0 (donde aterriza el tween de entrada). Naranja para
                // diferenciarlo del verde de los slots de capacity.
                Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.95f);
                Gizmos.DrawSphere(pos, 0.18f);
                Gizmos.color = new Color(0.6f, 0.3f, 0.05f, 1f);
                Gizmos.DrawWireSphere(pos, 0.22f);
            }
            else
            {
                float t = cap > 1 ? i / (float)(cap - 1) : 0f;
                Gizmos.color = new Color(0.35f, 0.85f - t * 0.35f, 0.5f + t * 0.3f, 0.85f);
                Gizmos.DrawSphere(pos, 0.12f);
            }

            if (!first)
            {
                Gizmos.color = new Color(0.2f, 0.7f, 0.3f, 0.35f);
                Gizmos.DrawLine(prev, pos);
            }
            prev = pos;
            first = false;
            drawn++;
        }

        // Label sobre el slot 0 con capacity actual, de donde sale el numero
        // (preset o auto) y el nombre del preset que se esta usando.
        path.SamplePath(entryPathOffset, out Vector3 fp, out _);
        fp.y = cardY;
        var activePreset = ResolveActivePreset();
        bool capFromPreset = activePreset != null && activePreset.capacity > 0;
        string capLabel = capFromPreset ? $"{drawn}" : $"{drawn} (auto)";
        string presetLabel = activePreset != null ? activePreset.name : "(sin preset)";
        UnityEditor.Handles.color = new Color(0.7f, 1f, 0.8f, 1f);
        UnityEditor.Handles.Label(fp + Vector3.up * 0.35f,
            $"Preset: {presetLabel}\nSlots {capLabel}\nSpacing {spacing:0.00}");
    }
#endif
}
