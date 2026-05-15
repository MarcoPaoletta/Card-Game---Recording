using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Choreografia de salida del chunk. Cada carta tiene su propio pipeline
/// independiente: stagger inicial -> deslizar hasta el borde -> saltar al
/// target. NO hay sincronizacion entre cartas; apenas una llega al borde
/// vuela directo a su destino, sin esperar a las otras. El stagger inicial
/// entre cartas crea naturalmente el efecto en cadena.
///
/// El target ya viene asignado por <see cref="Chunk.FlyOutStaggered"/> para
/// que las reservas de slots en OrdersManager/ConveyorBelt se hagan en el
/// momento del click, no diferido.
/// </summary>
public static class BoardExitTweens
{
    public struct Tunings
    {
        public float exitDuration;
        public float exitStagger;
        public Ease exitEase;
        public CardTransferTweens.JumpParams jump;
    }

    public struct CardAssignment
    {
        public Transform card;
        public Order order;
        public Transform orderSlot;
        public Transform beltSlot;
    }

    /// <summary>
    /// Ejecuta la coreografia completa. <paramref name="onCardDone"/> se invoca
    /// una vez por carta (al final de su fase 2). El caller cuenta y dispara
    /// el cleanup del chunk cuando todas llegaron.
    /// </summary>
    public static void Play(
        List<CardAssignment> ordered,
        Vector3 dirVec,
        float exitDistance,
        ConveyorBelt belt,
        Tunings t,
        Action onCardDone)
    {
        int n = ordered.Count;
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            var a = ordered[i];
            float startDelay = i * t.exitStagger;
            DOVirtual.DelayedCall(startDelay, () =>
            {
                if (a.card == null) { onCardDone?.Invoke(); return; }

                // Fase 1: deslizar hasta el borde.
                var edgePos = a.card.position + dirVec * exitDistance;
                a.card.DOMove(edgePos, t.exitDuration).SetEase(t.exitEase).OnComplete(() =>
                {
                    if (a.card == null) { onCardDone?.Invoke(); return; }
                    // Fase 2: saltar directo al target, sin esperar a las otras cartas.
                    if (a.orderSlot != null)
                        CardTransferTweens.BoardToOrder(a.card, a.orderSlot, a.order, t.jump, onCardDone);
                    else if (a.beltSlot != null && belt != null)
                        CardTransferTweens.BoardToBelt(a.card, a.beltSlot, belt, t.jump, onCardDone);
                    else
                        CardTransferTweens.BoardToVoid(a.card, a.card.position + dirVec * (exitDistance * 0.3f), t.jump, onCardDone);
                });
            });
        }
    }
}
