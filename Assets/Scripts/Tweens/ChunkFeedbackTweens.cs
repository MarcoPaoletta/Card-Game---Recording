using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Tweens de feedback sobre un chunk del tablero: bump-collide del chunk
/// clickeado, flash rojo del bloqueador, cascada de punch scale en la cadena
/// de cartas, y rebote de retorno.
///
/// El flash usa un <see cref="MaterialColorCache"/> que captura los colores
/// originales la primera vez (despues de <see cref="Card.Setup"/>) y los reusa
/// en flashes subsecuentes. Esto evita el bug clasico de "capturar el color
/// durante un flash en curso" — que dejaba al chunk con un tinte rojizo.
/// </summary>
public static class ChunkFeedbackTweens
{
    /// <summary>
    /// Snapshot de los colores originales de todos los materiales de un chunk.
    /// Se captura lazy en el primer <see cref="Flash"/> y se reusa siempre.
    /// </summary>
    public class MaterialColorCache
    {
        struct Entry { public Material mat; public bool useBase; public Color original; }
        readonly List<Entry> entries = new List<Entry>();
        bool captured;

        public void EnsureCaptured(Transform root)
        {
            if (captured) return;
            entries.Clear();
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr == null) continue;
                foreach (var mat in mr.materials)
                {
                    if (mat == null) continue;
                    bool useBase = mat.HasProperty("_BaseColor");
                    entries.Add(new Entry
                    {
                        mat = mat,
                        useBase = useBase,
                        original = useBase ? mat.GetColor("_BaseColor") : mat.color,
                    });
                }
            }
            captured = true;
        }

        public void Flash(float flashDuration)
        {
            foreach (var e in entries)
            {
                if (e.mat == null) continue;
                // Cancelar cualquier flash previo sobre este material para que
                // el siguiente arranque limpio. Restauramos al original (no al
                // valor actual) asi nunca se "ancla" en un color intermedio.
                DOTween.Kill(e.mat);
                var seq = DOTween.Sequence().SetTarget(e.mat);
                if (e.useBase)
                {
                    seq.Append(e.mat.DOColor(Color.red, "_BaseColor", flashDuration));
                    seq.Append(e.mat.DOColor(e.original, "_BaseColor", flashDuration));
                }
                else
                {
                    seq.Append(e.mat.DOColor(Color.red, flashDuration));
                    seq.Append(e.mat.DOColor(e.original, flashDuration));
                }
                // Si el sequence es matado (ej. otro flash sobre el mismo mat),
                // forzamos el color original para que no quede rojizo.
                var captured = e;
                seq.OnKill(() => { if (captured.mat != null) ForceRestore(captured); });
            }
        }

        static void ForceRestore(Entry e)
        {
            if (e.useBase) e.mat.SetColor("_BaseColor", e.original);
            else e.mat.color = e.original;
        }
    }

    /// <summary>Flash rojo solo (sin punch de transform).</summary>
    public static void FlashRed(Transform chunk, MaterialColorCache cache, float flashDuration)
    {
        if (chunk == null || cache == null) return;
        cache.EnsureCaptured(chunk);
        cache.Flash(flashDuration);
    }

    /// <summary>
    /// Impacto sobre el blocker: flash rojo + punch scale del transform.
    /// Se ejecuta sincronicamente (ambos arrancan al mismo tiempo).
    /// </summary>
    public static void BlockerImpact(Transform blocker, MaterialColorCache cache, float flashDuration, float punchScale, float punchDuration)
    {
        if (blocker == null) return;
        FlashRed(blocker, cache, flashDuration);
        blocker.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f);
    }

    /// <summary>
    /// Bump-collide: el chunk se desliza <paramref name="bumpDistance"/> en
    /// <paramref name="dirVec"/>, dispara <paramref name="onCollide"/> al
    /// tocar al blocker, y vuelve a la posicion original. La cascada
    /// (disparada externamente desde <paramref name="onCollide"/>) corre en
    /// paralelo con el return: ambos arrancan en el frame del impacto. Si la
    /// cascada dura mas que el return, se padea el sequence para que
    /// <paramref name="onComplete"/> no se dispare antes de tiempo.
    /// </summary>
    public static Sequence BumpCollideAndReturn(
        Transform chunk,
        Vector3 originalPos,
        Vector3 dirVec,
        float bumpDistance,
        float pushDuration,
        float cascadeHoldTime,
        float returnDuration,
        Action onCollide,
        Action onComplete)
    {
        if (chunk == null) return null;
        chunk.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(chunk.DOMove(originalPos + dirVec * bumpDistance, pushDuration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => onCollide?.Invoke());
        // Return arranca al mismo tiempo que la cascada (que se dispara en
        // onCollide). Si la cascada es mas larga, padeamos el extra al final.
        seq.Append(chunk.DOMove(originalPos, returnDuration).SetEase(Ease.InOutQuad));
        float extraHold = cascadeHoldTime - returnDuration;
        if (extraHold > 0f) seq.AppendInterval(extraHold);
        seq.OnComplete(() => onComplete?.Invoke());
        return seq;
    }

    /// <summary>
    /// Onda de punch scale recorriendo las cartas en orden. La primera
    /// (<paramref name="ordered"/>[0]) suele ser la leading card que choco.
    /// </summary>
    public static void CascadePunch(
        IList<Transform> ordered,
        float punchScale,
        float punchDuration,
        float stagger,
        int vibrato = 4,
        float elasticity = 0.5f)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            var card = ordered[i];
            if (card == null) continue;
            float delay = i * stagger;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (card == null) return;
                card.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato, elasticity);
            });
        }
    }

    /// <summary>Tiempo total que dura la cascada (ultimo card termina su punch).</summary>
    public static float CascadeTotalTime(int cardCount, float punchDuration, float stagger)
        => punchDuration + Mathf.Max(0, cardCount - 1) * stagger;
}
