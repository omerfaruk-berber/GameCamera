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
        if (target == null)
        {
            followVelocity = Vector3.zero;
            return;
        }

        // Duraklatmada SmoothDamp'e sıfır zaman adımı gönderme.
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        // Hedefin Update hareketinden sonra kameranın konumunu hesapla.
        if (smoothTime <= 0f)
        {
            SnapToTarget();
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            GetTargetPosition(),
            ref followVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime);
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
