using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class OrdersManager : MonoBehaviour
{
    [SerializeField] private List<Order> orders = new List<Order>();

    public int OrderCount => orders != null ? orders.Count : 0;
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

    public (Order order, Transform slot) AcquireNextSlot(Color color)
    {
        foreach (var o in orders)
        {
            if (o == null || o.IsFull) continue;
            var s = o.AcquireNextSlot(color);
            if (s != null) return (o, s);
        }
        return (null, null);
    }

    /// <summary>
    /// Devuelve el bounding box (XZ) que cubre los renderers de todas las orders.
    /// </summary>
    public Bounds GetGroupRenderBounds()
    {
        bool any = false;
        Bounds combined = new Bounds();
        foreach (var o in orders)
        {
            if (o == null) continue;
            var rs = o.GetComponentsInChildren<Renderer>();
            foreach (var r in rs)
            {
                if (r == null) continue;
                if (!any) { combined = r.bounds; any = true; }
                else combined.Encapsulate(r.bounds);
            }
            if (!any)
            {
                combined = new Bounds(o.transform.position, Vector3.zero);
                any = true;
            }
        }
        return any ? combined : new Bounds(Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// Traslada el grupo de orders (preservando layout relativo) para que el borde
    /// mas cercano al board quede a <paramref name="zGap"/> unidades del borde del
    /// board sobre el eje Z, centrado en X. El lado (positivo/negativo de Z) se
    /// elige segun donde estan las orders actualmente respecto al board.
    /// </summary>
    public void Reposition(Bounds boardBounds, float zGap)
    {
        if (orders == null || orders.Count == 0) return;
        var group = GetGroupRenderBounds();
        if (group.size == Vector3.zero) return;

        int side = group.center.z >= boardBounds.center.z ? +1 : -1;

        float deltaZ;
        if (side > 0)
        {
            float targetMinZ = boardBounds.max.z + zGap;
            deltaZ = targetMinZ - group.min.z;
        }
        else
        {
            float targetMaxZ = boardBounds.min.z - zGap;
            deltaZ = targetMaxZ - group.max.z;
        }

        float deltaX = boardBounds.center.x - group.center.x;
        var offset = new Vector3(deltaX, 0f, deltaZ);

        foreach (var o in orders)
        {
            if (o == null) continue;
            o.transform.position += offset;
        }
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
