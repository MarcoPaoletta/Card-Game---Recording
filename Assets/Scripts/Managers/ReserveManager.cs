using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Repositorio visual para cartas que no pudieron entrar a una order (chunk
/// con cartas sobrantes, o cartas de color que no matchea ningun slot abierto).
/// Cuando una order se "refilea" y queda con slots libres, ReserveManager
/// intenta volcar cartas matcheantes desde el reserve hacia esa order.
/// </summary>
public class ReserveManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform reserveArea;
    [SerializeField] private OrdersManager ordersManager;

    [Header("Layout visual")]
    [Tooltip("Distancia entre cartas en X.")]
    [SerializeField] private float spacingX = 0.35f;
    [Tooltip("Distancia entre filas en Z.")]
    [SerializeField] private float spacingZ = 0.5f;
    [Tooltip("Cuantas cartas por fila antes de pasar a la siguiente.")]
    [SerializeField] private int columns = 8;
    [Tooltip("Altura Y para las cartas en el reserve.")]
    [SerializeField] private float cardY = 0.5f;

    [Header("Animacion")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float jumpPower = 1.0f;
    [SerializeField] private float repackDuration = 0.2f;

    class Entry
    {
        public Transform card;
        public Color color;
        public Transform slot; // un GO vacio en reserveArea que sirve de "slot"
    }

    private readonly List<Entry> reserved = new List<Entry>();

    public int Count => reserved.Count;

    public bool HasArea => reserveArea != null;

    /// <summary>
    /// Crea un slot vacio en el reserveArea para que el chunk haga volar la carta
    /// hacia ahi. Devuelve el Transform destino. Si reserveArea es null, devuelve null.
    /// </summary>
    public Transform AcquireSlot(Color color)
    {
        if (reserveArea == null) return null;
        var go = new GameObject("ReserveSlot");
        var t = go.transform;
        t.SetParent(reserveArea, worldPositionStays: false);
        var entry = new Entry { color = color, slot = t };
        reserved.Add(entry);
        UpdateSlotPositions();
        return t;
    }

    /// <summary>
    /// Asocia la carta con el slot reservado y la deja parentada al reserveArea.
    /// Llamar desde el OnComplete del tween que llevo la carta al slot.
    /// Si en este momento hay un order con color matcheante y slot libre,
    /// la carta se redirige inmediatamente al order (evita race entre flush y arribo).
    /// </summary>
    public void AttachCardToSlot(Transform slot, Transform card)
    {
        if (slot == null || card == null) return;
        var entry = FindBySlot(slot);
        if (entry == null)
        {
            // El slot fue eliminado (no deberia pasar con el fix, pero defensivo).
            Destroy(card.gameObject);
            return;
        }
        entry.card = card;
        card.SetParent(slot, worldPositionStays: true);
        card.localPosition = Vector3.zero;

        // Si hay order disponible, mandar la carta ahi sin que pase por reserve visualmente.
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

        reserved.Remove(entry);
        card.SetParent(null, worldPositionStays: true);
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

        UpdateSlotPositions();
    }

    /// <summary>
    /// Intenta volcar cartas del reserve hacia <paramref name="order"/> mientras
    /// haya cartas del color del order y slots libres en el order.
    /// </summary>
    public void TryFlushTo(Order order)
    {
        if (order == null) return;
        Color color = order.Color;

        bool anyMoved = false;
        for (int i = reserved.Count - 1; i >= 0 && !order.IsFull; i--)
        {
            var e = reserved[i];
            if (!ColorUtil.ApproximatelyEqual(e.color, color)) continue;
            if (e.card == null)
            {
                // Carta todavia en vuelo hacia el reserve: NO tocar el slot.
                // Cuando aterrice, AttachCardToSlot + TryForwardEntry la llevaran
                // al order automaticamente.
                continue;
            }
            var orderSlot = order.AcquireNextSlot(color);
            if (orderSlot == null) break;

            var card = e.card;
            var sourceSlot = e.slot;
            reserved.RemoveAt(i);
            anyMoved = true;

            // Despegar la carta del slot reserve.
            card.SetParent(null, worldPositionStays: true);
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

        if (anyMoved) UpdateSlotPositions();
    }

    public void Clear()
    {
        foreach (var e in reserved)
        {
            if (e.card != null) Destroy(e.card.gameObject);
            if (e.slot != null) Destroy(e.slot.gameObject);
        }
        reserved.Clear();
    }

    Entry FindBySlot(Transform slot)
    {
        foreach (var e in reserved) if (e.slot == slot) return e;
        return null;
    }

    void UpdateSlotPositions()
    {
        if (reserveArea == null) return;
        for (int i = 0; i < reserved.Count; i++)
        {
            var slot = reserved[i].slot;
            if (slot == null) continue;
            int row = i / Mathf.Max(1, columns);
            int col = i % Mathf.Max(1, columns);
            float x = (col - (columns - 1) * 0.5f) * spacingX;
            float z = row * spacingZ;
            Vector3 localPos = new Vector3(x, cardY - reserveArea.position.y, z);
            slot.DOKill();
            slot.DOLocalMove(localPos, repackDuration).SetEase(Ease.OutQuad);
        }
    }

}
