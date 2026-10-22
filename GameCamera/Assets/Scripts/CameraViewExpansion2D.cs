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
    private float appliedBaseOrthographicSize;
    private float? groupBaseOrthographicSize;
    private Vector2 appliedViewOffset;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        cameraController = GetComponent<MainCamera>();
    }

    private void OnEnable()
    {
        // Her etkinleştirmede mevcut boyutu temel al; kareler arasında biriktirme.
        baseOrthographicSize = cameraController.AcquireSizeControl(this);
        bool hasGroup = TryGetComponent(out CameraTargetGroup2D group) && group.isActiveAndEnabled &&
            group.TryGetGroupBounds(out _);
        ApplyExpansion(hasGroup ? group.CurrentSize : (float?)null);
    }

    private void OnDisable()
    {
        // Bileşen kapatıldığında kendi boyut ve kadraj değişikliğini geri al.
        if (controlledCamera != null)
        {
            controlledCamera.orthographicSize = groupBaseOrthographicSize ?? baseOrthographicSize;
        }

        if (cameraController != null)
        {
            cameraController.SetViewOffset(Vector2.zero);
        }

        appliedViewOffset = Vector2.zero;
        groupBaseOrthographicSize = null;
        if (cameraController != null)
        {
            cameraController.ReleaseSizeControl(this);
        }
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
            ApplyExpansion(groupBaseOrthographicSize);
        }
    }

    // Boyut değişikliklerini MainCamera sıralar; grup zoom'u ile iki bağımsız yazıcı oluşmaz.
    internal void ApplyExpansion(float? groupSize)
    {
        groupBaseOrthographicSize = groupSize;
        appliedBaseOrthographicSize = groupSize ?? baseOrthographicSize;
        GetExpansion(controlledCamera.aspect, out float sizeIncrease, out Vector2 centerOffset);
        controlledCamera.orthographicSize = appliedBaseOrthographicSize + sizeIncrease;
        // Kamera döndüğünde de sağ/sol, ekranın sağ/sol yönünü ifade eder.
        appliedViewOffset = MainCamera.CameraPlaneToWorldOffset(transform.rotation, centerOffset);
        cameraController.SetViewOffset(appliedViewOffset);
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

    internal void GetPreviewView(
        out Vector3 baseCenter, out float baseSize, out Vector3 expandedCenter, out float expandedSize)
    {
        Camera previewCamera = GetComponent<Camera>();
        bool expansionApplied = Application.isPlaying && controlledCamera != null;
        baseSize = expansionApplied ? appliedBaseOrthographicSize : previewCamera.orthographicSize;
        baseCenter = transform.position;
        if (expansionApplied)
        {
            baseCenter -= (Vector3)appliedViewOffset;
            expandedCenter = transform.position;
            expandedSize = previewCamera.orthographicSize;
            return;
        }

        GetExpansion(previewCamera.aspect, out float sizeIncrease, out Vector2 centerOffset);
        expandedSize = baseSize + sizeIncrease;
        expandedCenter = baseCenter + (Vector3)MainCamera.CameraPlaneToWorldOffset(transform.rotation, centerOffset);
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
        GetPreviewView(out Vector3 baseCenter, out float baseSize, out Vector3 expandedCenter, out float expandedSize);
        Vector3 relativeBaseCenter = Quaternion.Inverse(transform.rotation) * (baseCenter - expandedCenter);
        Rect baseRect = GetViewRect(relativeBaseCenter, baseSize, aspect);
        Rect expandedRect = GetViewRect(Vector2.zero, expandedSize, aspect);

        // Çizimleri kameranın dönüşüyle hizala; merkez derinliği oyun düzleminden alınır.
        float depth = Mathf.Abs(transform.forward.z) > 0.0001f ? -expandedCenter.z / transform.forward.z : 1f;
        depth = Mathf.Max(previewCamera.nearClipPlane, depth);
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            expandedCenter + transform.forward * depth, transform.rotation, Vector3.one);
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
        UnityEditor.Handles.Label(Gizmos.matrix.MultiplyPoint3x4(
            new Vector3(baseRect.xMin, baseRect.yMin, 0f)), "Base View");
        UnityEditor.Handles.Label(Gizmos.matrix.MultiplyPoint3x4(
            new Vector3(expandedRect.xMin, expandedRect.yMax, 0f)), "Expanded View");
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
