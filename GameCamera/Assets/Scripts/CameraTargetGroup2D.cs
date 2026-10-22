using System.Collections.Generic;
using UnityEngine;

/// <summary>Birden fazla hedefi aynı ortografik kadrajda tutacak merkez ve boyutu hesaplar.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(Camera), typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraTargetGroup2D : MonoBehaviour
{
    [Header("Target Group")]
    [SerializeField, Tooltip("Objects kept in the same frame. Empty, destroyed, and inactive targets are ignored.")]
    private List<Transform> targets = new List<Transform>();

    [SerializeField, Tooltip("Includes active Renderer bounds. The Transform position is used when no Renderer is available.")]
    private bool includeRendererBounds = true;

    [Header("Group Framing")]
    [SerializeField, Tooltip("Horizontal and vertical space around the targets in camera-plane world units.")]
    private Vector2 padding = Vector2.one;

    [SerializeField, Min(0.01f), Tooltip("Minimum orthographic Size. The view expands without a maximum when targets move apart.")]
    private float minimumSize = 5f;

    [SerializeField, Min(0f), Tooltip("Zoom smoothing time when targets move closer. The view expands immediately to keep targets visible.")]
    private float zoomDamping = 0.35f;

    [SerializeField, Tooltip("Rotates the camera toward the group center when Look At Target is not controlling rotation.")]
    private bool rotateToGroup = false;

    [SerializeField, Min(0f), Tooltip("Rotation smoothing time toward the group center. Set to 0 for an immediate rotation.")]
    private float rotationDamping = 0.15f;

    [SerializeField, Tooltip("Shows group bounds, center, and target connections in the Scene view.")]
    private bool showScenePreview = true;

    private readonly List<Renderer> memberRenderers = new List<Renderer>();
    private Camera controlledCamera;
    private MainCamera cameraController;
    private float initialSize;
    private float currentSize;
    private float zoomVelocity;
    private bool isFraming;

    internal float CurrentSize => currentSize > 0f ? currentSize : Mathf.Max(0.01f, minimumSize);

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        cameraController = GetComponent<MainCamera>();
    }

    private void OnEnable()
    {
        initialSize = cameraController.AcquireSizeControl(this);
        currentSize = initialSize;
        zoomVelocity = 0f;
        isFraming = false;
    }

    private void OnDisable()
    {
        StopFraming();
        if (cameraController != null)
        {
            cameraController.ReleaseSizeControl(this);
        }
    }

    private void OnValidate()
    {
        padding = new Vector2(Mathf.Max(0f, padding.x), Mathf.Max(0f, padding.y));
        minimumSize = Mathf.Max(0.01f, minimumSize);
        zoomDamping = Mathf.Max(0f, zoomDamping);
        rotationDamping = Mathf.Max(0f, rotationDamping);
    }

    /// <summary>Gruba geçerli ve tekrarlanmayan bir hedef ekler.</summary>
    public void AddTarget(Transform newTarget)
    {
        if (newTarget != null && !targets.Contains(newTarget))
        {
            targets.Add(newTarget);
        }
    }

    /// <summary>Hedefi gruptan çıkarır.</summary>
    public void RemoveTarget(Transform oldTarget)
    {
        targets.Remove(oldTarget);
    }

    /// <summary>Grubu boşaltır; MainCamera sonraki güncellemede tek hedef takibine döner.</summary>
    public void ClearTargets()
    {
        targets.Clear();
    }

    /// <summary>Geçerli grup üyelerinin dünya koordinatlarındaki ortak sınırını verir.</summary>
    public bool TryGetGroupBounds(out Bounds groupBounds)
    {
        groupBounds = default;
        bool hasBounds = false;
        foreach (Transform member in targets)
        {
            if (!IsValidMember(member))
            {
                continue;
            }

            Bounds memberBounds = new Bounds(member.position, Vector3.zero);
            bool hasRendererBounds = false;
            if (includeRendererBounds)
            {
                memberRenderers.Clear();
                member.GetComponentsInChildren(false, memberRenderers);
                foreach (Renderer memberRenderer in memberRenderers)
                {
                    if (memberRenderer != null && memberRenderer.enabled)
                    {
                        if (hasRendererBounds)
                        {
                            memberBounds.Encapsulate(memberRenderer.bounds);
                        }
                        else
                        {
                            memberBounds = memberRenderer.bounds;
                            hasRendererBounds = true;
                        }
                    }
                }
            }

            if (hasBounds)
            {
                groupBounds.Encapsulate(memberBounds);
            }
            else
            {
                groupBounds = memberBounds;
                hasBounds = true;
            }
        }
        return hasBounds;
    }

    private bool IsValidMember(Transform member)
    {
        // Kamerayla birlikte taşınan hedefler takip döngüsü oluşturur.
        return member != null && member.gameObject.activeInHierarchy && member != transform &&
            !member.IsChildOf(transform) && !transform.IsChildOf(member);
    }

    internal float UpdateSize(Bounds groupBounds, float deltaTime, bool snapImmediately)
    {
        float requiredSize = CalculateRequiredSize(groupBounds, transform.position, transform.rotation,
            controlledCamera.aspect, padding, minimumSize);
        isFraming = true;
        currentSize = CalculateZoomSize(currentSize, requiredSize, zoomDamping,
            deltaTime, snapImmediately, ref zoomVelocity);
        return currentSize;
    }

    internal static float CalculateZoomSize(float currentSize, float requiredSize, float damping,
        float deltaTime, bool snapImmediately, ref float velocity)
    {
        if (snapImmediately || requiredSize >= currentSize || damping <= 0f)
        {
            velocity = 0f;
            return requiredSize;
        }
        if (deltaTime > 0f)
        {
            return Mathf.Max(requiredSize, Mathf.SmoothDamp(currentSize, requiredSize,
                ref velocity, damping, Mathf.Infinity, deltaTime));
        }
        return currentSize;
    }

    internal void StopFraming()
    {
        if (isFraming && controlledCamera != null)
        {
            controlledCamera.orthographicSize = initialSize;
        }
        currentSize = initialSize;
        zoomVelocity = 0f;
        isFraming = false;
    }

    internal Quaternion CalculateRotation(Vector3 groupCenter, float deltaTime, bool snapImmediately)
    {
        Vector3 direction = groupCenter - transform.position;
        if (!rotateToGroup || direction.sqrMagnitude < 0.000001f)
        {
            return transform.rotation;
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

        Quaternion desired = Quaternion.LookRotation(direction, up);
        float blend = snapImmediately || rotationDamping <= 0f
            ? 1f : 1f - Mathf.Exp(-deltaTime / rotationDamping);
        return Quaternion.Slerp(transform.rotation, desired, blend);
    }

    // Gerçek kamera konumunu kullan: yumuşak takip gecikmesi de kadraja sığmalıdır.
    internal static float CalculateRequiredSize(Bounds groupBounds, Vector3 cameraPosition,
        Quaternion cameraRotation, float aspect, Vector2 padding, float minimumSize)
    {
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        Vector3 relativeCenter = groupBounds.center - cameraPosition;
        Vector3 extents = groupBounds.extents;
        float halfWidth = Mathf.Abs(Vector3.Dot(relativeCenter, right)) +
            Vector3.Dot(new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)), extents) +
            Mathf.Max(0f, padding.x);
        float halfHeight = Mathf.Abs(Vector3.Dot(relativeCenter, up)) +
            Vector3.Dot(new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)), extents) +
            Mathf.Max(0f, padding.y);
        return Mathf.Max(Mathf.Max(0.01f, minimumSize),
            Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.0001f, aspect)));
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview || !isActiveAndEnabled ||
            !TryGetComponent(out MainCamera mainCamera) || !mainCamera.Is2D ||
            !TryGetGroupBounds(out Bounds groupBounds))
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.85f, 0.35f, 1f);
        Gizmos.DrawWireCube(groupBounds.center, groupBounds.size);
        Gizmos.DrawWireSphere(groupBounds.center, 0.15f);
        foreach (Transform member in targets)
        {
            if (IsValidMember(member))
            {
                Gizmos.DrawLine(groupBounds.center, member.position);
            }
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(groupBounds.center, "Target Group");
#endif
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
