using UnityEngine;

public class CameraFitterManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [Tooltip("Punto al que la cámara mira (centro del board).")]
    [SerializeField] private Transform target;

    [Header("Tuning")]
    [Tooltip("Aire alrededor del board, en unidades de mundo (se agrega a cada lado).")]
    [SerializeField] private float padding = 0.5f;
    [SerializeField] private float minSize = 1f;
    [SerializeField] private float maxSize = 50f;

    public void FitTo(float worldWidth, float worldHeight)
    {
        if (cam == null || target == null) return;
        float pad = Mathf.Max(0f, padding);

        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;

        float hx = worldWidth * 0.5f + pad;
        float hz = worldHeight * 0.5f + pad;
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
        }
        else
        {
            float halfFovV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * cam.aspect);
            float tanV = Mathf.Tan(halfFovV);
            float tanH = Mathf.Tan(halfFovH);

            Vector3 fwd = cam.transform.forward.normalized;
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
            cam.transform.position = target.position - fwd * requiredDist;
        }
    }
}
