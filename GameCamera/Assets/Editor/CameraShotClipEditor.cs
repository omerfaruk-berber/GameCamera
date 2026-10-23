using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Uses Timeline's native exposed-reference drawer so scene references stay on the director.</summary>
[CustomEditor(typeof(CameraShotClip))]
public class CameraShotClipEditor : Editor
{
    private SerializedProperty shotProperty;
    private SerializedProperty dollyModeProperty;
    private SerializedProperty startPositionProperty;
    private SerializedProperty endPositionProperty;

    private void OnEnable()
    {
        shotProperty = serializedObject.FindProperty("shot");
        dollyModeProperty = serializedObject.FindProperty("dollyMode");
        startPositionProperty = serializedObject.FindProperty("startPosition");
        endPositionProperty = serializedObject.FindProperty("endPosition");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(shotProperty);
        EditorGUILayout.PropertyField(dollyModeProperty);
        if (dollyModeProperty.enumValueIndex == (int)CameraShotDollyMode.Timeline)
        {
            EditorGUILayout.PropertyField(startPositionProperty);
            EditorGUILayout.PropertyField(endPositionProperty);
            EditorGUILayout.HelpBox("The clip duration controls Dolly travel. Reversing Start Position and End Position reverses travel. Shots without a Dolly Cart use their Transform.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Uses the cart's current playback, speed, loop and damping settings. Seeking does not rewind live Dolly playback.", MessageType.None);
        }
        if (serializedObject.ApplyModifiedProperties())
        {
            TimelineEditor.Refresh(RefreshReason.ContentsModified | RefreshReason.SceneNeedsUpdate);
        }

        PlayableDirector contextDirector = serializedObject.context as PlayableDirector;
        if (contextDirector == null)
        {
            EditorGUILayout.HelpBox("Select this clip in a scene PlayableDirector's Timeline to assign a scene shot and preview the sequence.", MessageType.Info);
            return;
        }

        CameraShotClip cameraClip = (CameraShotClip)target;
        CameraShotTrack track = FindTrack(contextDirector.playableAsset as TimelineAsset, cameraClip);
        string warning = GetValidationMessage(cameraClip, track, contextDirector);
        if (!string.IsNullOrEmpty(warning))
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
    }

    internal static string GetValidationMessage(CameraShotClip clip, CameraShotTrack track, PlayableDirector director)
    {
        if (director == null)
        {
            return "Open this Timeline on a scene PlayableDirector to resolve the shot reference.";
        }
        if (track == null || !(director.GetGenericBinding(track) is CameraShotDirector output))
        {
            return "Bind the Director Camera Track to a Camera Shots feature on the output camera.";
        }
        DirectorsCamera shot = clip.shot.Resolve(director);
        if (shot == null)
        {
            return "Assign a registered shot camera. Empty or missing shots do not contribute to the output.";
        }
        bool registered = false;
        for (int i = 0; i < output.Shots.Count; i++)
        {
            if (output.Shots[i] == shot)
            {
                registered = true;
                break;
            }
        }
        if (!registered)
        {
            return "Add this shot to the bound Camera Shots feature's Shots list before using it in Timeline.";
        }
        if (!shot.isActiveAndEnabled || shot == output.GetComponent<DirectorsCamera>() ||
            shot.GetComponent<MainCamera>() != null || shot.GetComponent<CameraShotDirector>() != null ||
            shot.transform.IsChildOf(output.transform) || output.transform.IsChildOf(shot.transform))
        {
            return "This shot is unavailable. Use an enabled, independent Director's Camera without MainCamera or its own Camera Shots feature.";
        }
        CameraDollyCart cart = shot.GetComponent<CameraDollyCart>();
        if (cart != null && cart.isActiveAndEnabled && (cart.Track == null || !cart.Track.IsValid))
        {
            return "The Dolly Cart has no valid track. Assign a valid Dolly Track to animate travel; the shot's Transform is used otherwise.";
        }
        return null;
    }

    private static CameraShotTrack FindTrack(TimelineAsset timeline, CameraShotClip asset)
    {
        if (timeline == null)
        {
            return null;
        }
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (!(track is CameraShotTrack cameraTrack))
            {
                continue;
            }
            foreach (TimelineClip clip in cameraTrack.GetClips())
            {
                if (clip.asset == asset)
                {
                    return cameraTrack;
                }
            }
        }
        return null;
    }
}

/// <summary>Displays shot assignment problems directly on the Timeline clip.</summary>
[CustomTimelineEditor(typeof(CameraShotClip))]
public class CameraShotTimelineClipEditor : ClipEditor
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        ClipDrawOptions options = base.GetClipOptions(clip);
        if (!(clip.asset is CameraShotClip cameraClip))
        {
            return options;
        }
        PlayableDirector director = TimelineEditor.inspectedDirector;
        options.errorText = CameraShotClipEditor.GetValidationMessage(cameraClip,
            clip.GetParentTrack() as CameraShotTrack, director);
        DirectorsCamera shot = director != null ? cameraClip.shot.Resolve(director) : null;
        options.tooltip = shot != null ? shot.name + " (" + cameraClip.dollyMode + ")" : "Camera Shot";
        return options;
    }
}
