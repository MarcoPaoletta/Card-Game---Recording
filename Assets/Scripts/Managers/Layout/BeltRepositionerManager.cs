using UnityEngine;

/// <summary>
/// Unica responsabilidad: trasladar el ConveyorBelt como grupo rigido para
/// que su tubo central (la parte del belt que esta directamente arriba/abajo
/// de las orders) quede a <c>beltZGap</c> del borde exterior de orders, y
/// centrado en X sobre el board. No conoce el path interno: mueve el root
/// transform del belt y los hijos (Points/Slots/Visuals) lo siguen.
///
/// Usa DOS bboxes distintos:
///  - "Inner bbox" (filtrado al rango X de las orders) para calcular el gap.
///    Esto generaliza a cualquier preset: arcos anchos en ∩ tienen su tubo
///    central (apex) lejos de los portales — sin filtrar daria un gap visual
///    enorme. Filtrando, las orders quedan pegadas al tubo que tienen encima.
///  - "Outer bbox" (renderers completos) para el anchor de camara, asi el
///    encuadre cubre el belt entero (portales, grosor del tubo, etc.).
/// </summary>
public class BeltRepositionerManager : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Raiz del ConveyorBelt. Se traslada como grupo rigido (Points/Slots/Visuals son hijos y siguen).")]
    [SerializeField] private Transform beltRoot;
    [Tooltip("Anchor (transform) que marca el borde exterior del grupo de orders, actualizado por OrdersManager tras Reposition.")]
    [SerializeField] private Transform ordersOuterEdge;
    [Tooltip("Anchor que se reposiciona al borde exterior del belt (visual completo, incluido grosor de tubo y portales) tras Reposition. Para usar en cameraTargets.")]
    [SerializeField] private Transform beltOuterEdgeAnchor;
    [Tooltip("Padding extra en X aplicado al rango de filtro del inner bbox. 0 = filtra estricto al ancho de las orders; >0 ensancha el corredor (util si querés incluir un poco mas de tubo lateral).")]
    [SerializeField] private float innerFilterXPadding = 0.1f;

    /// <summary>
    /// Reposiciona el belt para alinear su tubo central (filtrado al rango X
    /// de las orders) con <c>ordersOuterEdge.z ± beltZGap</c> y centrarlo en
    /// X sobre el board.
    /// </summary>
    /// <param name="ordersBounds">Bbox de los renderers del grupo de orders, usado
    /// para filtrar los renderers del belt al corredor X de las orders.</param>
    public void Reposition(Bounds boardBounds, Bounds ordersBounds, float beltZGap)
    {
        if (beltRoot == null || ordersOuterEdge == null) return;

        Bounds full = ComputeBeltRendererBounds();
        if (full.size == Vector3.zero) return;

        int side = full.center.z >= boardBounds.center.z ? +1 : -1;
        BeltAnchorMode mode = ResolveAnchorMode();
        Bounds inner = ComputeInnerBounds(mode, side, ordersBounds, full);

        float deltaZ;
        if (side > 0)
        {
            float targetMinZ = ordersOuterEdge.position.z + beltZGap;
            deltaZ = targetMinZ - inner.min.z;
        }
        else
        {
            float targetMaxZ = ordersOuterEdge.position.z - beltZGap;
            deltaZ = targetMaxZ - inner.max.z;
        }
        float deltaX = boardBounds.center.x - full.center.x;
        Vector3 offset = new Vector3(deltaX, 0f, deltaZ);

        beltRoot.position += offset;

        if (beltOuterEdgeAnchor != null)
        {
            // Outer anchor usa el bbox completo (renderers) post-translation,
            // asi la camara incluye portales y grosor del tubo.
            float outerZ = side > 0 ? full.max.z + deltaZ : full.min.z + deltaZ;
            beltOuterEdgeAnchor.position = new Vector3(
                boardBounds.center.x,
                beltOuterEdgeAnchor.position.y,
                outerZ);
        }
    }

    /// <summary>
    /// Lee el anchorMode del preset activo del ConveyorBelt. Si no hay preset
    /// (o el ConveyorBelt no esta en beltRoot), default a CenterColumn.
    /// </summary>
    BeltAnchorMode ResolveAnchorMode()
    {
        if (beltRoot == null) return BeltAnchorMode.CenterColumn;
        var belt = beltRoot.GetComponent<ConveyorBelt>();
        var preset = belt != null ? belt.ActivePreset : null;
        return preset != null ? preset.anchorMode : BeltAnchorMode.CenterColumn;
    }

    Bounds ComputeInnerBounds(BeltAnchorMode mode, int side, Bounds ordersBounds, Bounds fullFallback)
    {
        switch (mode)
        {
            case BeltAnchorMode.NearestPortal:
                return TryGetNearestPortal(side, out Vector3 portal)
                    ? new Bounds(portal, Vector3.zero)
                    : fullFallback;
            case BeltAnchorMode.FullBounds:
                return fullFallback;
            case BeltAnchorMode.CenterColumn:
            default:
            {
                float minX = ordersBounds.min.x - innerFilterXPadding;
                float maxX = ordersBounds.max.x + innerFilterXPadding;
                Bounds b = ComputeBeltRendererBounds(minX, maxX);
                return b.size == Vector3.zero ? fullFallback : b;
            }
        }
    }

    /// <summary>
    /// Posicion world-space del portal del path mas cercano a las orders en Z.
    /// side > 0 = belt en Z+ del board → el portal con menor Z. side < 0 →
    /// el portal con mayor Z. Retorna false si no hay path con al menos 2
    /// puntos (no se puede determinar el extremo).
    /// </summary>
    bool TryGetNearestPortal(int side, out Vector3 portal)
    {
        portal = default;
        if (beltRoot == null) return false;
        var path = beltRoot.GetComponent<BeltPath>();
        if (path == null) return false;
        var container = path.PointsContainer;
        int n = container != null ? container.childCount : 0;
        if (n < 2) return false;

        Vector3 p0 = container.GetChild(0).position;
        Vector3 pN = container.GetChild(n - 1).position;
        portal = (side > 0) ? (p0.z < pN.z ? p0 : pN) : (p0.z > pN.z ? p0 : pN);
        return true;
    }

    Bounds ComputeBeltRendererBounds() => ComputeBeltRendererBounds(float.NegativeInfinity, float.PositiveInfinity);

    /// <summary>
    /// Bbox de los renderers bajo beltRoot que solapan en X con [xMin, xMax].
    /// Si no hay match, retorna Bounds vacio (size = 0).
    /// </summary>
    Bounds ComputeBeltRendererBounds(float xMin, float xMax)
    {
        if (beltRoot == null) return new Bounds();
        var renderers = beltRoot.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds b = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            // Solapamiento de intervalos en X.
            if (r.bounds.max.x < xMin || r.bounds.min.x > xMax) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds();
    }
}
