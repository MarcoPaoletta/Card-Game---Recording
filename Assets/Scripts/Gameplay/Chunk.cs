using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    [Header("Movimiento (sin blocker)")]
    [Tooltip("Duracion del deslizamiento de cada carta hacia el borde del board (fase 1).")]
    [SerializeField] private float exitDuration = 0.25f;
    [Tooltip("Stagger entre cartas (cada carta arranca su slide con este offset; el jump al target arranca apenas termina su propio slide).")]
    [SerializeField] private float exitStagger = 0.05f;
    [Tooltip("Duracion del jump de cada carta al target final.")]
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float jumpPower = 6.0f;
    [SerializeField] private int jumpCount = 1;

    [Header("Choque con blocker")]
    [Tooltip("Duracion del empuje del chunk clickeado hacia el blocker.")]
    [SerializeField] private float pushDuration = 0.1f;
    [Tooltip("Duracion del retorno a la posicion original despues del choque.")]
    [SerializeField] private float returnDuration = 0.18f;
    [Tooltip("Duracion del flash rojo del blocker (mitad ida, mitad vuelta).")]
    [SerializeField] private float flashDuration = 0.12f;
    [Tooltip("Magnitud del punch scale (tanto el del blocker como el de las cartas en cascada).")]
    [SerializeField] private float punchScale = 0.22f;
    [Tooltip("Duracion del punch scale del blocker.")]
    [SerializeField] private float blockerPunchDuration = 0.22f;
    [Tooltip("Duracion del punch scale de cada carta en la cascada.")]
    [SerializeField] private float cascadePunchDuration = 0.14f;
    [Tooltip("Stagger entre punches en la cascada de cartas.")]
    [SerializeField] private float cascadeStagger = 0.025f;
    [Tooltip("Tiempo minimo de espera con el chunk pegado al blocker (independiente de la cascada).")]
    [SerializeField] private float minCollisionHold = 0.05f;

    private CardsSpawnerManager spawner;
    private OrdersManager ordersManager;
    private ConveyorBelt reserveManager;
    private CellDirection direction;
    private List<Vector2Int> cells;
    private Vector3 originalPosition;
    private bool isInteractable = true;

    private readonly ChunkFeedbackTweens.MaterialColorCache flashCache = new ChunkFeedbackTweens.MaterialColorCache();

    public CellDirection Direction => direction;
    public IReadOnlyList<Vector2Int> Cells => cells;
    public bool IsInteractable => isInteractable;
    public Color Color { get; private set; }

    public void Init(CardsSpawnerManager spawner, OrdersManager orders, ConveyorBelt reserve, CellDirection dir, List<Vector2Int> cells, Color color)
    {
        this.spawner = spawner;
        this.ordersManager = orders;
        this.reserveManager = reserve;
        this.direction = dir;
        this.cells = cells;
        this.Color = color;
        originalPosition = transform.position;
    }

    public void OnClicked()
    {
        if (!isInteractable || spawner == null) return;
        isInteractable = false;

        var (blocker, distance) = spawner.CalculateChunkMove(this);
        Vector3 dirVec = WorldStep(direction);

        if (blocker != null) BumpInto(blocker, dirVec, distance);
        else FlyOutStaggered(dirVec, distance);
    }

    // --- Choque con blocker ---

    void BumpInto(Chunk blocker, Vector3 dirVec, float bumpDistance)
    {
        // Cartas ordenadas leading-first: la primera es la que "toca" al blocker
        // y desde ella se propaga la cascada de punch hacia atras.
        var ordered = SortCardsLeadingFirst(dirVec);
        float cascadeTime = ChunkFeedbackTweens.CascadeTotalTime(ordered.Count, cascadePunchDuration, cascadeStagger);
        float hold = Mathf.Max(minCollisionHold, cascadeTime);

        ChunkFeedbackTweens.BumpCollideAndReturn(
            transform, originalPosition, dirVec, bumpDistance,
            pushDuration, hold, returnDuration,
            onCollide: () =>
            {
                ChunkFeedbackTweens.BlockerImpact(blocker.transform, blocker.flashCache, flashDuration, punchScale, blockerPunchDuration);
                ChunkFeedbackTweens.CascadePunch(ordered, punchScale, cascadePunchDuration, cascadeStagger);
            },
            onComplete: () => isInteractable = true);
    }

    // --- Salida del board (2 fases) ---

    void FlyOutStaggered(Vector3 dirVec, float exitDistance)
    {
        var box = GetComponent<Collider>();
        if (box != null) box.enabled = false;

        // Sacar el chunk del registro del spawner ya mismo: a partir de aca
        // este chunk no debe contar como blocker para CalculateChunkMove de
        // otros chunks. Si esperamos hasta que termine la animacion, otro
        // chunk clickeado en el medio rebotaria contra una posicion que en
        // realidad esta libre.
        if (spawner != null) spawner.UnregisterChunk(this);

        var ordered = SortCardsLeadingFirst(dirVec);
        foreach (var t in ordered)
        {
            var c = t.GetComponent<Card>();
            if (c != null) c.HideArrow();
        }

        // Reservar targets upfront para no diferir el estado de OrdersManager/Belt.
        var assignments = new List<BoardExitTweens.CardAssignment>(ordered.Count);
        foreach (var card in ordered)
        {
            var assign = ordersManager != null ? ordersManager.AcquireNextSlot(this.Color) : (null, (Transform)null);
            Transform reserveSlot = null;
            if (assign.slot == null && reserveManager != null)
                reserveSlot = reserveManager.AcquireSlot(this.Color);
            assignments.Add(new BoardExitTweens.CardAssignment
            {
                card = card,
                order = assign.order,
                orderSlot = assign.slot,
                beltSlot = reserveSlot,
            });
        }

        int remaining = ordered.Count;
        System.Action onCardDone = () =>
        {
            remaining--;
            if (remaining <= 0)
            {
                // El UnregisterChunk ya se hizo al arrancar el fly out; aca
                // solo destruimos el GO una vez que todas las cartas llegaron.
                Destroy(gameObject);
            }
        };

        var tunings = new BoardExitTweens.Tunings
        {
            exitDuration = exitDuration,
            exitStagger = exitStagger,
            exitEase = Ease.OutQuad,
            jump = new CardTransferTweens.JumpParams
            {
                jumpPower = jumpPower,
                jumpCount = jumpCount,
                duration = jumpDuration,
                ease = Ease.OutQuad,
            },
        };

        BoardExitTweens.Play(assignments, dirVec, exitDistance, reserveManager, tunings, onCardDone);
    }

    List<Transform> SortCardsLeadingFirst(Vector3 dirVec)
    {
        var ordered = new List<Transform>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++) ordered.Add(transform.GetChild(i));
        ordered.Sort((a, b) => Vector3.Dot(b.position, dirVec).CompareTo(Vector3.Dot(a.position, dirVec)));
        return ordered;
    }

    public static Vector3 WorldStep(CellDirection d)
    {
        switch (d)
        {
            case CellDirection.Right: return Vector3.right;
            case CellDirection.Left:  return Vector3.left;
            case CellDirection.Up:    return Vector3.forward;
            case CellDirection.Down:  return Vector3.back;
        }
        return Vector3.zero;
    }
}
