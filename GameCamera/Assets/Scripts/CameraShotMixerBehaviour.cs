using UnityEngine;
using UnityEngine.Playables;

/// <summary>Mixes clip weights immediately on evaluation and retains live inputs for the output's late pass.</summary>
internal sealed class CameraShotMixerBehaviour : PlayableBehaviour
{
    internal struct Input
    {
        internal int Index;
        internal double Start;
        internal double Duration;
    }

    private PlayableDirector playableDirector;
    private CameraShotDirector output;
    private Input[] inputs;
    private CameraShotTimelineSample[] samples;
    private int inputCount;
    private bool ownsOutput;
    private bool stopped;
    private bool reportedOwnershipConflict;

    internal void Initialize(PlayableDirector director, Input[] orderedInputs, int count)
    {
        playableDirector = director;
        inputs = orderedInputs;
        inputCount = count;
        samples = new CameraShotTimelineSample[count];
        if (playableDirector != null)
        {
            playableDirector.stopped += OnDirectorStopped;
            playableDirector.paused += OnDirectorPaused;
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (stopped)
        {
            return;
        }
        CameraShotDirector binding = playerData as CameraShotDirector;
        if (binding != output)
        {
            ReleaseOutput();
            output = binding;
            reportedOwnershipConflict = false;
        }
        if (output == null || !output.isActiveAndEnabled)
        {
            ReleaseOutput();
            return;
        }
        if (ownsOutput && !output.IsTimelineOwner(this))
        {
            ownsOutput = false;
        }
        if (!ownsOutput)
        {
            ownsOutput = output.BeginTimelineControl(this);
            if (!ownsOutput)
            {
                if (output.IsTimelineControlled && !reportedOwnershipConflict)
                {
                    Debug.LogWarning("Director Camera Track could not take control because another Timeline track already owns this Camera Shots output.", output);
                    reportedOwnershipConflict = true;
                }
                return;
            }
        }

        int sampleCount = 0;
        for (int i = 0; i < inputCount; i++)
        {
            Input input = inputs[i];
            float weight = playable.GetInputWeight(input.Index);
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
            {
                continue;
            }
            ScriptPlayable<CameraShotPlayableBehaviour> clip =
                (ScriptPlayable<CameraShotPlayableBehaviour>)playable.GetInput(input.Index);
            CameraShotPlayableBehaviour behaviour = clip.GetBehaviour();
            if (!output.IsTimelineShotUsable(behaviour.Shot))
            {
                continue;
            }
            samples[sampleCount++] = behaviour.Sample(clip.GetTime(), input.Duration, weight);
        }

        bool advancing = Application.isPlaying && playableDirector != null &&
            playableDirector.state == PlayState.Playing && info.effectivePlayState == PlayState.Playing &&
            info.evaluationType == FrameData.EvaluationType.Playback;
        output.ApplyTimelineFrame(this, samples, sampleCount, advancing);
    }

    public override void OnGraphStop(Playable playable)
    {
        FreezeLivePlayback();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        ReleaseOutput();
        if (playableDirector != null)
        {
            playableDirector.stopped -= OnDirectorStopped;
            playableDirector.paused -= OnDirectorPaused;
        }
        playableDirector = null;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        stopped = true;
        ReleaseOutput();
    }

    private void OnDirectorPaused(PlayableDirector director)
    {
        FreezeLivePlayback();
    }

    private void FreezeLivePlayback()
    {
        if (ownsOutput && output != null)
        {
            output.SetTimelinePlaybackAdvancing(this, false);
        }
    }

    private void ReleaseOutput()
    {
        if (ownsOutput && output != null)
        {
            output.EndTimelineControl(this);
        }
        ownsOutput = false;
        output = null;
    }
}

/// <summary>A single evaluated Timeline input. The mixer owns and reuses the containing array.</summary>
internal struct CameraShotTimelineSample
{
    internal DirectorsCamera Shot;
    internal float Weight;
    internal bool IsLive;
    internal bool HasPoseOverride;
    internal Vector3 Position;
    internal Quaternion Rotation;
}
