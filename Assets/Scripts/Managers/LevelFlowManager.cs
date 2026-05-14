using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maquina de estado del nivel: tracker de cuantas orders se esperan por color y
/// cuantas ya se completaron. Mantiene una cola con los proximos colores de order
/// que se necesitan, para que OrdersManager decida si "refilear" un order cuando
/// se llena (y con que color) o dejarlo permanentemente abajo.
/// Dispara OnLevelComplete cuando todas las orders esperadas se completaron.
/// </summary>
public class LevelFlowManager : MonoBehaviour
{
    [SerializeField] private OrdersManager ordersManager;

    private readonly Queue<Color> pendingOrderColors = new Queue<Color>();
    private int totalExpected;
    private int totalCompleted;

    public event Action OnLevelComplete;
    public event Action OnLevelStarted;
    public event Action OnProgressChanged;

    public bool IsLevelComplete => totalExpected > 0 && totalCompleted >= totalExpected;
    public int TotalExpected => totalExpected;
    public int TotalCompleted => totalCompleted;

    /// <summary>
    /// Recalcula el plan de orders a partir de las celdas. Asigna los colores
    /// iniciales a las orders en escena y guarda el resto en la cola.
    /// </summary>
    public void Initialize(List<CellEntry> cells)
    {
        pendingOrderColors.Clear();
        totalExpected = 0;
        totalCompleted = 0;

        if (cells == null || cells.Count == 0)
        {
            if (ordersManager != null) ordersManager.AssignColors(new List<Color>());
            OnLevelStarted?.Invoke();
            OnProgressChanged?.Invoke();
            return;
        }

        // Contar celdas por color preservando el orden de primera aparicion.
        var cellsByColor = new Dictionary<Color, int>();
        var firstSeen = new List<Color>();
        foreach (var c in cells)
        {
            var key = ColorUtil.Quantize(new Color(c.r, c.g, c.b, 1f));
            if (!cellsByColor.ContainsKey(key))
            {
                cellsByColor[key] = 0;
                firstSeen.Add(key);
            }
            cellsByColor[key]++;
        }

        // Expandir: cada color aparece (cells/2) veces.
        // 2 celdas = 8 cartas = 1 order de ese color.
        var allOrders = new Queue<Color>();
        foreach (var col in firstSeen)
        {
            int n = cellsByColor[col] / 2;
            for (int i = 0; i < n; i++) allOrders.Enqueue(col);
        }
        totalExpected = allOrders.Count;

        // Los primeros N (donde N = cantidad de orders en escena) son la asignacion
        // inicial; el resto queda en la cola para ir refileando.
        int sceneCount = ordersManager != null ? ordersManager.OrderCount : 0;
        var initialPalette = new List<Color>();
        for (int i = 0; i < sceneCount && allOrders.Count > 0; i++)
            initialPalette.Add(allOrders.Dequeue());

        if (ordersManager != null) ordersManager.AssignColors(initialPalette);

        while (allOrders.Count > 0) pendingOrderColors.Enqueue(allOrders.Dequeue());

        OnLevelStarted?.Invoke();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Devuelve el proximo color de order a generar tras un fill. False si no
    /// quedan orders por generar (en ese caso el order debe quedar abajo).
    /// </summary>
    public bool TryGetNextOrderColor(out Color color)
    {
        if (pendingOrderColors.Count > 0)
        {
            color = pendingOrderColors.Dequeue();
            return true;
        }
        color = default;
        return false;
    }

    public void NotifyOrderCompleted()
    {
        totalCompleted++;
        OnProgressChanged?.Invoke();
        if (IsLevelComplete) OnLevelComplete?.Invoke();
    }

}
