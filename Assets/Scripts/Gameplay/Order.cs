using System;
using DG.Tweening;
using UnityEngine;

public class Order : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField] private Color color = Color.white;
    [Tooltip("Indice del material en el MeshRenderer del model donde se aplica el color del order.")]
    [SerializeField] private int colorMaterialIndex = 1;
    [SerializeField] private float scaleDuration = 0.3f;

    private Transform[] slots;
    private int reservedCount;
    private int deliveredCount;
    private Vector3 originalScale;

    public bool IsFull => slots != null && reservedCount >= slots.Length;
    public int SlotCount => slots != null ? slots.Length : 0;
    public Color Color => color;
    public event Action<Order> OnFilled;

    public void SetColor(Color c)
    {
        color = c;
        if (model != null) ApplyColor();
    }

    void Awake()
    {
        if (model != null)
        {
            originalScale = model.localScale;
            slots = new Transform[model.childCount];
            for (int i = 0; i < model.childCount; i++) slots[i] = model.GetChild(i);
            ApplyColor();
        }
    }

    void ApplyColor()
    {
        var mr = model.GetComponent<MeshRenderer>();
        if (mr == null) return;
        var mats = mr.materials;
        if (colorMaterialIndex < 0 || colorMaterialIndex >= mats.Length) return;
        var mat = mats[colorMaterialIndex];
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }

    public Transform AcquireNextSlot(Color cardColor)
    {
        if (IsFull || slots == null) return null;
        if (!ColorsApproximatelyEqual(cardColor, color)) return null;
        var s = slots[reservedCount];
        reservedCount++;
        return s;
    }

    static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        const float eps = 0.01f;
        return Mathf.Abs(a.r - b.r) < eps
            && Mathf.Abs(a.g - b.g) < eps
            && Mathf.Abs(a.b - b.b) < eps;
    }

    public void NotifyDelivered()
    {
        deliveredCount++;
        if (slots != null && deliveredCount >= slots.Length) OnFilled?.Invoke(this);
    }

    public Tween PlayScaleDown()
    {
        if (model == null) return null;
        return model.DOScale(Vector3.zero, scaleDuration).SetEase(Ease.InBack);
    }

    public Tween PlayScaleUp()
    {
        if (model == null) return null;
        return model.DOScale(originalScale, scaleDuration).SetEase(Ease.OutBack);
    }

    public void ResetSlots()
    {
        if (slots == null) return;
        foreach (var s in slots)
        {
            if (s == null) continue;
            for (int i = s.childCount - 1; i >= 0; i--)
                Destroy(s.GetChild(i).gameObject);
        }
        reservedCount = 0;
        deliveredCount = 0;
    }
}
