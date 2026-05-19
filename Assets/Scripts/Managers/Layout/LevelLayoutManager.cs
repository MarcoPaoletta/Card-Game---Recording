using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador de layout del nivel. Coordina BoardResizerManager (tamano del board),
/// OrdersManager (posicion de las orders), BeltRepositionerManager (posicion de la
/// cinta) y CameraFitterManager (zoom/posicion de la camara) en la secuencia
/// correcta. Centraliza los margenes en un solo lugar.
///
/// Cadena de anclaje: Board (resize) -> Orders (anclado a borde del board) ->
/// Belt (anclado al borde exterior de Orders) -> Camara (encuadra anchors moviles).
/// Esto permite niveles arbitrariamente grandes: la cadena se expande sola.
/// </summary>
public class LevelLayoutManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private BoardResizerManager boardResizer;
    [SerializeField] private OrdersManager ordersManager;
    [SerializeField] private BeltRepositionerManager beltRepositioner;
    [SerializeField] private CameraFitterManager cameraFitter;

    [Header("Margenes")]
    [Tooltip("Aire entre el grid de celdas y el borde del board (unidades de mundo).")]
    [SerializeField] private float boardGridMargin = 0.4f;
    [Tooltip("Distancia en Z entre el borde del board y el borde mas cercano de las orders.")]
    [SerializeField] private float ordersZGap = 1.0f;
    [Tooltip("Distancia en Z entre el borde exterior de las orders y el borde mas cercano de la cinta.")]
    [SerializeField] private float beltZGap = 0.6f;
    [Tooltip("Aire alrededor del encuadre de la camara.")]
    [SerializeField] private float cameraPadding = 0.5f;

    [Header("Encuadre de camara")]
    [Tooltip("Puntos que la camara debe encuadrar siempre. Tipicamente los 4 anchors del board (BoardTop/Bottom/Left/Right) " +
             "mas anchors moviles de orders y belt (OrdersOuterEdge/BeltOuterEdge): la camara toma el bbox que los contiene a todos. " +
             "Asi el encuadre se abre solo cuando crece el nivel.")]
    [SerializeField] private Transform[] cameraTargets;

    /// <summary>
    /// Aplica toda la secuencia de layout para un nivel:
    /// 1) Reescala el board para que entren las celdas con su margen.
    /// 2) Reposiciona el grupo de orders a la distancia configurada del board.
    /// 3) Reposiciona la cinta a la distancia configurada del borde exterior de orders.
    /// 4) Encuadra la camara para que board + orders + belt entren con el padding.
    /// </summary>
    public void Layout(List<CellEntry> cells)
    {
        if (boardResizer != null)
            boardResizer.ResizeToFit(cells, boardGridMargin);

        Bounds boardBounds = boardResizer != null
            ? boardResizer.GetBoardWorldBounds()
            : new Bounds();

        if (ordersManager != null)
            ordersManager.Reposition(boardBounds, ordersZGap);

        if (beltRepositioner != null)
            beltRepositioner.Reposition(boardBounds, beltZGap);

        if (cameraFitter != null)
            cameraFitter.FitToTargets(cameraTargets, cameraPadding);
    }
}
