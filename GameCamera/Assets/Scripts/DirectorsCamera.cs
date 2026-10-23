using UnityEngine;

/// <summary>Sinematik özellikler için bağımsız kamera gövdesi; kendiliğinden hareket etmez.</summary>
[AddComponentMenu("Camera/Director's Camera")]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class DirectorsCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField, Tooltip("Selects 2D orthographic or 3D perspective behavior.")]
    private GameCameraMode cameraMode = GameCameraMode.Mode2D;

    private Camera controlledCamera;

    /// <summary>The runtime director that owns this shot; null for an independent camera.</summary>
    public CameraShotDirector ShotOwner { get; internal set; }

    internal CameraShotDirector OutputDirector { get; set; }

    // Shot carts keep their playback state while off screen. The rendering output has one pose writer.
    internal bool CanAdvanceDolly => isActiveAndEnabled &&
        (ShotOwner == null || ShotOwner.ActiveShot == this) && CanApplyDollyPose;

    internal bool CanApplyDollyPose => OutputDirector == null || !OutputDirector.IsControllingOutput;

    /// <summary>The active 2D or 3D behavior mode.</summary>
    public GameCameraMode CameraMode => cameraMode;

    /// <summary>Returns true while the controller uses orthographic 2D behavior.</summary>
    public bool Is2D => cameraMode == GameCameraMode.Mode2D;

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
        cameraMode = GameCameraMode.Mode2D;
        ApplyCameraMode();
    }

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        ApplyCameraMode();
    }

    private void OnValidate()
    {
        ApplyCameraMode();
    }

    /// <summary>Changes the camera between orthographic 2D and perspective 3D behavior.</summary>
    public void SetCameraMode(GameCameraMode newMode)
    {
        cameraMode = newMode;
        ApplyCameraMode();
    }

    /// <summary>
    /// Dünya koordinatlarında konumu ve dönüşü anında uygular; Z hareketi serbesttir.
    /// Zaman adımı kullanmaz, duraklatılmış oyunda da açıkça çağrıldığında çalışır.
    /// </summary>
    public void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    private void ApplyCameraMode()
    {
        Camera attachedCamera = ControlledCamera;
        if (attachedCamera != null)
        {
            attachedCamera.orthographic = Is2D;
        }
    }
}
