/// <summary>
/// Modo de anclaje del belt al grupo de orders, usado por
/// BeltRepositionerManager para decidir que region del belt se considera
/// "borde interior" (el que pega al gap configurado con las orders).
/// Cada BeltPreset declara su modo segun su geometria.
/// </summary>
public enum BeltAnchorMode
{
    /// <summary>
    /// Bbox de los renderers cuya X solapa el rango X de las orders (corredor
    /// central). Bueno para arcos (∩ / ∪) donde el visual relevante por encima
    /// de las orders es el tubo central, no los portales laterales. Default.
    /// </summary>
    CenterColumn = 0,

    /// <summary>
    /// Usa el control point del path (portal) cuya Z queda mas cerca del
    /// grupo de orders. Bueno para belts donde querés que un portal especifico
    /// quede pegado a las orders (belts rectos, asimetricos, o cuando un
    /// extremo del path es el "frente" de la cinta).
    /// </summary>
    NearestPortal = 1,

    /// <summary>
    /// Bbox completo de los renderers del belt, sin filtrar. Comportamiento
    /// mas viejo. Bueno para belts compactos donde el visual completo debe
    /// quedar pegado al gap con las orders.
    /// </summary>
    FullBounds = 2,
}
