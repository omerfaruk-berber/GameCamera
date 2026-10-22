using UnityEngine;

public enum DollyPlaybackMode
{
    Once,
    Loop,
    PingPong
}

public enum DollyOrientationMode
{
    KeepRotation,
    FollowTrack,
    LookAtTarget
}

/// <summary>Director's Camera'yı bir Dolly Track üzerinde oynatan cart davranışı.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(DirectorsCamera))]
[DisallowMultipleComponent]
public class CameraDollyCart : MonoBehaviour
{
    [Header("Track & Playback")]
    [SerializeField, Tooltip("Dolly Track used to move the camera.")]
    private CameraDollyTrack track;

    [SerializeField, Min(0f), Tooltip("Cart movement speed in world units per second.")]
    private float speed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("Starting position on the track. 0 is the beginning and 1 is the end.")]
    private float startPosition;

    [SerializeField, Tooltip("Starts cart movement automatically when play begins.")]
    private bool playOnStart = true;

    [SerializeField, Tooltip("Playback behavior used when the cart reaches the end of the track.")]
    private DollyPlaybackMode playbackMode = DollyPlaybackMode.Once;

    [Header("Orientation")]
    [SerializeField, Tooltip("Controls camera rotation while moving along the track.")]
    private DollyOrientationMode orientation = DollyOrientationMode.KeepRotation;

    [SerializeField, Tooltip("Object the camera faces when Look At Target is selected. This orientation is available only in 3D mode.")]
    private Transform lookAtTarget = null;

    [SerializeField, Tooltip("Euler rotation added after the calculated orientation.")]
    private Vector3 rotationOffset = Vector3.zero;

    [SerializeField, Min(0f), Tooltip("Approximate rotation smoothing time. Set to 0 for an immediate rotation.")]
    private float rotationDamping = 0.15f;

    [Header("Scene Preview")]
    [SerializeField, Tooltip("Shows the cart position and movement direction in the Scene view.")]
    private bool showScenePreview = true;

    private DirectorsCamera cameraController;
    private float currentDistance;
    private int travelDirection = 1;
    private bool isInitialized;
    private bool isPlaying;

    /// <summary>Cart'ın kullandığı Dolly Track.</summary>
    public CameraDollyTrack Track => track;

    /// <summary>Cart hareketinde kullanılan mevcut yönlendirme biçimi.</summary>
    public DollyOrientationMode Orientation => orientation;

    /// <summary>Cart hareketinin şu anda çalışıp çalışmadığını bildirir.</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>Cart'ın ray üzerindeki mevcut 0–1 konumu.</summary>
    public float NormalizedPosition
    {
        get
        {
            if (!isInitialized)
            {
                return startPosition;
            }

            float length = track != null ? track.Length : 0f;
            return length > 0.0001f
                ? Mathf.Clamp01(currentDistance / length)
                : startPosition;
        }
    }

    private void Awake()
    {
        cameraController = GetComponent<DirectorsCamera>();
    }

    private void Start()
    {
        InitializePosition();
        if (playOnStart)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        isPlaying = false;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        startPosition = Mathf.Clamp01(startPosition);
        rotationDamping = Mathf.Max(0f, rotationDamping);
    }

    private void LateUpdate()
    {
        if (!isPlaying || track == null || !track.IsValid || Time.deltaTime <= 0f)
        {
            return;
        }

        float length = track.Length;
        float movement = speed * Time.deltaTime;

        switch (playbackMode)
        {
            case DollyPlaybackMode.Loop:
                travelDirection = 1;
                currentDistance = Mathf.Repeat(currentDistance + movement, length);
                break;

            case DollyPlaybackMode.PingPong:
                AdvancePingPong(movement, length);
                break;

            default:
                travelDirection = 1;
                currentDistance = Mathf.Min(currentDistance + movement, length);
                if (currentDistance >= length)
                {
                    isPlaying = false;
                }
                break;
        }

        ApplyCurrentPose(Time.deltaTime, false);
    }

    /// <summary>Cart'ın kullanacağı rayı değiştirir; yeni ray başlangıç konumundan hazırlanır.</summary>
    public void SetTrack(CameraDollyTrack newTrack)
    {
        track = newTrack;
        isPlaying = false;
        isInitialized = false;
        travelDirection = 1;
    }

    /// <summary>Mevcut konumdan oynatmayı başlatır veya tamamlanmış Once oynatımını yeniden başlatır.</summary>
    public void Play()
    {
        EnsureInitialized();
        if (track == null || !track.IsValid)
        {
            isPlaying = false;
            return;
        }

        if (playbackMode == DollyPlaybackMode.Once && currentDistance >= track.Length)
        {
            currentDistance = startPosition * track.Length;
            travelDirection = 1;
        }

        ApplyCurrentPose(0f, true);
        isPlaying = true;
    }

    /// <summary>Cart'ı mevcut ray konumunda duraklatır.</summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>Oynatmayı durdurur ve cart'ı ayarlanan başlangıç konumuna getirir.</summary>
    public void Stop()
    {
        InitializePosition();
        isPlaying = false;
        ApplyCurrentPose(0f, true);
    }

    /// <summary>Cart'ı ray üzerindeki 0–1 konumuna anında yerleştirir.</summary>
    public void SetNormalizedPosition(float normalizedPosition)
    {
        if (track == null || !track.IsValid)
        {
            return;
        }

        currentDistance = Mathf.Clamp01(normalizedPosition) * track.Length;
        travelDirection = 1;
        isInitialized = true;
        ApplyCurrentPose(0f, true);
    }

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializePosition();
        }
    }

    private void InitializePosition()
    {
        float length = track != null ? track.Length : 0f;
        currentDistance = startPosition * length;
        travelDirection = 1;
        isInitialized = true;
    }

    private void AdvancePingPong(float movement, float length)
    {
        float phase = travelDirection > 0
            ? currentDistance
            : 2f * length - currentDistance;
        phase = Mathf.Repeat(phase + movement, 2f * length);

        if (phase <= length)
        {
            currentDistance = phase;
            travelDirection = phase < length ? 1 : -1;
        }
        else
        {
            currentDistance = 2f * length - phase;
            travelDirection = -1;
        }
    }

    private void ApplyCurrentPose(float deltaTime, bool snapRotation)
    {
        if (track == null || !track.IsValid)
        {
            return;
        }

        if (cameraController == null)
        {
            cameraController = GetComponent<DirectorsCamera>();
        }

        Vector3 position = track.EvaluatePosition(NormalizedPosition);
        Quaternion rotation = CalculateRotation(position, deltaTime, snapRotation);
        cameraController.SetPose(position, rotation);
    }

    private Quaternion CalculateRotation(Vector3 position, float deltaTime, bool snapRotation)
    {
        Quaternion currentRotation = transform.rotation;
        Vector3 direction;

        switch (orientation)
        {
            case DollyOrientationMode.FollowTrack:
                direction = track.EvaluateTangent(NormalizedPosition) * travelDirection;
                break;

            case DollyOrientationMode.LookAtTarget:
                if (cameraController.Is2D)
                {
                    return currentRotation;
                }

                direction = lookAtTarget != null ? lookAtTarget.position - position : Vector3.zero;
                break;

            default:
                return currentRotation;
        }

        if (direction.sqrMagnitude <= 0.000001f)
        {
            return currentRotation;
        }

        Quaternion desiredRotation;
        if (cameraController.Is2D)
        {
            Vector2 planarDirection = new Vector2(direction.x, direction.y);
            if (planarDirection.sqrMagnitude <= 0.000001f)
            {
                return currentRotation;
            }

            float angle = Mathf.Atan2(planarDirection.y, planarDirection.x) * Mathf.Rad2Deg - 90f;
            desiredRotation = Quaternion.Euler(0f, 0f, angle + rotationOffset.z);
        }
        else
        {
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(direction.normalized, up)) > 0.999f)
            {
                up = Vector3.ProjectOnPlane(transform.up, direction).normalized;
                if (up.sqrMagnitude <= 0.000001f)
                {
                    up = Vector3.forward;
                }
            }

            desiredRotation = Quaternion.LookRotation(direction.normalized, up) * Quaternion.Euler(rotationOffset);
        }

        if (snapRotation || rotationDamping <= 0f || deltaTime <= 0f)
        {
            return desiredRotation;
        }

        float blend = 1f - Mathf.Exp(-deltaTime / rotationDamping);
        return Quaternion.Slerp(currentRotation, desiredRotation, blend);
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview || track == null || !track.IsValid)
        {
            return;
        }

        float previewPosition = Application.isPlaying && isInitialized
            ? NormalizedPosition
            : startPosition;
        Vector3 position = track.EvaluatePosition(previewPosition);
        Vector3 tangent = track.EvaluateTangent(previewPosition);
        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(0.2f, 0.65f, 1f, 1f);
        Gizmos.DrawWireSphere(position, 0.3f);
        Gizmos.DrawLine(position, position + tangent);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position, "Dolly Cart");
#endif
        Gizmos.color = previousColor;
    }
}
