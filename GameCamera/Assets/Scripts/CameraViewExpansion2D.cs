using UnityEngine;

/// <summary>
/// Görüş alanını sola, sağa veya her yöne genişletir; görüntü oranını korur.
/// Konum değişikliklerini MainCamera üzerinden uygular.
/// </summary>
[RequireComponent(typeof(Camera), typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraViewExpansion2D : MonoBehaviour
{
    [Header("View Expansion")]
    [SerializeField, Min(0f), Tooltip("Sol kenara eklenecek görüş mesafesi (dünya birimi).")]
    private float leftExpansion;

    [SerializeField, Min(0f), Tooltip("Sağ kenara eklenecek görüş mesafesi (dünya birimi).")]
    private float rightExpansion;

    [SerializeField, Min(0f), Tooltip("Üst ve alta eklenecek mesafe. Sağ ve sol, görüntü oranına göre genişler.")]
    private float allSidesExpansion;

    [Header("Scene Preview")]
    [SerializeField, Tooltip("Scene görünümünde başlangıç ve genişletilmiş kadrajı gösterir. Gizmos açık olmalıdır.")]
    private bool showScenePreview = true;

    private Camera controlledCamera;
    private MainCamera cameraController;
    private float baseOrthographicSize;
    private Vector2 appliedViewOffset;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        cameraController = GetComponent<MainCamera>();
    }

    private void OnEnable()
    {
        // Her etkinleştirmede mevcut boyutu temel al; kareler arasında biriktirme.
        baseOrthographicSize = controlledCamera.orthographicSize;
        ApplyExpansion();
    }

    private void Update()
    {
        // Inspector değişikliklerini ve ekran oranındaki değişimleri takip et.
        ApplyExpansion();
    }

    private void OnDisable()
    {
        // Bileşen kapatıldığında kendi boyut ve kadraj değişikliğini geri al.
        if (controlledCamera != null)
        {
            controlledCamera.orthographicSize = baseOrthographicSize;
        }

        if (cameraController != null)
        {
            cameraController.SetViewOffset(Vector2.zero);
        }

        appliedViewOffset = Vector2.zero;
    }

    /// <summary>
    /// Genişleme miktarlarını dünya birimiyle ayarlar. Negatif değerler sıfırlanır.
    /// Devre dışıyken verilen ayarlar bir sonraki etkinleştirmede uygulanır.
    /// </summary>
    public void SetExpansion(float left, float right, float allSides = 0f)
    {
        leftExpansion = Mathf.Max(0f, left);
        rightExpansion = Mathf.Max(0f, right);
        allSidesExpansion = Mathf.Max(0f, allSides);

        if (isActiveAndEnabled)
        {
            ApplyExpansion();
        }
    }

    private void ApplyExpansion()
    {
        GetExpansion(controlledCamera.aspect, out float sizeIncrease, out Vector2 centerOffset);
        controlledCamera.orthographicSize = baseOrthographicSize + sizeIncrease;
        cameraController.SetViewOffset(centerOffset);
        appliedViewOffset = centerOffset;
    }

    private void GetExpansion(float aspect, out float sizeIncrease, out Vector2 centerOffset)
    {
        float left = Mathf.Max(0f, leftExpansion);
        float right = Mathf.Max(0f, rightExpansion);
        float allSides = Mathf.Max(0f, allSidesExpansion);
        aspect = Mathf.Max(0.0001f, aspect);

        // Orthographic Size, dikey görüş alanının yarısıdır.
        // Yatay ek mesafeyi görüntü oranıyla dikey boyuta çevir.
        sizeIncrease = allSides + (left + right) / (2f * aspect);

        // Tek tarafa genişlerken karşı yatay kenarı yerinde tut.
        centerOffset = new Vector2((right - left) * 0.5f, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview || !isActiveAndEnabled)
        {
            return;
        }

        Camera previewCamera = GetComponent<Camera>();
        if (previewCamera == null)
        {
            return;
        }

        float aspect = Mathf.Max(0.0001f, previewCamera.aspect);
        bool expansionApplied = Application.isPlaying && controlledCamera != null;
        float baseSize = expansionApplied ? baseOrthographicSize : previewCamera.orthographicSize;
        Vector2 baseCenter = (Vector2)transform.position;

        // Play sırasında uygulanmış kaymayı çıkar; temel kadrajı iki kez kaydırma.
        if (expansionApplied)
        {
            baseCenter -= appliedViewOffset;
        }

        GetExpansion(aspect, out float sizeIncrease, out Vector2 centerOffset);
        Rect baseRect = GetViewRect(baseCenter, baseSize, aspect);
        Rect expandedRect = GetViewRect(baseCenter + centerOffset, baseSize + sizeIncrease, aspect);

        // Çizimi 2B oyun düzlemine (Z = 0) yerleştir; sahne verilerini değiştirme.
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.12f);

        // Yalnız eklenen alanları boya; köşelerde üst üste saydamlık oluşturma.
        DrawExpansionArea(expandedRect.xMin, baseRect.xMin, baseRect.yMin, baseRect.yMax);
        DrawExpansionArea(baseRect.xMax, expandedRect.xMax, baseRect.yMin, baseRect.yMax);
        DrawExpansionArea(expandedRect.xMin, expandedRect.xMax, baseRect.yMax, expandedRect.yMax);
        DrawExpansionArea(expandedRect.xMin, expandedRect.xMax, expandedRect.yMin, baseRect.yMin);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(baseRect.center, baseRect.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(expandedRect.center, expandedRect.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(baseRect.xMin, baseRect.yMin, 0f), "Base View");
        UnityEditor.Handles.Label(new Vector3(expandedRect.xMin, expandedRect.yMax, 0f), "Expanded View");
#endif

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static Rect GetViewRect(Vector2 center, float halfHeight, float aspect)
    {
        Vector2 size = new Vector2(halfHeight * aspect * 2f, halfHeight * 2f);
        return new Rect(center - size * 0.5f, size);
    }

    private static void DrawExpansionArea(float xMin, float xMax, float yMin, float yMax)
    {
        if (xMax <= xMin || yMax <= yMin)
        {
            return;
        }

        Gizmos.DrawCube(
            new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f),
            new Vector3(xMax - xMin, yMax - yMin, 0f));
    }
}
