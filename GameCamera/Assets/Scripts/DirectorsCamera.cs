using UnityEngine;

/// <summary>Sinematik özellikler için bağımsız kamera gövdesi; kendiliğinden hareket etmez.</summary>
[AddComponentMenu("Camera/Director's Camera")]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class DirectorsCamera : MonoBehaviour
{
    private Camera controlledCamera;

    /// <summary>Aynı nesnedeki Unity Camera bileşenine erişir; editörde de kullanılabilir.</summary>
    public Camera ControlledCamera
    {
        get
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            return controlledCamera;
        }
    }

    /// <summary>
    /// Unity Camera bileşeninin gerçek arka plan rengini yönetir.
    /// Renk atandığında düz renk arka plan modu etkinleştirilir.
    /// </summary>
    public Color BackgroundColor
    {
        get => ControlledCamera.backgroundColor;
        set
        {
            Camera attachedCamera = ControlledCamera;
            attachedCamera.backgroundColor = value;
            attachedCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    private void Reset()
    {
        // İlk eklemede veya Reset komutunda 2B görünümle başla; diğer ayarları koru.
        ControlledCamera.orthographic = true;
    }

    private void Awake()
    {
        // Oyun başlarken yalnızca referansı al; kullanıcının görüntü seçimini koru.
        controlledCamera = GetComponent<Camera>();
    }

    /// <summary>
    /// Dünya koordinatlarında konumu ve dönüşü anında uygular; Z hareketi serbesttir.
    /// Zaman adımı kullanmaz, duraklatılmış oyunda da açıkça çağrıldığında çalışır.
    /// </summary>
    public void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }
}
