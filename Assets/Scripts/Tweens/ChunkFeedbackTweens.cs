using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Tweens de feedback sobre un chunk del tablero: rebote contra blocker,
/// flash rojo y punch de escala. Centralizan los parametros para que el
/// "feel" sea consistente.
/// </summary>
public static class ChunkFeedbackTweens
{
    /// <summary>
    /// Rebote: el chunk se mueve <paramref name="nudge"/> en la direccion de empuje
    /// y vuelve a su posicion original. Util cuando hay un blocker.
    /// </summary>
    public static Sequence Bounce(Transform chunk, Vector3 originalPos, Vector3 dirVec, float nudge, float halfDuration, Action onComplete = null)
    {
        if (chunk == null) return null;
        var seq = DOTween.Sequence();
        seq.Append(chunk.DOMove(originalPos + dirVec * nudge, halfDuration).SetEase(Ease.OutQuad));
        seq.Append(chunk.DOMove(originalPos, halfDuration).SetEase(Ease.InQuad));
        if (onComplete != null) seq.OnComplete(() => onComplete());
        return seq;
    }

    /// <summary>
    /// Combina punch de escala + flash rojo en todos los materiales hijos.
    /// </summary>
    public static void FlashAndPunch(Transform chunk, float punchScale, float flashDuration)
    {
        if (chunk == null) return;
        chunk.DOKill(complete: true);
        chunk.DOPunchScale(Vector3.one * punchScale, flashDuration * 2.5f, 6, 0.5f);

        foreach (var mr in chunk.GetComponentsInChildren<MeshRenderer>())
        foreach (var mat in mr.materials)
            FlashRed(mat, flashDuration);
    }

    static void FlashRed(Material mat, float flashDuration)
    {
        bool useBase = mat.HasProperty("_BaseColor");
        Color original = useBase ? mat.GetColor("_BaseColor") : mat.color;
        var seq = DOTween.Sequence();
        if (useBase)
        {
            seq.Append(mat.DOColor(Color.red, "_BaseColor", flashDuration));
            seq.Append(mat.DOColor(original, "_BaseColor", flashDuration));
        }
        else
        {
            seq.Append(mat.DOColor(Color.red, flashDuration));
            seq.Append(mat.DOColor(original, flashDuration));
        }
    }
}
