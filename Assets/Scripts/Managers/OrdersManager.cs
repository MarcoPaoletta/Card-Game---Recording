using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class OrdersManager : MonoBehaviour
{
    [SerializeField] private List<Order> orders = new List<Order>();
    [Tooltip("Tiempo entre que el order termina de hacer scale down y el nuevo aparece.")]
    [SerializeField] private float refillDelay = 0.15f;

    [Header("Refs")]
    [SerializeField] private LevelFlowManager levelFlow;
    [SerializeField] private ConveyorBelt reserve;

    public int OrderCount => orders != null ? orders.Count : 0;

    void Awake()
    {
        if (orders == null) orders = new List<Order>();
        orders.RemoveAll(o => o == null);
        if (orders.Count == 0)
            orders = new List<Order>(FindObjectsByType<Order>(FindObjectsSortMode.InstanceID));

        foreach (var o in orders)
            if (o != null) o.OnFilled += HandleOrderFilled;
    }

    /// <summary>
    /// Asigna colores a las orders en escena. Los slots no usados (palette mas
    /// corto que la cantidad de orders) se escalan a cero para no quedar visibles.
    /// </summary>
    public void AssignColors(IReadOnlyList<Color> palette)
    {
        if (palette == null) return;
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i] == null) continue;
            if (i < palette.Count)
            {
                orders[i].SetColor(palette[i]);
                // ResetForLevel ya las puso en escala original; nada mas que hacer.
            }
            else
            {
                // Order sin uso en este nivel: ocultar.
                orders[i].PlayScaleDown();
            }
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
    /// Restaura todas las orders al estado de inicio de un nivel: limpia slots,
    /// restaura escala, listas para recibir colores nuevos.
    /// </summary>
    public void ResetForLevel()
    {
        foreach (var o in orders)
        {
            if (o == null) continue;
            o.ResetForLevel();
        }
    }

    /// <summary>
    /// Llamado cuando una order se completa. Decide si reciclarla (con mismo o
    /// nuevo color segun lo que pida LevelFlowManager) o dejarla abajo.
    /// </summary>
    void HandleOrderFilled(Order order)
    {
        if (levelFlow != null) levelFlow.NotifyOrderCompleted();

        bool refill = false;
        Color nextColor = order.Color;
        if (levelFlow != null && levelFlow.TryGetNextOrderColor(out var c))
        {
            refill = true;
            nextColor = c;
        }

        var down = order.PlayScaleDown();
        if (!refill)
        {
            // Permanentemente abajo: no hay mas cartas que requieran esta order.
            return;
        }

        if (down != null)
        {
            down.OnComplete(() =>
            {
                order.ResetSlots();
                order.SetColor(nextColor);
                DOVirtual.DelayedCall(refillDelay, () =>
                {
                    order.PlayScaleUp();
                    // Despues de mostrar el order vacio, drenar lo que se pueda del reserve.
                    if (reserve != null) reserve.TryFlushTo(order);
                });
            });
        }
        else
        {
            order.ResetSlots();
            order.SetColor(nextColor);
            if (reserve != null) reserve.TryFlushTo(order);
        }
    }

    public IReadOnlyList<Order> Orders => orders;

    /// <summary>
    /// Bounding box (XZ) que cubre los renderers de todas las orders.
    /// Usado por LevelLayoutManager para encuadrar la camara.
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
    /// board sobre el eje Z, centrado en X.
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
}
