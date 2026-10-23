using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Sequences and blends registered shots through a single Camera Shots output.</summary>
[DisplayName("Director Camera Track")]
[TrackColor(0.26f, 0.56f, 0.86f)]
[TrackBindingType(typeof(CameraShotDirector))]
[TrackClipType(typeof(CameraShotClip))]
public class CameraShotTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<CameraShotMixerBehaviour> playable = ScriptPlayable<CameraShotMixerBehaviour>.Create(graph, inputCount);
        CameraShotMixerBehaviour.Input[] inputs = new CameraShotMixerBehaviour.Input[inputCount];
        int index = 0;
        foreach (TimelineClip clip in GetClips())
        {
            if (index >= inputCount)
            {
                break;
            }
            inputs[index] = new CameraShotMixerBehaviour.Input
            {
                Index = index,
                Start = clip.start,
                Duration = clip.duration
            };
            index++;
        }

        // Keep graph input indices intact; only evaluation order is sorted. Equal starts retain their order.
        for (int i = 1; i < index; i++)
        {
            CameraShotMixerBehaviour.Input current = inputs[i];
            int previous = i - 1;
            while (previous >= 0 && inputs[previous].Start > current.Start)
            {
                inputs[previous + 1] = inputs[previous];
                previous--;
            }
            inputs[previous + 1] = current;
        }

        PlayableDirector director = graph.GetResolver() as PlayableDirector;
        if (director == null && go != null)
        {
            director = go.GetComponent<PlayableDirector>();
        }
        playable.GetBehaviour().Initialize(director, inputs, index);
        return playable;
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        CameraShotDirector output = director.GetGenericBinding(this) as CameraShotDirector;
        if (output != null)
        {
            GameObject outputObject = output.gameObject;
            driver.AddFromName<Transform>(outputObject, "m_LocalPosition.x");
            driver.AddFromName<Transform>(outputObject, "m_LocalPosition.y");
            driver.AddFromName<Transform>(outputObject, "m_LocalPosition.z");
            driver.AddFromName<Transform>(outputObject, "m_LocalRotation.x");
            driver.AddFromName<Transform>(outputObject, "m_LocalRotation.y");
            driver.AddFromName<Transform>(outputObject, "m_LocalRotation.z");
            driver.AddFromName<Transform>(outputObject, "m_LocalRotation.w");
            driver.AddFromName<DirectorsCamera>(outputObject, "cameraMode");
            driver.AddFromName<Camera>(outputObject, "orthographic");
            driver.AddFromName<Camera>(outputObject, "orthographic size");
            driver.AddFromName<Camera>(outputObject, "field of view");
            // The native physical-camera FOV setter also changes focal length.
            driver.AddFromName<Camera>(outputObject, "m_FocalLength");
            // Orthographic/perspective changes can reset the physical projection mode.
            driver.AddFromName<Camera>(outputObject, "m_projectionMatrixMode");
            driver.AddFromName<Camera>(outputObject, "near clip plane");
            driver.AddFromName<Camera>(outputObject, "far clip plane");
            driver.AddFromName<Camera>(outputObject, "m_ClearFlags");
            driver.AddFromName<Camera>(outputObject, "m_BackGroundColor.r");
            driver.AddFromName<Camera>(outputObject, "m_BackGroundColor.g");
            driver.AddFromName<Camera>(outputObject, "m_BackGroundColor.b");
            driver.AddFromName<Camera>(outputObject, "m_BackGroundColor.a");

            for (int i = 0; i < output.Shots.Count; i++)
            {
                DirectorsCamera shot = output.Shots[i];
                if (shot == null)
                {
                    continue;
                }
                driver.AddFromName<Camera>(shot.gameObject, "m_Enabled");
                AudioListener[] listeners = shot.GetComponents<AudioListener>();
                for (int listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
                {
                    driver.AddFromName(listeners[listenerIndex], "m_Enabled");
                }
            }
        }
        base.GatherProperties(director, driver);
    }
}
