using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class OrdersManager : MonoBehaviour
{
    [SerializeField] private List<Order> orders = new List<Order>();
    [Tooltip("Tiempo entre que el order termina de hacer scale down y el nuevo aparece.")]
    [SerializeField] private float refillDelay = 0.15f;

    void Awake()
    {
        if (orders == null) orders = new List<Order>();
        orders.RemoveAll(o => o == null);
        if (orders.Count == 0)
            orders = new List<Order>(FindObjectsByType<Order>(FindObjectsSortMode.InstanceID));

        foreach (var o in orders)
            if (o != null) o.OnFilled += HandleOrderFilled;
    }

    public void AssignColors(IReadOnlyList<Color> palette)
    {
        if (palette == null || palette.Count == 0) return;
        int n = 0;
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i] == null) continue;
            orders[i].SetColor(palette[n % palette.Count]);
            n++;
        }
    }

    public (Order order, Transform slot) AcquireNextSlot()
    {
        foreach (var o in orders)
        {
            if (o == null || o.IsFull) continue;
            var s = o.AcquireNextSlot();
            if (s != null) return (o, s);
        }
        return (null, null);
    }

    void HandleOrderFilled(Order order)
    {
        var down = order.PlayScaleDown();
        if (down != null)
        {
            down.OnComplete(() =>
            {
                order.ResetSlots();
                DOVirtual.DelayedCall(refillDelay, () => order.PlayScaleUp());
            });
        }
    }
}
