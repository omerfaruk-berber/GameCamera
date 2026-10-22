using UnityEngine;

/// <summary>Kamera konumunu değiştirmeden bakış hedefini izler.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraLookAtTarget2D : MonoBehaviour
{
    [Header("Look At Settings")]
    [SerializeField, Tooltip("Target to look at. If empty, the active group center or Main Camera follow target is used.")]
    private Transform lookAtTarget;

    [SerializeField, Min(0f), Tooltip("Rotation smoothing time in seconds. Set to 0 for an immediate rotation.")]
    private float rotationDamping = 0.15f;

    [SerializeField, Tooltip("Restores the camera rotation captured before this feature was enabled.")]
    private bool restoreRotationOnDisable = true;

    [SerializeField, Tooltip("Shows a line from the camera to the look-at target in the Scene view.")]
    private bool showScenePreview = true;

    private Quaternion initialRotation;

    /// <summary>Özel veya tek takip hedefinin Transform referansı; grup merkezi ayrıca hesaplanır.</summary>
    public Transform Target => lookAtTarget != null
        ? lookAtTarget
        : GetComponent<MainCamera>().Target;

    private void OnEnable()
    {
        initialRotation = transform.rotation;
    }

    private void OnDisable()
    {
        if (restoreRotationOnDisable && !GetComponent<MainCamera>().Is2D)
        {
            transform.rotation = initialRotation;
        }
    }

    /// <summary>null verilirse grup merkezini veya MainCamera hedefini kullanır; kapatılması dönüşü durdurur.</summary>
    public void SetLookAtTarget(Transform newTarget, bool snapImmediately = false)
    {
        lookAtTarget = newTarget;
        if (snapImmediately && isActiveAndEnabled)
        {
            ApplyRotation(0f, true);
        }
    }

    // MainCamera, konum takibinden sonra çağırır; ayrı bir LateUpdate yarışması oluşmaz.
    internal void ApplyRotation(float deltaTime, bool snapImmediately = false, Vector3? groupCenter = null)
    {
        MainCamera mainCamera = GetComponent<MainCamera>();
        if (mainCamera.Is2D)
        {
            return;
        }

        if ((!snapImmediately && deltaTime <= 0f) || !TryGetFocusPosition(groupCenter, out Vector3 focusPosition))
        {
            return;
        }

        Vector3 direction = focusPosition - transform.position;
        if (direction.sqrMagnitude < 0.000001f)
        {
            return;
        }

        direction.Normalize();
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.999f)
        {
            up = Vector3.ProjectOnPlane(transform.up, direction);
            if (up.sqrMagnitude < 0.000001f)
            {
                up = Vector3.forward;
            }
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction, up);
        float blend = snapImmediately || rotationDamping <= 0f
            ? 1f
            : 1f - Mathf.Exp(-deltaTime / rotationDamping);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }

    private bool TryGetFocusPosition(Vector3? groupCenter, out Vector3 focusPosition)
    {
        if (lookAtTarget != null)
        {
            focusPosition = lookAtTarget.position;
            return true;
        }
        MainCamera mainCamera = GetComponent<MainCamera>();
        if (mainCamera.Is2D && !groupCenter.HasValue && TryGetComponent(out CameraTargetGroup2D group) &&
            group.isActiveAndEnabled && group.TryGetGroupBounds(out Bounds groupBounds))
        {
            groupCenter = groupBounds.center;
        }
        if (groupCenter.HasValue)
        {
            focusPosition = groupCenter.Value;
            return true;
        }

        Transform followTarget = Target;
        focusPosition = followTarget != null ? followTarget.position : Vector3.zero;
        return followTarget != null;
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview || !isActiveAndEnabled ||
            !TryGetComponent(out MainCamera mainCamera) || mainCamera.Is2D ||
            !TryGetFocusPosition(null, out Vector3 focusPosition))
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(1f, 0.55f, 0.1f);
        Gizmos.DrawLine(transform.position, focusPosition);
        Gizmos.DrawWireSphere(focusPosition, 0.15f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(focusPosition, "Look At Target");
#endif
        Gizmos.color = previousColor;
    }
}
