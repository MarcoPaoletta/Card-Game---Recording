using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador de layout del nivel. Coordina BoardResizerManager (tamano del board),
/// OrdersManager (posicion de las orders) y CameraFitterManager (zoom/posicion de la
/// camara) en la secuencia correcta. Centraliza los margenes en un solo lugar.
/// </summary>
public class LevelLayoutManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private BoardResizerManager boardResizer;
    [SerializeField] private OrdersManager ordersManager;
    [SerializeField] private CameraFitterManager cameraFitter;

    [Header("Margenes")]
    [Tooltip("Aire entre el grid de celdas y el borde del board (unidades de mundo).")]
    [SerializeField] private float boardGridMargin = 0.4f;
    [Tooltip("Distancia en Z entre el borde del board y el borde mas cercano de las orders.")]
    [SerializeField] private float ordersZGap = 1.0f;
    [Tooltip("Aire alrededor del encuadre de la camara.")]
    [SerializeField] private float cameraPadding = 0.5f;

    [Header("Encuadre de camara")]
    [Tooltip("Puntos que la camara debe encuadrar siempre. Tipicamente FrameTop/FrameBottom (frames deseados) " +
             "y los 4 anchors del board (BoardTop/Bottom/Left/Right) como minimo garantizado: la camara toma " +
             "el bbox que los contiene a todos. Si los frames se quedan cortos, los board anchors fuerzan a " +
             "abrir el encuadre hasta cubrir el tablero.")]
    [SerializeField] private Transform[] cameraTargets;

    /// <summary>
    /// Aplica toda la secuencia de layout para un nivel:
    /// 1) Reescala el board para que entren las celdas con su margen.
    /// 2) Reposiciona el grupo de orders a la distancia configurada del board.
    /// 3) Encuadra la camara para que board + orders entren con el padding.
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

        if (cameraFitter != null)
            cameraFitter.FitToTargets(cameraTargets, cameraPadding);
    }
}
