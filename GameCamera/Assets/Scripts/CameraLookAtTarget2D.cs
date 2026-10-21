using UnityEngine;

/// <summary>Kamera konumunu değiştirmeden bakış hedefini izler.</summary>
[RequireComponent(typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraLookAtTarget2D : MonoBehaviour
{
    [Header("Look At Settings")]
    [SerializeField, Tooltip("Bakılacak hedef. Boş bırakılırsa MainCamera takip hedefi kullanılır.")]
    private Transform lookAtTarget;

    [SerializeField, Min(0f), Tooltip("Dönüşün yumuşama süresi (saniye). 0 hedefe anında döner.")]
    private float rotationDamping = 0.15f;

    [SerializeField, Tooltip("Bileşen kapatılınca etkinleştirmeden önceki kamera dönüşünü geri yükler.")]
    private bool restoreRotationOnDisable = true;

    [SerializeField, Tooltip("Scene üzerinde bakış hedefine uzanan çizgiyi gösterir.")]
    private bool showScenePreview = true;

    private MainCamera cameraController;
    private Quaternion initialRotation;

    /// <summary>Özel bakış hedefi veya atanmışsa MainCamera takip hedefi.</summary>
    public Transform Target => lookAtTarget != null
        ? lookAtTarget
        : GetComponent<MainCamera>().Target;

    private void Awake()
    {
        cameraController = GetComponent<MainCamera>();
    }

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

    /// <summary>null verilirse MainCamera hedefini kullanır; devre dışı bırakmak dönüşü durdurur.</summary>
    public void SetLookAtTarget(Transform newTarget, bool snapImmediately = false)
    {
        lookAtTarget = newTarget;
        if (snapImmediately && isActiveAndEnabled)
        {
            ApplyRotation(0f, true);
        }
    }

    // MainCamera, konum takibinden sonra çağırır; ayrı bir LateUpdate yarışması oluşmaz.
    internal void ApplyRotation(float deltaTime, bool snapImmediately = false)
    {
        Transform focus = lookAtTarget != null ? lookAtTarget : cameraController.Target;
        if (focus == null || (!snapImmediately && deltaTime <= 0f))
        {
            return;
        }

        Vector3 direction = focus.position - transform.position;
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

    private void OnDrawGizmos()
    {
        if (!showScenePreview || !isActiveAndEnabled || Target == null)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(1f, 0.55f, 0.1f);
        Gizmos.DrawLine(transform.position, Target.position);
        Gizmos.DrawWireSphere(Target.position, 0.15f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(Target.position, "Look At Target");
#endif
        Gizmos.color = previousColor;
    }
}
