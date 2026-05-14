using UnityEngine;

/// <summary>
/// Unica responsabilidad: posicionar y zoomear la camara para encuadrar una
/// region (XZ) del mundo con un padding dado. No conoce ni board ni orders:
/// recibe el rectangulo a encuadrar desde LevelLayoutManager.
/// </summary>
public class CameraFitterManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;

    [Header("Limites de zoom")]
    [SerializeField] private float minSize = 1f;
    [SerializeField] private float maxSize = 50f;

    [Header("Distancia (perspectiva / preservada ortho)")]
    [Tooltip("Si > 0, se usa como distancia entre la camara y el centro del encuadre. " +
             "Si <= 0, se preserva la distancia actual de la camara al origen del mundo.")]
    [SerializeField] private float fixedViewDistance = -1f;

    /// <summary>
    /// Encuadra la region (XZ) descripta por <paramref name="worldBounds"/>.
    /// Centra la camara sobre bounds.center (corriendo a lo largo de su forward)
    /// y ajusta <c>orthographicSize</c> (o distancia en perspectiva) para que
    /// toda la region entre con <paramref name="padding"/> de aire.
    /// </summary>
    public void FitToBounds(Bounds worldBounds, float padding)
    {
        if (cam == null) return;
        float pad = Mathf.Max(0f, padding);

        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        Vector3 fwd = cam.transform.forward.normalized;
        Vector3 center = worldBounds.center;

        float hx = worldBounds.size.x * 0.5f + pad;
        float hz = worldBounds.size.z * 0.5f + pad;
        Vector3[] corners = {
            new Vector3(+hx, 0f, +hz),
            new Vector3(-hx, 0f, +hz),
            new Vector3(+hx, 0f, -hz),
            new Vector3(-hx, 0f, -hz),
        };

        float maxAbsR = 0f, maxAbsU = 0f;
        foreach (var v in corners)
        {
            maxAbsR = Mathf.Max(maxAbsR, Mathf.Abs(Vector3.Dot(v, right)));
            maxAbsU = Mathf.Max(maxAbsU, Mathf.Abs(Vector3.Dot(v, up)));
        }

        if (cam.orthographic)
        {
            float requiredSize = Mathf.Max(maxAbsU, maxAbsR / Mathf.Max(0.01f, cam.aspect));
            cam.orthographicSize = Mathf.Clamp(requiredSize, minSize, maxSize);

            float dist = fixedViewDistance > 0f
                ? fixedViewDistance
                : Mathf.Max(0.01f, cam.transform.position.magnitude);
            cam.transform.position = center - fwd * dist;
        }
        else
        {
            float halfFovV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * cam.aspect);
            float tanV = Mathf.Tan(halfFovV);
            float tanH = Mathf.Tan(halfFovH);

            float requiredDist = minSize;
            foreach (var v in corners)
            {
                float dotR = Vector3.Dot(v, right);
                float dotU = Vector3.Dot(v, up);
                float dotF = Vector3.Dot(v, fwd);
                float dH = Mathf.Abs(dotR) / tanH - dotF;
                float dV = Mathf.Abs(dotU) / tanV - dotF;
                requiredDist = Mathf.Max(requiredDist, dH, dV);
            }
            requiredDist = Mathf.Clamp(requiredDist, minSize, maxSize);
            cam.transform.position = center - fwd * requiredDist;
        }
    }
}
