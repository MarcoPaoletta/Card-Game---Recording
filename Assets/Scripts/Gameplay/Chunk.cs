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
            var seq = DOTween.Sequence();
            seq.Append(transform.DOMove(originalPosition + dirVec * bounceNudge, bounceDuration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOMove(originalPosition, bounceDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => isInteractable = true);
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
        // Leading card first (mayor proyección en dir).
        ordered.Sort((a, b) => Vector3.Dot(b.position, dirVec).CompareTo(Vector3.Dot(a.position, dirVec)));

        int remaining = ordered.Count;
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

            Vector3 cardTarget;
            if (targetSlot != null) cardTarget = targetSlot.position;
            else if (reserveSlot != null) cardTarget = reserveSlot.position;
            else cardTarget = card.position + dirVec * distance;

            var seq = DOTween.Sequence();
            if (delay > 0f) seq.AppendInterval(delay);
            seq.Append(card.DOJump(cardTarget, jumpPower, jumpCount, moveDuration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                if (targetSlot != null && card != null)
                {
                    card.SetParent(targetSlot, worldPositionStays: true);
                    card.localPosition = Vector3.zero;
                    card.localRotation = Quaternion.identity;
                    targetOrder.NotifyDelivered();
                }
                else if (reserveSlot != null && card != null && reserveManager != null)
                {
                    reserveManager.AttachCardToSlot(reserveSlot, card);
                }
                else if (card != null)
                {
                    Destroy(card.gameObject);
                }
                remaining--;
                if (remaining <= 0)
                {
                    if (spawner != null) spawner.UnregisterChunk(this);
                    Destroy(gameObject);
                }
            });
        }
    }

    public void FlashAndPunch()
    {
        transform.DOKill(complete: true);
        transform.DOPunchScale(Vector3.one * punchScale, flashDuration * 2.5f, 6, 0.5f);

        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        foreach (var mat in mr.materials)
            FlashRed(mat);
    }

    void FlashRed(Material mat)
    {
        bool useBase = mat.HasProperty("_BaseColor");
        Color original = useBase ? mat.GetColor("_BaseColor") : mat.color;
        var seq = DOTween.Sequence();
        if (useBase)
        {
            seq.Append(mat.DOColor(Color.red, "_BaseColor", flashDuration));
            seq.Append(mat.DOColor(original, "_BaseColor", flashDuration));
        }
        else
        {
            seq.Append(mat.DOColor(Color.red, flashDuration));
            seq.Append(mat.DOColor(original, flashDuration));
        }
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
