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
    [Tooltip("Transform padre del grupo de orders. Si esta seteado, Reposition mueve este transform (los Order hijos lo siguen) en vez de mover cada hijo individual. Mantiene la jerarquia coherente (el padre tracking el grupo).")]
    [SerializeField] private Transform groupRoot;
    [Tooltip("Transform que se reposiciona al borde exterior (lado opuesto al board) del grupo de orders tras Reposition. Sirve para que el belt y/o la camara lo encuadren.")]
    [SerializeField] private Transform outerEdgeAnchor;

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
            // Esta order ya no vuelve, pero puede haber cartas dando vueltas
            // en la cinta que matcheen OTRA order que aun tenga capacidad.
            if (reserve != null) reserve.TryFlushToAny();
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
                    // Drenar lo que se pueda hacia esta order (que recien abre con
                    // un color nuevo) y, ademas, intentar drenar el resto de la
                    // cinta a cualquier otra order disponible: si abrio mas de
                    // una a la vez, no queremos que nadie quede esperando.
                    if (reserve != null)
                    {
                        reserve.TryFlushTo(order);
                        reserve.TryFlushToAny();
                    }
                });
            });
        }
        else
        {
            order.ResetSlots();
            order.SetColor(nextColor);
            if (reserve != null)
            {
                reserve.TryFlushTo(order);
                reserve.TryFlushToAny();
            }
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

        if (groupRoot != null)
        {
            // Paso 1: recentrar el padre sobre el bbox de los hijos sin moverlos
            // visualmente. Si la escena tiene el padre offseteado (ej. en (0,0,1.49)
            // pero los hijos en z=5.32), esto sincroniza el gizmo del padre con
            // el centro visual del grupo. Una sola vez por Reposition basta.
            Vector3 desiredParentPos = new Vector3(group.center.x, groupRoot.position.y, group.center.z);
            Vector3 parentDelta = desiredParentPos - groupRoot.position;
            if (parentDelta.sqrMagnitude > 0.0001f)
            {
                int n = orders.Count;
                var saved = new Vector3[n];
                for (int i = 0; i < n; i++)
                    if (orders[i] != null) saved[i] = orders[i].transform.position;
                groupRoot.position = desiredParentPos;
                for (int i = 0; i < n; i++)
                    if (orders[i] != null) orders[i].transform.position = saved[i];
            }

            // Paso 2: mover el padre por el offset; los hijos siguen.
            groupRoot.position += offset;
        }
        else
        {
            foreach (var o in orders)
            {
                if (o == null) continue;
                o.transform.position += offset;
            }
        }

        if (outerEdgeAnchor != null)
        {
            float outerZ = side > 0 ? group.max.z + deltaZ : group.min.z + deltaZ;
            outerEdgeAnchor.position = new Vector3(
                boardBounds.center.x,
                outerEdgeAnchor.position.y,
                outerZ);
        }
    }
}
