using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private int jumpCount = 1;
    [SerializeField] private float cardStaggerDelay = 0.06f;
    [SerializeField] private float bounceDuration = 0.12f;
    [SerializeField] private float bounceNudge = 0.15f;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float punchScale = 0.2f;

    private CardsSpawnerManager spawner;
    private OrdersManager ordersManager;
    private ConveyorBelt reserveManager;
    private CellDirection direction;
    private List<Vector2Int> cells;
    private Vector3 originalPosition;
    private bool isInteractable = true;

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

        if (blocker != null)
        {
            blocker.FlashAndPunch();
            ChunkFeedbackTweens.Bounce(
                transform, originalPosition, dirVec, bounceNudge, bounceDuration,
                () => isInteractable = true);
        }
        else
        {
            FlyOutStaggered(dirVec, distance);
        }
    }

    private void FlyOutStaggered(Vector3 dirVec, float distance)
    {
        // Desactivar el collider para que el chunk no se pueda re-clickear durante la salida.
        var box = GetComponent<Collider>();
        if (box != null) box.enabled = false;

        var ordered = new List<Transform>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++) ordered.Add(transform.GetChild(i));

        // La flecha solo tiene sentido mientras el chunk esta en el tablero.
        foreach (var t in ordered)
        {
            var c = t.GetComponent<Card>();
            if (c != null) c.HideArrow();
        }
        // Leading card first (mayor proyeccion en dir).
        ordered.Sort((a, b) => Vector3.Dot(b.position, dirVec).CompareTo(Vector3.Dot(a.position, dirVec)));

        var jp = new CardTransferTweens.JumpParams
        {
            jumpPower = jumpPower,
            jumpCount = jumpCount,
            duration = moveDuration,
            ease = Ease.OutQuad,
        };

        int remaining = ordered.Count;
        System.Action onCardDone = () =>
        {
            remaining--;
            if (remaining <= 0)
            {
                if (spawner != null) spawner.UnregisterChunk(this);
                Destroy(gameObject);
            }
        };

        for (int i = 0; i < ordered.Count; i++)
        {
            var card = ordered[i];
            float delay = i * cardStaggerDelay;

            var assign = ordersManager != null ? ordersManager.AcquireNextSlot(this.Color) : (null, (Transform)null);
            Order targetOrder = assign.order;
            Transform targetSlot = assign.slot;

            // Si no hay order disponible (lleno o color distinto), mandar al reserve.
            Transform reserveSlot = null;
            if (targetSlot == null && reserveManager != null)
                reserveSlot = reserveManager.AcquireSlot(this.Color);

            ScheduleCardDeparture(card, delay, targetOrder, targetSlot, reserveSlot, dirVec, distance, jp, onCardDone);
        }
    }

    void ScheduleCardDeparture(Transform card, float delay, Order targetOrder, Transform targetSlot, Transform reserveSlot, Vector3 dirVec, float distance, CardTransferTweens.JumpParams jp, System.Action onDone)
    {
        // El stagger se hace con un DelayedCall en vez de prepender al Sequence
        // del transfer: asi cada caso (Order/Belt/Void) puede usar su tween
        // tipado sin tener que mezclar el delay con la logica de cleanup.
        DOVirtual.DelayedCall(delay, () =>
        {
            if (card == null) { onDone?.Invoke(); return; }
            if (targetSlot != null)
                CardTransferTweens.BoardToOrder(card, targetSlot, targetOrder, jp, onDone);
            else if (reserveSlot != null && reserveManager != null)
                CardTransferTweens.BoardToBelt(card, reserveSlot, reserveManager, jp, onDone);
            else
                CardTransferTweens.BoardToVoid(card, card.position + dirVec * distance, jp, onDone);
        });
    }

    public void FlashAndPunch()
    {
        ChunkFeedbackTweens.FlashAndPunch(transform, punchScale, flashDuration);
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
