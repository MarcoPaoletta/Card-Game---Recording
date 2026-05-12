using UnityEngine;
using UnityEngine.InputSystem;

public class TouchManager : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask raycastLayers = ~0;
    [SerializeField] private string cardNamePrefix = "Card";

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (TryGetPointerDownPosition(out Vector2 screenPosition))
        {
            TryHideTouchedCard(screenPosition);
        }
    }

    private bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    private void TryHideTouchedCard(Vector2 screenPosition)
    {
        if (targetCamera == null)
        {
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayers)
            && hit.collider.gameObject.name.StartsWith(cardNamePrefix))
        {
            Destroy(hit.collider.gameObject);
        }
    }
}
