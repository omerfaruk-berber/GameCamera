using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class MainCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField, Tooltip("X/Y düzleminde takip edilecek nesne.")]
    private Transform target;

    [SerializeField, Tooltip("Hedefe göre dünya koordinatlarında X/Y kadraj kayması.")]
    private Vector2 offset = Vector2.zero;

    [SerializeField, Min(0f), Tooltip("Takibin yaklaşık yumuşama süresi (saniye). 0 anında takip eder.")]
    private float smoothTime = 0.2f;

    [SerializeField, Tooltip("Oyun başlarken kamerayı doğrudan hedefe yerleştirir.")]
    private bool snapOnStart = true;

    // SmoothDamp kareler arasındaki takip hızını bu alanda saklar.
    private Vector3 followVelocity;

    // Ek kamera davranışlarının takip kadrajına eklediği dünya birimi kayması.
    private Vector2 viewOffset;

    /// <summary>Şu anda takip edilen hedef; hedef atanmamışsa null döner.</summary>
    public Transform Target => target;

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
        // Bileşen editörde ilk eklendiğinde 2B görünümü hazırla.
        GetComponent<Camera>().orthographic = true;
    }

    private void Awake()
    {
        GetComponent<Camera>().orthographic = true;
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
        // Duraklatmada SmoothDamp'e sıfır zaman adımı gönderme.
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        // Konumu tek noktadan uygula; bölge davranışı normal takibin yerini alır.
        if (target == null)
        {
            followVelocity = Vector3.zero;
        }
        else if (TryGetComponent(out CameraDeadSoftZone2D zones) && zones.isActiveAndEnabled)
        {
            followVelocity = Vector3.zero;
            Vector3 framingPosition = target.position + (Vector3)(offset + viewOffset);
            transform.position = zones.CalculatePosition(framingPosition, Time.deltaTime);
        }
        else if (smoothTime <= 0f)
        {
            SnapToTarget();
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                GetTargetPosition(),
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        // Konum tamamlandıktan sonra bağımsız bakış hedefine dön.
        if (TryGetComponent(out CameraLookAtTarget2D lookAt) && lookAt.isActiveAndEnabled)
        {
            lookAt.ApplyRotation(Time.deltaTime);
        }
    }

    /// <summary>Hedefi değiştirir. null verilirse takip durur.</summary>
    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        followVelocity = Vector3.zero;

        if (snapImmediately)
        {
            SnapToTarget();
        }
    }

    /// <summary>Yumuşatma uygulamadan mevcut hedefe ve kadraj kaymasına yerleşir.</summary>
    public void SnapToTarget()
    {
        followVelocity = Vector3.zero;

        if (target != null)
        {
            transform.position = GetTargetPosition();
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

    private Vector3 GetTargetPosition()
    {
        Vector3 targetPosition = target.position;

        // Hedefin Z konumundan bağımsız olarak kameranın derinliğini koru.
        return new Vector3(
            targetPosition.x + offset.x + viewOffset.x,
            targetPosition.y + offset.y + viewOffset.y,
            transform.position.z);
    }
}
