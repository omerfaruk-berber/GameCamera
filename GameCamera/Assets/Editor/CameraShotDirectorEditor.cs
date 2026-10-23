using UnityEditor;
using UnityEditor.Timeline;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>Shot creation, assignment and playback controls shared by both Inspector entry points.</summary>
[CustomEditor(typeof(CameraShotDirector))]
public class CameraShotDirectorEditor : Editor
{
    private SerializedProperty shotsProperty;
    private SerializedProperty startingShotProperty;
    private SerializedProperty durationProperty;
    private ReorderableList shotList;
    private string assignmentError;

    private void OnEnable()
    {
        shotsProperty = serializedObject.FindProperty("shots");
        startingShotProperty = serializedObject.FindProperty("startingShot");
        durationProperty = serializedObject.FindProperty("defaultBlendDuration");
        shotList = new ReorderableList(serializedObject, shotsProperty, true, true, true, true);
        shotList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Shots");
        shotList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
        shotList.drawElementCallback = DrawShot;
        shotList.onAddCallback = list =>
        {
            int index = shotsProperty.arraySize++;
            shotsProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
            list.index = index;
        };
        shotList.onRemoveCallback = list =>
        {
            int index = list.index;
            if (index < 0 || index >= shotsProperty.arraySize)
            {
                return;
            }
            shotsProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
            shotsProperty.DeleteArrayElementAtIndex(index);
            list.index = Mathf.Min(index, shotsProperty.arraySize - 1);
        };
    }

    public override void OnInspectorGUI()
    {
        CameraShotDirector director = (CameraShotDirector)target;
        serializedObject.Update();
        shotList.DoLayoutList();
        EditorGUILayout.PropertyField(startingShotProperty);
        EditorGUILayout.PropertyField(durationProperty);
        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }

        if (!string.IsNullOrEmpty(assignmentError))
        {
            EditorGUILayout.HelpBox(assignmentError, MessageType.Warning);
        }
        if (director.Shots.Count == 0)
        {
            EditorGUILayout.HelpBox("Create a shot or add an existing independent Director's Camera. An empty list leaves the output unchanged.", MessageType.Info);
        }
        if (startingShotProperty.objectReferenceValue != null &&
            !ContainsShot(director, (DirectorsCamera)startingShotProperty.objectReferenceValue))
        {
            EditorGUILayout.HelpBox("Starting Shot must also be in the Shots list. The first usable listed shot will be used instead.", MessageType.Warning);
        }

        bool prefabAsset = EditorUtility.IsPersistent(director) ||
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null;
        using (new EditorGUI.DisabledScope(prefabAsset))
        {
            if (GUILayout.Button(new GUIContent("Create Shot", "Creates an independent shot in the output camera's scene and adds it to this list.")))
            {
                CreateShot(director);
                GUIUtility.ExitGUI();
            }
        }
        using (new EditorGUI.DisabledScope(prefabAsset || Application.isPlaying))
        {
            if (GUILayout.Button(new GUIContent("Create Timeline", "Creates a Timeline asset and a separate PlayableDirector bound to this output. Add Camera Shot clips to arrange your sequence.")))
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Director Camera Timeline",
                    "Director Camera Timeline", "playable", "Choose where to save the Timeline asset.");
                if (!string.IsNullOrEmpty(path))
                {
                    PlayableDirector timelineDirector = CreateTimeline(director, path);
                    Selection.activeGameObject = timelineDirector.gameObject;
                    TimelineEditor.GetOrCreateWindow().SetTimeline(timelineDirector);
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (prefabAsset)
        {
            EditorGUILayout.HelpBox("Create scene shots from a scene instance. Existing references can still be edited in Prefab Mode.", MessageType.Info);
        }

        if (Application.isPlaying)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Active Shot", director.ActiveShot, typeof(DirectorsCamera), true);
                EditorGUILayout.Toggle("Blending", director.IsBlending);
                EditorGUILayout.Toggle("Timeline Controlled", director.IsTimelineControlled);
                EditorGUILayout.Toggle("Holding Shot", director.IsHoldingShot);
            }
            if (director.IsTimelineControlled)
            {
                EditorGUILayout.HelpBox("Timeline controls this output. Stop the sequence to select or release shots manually.", MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(director.IsTimelineControlled ||
                (director.ActiveShot == null && !director.IsHoldingShot)))
            {
                if (GUILayout.Button(new GUIContent("Release Shot", "Restores the output pose and camera settings saved before shot selection began.")))
                {
                    director.ReleaseShot();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("The starting shot is selected automatically in Play Mode. Activate controls are available during play.", MessageType.None);
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    private void DrawShot(Rect rect, int index, bool active, bool focused)
    {
        CameraShotDirector director = (CameraShotDirector)target;
        SerializedProperty element = shotsProperty.GetArrayElementAtIndex(index);
        DirectorsCamera shot = (DirectorsCamera)element.objectReferenceValue;
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        const float buttonWidth = 58f;
        Rect fieldRect = new Rect(rect.x, rect.y, Mathf.Max(30f, rect.width - buttonWidth * 2f - 8f), rect.height);
        Rect selectRect = new Rect(fieldRect.xMax + 4f, rect.y, buttonWidth, rect.height);
        Rect activateRect = new Rect(selectRect.xMax + 4f, rect.y, buttonWidth, rect.height);

        EditorGUI.BeginChangeCheck();
        DirectorsCamera newShot = (DirectorsCamera)EditorGUI.ObjectField(fieldRect, shot, typeof(DirectorsCamera), true);
        if (EditorGUI.EndChangeCheck())
        {
            assignmentError = GetAssignmentError(director, newShot, index);
            if (string.IsNullOrEmpty(assignmentError))
            {
                element.objectReferenceValue = newShot;
                shot = newShot;
            }
        }
        using (new EditorGUI.DisabledScope(shot == null))
        {
            if (GUI.Button(selectRect, new GUIContent("Select", "Selects this shot object in the Hierarchy.")))
            {
                Selection.activeGameObject = shot.gameObject;
            }
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !director.isActiveAndEnabled ||
            director.IsTimelineControlled || shot == null))
        {
            if (GUI.Button(activateRect, new GUIContent("Activate", "Selects this shot using Default Blend Duration.")))
            {
                serializedObject.ApplyModifiedProperties();
                director.ActivateShot(shot, director.DefaultBlendDuration);
                SceneView.RepaintAll();
            }
        }
    }

    private static string GetAssignmentError(CameraShotDirector director, DirectorsCamera shot, int index)
    {
        if (shot == null)
        {
            return null;
        }
        DirectorsCamera output = director.GetComponent<DirectorsCamera>();
        if (shot == output || shot.transform.IsChildOf(output.transform) || output.transform.IsChildOf(shot.transform))
        {
            return "Use an independent shot outside the output camera's parent/child hierarchy.";
        }
        if (EditorUtility.IsPersistent(shot) || shot.GetComponent<MainCamera>() != null ||
            shot.GetComponent<CameraShotDirector>() != null)
        {
            return "A shot must be a scene Director's Camera without MainCamera or its own Camera Shots feature.";
        }
        for (int i = 0; i < director.Shots.Count; i++)
        {
            if (i != index && director.Shots[i] == shot)
            {
                return "This shot is already in the list.";
            }
        }
        CameraShotDirector owner = FindConfiguredOwner(shot, director);
        return owner != null ? "This shot is already assigned to another Camera Shots director." : null;
    }

    internal static CameraShotDirector FindConfiguredOwner(DirectorsCamera shot, CameraShotDirector excluded = null)
    {
        // Editor-only discovery runs on assignment/menu actions, never in the runtime update loop.
        CameraShotDirector[] directors = Object.FindObjectsByType<CameraShotDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < directors.Length; i++)
        {
            if (directors[i] != excluded && ContainsShot(directors[i], shot))
            {
                return directors[i];
            }
        }
        return null;
    }

    private static bool ContainsShot(CameraShotDirector director, DirectorsCamera shot)
    {
        for (int i = 0; i < director.Shots.Count; i++)
        {
            if (director.Shots[i] == shot)
            {
                return true;
            }
        }
        return false;
    }

    private static void CreateShot(CameraShotDirector director)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Director Shot");

        DirectorsCamera output = director.GetComponent<DirectorsCamera>();
        GameObject shotObject = new GameObject("Director Shot " + (director.Shots.Count + 1));
        SceneManager.MoveGameObjectToScene(shotObject, output.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(shotObject, "Create Director Shot");
        Camera shotCamera = Undo.AddComponent<Camera>(shotObject);
        DirectorsCamera shot = Undo.AddComponent<DirectorsCamera>(shotObject);
        Undo.RecordObject(shotObject.transform, "Position Director Shot");
        shot.SetPose(output.transform.position, output.transform.rotation);
        Undo.RecordObject(shotCamera, "Copy Shot Camera Settings");
        shotCamera.CopyFrom(output.ControlledCamera);
        shotCamera.enabled = false;
        Undo.RecordObject(shot, "Set Shot Camera Mode");
        shot.SetCameraMode(output.CameraMode);

        Undo.RecordObject(director, "Register Director Shot");
        director.RegisterShot(shot);
        SerializedObject directorData = new SerializedObject(director);
        SerializedProperty startingShot = directorData.FindProperty("startingShot");
        if (startingShot.objectReferenceValue == null)
        {
            startingShot.objectReferenceValue = shot;
            directorData.ApplyModifiedProperties();
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(director);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(shotCamera);
        EditorUtility.SetDirty(shot);
        Undo.CollapseUndoOperations(undoGroup);
        EditorGUIUtility.PingObject(shotObject);
        SceneView.RepaintAll();
    }

    /// <summary>Creates the authoring objects without modifying an existing scene camera hierarchy.</summary>
    internal static PlayableDirector CreateTimeline(CameraShotDirector director, string assetPath)
    {
        // Preserve any existing asset if an already-used filename was chosen.
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Director Camera Timeline");

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(timeline, assetPath);
        Undo.RegisterCreatedObjectUndo(timeline, "Create Camera Timeline Asset");
        // Timeline records its own track creation and parent-list changes in this Undo group.
        CameraShotTrack track = timeline.CreateTrack<CameraShotTrack>(null, "Director Camera Track");

        GameObject timelineObject = new GameObject("Director Camera Timeline");
        SceneManager.MoveGameObjectToScene(timelineObject, director.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(timelineObject, "Create Camera Timeline Director");
        PlayableDirector playableDirector = Undo.AddComponent<PlayableDirector>(timelineObject);
        Undo.RecordObject(playableDirector, "Bind Director Camera Timeline");
        playableDirector.playOnAwake = false;
        playableDirector.extrapolationMode = DirectorWrapMode.Hold;
        playableDirector.playableAsset = timeline;
        playableDirector.SetGenericBinding(track, director);
        EditorUtility.SetDirty(playableDirector);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssetIfDirty(timeline);
        Undo.CollapseUndoOperations(undoGroup);
        EditorGUIUtility.PingObject(timeline);
        return playableDirector;
    }
}
