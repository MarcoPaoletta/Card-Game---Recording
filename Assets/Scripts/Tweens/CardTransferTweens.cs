using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Tweens de transferencia de cartas entre los tres "stores" del juego:
/// Board (chunk en el tablero), Belt (cinta), Order (slot final).
/// Cada metodo es un caso de uso especifico y encapsula el patron
/// jump + reparentado + cleanup, asi los call sites quedan declarativos.
/// </summary>
public static class CardTransferTweens
{
    /// <summary>Parametros comunes de un salto entre stores.</summary>
    public struct JumpParams
    {
        public float jumpPower;
        public int jumpCount;
        public float duration;
        public Ease ease;

        public static JumpParams Default => new JumpParams
        {
            jumpPower = 1.0f,
            jumpCount = 1,
            duration = 0.45f,
            ease = Ease.OutQuad,
        };
    }

    /// <summary>
    /// Board -> Order. Salta la carta al slot del order, la reparenta y notifica
    /// la entrega. Si <paramref name="onArrived"/> esta seteado, se invoca despues.
    /// </summary>
    public static Sequence BoardToOrder(Transform card, Transform orderSlot, Order order, JumpParams jp, Action onArrived = null)
    {
        if (card == null || orderSlot == null) return null;
        card.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(card.DOJump(orderSlot.position, jp.jumpPower, jp.jumpCount, jp.duration).SetEase(jp.ease));
        seq.OnComplete(() =>
        {
            if (card == null || orderSlot == null) return;
            card.SetParent(orderSlot, worldPositionStays: true);
            card.localPosition = Vector3.zero;
            card.localRotation = Quaternion.identity;
            if (order != null) order.NotifyDelivered();
            onArrived?.Invoke();
        });
        return seq;
    }

    /// <summary>
    /// Board -> Belt. Salta la carta al slot de la cinta y delega el attach
    /// final al ConveyorBelt (que se encarga del parent + estado interno del entry).
    /// </summary>
    public static Sequence BoardToBelt(Transform card, Transform beltSlot, ConveyorBelt belt, JumpParams jp, Action onArrived = null)
    {
        if (card == null || beltSlot == null || belt == null) return null;
        card.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(card.DOJump(beltSlot.position, jp.jumpPower, jp.jumpCount, jp.duration).SetEase(jp.ease));
        seq.OnComplete(() =>
        {
            if (card != null && beltSlot != null) belt.AttachCardToSlot(beltSlot, card);
            onArrived?.Invoke();
        });
        return seq;
    }

    /// <summary>
    /// Belt -> Order. Desacopla la carta del slot de la cinta, la salta al slot
    /// del order, la reparenta y notifica la entrega. Destruye el slot fuente.
    /// </summary>
    public static Sequence BeltToOrder(Transform card, Transform beltSlot, Transform orderSlot, Order order, JumpParams jp)
    {
        if (card == null || orderSlot == null) return null;

        card.SetParent(null, worldPositionStays: true);
        card.localScale = Vector3.one;
        if (beltSlot != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(beltSlot.gameObject);
            else UnityEngine.Object.DestroyImmediate(beltSlot.gameObject);
        }

        card.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(card.DOJump(orderSlot.position, jp.jumpPower, jp.jumpCount, jp.duration).SetEase(jp.ease));
        seq.OnComplete(() =>
        {
            if (card == null || orderSlot == null) return;
            card.SetParent(orderSlot, worldPositionStays: true);
            card.localPosition = Vector3.zero;
            card.localRotation = Quaternion.identity;
            if (order != null) order.NotifyDelivered();
        });
        return seq;
    }

    /// <summary>
    /// Board -> Salida libre (no hay order ni belt disponible). Salta la carta
    /// hacia <paramref name="worldTarget"/> y la destruye al llegar.
    /// </summary>
    public static Sequence BoardToVoid(Transform card, Vector3 worldTarget, JumpParams jp, Action onComplete = null)
    {
        if (card == null) return null;
        card.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(card.DOJump(worldTarget, jp.jumpPower, jp.jumpCount, jp.duration).SetEase(jp.ease));
        seq.OnComplete(() =>
        {
            if (card != null) UnityEngine.Object.Destroy(card.gameObject);
            onComplete?.Invoke();
        });
        return seq;
    }
}
