using UnityEngine;

/// <summary>
/// Unica responsabilidad: trasladar el ConveyorBelt como grupo rigido para
/// que su borde mas cercano al board quede a <c>beltZGap</c> unidades del
/// borde exterior del grupo de orders (anclado en <see cref="ordersOuterEdge"/>),
/// centrado en X sobre el board. No conoce el path interno: mueve el root
/// transform del belt y los hijos (Points/Slots/Visuals) lo siguen.
///
/// Tambien actualiza opcionalmente <see cref="beltOuterEdgeAnchor"/> para que
/// la camara pueda usarlo como objetivo de encuadre.
/// </summary>
public class BeltRepositionerManager : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Raiz del ConveyorBelt. Se traslada como grupo rigido (Points/Slots/Visuals son hijos y siguen).")]
    [SerializeField] private Transform beltRoot;
    [Tooltip("Anchor (transform) que marca el borde exterior del grupo de orders, actualizado por OrdersManager tras Reposition.")]
    [SerializeField] private Transform ordersOuterEdge;
    [Tooltip("Anchor que se reposiciona al borde exterior del belt tras Reposition. Para usar en cameraTargets.")]
    [SerializeField] private Transform beltOuterEdgeAnchor;

    /// <summary>
    /// Calcula el bbox de los renderers bajo <see cref="beltRoot"/> y traslada
    /// el root para alinear su borde interior (lado del board) con
    /// <c>ordersOuterEdge.z + beltZGap</c>, y centrarlo en X sobre el board.
    /// El "lado" (Z+ o Z-) se infiere comparando la posicion actual del belt
    /// con el centro del board.
    /// </summary>
    public void Reposition(Bounds boardBounds, float beltZGap)
    {
        if (beltRoot == null || ordersOuterEdge == null) return;

        Bounds group = ComputeBeltBounds();
        if (group.size == Vector3.zero) return;

        int side = group.center.z >= boardBounds.center.z ? +1 : -1;
        float deltaZ;
        if (side > 0)
        {
            float targetMinZ = ordersOuterEdge.position.z + beltZGap;
            deltaZ = targetMinZ - group.min.z;
        }
        else
        {
            float targetMaxZ = ordersOuterEdge.position.z - beltZGap;
            deltaZ = targetMaxZ - group.max.z;
        }
        float deltaX = boardBounds.center.x - group.center.x;
        Vector3 offset = new Vector3(deltaX, 0f, deltaZ);

        beltRoot.position += offset;

        if (beltOuterEdgeAnchor != null)
        {
            float outerZ = side > 0 ? group.max.z + deltaZ : group.min.z + deltaZ;
            beltOuterEdgeAnchor.position = new Vector3(
                boardBounds.center.x,
                beltOuterEdgeAnchor.position.y,
                outerZ);
        }
    }

    /// <summary>
    /// Bbox del belt usando los control points del BeltPath (el "esqueleto" del
    /// preset activo) en vez de los renderers. Razon: cada preset reescribe los
    /// localPoints y define su propia geometria de arco, asi que un bbox basado
    /// en path se acomoda solo a cualquier preset. Los renderers (visuals + portales)
    /// se extienden por padding/tube-thickness y dan un bbox demasiado holgado.
    /// Fallback a renderers si no hay path (raro, pero seguro).
    /// </summary>
    Bounds ComputeBeltBounds()
    {
        if (beltRoot == null) return new Bounds();

        var path = beltRoot.GetComponent<BeltPath>();
        if (path != null)
        {
            var container = path.PointsContainer;
            int n = container != null ? container.childCount : 0;
            if (n > 0)
            {
                bool anyP = false;
                Bounds bp = new Bounds();
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = container.GetChild(i).position;
                    if (!anyP) { bp = new Bounds(p, Vector3.zero); anyP = true; }
                    else bp.Encapsulate(p);
                }
                if (anyP) return bp;
            }
        }

        var renderers = beltRoot.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds b = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds(beltRoot.position, Vector3.zero);
    }
}
