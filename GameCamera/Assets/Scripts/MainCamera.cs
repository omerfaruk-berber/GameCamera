using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Camera/Main Camera")]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class MainCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField, Tooltip("Selects 2D orthographic or 3D perspective behavior.")]
    private GameCameraMode cameraMode = GameCameraMode.Mode2D;

    [Header("Follow Settings")]
    [SerializeField, Tooltip("The object followed by the camera.")]
    private Transform target;

    [SerializeField, Tooltip("World-space X/Y framing offset used in 2D mode.")]
    private Vector2 offset = Vector2.zero;

    [SerializeField, Tooltip("World-space X/Y/Z offset from the target used in 3D mode.")]
    private Vector3 offset3D = new Vector3(0f, 2f, -10f);

    [SerializeField, Min(0f), Tooltip("Approximate follow smoothing time in seconds. Set to 0 for immediate movement.")]
    private float smoothTime = 0.2f;

    [SerializeField, Tooltip("Moves the camera directly to the target when play begins.")]
    private bool snapOnStart = true;

    // SmoothDamp kareler arasındaki takip hızını bu alanda saklar.
    private Vector3 followVelocity;

    // Ek kamera davranışlarının takip kadrajına eklediği dünya birimi kayması.
    private Vector2 viewOffset;

    // Boyutu etkileyen özellikler aynı başlangıç değerini paylaşır.
    private readonly HashSet<MonoBehaviour> sizeOwners = new HashSet<MonoBehaviour>();
    private float unmodifiedOrthographicSize;

    /// <summary>Şu anda takip edilen hedef; hedef atanmamışsa null döner.</summary>
    public Transform Target => target;

    /// <summary>The active 2D or 3D behavior mode.</summary>
    public GameCameraMode CameraMode => cameraMode;

    /// <summary>Returns true while the controller uses orthographic 2D behavior.</summary>
    public bool Is2D => cameraMode == GameCameraMode.Mode2D;

    /// <summary>
    /// Unity Camera bileşeninin arka plan rengini yönetir.
    /// Renk atandığında düz renk arka plan modu etkinleştirilir.
    /// </summary>
    public Color BackgroundColor
    {
        get => GetComponent<Camera>().backgroundColor;
        set
        {
            Camera attachedCamera = GetComponent<Camera>();
            attachedCamera.backgroundColor = value;
            attachedCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    private void Reset()
    {
        cameraMode = GameCameraMode.Mode2D;
        ApplyCameraMode();
    }

    private void Awake()
    {
        ApplyCameraMode();
    }

    private void OnValidate()
    {
        followVelocity = Vector3.zero;
        ApplyCameraMode();
    }

    private void OnEnable()
    {
        followVelocity = Vector3.zero;
    }

    private void Start()
    {
        if (snapOnStart)
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        CameraPixelMove2D pixelMove = BeginPixelMove();
        bool hasGroup = TryGetTargetGroup(out CameraTargetGroup2D group, out Bounds groupBounds);
        if (!hasGroup && group != null)
        {
            group.StopFraming();
        }
        ApplyViewExpansion(hasGroup ? group.CurrentSize : (float?)null);

        // Duraklatmada SmoothDamp'e sıfır zaman adımı gönderme.
        if (Time.deltaTime <= 0f)
        {
            if (pixelMove != null)
            {
                transform.position = pixelMove.CalculatePosition(transform.position);
            }
            return;
        }

        // Konumu tek noktadan uygula; bölge davranışı normal takibin yerini alır.
        bool hasFollowTarget = hasGroup || target != null;
        Vector3 followPosition = hasGroup ? groupBounds.center : target != null ? target.position : transform.position;
        if (!hasFollowTarget)
        {
            followVelocity = Vector3.zero;
        }
        else if (Is2D && TryGetComponent(out CameraDeadSoftZone2D zones) && zones.isActiveAndEnabled)
        {
            followVelocity = Vector3.zero;
            Vector3 framingPosition = followPosition + (Vector3)(offset + viewOffset);
            transform.position = zones.CalculatePosition(framingPosition, Time.deltaTime);
        }
        else if (smoothTime <= 0f)
        {
            followVelocity = Vector3.zero;
            transform.position = GetTargetPosition(followPosition);
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                GetTargetPosition(followPosition),
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        CompleteFraming(hasGroup ? group : null, groupBounds, Time.deltaTime, false, pixelMove);
    }

    /// <summary>Tek takip hedefini değiştirir. Geçerli bir hedef grubu varken grup önceliklidir.</summary>
    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        followVelocity = Vector3.zero;

        if (snapImmediately)
        {
            SnapToTarget();
        }
    }

    /// <summary>Changes the camera between orthographic 2D and perspective 3D behavior.</summary>
    public void SetCameraMode(GameCameraMode newMode)
    {
        cameraMode = newMode;
        followVelocity = Vector3.zero;
        ApplyCameraMode();
        SnapToTarget();
    }

    /// <summary>Yumuşatma uygulamadan mevcut hedefe ve kadraj kaymasına yerleşir.</summary>
    public void SnapToTarget()
    {
        followVelocity = Vector3.zero;
        CameraPixelMove2D pixelMove = BeginPixelMove();

        if (TryGetTargetGroup(out CameraTargetGroup2D group, out Bounds groupBounds))
        {
            ApplyViewExpansion(group.CurrentSize);
            transform.position = GetTargetPosition(groupBounds.center);
            CompleteFraming(group, groupBounds, 0f, true, pixelMove);
        }
        else
        {
            if (group != null)
            {
                group.StopFraming();
            }
            ApplyViewExpansion(null);
            if (target != null)
            {
                transform.position = GetTargetPosition(target.position);
            }
            if (pixelMove != null)
            {
                transform.position = pixelMove.CalculatePosition(transform.position);
            }
        }
    }

    private CameraPixelMove2D BeginPixelMove()
    {
        if (Is2D && TryGetComponent(out CameraPixelMove2D pixelMove) && pixelMove.isActiveAndEnabled)
        {
            transform.position = pixelMove.RestorePosition(transform.position);
            return pixelMove;
        }
        return null;
    }

    private bool TryGetTargetGroup(out CameraTargetGroup2D group, out Bounds groupBounds)
    {
        groupBounds = default;
        if (!TryGetComponent(out group))
        {
            return false;
        }

        return Is2D && group.isActiveAndEnabled && group.TryGetGroupBounds(out groupBounds);
    }

    private void CompleteFraming(CameraTargetGroup2D group, Bounds groupBounds, float deltaTime,
        bool snapImmediately, CameraPixelMove2D pixelMove)
    {
        // Bakış bileşeni açıksa dönüşü o yönetir; özel hedef boşken grup merkezine bakar.
        if (!Is2D && TryGetComponent(out CameraLookAtTarget2D lookAt) && lookAt.isActiveAndEnabled)
        {
            lookAt.ApplyRotation(deltaTime, snapImmediately, group != null ? groupBounds.center : (Vector3?)null);
        }
        else if (group != null)
        {
            transform.rotation = group.CalculateRotation(groupBounds.center, deltaTime, snapImmediately);
        }

        // Dönüşten sonra ekranın sağ/sol kaymasını yenile, sonra gerçek konuma göre grubu sığdır.
        ApplyViewExpansion(group != null ? group.CurrentSize : (float?)null);
        if (pixelMove != null)
        {
            transform.position = pixelMove.CalculatePosition(transform.position);
        }
        if (group != null)
        {
            ApplyViewExpansion(group.UpdateSize(groupBounds, deltaTime, snapImmediately));
        }
    }

    private void ApplyViewExpansion(float? groupSize)
    {
        if (TryGetComponent(out CameraViewExpansion2D expansion) && expansion.isActiveAndEnabled)
        {
            expansion.ApplyExpansion(Is2D ? groupSize : null);
        }
        else if (Is2D && groupSize.HasValue)
        {
            GetComponent<Camera>().orthographicSize = groupSize.Value;
        }
    }

    internal float AcquireSizeControl(MonoBehaviour owner)
    {
        if (sizeOwners.Count == 0)
        {
            unmodifiedOrthographicSize = GetComponent<Camera>().orthographicSize;
        }
        sizeOwners.Add(owner);
        return unmodifiedOrthographicSize;
    }

    internal void ReleaseSizeControl(MonoBehaviour owner)
    {
        if (!sizeOwners.Remove(owner))
        {
            return;
        }

        if (sizeOwners.Count == 0)
        {
            GetComponent<Camera>().orthographicSize = unmodifiedOrthographicSize;
        }
        else
        {
            bool hasGroup = TryGetTargetGroup(out CameraTargetGroup2D group, out _);
            ApplyViewExpansion(hasGroup ? group.CurrentSize : (float?)null);
        }
    }

    /// <summary>
    /// Görüş davranışının kadraj kaymasını uygular. Bu değeri tek bir bileşen yönetmelidir.
    /// Hedef olmasa da kayma uygulanır; tekrar çağrılar birikerek sürüklenme oluşturmaz.
    /// </summary>
    public void SetViewOffset(Vector2 newOffset)
    {
        Vector2 difference = newOffset - viewOffset;
        viewOffset = newOffset;
        transform.position += new Vector3(difference.x, difference.y, 0f);
    }

    /// <summary>Kamera düzlemindeki kaymayı Z'yi koruyan dünya X/Y kaymasına çevirir.</summary>
    internal static Vector2 CameraPlaneToWorldOffset(Quaternion rotation, Vector2 planeOffset)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        float determinant = right.x * up.y - right.y * up.x;

        // Kamera oyun düzlemine paralel bakıyorsa sabit Z ile çözüm üretilemez.
        if (Mathf.Abs(determinant) < 0.0001f)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (planeOffset.x * up.y - right.y * planeOffset.y) / determinant,
            (right.x * planeOffset.y - planeOffset.x * up.x) / determinant);
    }

    private Vector3 GetTargetPosition(Vector3 targetPosition)
    {
        if (!Is2D)
        {
            return targetPosition + offset3D;
        }

        // Hedefin Z konumundan bağımsız olarak kameranın derinliğini koru.
        return new Vector3(
            targetPosition.x + offset.x + viewOffset.x,
            targetPosition.y + offset.y + viewOffset.y,
            transform.position.z);
    }

    private void ApplyCameraMode()
    {
        Camera attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null)
        {
            return;
        }

        attachedCamera.orthographic = Is2D;
        if (!Is2D && viewOffset != Vector2.zero)
        {
            SetViewOffset(Vector2.zero);
        }
    }
}
