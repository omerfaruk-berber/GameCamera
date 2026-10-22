using UnityEngine;

/// <summary>İç bölgede konumu sabit tutar; dış sınıra yaklaştıkça takibi hızlandırır.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(Camera), typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraDeadSoftZone2D : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField, Tooltip("Stationary area relative to the frame width and height. 0.2 means 20 percent.")]
    private Vector2 deadZoneSize = new Vector2(0.2f, 0.2f);

    [SerializeField, Tooltip("Outer boundary of the smooth follow area. It cannot be smaller than the Dead Zone.")]
    private Vector2 softZoneSize = new Vector2(0.8f, 0.8f);

    [SerializeField, Min(0f), Tooltip("Follow smoothing time near the Dead Zone. Response becomes faster near the outer boundary.")]
    private float damping = 0.35f;

    [SerializeField, Tooltip("Shows the Dead Zone and Soft Zone boundaries in the Scene view.")]
    private bool showScenePreview = true;

    private void OnValidate()
    {
        SetZoneSizes(deadZoneSize, softZoneSize);
        damping = Mathf.Max(0f, damping);
    }

    /// <summary>Bölge boyutlarını 0–1 aralığına sınırlar; Soft Zone iç alanı kapsar.</summary>
    public void SetZoneSizes(Vector2 deadSize, Vector2 softSize)
    {
        deadZoneSize = new Vector2(Mathf.Clamp01(deadSize.x), Mathf.Clamp01(deadSize.y));
        softZoneSize = new Vector2(
            Mathf.Max(deadZoneSize.x, Mathf.Clamp01(softSize.x)),
            Mathf.Max(deadZoneSize.y, Mathf.Clamp01(softSize.y)));
    }

    // MainCamera konumu uygular; bu bileşen sadece takip sonucunu hesaplar.
    internal Vector3 CalculatePosition(Vector3 framingPosition, float deltaTime)
    {
        Camera controlledCamera = GetComponent<Camera>();
        Vector3 localTarget = Quaternion.Inverse(transform.rotation) * (framingPosition - transform.position);
        Vector2 halfView = new Vector2(
            controlledCamera.orthographicSize * controlledCamera.aspect,
            controlledCamera.orthographicSize);

        Vector2 correction = new Vector2(
            CalculateAxisCorrection(localTarget.x, halfView.x * deadZoneSize.x,
                halfView.x * softZoneSize.x, damping, deltaTime),
            CalculateAxisCorrection(localTarget.y, halfView.y * deadZoneSize.y,
                halfView.y * softZoneSize.y, damping, deltaTime));

        Vector2 worldOffset = MainCamera.CameraPlaneToWorldOffset(transform.rotation, correction);
        return transform.position + new Vector3(worldOffset.x, worldOffset.y, 0f);
    }

    internal static float CalculateAxisCorrection(
        float error, float deadHalfSize, float softHalfSize, float damping, float deltaTime)
    {
        float distance = Mathf.Abs(error);
        if (distance <= deadHalfSize || deltaTime <= 0f)
        {
            return 0f;
        }

        float excess = distance - deadHalfSize;
        float progress = softHalfSize > deadHalfSize
            ? Mathf.InverseLerp(deadHalfSize, softHalfSize, distance)
            : 1f;

        // Sınıra yaklaştıkça tepki süresini kısalt; hedefi aşarak salınım üretme.
        float responseTime = Mathf.Max(0f, damping) * Mathf.Lerp(1f, 0.1f, Mathf.SmoothStep(0f, 1f, progress));
        float movement = responseTime <= 0f ? excess : excess * (1f - Mathf.Exp(-deltaTime / responseTime));

        // Işınlanma veya çok hızlı harekette hedefi dış sınırın içinde tut.
        float hardCorrection = Mathf.Max(0f, distance - softHalfSize);
        return Mathf.Sign(error) * Mathf.Max(movement, hardCorrection);
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview || !isActiveAndEnabled ||
            !TryGetComponent(out MainCamera mainCamera) || !mainCamera.Is2D)
        {
            return;
        }

        Camera previewCamera = GetComponent<Camera>();
        Vector3 center = transform.position;
        float halfHeight = previewCamera.orthographicSize;
        if (TryGetComponent(out CameraViewExpansion2D expansion) && expansion.isActiveAndEnabled)
        {
            expansion.GetPreviewView(out _, out _, out center, out halfHeight);
        }

        float depth = Mathf.Abs(transform.forward.z) > 0.0001f ? -center.z / transform.forward.z : 1f;
        depth = Mathf.Max(previewCamera.nearClipPlane, depth);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(center + transform.forward * depth, transform.rotation, Vector3.one);

        Vector3 viewSize = new Vector3(halfHeight * previewCamera.aspect * 2f, halfHeight * 2f, 0f);
        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.1f);
        Gizmos.DrawCube(Vector3.zero, Vector3.Scale(viewSize, new Vector3(softZoneSize.x, softZoneSize.y, 1f)));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.Scale(viewSize, new Vector3(softZoneSize.x, softZoneSize.y, 1f)));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.Scale(viewSize, new Vector3(deadZoneSize.x, deadZoneSize.y, 1f)));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(Gizmos.matrix.MultiplyPoint3x4(new Vector3(
            -viewSize.x * deadZoneSize.x * 0.5f, viewSize.y * deadZoneSize.y * 0.5f, 0f)), "Dead Zone");
        UnityEditor.Handles.Label(Gizmos.matrix.MultiplyPoint3x4(new Vector3(
            -viewSize.x * softZoneSize.x * 0.5f, viewSize.y * softZoneSize.y * 0.5f, 0f)), "Soft Zone");
#endif

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
