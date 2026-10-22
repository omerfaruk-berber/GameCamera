using UnityEngine;

/// <summary>Kamera konumunu değiştirmeden bakış hedefini izler.</summary>
[RequireComponent(typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraLookAtTarget2D : MonoBehaviour
{
    [Header("Look At Settings")]
    [SerializeField, Tooltip("Bakılacak hedef. Boşsa geçerli grubun merkezi, grup yoksa MainCamera takip hedefi kullanılır.")]
    private Transform lookAtTarget;

    [SerializeField, Min(0f), Tooltip("Dönüşün yumuşama süresi (saniye). 0 hedefe anında döner.")]
    private float rotationDamping = 0.15f;

    [SerializeField, Tooltip("Bileşen kapatılınca etkinleştirmeden önceki kamera dönüşünü geri yükler.")]
    private bool restoreRotationOnDisable = true;

    [SerializeField, Tooltip("Scene üzerinde bakış hedefine uzanan çizgiyi gösterir.")]
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
        if (restoreRotationOnDisable)
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
            // Tam yukarı/aşağı bakarken geçerli bir yukarı ekseni seç.
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
        if (!groupCenter.HasValue && TryGetComponent(out CameraTargetGroup2D group) &&
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
        if (!showScenePreview || !isActiveAndEnabled || !TryGetFocusPosition(null, out Vector3 focusPosition))
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
