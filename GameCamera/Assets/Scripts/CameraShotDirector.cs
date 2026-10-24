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

    [SerializeField, Tooltip("Manual shot transition: instant Cut, slow Gentle, adjustable Custom, or fast edge Blur. Different projection modes always cut. Timeline uses its own clip weights.")]
    private CameraShotTransition transition = CameraShotTransition.Cut;

    // Keep the existing serialized duration field; the Custom speed slider displays its reciprocal.
    [SerializeField, Range(0.25f, 4f), Tooltip("Duration of the Custom transition in seconds. Transition Speed controls its inverse: higher speeds finish sooner.")]
    private float defaultBlendDuration = 1f;

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
    private bool easeBlend;
    private bool blurBlend;
    private float blurStartStrength;
    private float blurStrength;
    private bool hasSavedOutput;
    private bool runtimeInitialized;
    private bool hasStarted;
    private bool shotsChanged;
    private object timelineOwner;
    private CameraShotTimelineSample[] timelineSamples;
    private int timelineSampleCount;
    private bool timelinePlaybackAdvancing;
    private bool timelineBlending;
    private bool holdingShot;
    private CameraState heldOutput;
    private CameraState previewOutput;

    /// <summary>Configured shots in selection order. Use RegisterShot and UnregisterShot to change the list.</summary>
    public IReadOnlyList<DirectorsCamera> Shots => shotView ?? (shotView = shots.AsReadOnly());

    /// <summary>The incoming or currently selected shot, or null while the output is released.</summary>
    public DirectorsCamera ActiveShot => activeRegistration != null ? activeRegistration.Controller : null;

    /// <summary>Whether the output is currently moving toward the selected shot.</summary>
    public bool IsBlending => IsTimelineControlled ? timelineBlending :
        !holdingShot && activeRegistration != null && blendElapsed < blendDuration;

    /// <summary>Whether an exclusive Timeline mixer currently controls this output.</summary>
    public bool IsTimelineControlled => timelineOwner != null;

    /// <summary>Whether the output is holding its last evaluated Timeline pose.</summary>
    public bool IsHoldingShot => holdingShot;

    /// <summary>Preset used by ActivateShot(shot) and the Inspector Activate buttons.</summary>
    public CameraShotTransition Transition
    {
        get => transition;
        set => transition = SanitizeTransition(value);
    }

    /// <summary>Custom transition speed, from 0.25 to 4. Its duration is 1 / speed seconds.</summary>
    public float TransitionSpeed
    {
        get => 1f / SanitizeCustomDuration(defaultBlendDuration);
        set => defaultBlendDuration = 1f / SanitizeCustomDuration(value);
    }

    /// <summary>Duration of the selected manual preset in seconds. Cut is zero.</summary>
    public float DefaultBlendDuration
    {
        get
        {
            switch (transition)
            {
                case CameraShotTransition.Gentle: return 1.25f;
                case CameraShotTransition.Custom: return SanitizeCustomDuration(defaultBlendDuration);
                case CameraShotTransition.Blur: return 0.35f;
                default: return 0f;
            }
        }
    }

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
        if (runtimeInitialized && activeRegistration == null && !IsTimelineControlled && !holdingShot)
        {
            SelectStartingShot();
        }
    }

    private void OnValidate()
    {
        transition = SanitizeTransition(transition);
        defaultBlendDuration = SanitizeCustomDuration(defaultBlendDuration);
        // OnValidate may run off the main thread. Component access is deferred to LateUpdate.
        shotsChanged = true;
    }

    private void OnDisable()
    {
        timelineOwner = null;
        ClearTimelineSamples();
        ReleaseShot();
        RestoreRegistrations();
    }

    private void RestoreRegistrations()
    {
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

    /// <summary>Selects a registered, enabled shot using the configured manual transition preset.</summary>
    public bool ActivateShot(DirectorsCamera shot)
    {
        return TryActivateShot(shot, DefaultBlendDuration, transition != CameraShotTransition.Cut,
            transition == CameraShotTransition.Blur);
    }

    /// <summary>Selects a shot with an explicit linear, effect-free duration. Zero or invalid durations cut immediately.</summary>
    public bool ActivateShot(DirectorsCamera shot, float blendDuration)
    {
        return TryActivateShot(shot, SanitizeDuration(blendDuration), false, false);
    }

    private bool TryActivateShot(DirectorsCamera shot, float duration, bool eased, bool withBlur)
    {
        if (IsTimelineControlled || !Application.isPlaying || !isActiveAndEnabled || !InitializeRuntime())
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

        SelectShot(registration, duration, eased, withBlur);
        return true;
    }

    /// <summary>Stops shot control and restores the output pose and settings saved before selection began.</summary>
    public void ReleaseShot()
    {
        if (IsTimelineControlled)
        {
            return;
        }
        holdingShot = false;
        ClearTransitionBlur();
        activeRegistration = null;
        blendDuration = 0f;
        blendElapsed = 0f;
        if (hasSavedOutput)
        {
            if (cameraController != null && outputCamera != null && outputTransform != null)
            {
                savedOutput.Restore(cameraController, outputCamera, outputTransform);
            }
            hasSavedOutput = false;
        }
    }

    // A graph owns one lease. Old graph callbacks cannot write into a newer session.
    internal bool BeginTimelineControl(object owner)
    {
        if (owner == null || !isActiveAndEnabled)
        {
            return false;
        }
        if (timelineOwner != null)
        {
            return ReferenceEquals(timelineOwner, owner);
        }
        CacheOutput();
        if (!cameraController.isActiveAndEnabled)
        {
            return false;
        }
        CameraState current = CameraState.Capture(cameraController, outputCamera, outputTransform);
        if (!InitializeRuntime())
        {
            return false;
        }
        if (!Application.isPlaying)
        {
            previewOutput = current;
        }
        if (!hasSavedOutput)
        {
            savedOutput = current;
            hasSavedOutput = true;
        }
        timelineOwner = owner;
        ClearTransitionBlur();
        heldOutput = current;
        holdingShot = true;
        blendDuration = 0f;
        blendElapsed = 0f;
        return true;
    }

    internal bool IsTimelineShotUsable(DirectorsCamera shot)
    {
        if (shotsChanged && runtimeInitialized)
        {
            SynchronizeShots();
        }
        return shot != null && registrations.TryGetValue(shot, out ShotRegistration registration) && registration.IsUsable;
    }

    internal bool IsTimelineOwner(object owner) => ReferenceEquals(timelineOwner, owner);

    internal void ApplyTimelineFrame(object owner, CameraShotTimelineSample[] samples, int count,
        bool livePlaybackAdvancing)
    {
        if (!ReferenceEquals(timelineOwner, owner) || !isActiveAndEnabled || samples == null)
        {
            return;
        }
        if (shotsChanged)
        {
            SynchronizeShots();
        }
        timelineSamples = samples;
        timelineSampleCount = Mathf.Clamp(count, 0, samples.Length);
        timelinePlaybackAdvancing = livePlaybackAdvancing && Application.isPlaying;
        ComposeTimelineFrame();
    }

    internal void SetTimelinePlaybackAdvancing(object owner, bool advancing)
    {
        if (ReferenceEquals(timelineOwner, owner))
        {
            timelinePlaybackAdvancing = advancing && Application.isPlaying;
        }
    }

    internal void EndTimelineControl(object owner)
    {
        if (!ReferenceEquals(timelineOwner, owner) || timelineOwner == null)
        {
            return;
        }
        timelineOwner = null;
        ClearTimelineSamples();
        if (Application.isPlaying)
        {
            // Keep a value snapshot, never a live reference to a cart that can keep moving.
            holdingShot = true;
            if (cameraController != null && outputCamera != null && outputTransform != null)
            {
                heldOutput.Apply(cameraController, outputCamera, outputTransform);
            }
        }
        else
        {
            ReleaseShot();
            if (cameraController != null && outputCamera != null && outputTransform != null)
            {
                previewOutput.Restore(cameraController, outputCamera, outputTransform);
            }
            RestoreRegistrations();
        }
    }

    internal bool CanAdvanceShotDolly(DirectorsCamera shot)
    {
        if (cameraController == null || !cameraController.isActiveAndEnabled)
        {
            return false;
        }
        if (IsTimelineControlled)
        {
            if (!timelinePlaybackAdvancing)
            {
                return false;
            }
            for (int i = 0; i < timelineSampleCount; i++)
            {
                CameraShotTimelineSample sample = timelineSamples[i];
                if (sample.IsLive && sample.Shot == shot && IsPositiveWeight(sample.Weight) && IsTimelineShotUsable(shot))
                {
                    return true;
                }
            }
            return false;
        }
        return !holdingShot && ActiveShot == shot;
    }

    private void ComposeTimelineFrame()
    {
        if (cameraController == null || outputCamera == null || !cameraController.isActiveAndEnabled)
        {
            return;
        }
        float accumulatedWeight = 0f;
        float dominantWeight = -1f;
        int contributorCount = 0;
        bool mixedProjection = false;
        GameCameraMode firstMode = default;
        bool firstOrthographic = false;
        CameraState mixed = default;
        CameraState dominant = default;
        ShotRegistration dominantRegistration = null;
        for (int i = 0; i < timelineSampleCount; i++)
        {
            CameraShotTimelineSample sample = timelineSamples[i];
            if (!IsPositiveWeight(sample.Weight) || sample.Shot == null ||
                !registrations.TryGetValue(sample.Shot, out ShotRegistration registration) || !registration.IsUsable)
            {
                continue;
            }
            CameraState state = registration.Capture();
            if (sample.HasPoseOverride)
            {
                state.Position = sample.Position;
                state.Rotation = sample.Rotation;
            }
            if (contributorCount == 0)
            {
                mixed = state;
                firstMode = state.Mode;
                firstOrthographic = state.Orthographic;
            }
            else
            {
                mixedProjection |= state.Mode != firstMode || state.Orthographic != firstOrthographic;
                mixed = CameraState.Interpolate(mixed, state, sample.Weight / (accumulatedWeight + sample.Weight));
            }
            // Mixer inputs are chronological; equal weights choose the later incoming shot.
            if (sample.Weight >= dominantWeight)
            {
                dominantWeight = sample.Weight;
                dominant = state;
                dominantRegistration = registration;
            }
            accumulatedWeight += sample.Weight;
            contributorCount++;
        }

        timelineBlending = !mixedProjection && contributorCount > 1;
        holdingShot = contributorCount == 0;
        if (contributorCount > 0)
        {
            mixed.ClearFlags = dominant.ClearFlags;
            heldOutput = mixedProjection ? dominant : mixed;
            activeRegistration = dominantRegistration;
        }
        heldOutput.Apply(cameraController, outputCamera, outputTransform);
    }

    private static bool IsPositiveWeight(float weight)
    {
        return weight > 0f && !float.IsInfinity(weight) && !float.IsNaN(weight);
    }

    private void ClearTimelineSamples()
    {
        timelineSamples = null;
        timelineSampleCount = 0;
        timelinePlaybackAdvancing = false;
        timelineBlending = false;
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

        if (!IsTimelineControlled && !holdingShot && activeRegistration != null && (!activeRegistration.IsUsable ||
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

    private void SelectShot(ShotRegistration registration, float duration, bool eased = false, bool withBlur = false)
    {
        holdingShot = false;
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
        easeBlend = eased;
        blurBlend = withBlur && blendDuration > 0f;
        // An interrupted Blur starts with its current strength instead of flashing back to a sharp image.
        blurStartStrength = blurBlend ? blurStrength : 0f;
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
        if (cameraController == null || !cameraController.isActiveAndEnabled)
        {
            if (!IsTimelineControlled && !holdingShot)
            {
                ReleaseShot();
            }
            return;
        }
        if (IsTimelineControlled)
        {
            if (timelinePlaybackAdvancing)
            {
                ComposeTimelineFrame();
            }
            else
            {
                heldOutput.Apply(cameraController, outputCamera, outputTransform);
            }
            return;
        }
        if (holdingShot)
        {
            heldOutput.Apply(cameraController, outputCamera, outputTransform);
            return;
        }
        if (activeRegistration == null)
        {
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
        float progress = IsBlending ? blendElapsed / blendDuration : 1f;
        float poseProgress = easeBlend ? Mathf.SmoothStep(0f, 1f, progress) : progress;
        CameraState state = IsBlending
            ? CameraState.Interpolate(blendSource, destination, poseProgress)
            : destination;
        state.Apply(cameraController, outputCamera, outputTransform);
        if (blurBlend && progress < 1f)
        {
            blurStrength = Mathf.Lerp(Mathf.Sin(Mathf.PI * progress), 1f - progress, blurStartStrength);
            CameraShotBlurFeature.SetBlur(outputCamera, blurStrength);
        }
        else
        {
            ClearTransitionBlur();
        }
    }

    private void ClearTransitionBlur()
    {
        if (blurStrength > 0f)
        {
            CameraShotBlurFeature.ClearBlur(outputCamera);
            blurStrength = 0f;
        }
    }

    private static CameraShotTransition SanitizeTransition(CameraShotTransition value)
    {
        return value >= CameraShotTransition.Cut && value <= CameraShotTransition.Blur
            ? value : CameraShotTransition.Cut;
    }

    private static float SanitizeCustomDuration(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
            ? 1f : Mathf.Clamp(value, 0.25f, 4f);
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
        public float FocalLength;
        public bool UsesPhysicalProperties;
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
                FocalLength = camera.focalLength,
                UsesPhysicalProperties = camera.usePhysicalProperties,
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

        public void Restore(DirectorsCamera controller, Camera camera, Transform pose)
        {
            Apply(controller, camera, pose);
            // Restore the original physical lens rather than round-tripping it through FOV.
            camera.focalLength = FocalLength;
            camera.usePhysicalProperties = UsesPhysicalProperties;
        }
    }
}
