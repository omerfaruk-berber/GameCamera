using UnityEditor;
using UnityEngine;

/// <summary>Director's Camera için arka plan ayarlarını ve özellik alanını gösterir.</summary>
[CustomEditor(typeof(DirectorsCamera))]
public class DirectorsCameraEditor : Editor
{
    private Editor dollyEditor;
    private bool dollyExpanded = true;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Director's Camera", EditorStyles.boldLabel);

        if (DrawDefaultInspector())
        {
            SceneView.RepaintAll();
        }

        DrawBackgroundSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Features", EditorStyles.boldLabel);

        if (GUILayout.Button(new GUIContent("Add Feature", "Add an optional cinematic behavior to this camera.")))
        {
            ShowAddFeatureMenu();
        }

        DrawDollyFeature();
    }

    private void DrawBackgroundSettings()
    {
        DirectorsCamera controller = (DirectorsCamera)target;
        Camera attachedCamera = controller.ControlledCamera;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Background Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Color backgroundColor = EditorGUILayout.ColorField(
            new GUIContent("Background Color", "Camera background color. Changing it enables Solid Color clearing."),
            controller.BackgroundColor);

        if (EditorGUI.EndChangeCheck())
        {
            // Renk ve arka plan modu Camera üzerinde saklanır; Undo ve prefab kaydı bu bileşene uygulanır.
            Undo.RecordObject(attachedCamera, "Change Camera Background");
            controller.BackgroundColor = backgroundColor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(attachedCamera);
            EditorUtility.SetDirty(attachedCamera);
            SceneView.RepaintAll();
        }
    }

    private void ShowAddFeatureMenu()
    {
        var menu = new GenericMenu();
        DirectorsCamera controller = (DirectorsCamera)target;
        CameraDollyCart existingFeature = controller.GetComponent<CameraDollyCart>();

        if (existingFeature != null)
        {
            menu.AddDisabledItem(new GUIContent(
                "Dolly Camera (Track & Cart) (Added)",
                "Moves the camera along a virtual track."),
                true);
        }
        else
        {
            menu.AddItem(new GUIContent(
                "Dolly Camera (Track & Cart)",
                "Moves the camera along a virtual track."),
                false,
                () =>
                {
                    if (controller == null || controller.GetComponent<CameraDollyCart>() != null)
                    {
                        return;
                    }

                    Undo.AddComponent<CameraDollyCart>(controller.gameObject);
                    Repaint();
                    SceneView.RepaintAll();
                });
        }

        menu.ShowAsContext();
    }

    private void DrawDollyFeature()
    {
        DirectorsCamera controller = (DirectorsCamera)target;
        CameraDollyCart feature = controller.GetComponent<CameraDollyCart>();
        if (feature == null)
        {
            ReleaseEditor(ref dollyEditor);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                dollyExpanded = EditorGUILayout.Foldout(dollyExpanded, "Dolly Camera (Track & Cart)", true);

                EditorGUI.BeginChangeCheck();
                bool enabled = GUILayout.Toggle(feature.enabled, new GUIContent("Enabled", "Enable or disable this feature."));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(feature, "Toggle Dolly Camera");
                    feature.enabled = enabled;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
                    EditorUtility.SetDirty(feature);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button(new GUIContent(
                    "Remove",
                    "Removes the cart feature but keeps its separate Dolly Track object. Use Ctrl+Z to undo."),
                    GUILayout.Width(65f)))
                {
                    ReleaseEditor(ref dollyEditor);
                    Undo.DestroyObjectImmediate(feature);
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                }
            }

            if (!dollyExpanded)
            {
                return;
            }

            if (controller.Is2D && feature.Orientation == DollyOrientationMode.LookAtTarget)
            {
                EditorGUILayout.HelpBox(
                    "Look At Target orientation is available only in 3D mode and is currently inactive.",
                    MessageType.Info);
            }

            CreateCachedEditor(feature, null, ref dollyEditor);
            dollyEditor.serializedObject.Update();
            DrawPropertiesExcluding(dollyEditor.serializedObject, "m_Script");
            if (dollyEditor.serializedObject.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            if (feature.Track == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Dolly Track or create a two-point starter track below.",
                    MessageType.Info);

                if (GUILayout.Button(new GUIContent(
                    "Create Dolly Track",
                    "Creates a new two-point Dolly Track in the scene and assigns it to this cart.")))
                {
                    CreateDollyTrack(controller, feature);
                    GUIUtility.ExitGUI();
                }
            }

            DrawPlaybackControls(feature);
        }
    }

    private static void DrawPlaybackControls(CameraDollyCart feature)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || feature.Track == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Play", "Starts playback from the current track position.")))
            {
                feature.Play();
            }

            if (GUILayout.Button(new GUIContent("Pause", "Pauses the cart at its current position.")))
            {
                feature.Pause();
            }

            if (GUILayout.Button(new GUIContent("Stop", "Stops the cart and returns it to the starting position.")))
            {
                feature.Stop();
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Current Position", feature.NormalizedPosition.ToString("0.000"));
        }
        else
        {
            EditorGUILayout.HelpBox("Playback Controls are available in Play Mode.", MessageType.None);
        }
    }

    private static void CreateDollyTrack(DirectorsCamera controller, CameraDollyCart feature)
    {
        GameObject trackObject = new GameObject("Dolly Track");
        Undo.RegisterCreatedObjectUndo(trackObject, "Create Dolly Track");
        CameraDollyTrack track = Undo.AddComponent<CameraDollyTrack>(trackObject);

        GameObject firstPointObject = new GameObject("Dolly Point 1");
        Undo.RegisterCreatedObjectUndo(firstPointObject, "Create Dolly Point");
        Undo.SetTransformParent(firstPointObject.transform, trackObject.transform, "Parent Dolly Point");
        firstPointObject.transform.position = controller.transform.position;

        GameObject secondPointObject = new GameObject("Dolly Point 2");
        Undo.RegisterCreatedObjectUndo(secondPointObject, "Create Dolly Point");
        Undo.SetTransformParent(secondPointObject.transform, trackObject.transform, "Parent Dolly Point");
        secondPointObject.transform.position = controller.transform.position + controller.transform.right * 5f;

        Undo.RecordObject(track, "Assign Dolly Points");
        track.SetControlPoints(new[] { firstPointObject.transform, secondPointObject.transform });
        EditorUtility.SetDirty(track);

        Undo.RecordObject(feature, "Assign Dolly Track");
        feature.SetTrack(track);
        PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
        EditorUtility.SetDirty(feature);

        Selection.activeGameObject = trackObject;
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        ReleaseEditor(ref dollyEditor);
    }

    private static void ReleaseEditor(ref Editor cachedEditor)
    {
        if (cachedEditor != null)
        {
            DestroyImmediate(cachedEditor);
            cachedEditor = null;
        }
    }
}
