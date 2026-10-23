using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Controls whether a shot's Dolly follows clip time or its existing playback.</summary>
public enum CameraShotDollyMode
{
    Timeline,
    LivePlayback
}

/// <summary>A shot reference and optional Dolly movement on a Director Camera Track.</summary>
[Serializable]
[DisplayName("Camera Shot")]
public class CameraShotClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("A scene shot registered in the track's Camera Shots director.")]
    public ExposedReference<DirectorsCamera> shot;

    [Tooltip("Timeline samples Dolly from clip time for scrubbing. Live Playback continues the cart's existing playback during play.")]
    public CameraShotDollyMode dollyMode = CameraShotDollyMode.Timeline;

    [Range(0f, 1f), Tooltip("Normalized Dolly position at the start of this clip. Used only in Timeline mode.")]
    public float startPosition;

    [Range(0f, 1f), Tooltip("Normalized Dolly position at the end of this clip. Use a lower end value to travel backwards.")]
    public float endPosition = 1f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override double duration => 5d;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CameraShotPlayableBehaviour> playable = ScriptPlayable<CameraShotPlayableBehaviour>.Create(graph);
        CameraShotPlayableBehaviour behaviour = playable.GetBehaviour();
        behaviour.Initialize(shot.Resolve(graph.GetResolver()), dollyMode,
            SanitizePosition(startPosition, 0f), SanitizePosition(endPosition, 1f));
        return playable;
    }

    private static float SanitizePosition(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }
}

/// <summary>Per-graph clip data; Dolly sampling never changes the source cart's playback state.</summary>
internal sealed class CameraShotPlayableBehaviour : PlayableBehaviour
{
    private DirectorsCamera shot;
    private CameraDollyCart cart;
    private Quaternion baselineRotation;
    private CameraShotDollyMode dollyMode;
    private float startPosition;
    private float endPosition;

    internal void Initialize(DirectorsCamera source, CameraShotDollyMode mode, float start, float end)
    {
        shot = source;
        dollyMode = mode;
        startPosition = start;
        endPosition = end;
        if (shot != null)
        {
            baselineRotation = shot.transform.rotation;
            shot.TryGetComponent(out cart);
        }
    }

    internal DirectorsCamera Shot => shot;

    internal CameraShotTimelineSample Sample(double localTime, double duration, float weight)
    {
        CameraShotTimelineSample sample = new CameraShotTimelineSample
        {
            Shot = shot,
            Weight = weight,
            IsLive = dollyMode == CameraShotDollyMode.LivePlayback
        };
        if (sample.IsLive || cart == null || !cart.isActiveAndEnabled)
        {
            return sample;
        }

        float progress = duration > 0d ? Mathf.Clamp01((float)(localTime / duration)) : 0f;
        float normalizedPosition = Mathf.Lerp(startPosition, endPosition, progress);
        sample.HasPoseOverride = cart.TryEvaluatePose(normalizedPosition,
            endPosition >= startPosition ? 1 : -1, baselineRotation, out sample.Position, out sample.Rotation);
        return sample;
    }
}
