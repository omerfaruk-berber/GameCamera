using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>Uses independent shot cameras to drive one rendering Director's Camera.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(DirectorsCamera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class CameraShotDirector : MonoBehaviour
{
    [SerializeField, Tooltip("Independent shot cameras managed by this director. Their Camera and AudioListener components are muted during play.")]
    private List<DirectorsCamera> shots = new List<DirectorsCamera>();

    [SerializeField, Tooltip("Shot selected when play begins. If unavailable, the first usable shot in the list is selected.")]
    private DirectorsCamera startingShot = null;

    [SerializeField, Min(0f), Tooltip("Transition duration used by the Inspector Activate buttons. 0 cuts immediately. Different projection modes always cut.")]
    private float defaultBlendDuration;

    private readonly Dictionary<DirectorsCamera, ShotRegistration> registrations =
        new Dictionary<DirectorsCamera, ShotRegistration>();
    private readonly List<DirectorsCamera> removedShots = new List<DirectorsCamera>();
    private ReadOnlyCollection<DirectorsCamera> shotView;
    private DirectorsCamera cameraController;
    private Camera outputCamera;
    private Transform outputTransform;
    private ShotRegistration activeRegistration;
    private CameraState savedOutput;
    private CameraState blendSource;
    private float blendDuration;
    private float blendElapsed;
    private bool hasSavedOutput;
    private bool runtimeInitialized;
    private bool hasStarted;
    private bool shotsChanged;

    /// <summary>Configured shots in selection order. Use RegisterShot and UnregisterShot to change the list.</summary>
    public IReadOnlyList<DirectorsCamera> Shots => shotView ?? (shotView = shots.AsReadOnly());

    /// <summary>The incoming or currently selected shot, or null while the output is released.</summary>
    public DirectorsCamera ActiveShot => activeRegistration != null ? activeRegistration.Controller : null;

    /// <summary>Whether the output is currently moving toward the selected shot.</summary>
    public bool IsBlending => activeRegistration != null && blendElapsed < blendDuration;

    /// <summary>Default transition duration used by the Inspector controls.</summary>
    public float DefaultBlendDuration => defaultBlendDuration;

    internal bool IsControllingOutput => hasSavedOutput;

    private void Awake()
    {
        CacheOutput();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !InitializeRuntime())
        {
            return;
        }

        if (hasStarted)
        {
            SelectStartingShot();
        }
    }

    private void Start()
    {
        hasStarted = true;
        if (runtimeInitialized && activeRegistration == null)
        {
            SelectStartingShot();
        }
    }

    private void OnValidate()
    {
        defaultBlendDuration = SanitizeDuration(defaultBlendDuration);
        // OnValidate may run off the main thread. Component access is deferred to LateUpdate.
        shotsChanged = true;
    }

    private void OnDisable()
    {
        ReleaseShot();
        foreach (ShotRegistration registration in registrations.Values)
        {
            registration.Restore(this);
        }
        registrations.Clear();
        if (cameraController != null && cameraController.OutputDirector == this)
        {
            cameraController.OutputDirector = null;
        }
        runtimeInitialized = false;
    }

    private void LateUpdate()
    {
        UpdateShot(Time.deltaTime);
    }

    /// <summary>Adds a compatible shot once. Runtime registration also takes control of rendering and audio.</summary>
    public bool RegisterShot(DirectorsCamera shot)
    {
        CacheOutput();
        if (Application.isPlaying && isActiveAndEnabled && !InitializeRuntime())
        {
            return false;
        }
        if (!IsCompatible(shot) || shots.Contains(shot))
        {
            return false;
        }

        shots.Add(shot);
        if (Application.isPlaying && isActiveAndEnabled)
        {
            SynchronizeShots();
        }
        return true;
    }

    /// <summary>Removes a shot without destroying it and restores its captured render and audio state.</summary>
    public bool UnregisterShot(DirectorsCamera shot)
    {
        if (!shots.Remove(shot))
        {
            return false;
        }
        if (runtimeInitialized)
        {
            SynchronizeShots();
        }
        return true;
    }

    /// <summary>Selects a registered, enabled shot during play. Invalid durations cut immediately.</summary>
    public bool ActivateShot(DirectorsCamera shot, float blendDuration = 0f)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !InitializeRuntime())
        {
            return false;
        }
        if (shotsChanged)
        {
            SynchronizeShots();
        }
        if (shot == null || !registrations.TryGetValue(shot, out ShotRegistration registration) ||
            !registration.IsUsable || !cameraController.isActiveAndEnabled)
        {
            return false;
        }

        SelectShot(registration, SanitizeDuration(blendDuration));
        return true;
    }

    /// <summary>Stops shot control and restores the output pose and settings saved before selection began.</summary>
    public void ReleaseShot()
    {
        activeRegistration = null;
        blendDuration = 0f;
        blendElapsed = 0f;
        if (hasSavedOutput)
        {
            if (cameraController != null && outputCamera != null && outputTransform != null)
            {
                savedOutput.Apply(cameraController, outputCamera, outputTransform);
            }
            hasSavedOutput = false;
        }
    }

    private void CacheOutput()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<DirectorsCamera>();
            outputCamera = cameraController.ControlledCamera;
            outputTransform = cameraController.transform;
        }
    }

    private bool InitializeRuntime()
    {
        if (runtimeInitialized)
        {
            return true;
        }
        CacheOutput();
        if (cameraController.ShotOwner != null || GetComponent<MainCamera>() != null)
        {
            Debug.LogWarning("Camera Shots requires an independent Director's Camera output, not a managed shot or MainCamera.", this);
            return false;
        }

        runtimeInitialized = true;
        cameraController.OutputDirector = this;
        SynchronizeShots();
        return true;
    }

    private bool IsCompatible(DirectorsCamera shot)
    {
        return shot != null && shot != cameraController &&
            (shot.ShotOwner == null || shot.ShotOwner == this) &&
            !shot.TryGetComponent<MainCamera>(out _) &&
            !shot.TryGetComponent<CameraShotDirector>(out _) &&
            !shot.transform.IsChildOf(outputTransform) && !outputTransform.IsChildOf(shot.transform);
    }

    private void SynchronizeShots()
    {
        shotsChanged = false;
        removedShots.Clear();
        foreach (KeyValuePair<DirectorsCamera, ShotRegistration> entry in registrations)
        {
            if (!shots.Contains(entry.Key) || !IsCompatible(entry.Key))
            {
                entry.Value.Restore(this);
                removedShots.Add(entry.Key);
            }
        }
        for (int i = 0; i < removedShots.Count; i++)
        {
            registrations.Remove(removedShots[i]);
        }
        removedShots.Clear();

        for (int i = 0; i < shots.Count; i++)
        {
            DirectorsCamera shot = shots[i];
            if (shot == null || registrations.ContainsKey(shot))
            {
                continue;
            }
            if (!IsCompatible(shot))
            {
                Debug.LogWarning("Camera Shots ignored an incompatible shot. Use independent cameras without another director or MainCamera.", this);
                continue;
            }

            registrations.Add(shot, new ShotRegistration(shot, this));
        }

        if (activeRegistration != null && (!activeRegistration.IsUsable ||
            !registrations.ContainsValue(activeRegistration)))
        {
            SelectFallback();
        }
    }

    private void SelectStartingShot()
    {
        if (startingShot != null && registrations.TryGetValue(startingShot, out ShotRegistration registration) &&
            registration.IsUsable)
        {
            SelectShot(registration, 0f);
        }
        else
        {
            SelectFallback();
        }
    }

    private void SelectFallback()
    {
        for (int i = 0; i < shots.Count; i++)
        {
            DirectorsCamera shot = shots[i];
            if (shot != null && registrations.TryGetValue(shot, out ShotRegistration registration) && registration.IsUsable)
            {
                SelectShot(registration, 0f);
                return;
            }
        }
        ReleaseShot();
    }

    private void SelectShot(ShotRegistration registration, float duration)
    {
        if (!hasSavedOutput)
        {
            savedOutput = CameraState.Capture(cameraController, outputCamera, outputTransform);
            hasSavedOutput = true;
        }

        // A new transition always starts from the image currently being rendered.
        blendSource = CameraState.Capture(cameraController, outputCamera, outputTransform);
        CameraState destination = registration.Capture();
        activeRegistration = registration;
        blendElapsed = 0f;
        blendDuration = blendSource.Mode == destination.Mode && blendSource.Orthographic == destination.Orthographic
            ? duration : 0f;
        ApplySelectedShot(destination);
    }

    // Explicit time input also makes transitions deterministic to verify independently of frame timing.
    private void UpdateShot(float deltaTime)
    {
        if (!runtimeInitialized)
        {
            return;
        }
        if (shotsChanged)
        {
            SynchronizeShots();
        }
        if (activeRegistration == null)
        {
            return;
        }
        if (!cameraController.isActiveAndEnabled)
        {
            ReleaseShot();
            return;
        }
        if (!activeRegistration.IsUsable)
        {
            SelectFallback();
            return;
        }

        CameraState destination = activeRegistration.Capture();
        if (blendSource.Mode != destination.Mode || blendSource.Orthographic != destination.Orthographic)
        {
            blendDuration = 0f;
        }
        blendElapsed = Mathf.Min(blendDuration, blendElapsed + Mathf.Max(0f, deltaTime));
        ApplySelectedShot(destination);
    }

    private void ApplySelectedShot(CameraState destination)
    {
        CameraState state = IsBlending
            ? CameraState.Interpolate(blendSource, destination, blendElapsed / blendDuration)
            : destination;
        state.Apply(cameraController, outputCamera, outputTransform);
    }

    private static float SanitizeDuration(float duration)
    {
        return float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : Mathf.Max(0f, duration);
    }

    private sealed class ShotRegistration
    {
        public readonly DirectorsCamera Controller;
        private readonly Camera camera;
        private readonly Transform pose;
        private readonly bool cameraWasEnabled;
        private readonly AudioListener[] listeners;
        private readonly bool[] listenerWasEnabled;

        public bool IsUsable => Controller != null && Controller.isActiveAndEnabled && camera != null;

        public ShotRegistration(DirectorsCamera controller, CameraShotDirector owner)
        {
            Controller = controller;
            camera = controller.ControlledCamera;
            pose = controller.transform;
            cameraWasEnabled = camera.enabled;
            listeners = controller.GetComponents<AudioListener>();
            listenerWasEnabled = new bool[listeners.Length];
            controller.ShotOwner = owner;
            camera.enabled = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                listenerWasEnabled[i] = listeners[i].enabled;
                listeners[i].enabled = false;
            }
        }

        public CameraState Capture() => CameraState.Capture(Controller, camera, pose);

        public void Restore(CameraShotDirector owner)
        {
            if (Controller != null && Controller.ShotOwner == owner)
            {
                Controller.ShotOwner = null;
            }
            if (camera != null)
            {
                camera.enabled = cameraWasEnabled;
            }
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                {
                    listeners[i].enabled = listenerWasEnabled[i];
                }
            }
        }
    }

    private struct CameraState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public GameCameraMode Mode;
        public bool Orthographic;
        public float Size;
        public float FieldOfView;
        public float NearClip;
        public float FarClip;
        public Color Background;
        public CameraClearFlags ClearFlags;

        public static CameraState Capture(DirectorsCamera controller, Camera camera, Transform pose)
        {
            return new CameraState
            {
                Position = pose.position,
                Rotation = pose.rotation,
                Mode = controller.CameraMode,
                Orthographic = camera.orthographic,
                Size = camera.orthographicSize,
                FieldOfView = camera.fieldOfView,
                NearClip = camera.nearClipPlane,
                FarClip = camera.farClipPlane,
                Background = camera.backgroundColor,
                ClearFlags = camera.clearFlags
            };
        }

        public static CameraState Interpolate(CameraState from, CameraState to, float progress)
        {
            return new CameraState
            {
                Position = Vector3.Lerp(from.Position, to.Position, progress),
                Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, progress),
                Mode = to.Mode,
                Orthographic = to.Orthographic,
                Size = Mathf.Lerp(from.Size, to.Size, progress),
                FieldOfView = Mathf.Lerp(from.FieldOfView, to.FieldOfView, progress),
                NearClip = Mathf.Lerp(from.NearClip, to.NearClip, progress),
                FarClip = Mathf.Lerp(from.FarClip, to.FarClip, progress),
                Background = Color.Lerp(from.Background, to.Background, progress),
                ClearFlags = to.ClearFlags
            };
        }

        public void Apply(DirectorsCamera controller, Camera camera, Transform pose)
        {
            pose.SetPositionAndRotation(Position, Rotation);
            if (controller.CameraMode != Mode)
            {
                controller.SetCameraMode(Mode);
            }
            camera.orthographic = Orthographic;
            camera.orthographicSize = Size;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = FarClip;
            camera.backgroundColor = Background;
            camera.clearFlags = ClearFlags;
        }
    }
}
