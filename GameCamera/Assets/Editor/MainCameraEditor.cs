using UnityEditor;
using UnityEngine;

/// <summary>MainCamera Inspector'ından ayrı kamera özelliklerini eklemeyi ve düzenlemeyi sağlar.</summary>
[CustomEditor(typeof(MainCamera))]
public class MainCameraEditor : Editor
{
    private enum FeatureMode
    {
        Any,
        Mode2D,
        Mode3D
    }

    private SerializedProperty cameraModeProperty;
    private SerializedProperty targetProperty;
    private SerializedProperty offset2DProperty;
    private SerializedProperty offset3DProperty;
    private SerializedProperty smoothTimeProperty;
    private SerializedProperty snapOnStartProperty;
    private Editor expansionEditor;
    private Editor lookAtEditor;
    private Editor zonesEditor;
    private Editor groupEditor;
    private Editor pixelMoveEditor;
    private bool expansionExpanded = true;
    private bool lookAtExpanded = true;
    private bool zonesExpanded = true;
    private bool groupExpanded = true;
    private bool pixelMoveExpanded = true;

    private void OnEnable()
    {
        cameraModeProperty = serializedObject.FindProperty("cameraMode");
        targetProperty = serializedObject.FindProperty("target");
        offset2DProperty = serializedObject.FindProperty("offset");
        offset3DProperty = serializedObject.FindProperty("offset3D");
        smoothTimeProperty = serializedObject.FindProperty("smoothTime");
        snapOnStartProperty = serializedObject.FindProperty("snapOnStart");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cameraModeProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Follow Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetProperty);
        bool is2D = cameraModeProperty.enumValueIndex == (int)GameCameraMode.Mode2D;
        EditorGUILayout.PropertyField(
            is2D ? offset2DProperty : offset3DProperty,
            new GUIContent("Offset"));
        EditorGUILayout.PropertyField(smoothTimeProperty);
        EditorGUILayout.PropertyField(snapOnStartProperty);

        bool settingsChanged = EditorGUI.EndChangeCheck();
        bool settingsApplied = serializedObject.ApplyModifiedProperties();
        if (settingsChanged && settingsApplied)
        {
            SceneView.RepaintAll();
        }

        DrawBackgroundSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Features", EditorStyles.boldLabel);

        if (GUILayout.Button(new GUIContent("Add Feature", "Add an optional behavior to this camera.")))
        {
            ShowAddFeatureMenu();
        }

        DrawFeature<CameraViewExpansion2D>("View Expansion", FeatureMode.Mode2D, ref expansionExpanded, ref expansionEditor);
        DrawFeature<CameraLookAtTarget2D>("Look At Target", FeatureMode.Mode3D, ref lookAtExpanded, ref lookAtEditor);
        DrawFeature<CameraDeadSoftZone2D>("Dead Zone & Soft Zone", FeatureMode.Mode2D, ref zonesExpanded, ref zonesEditor);
        DrawFeature<CameraTargetGroup2D>("Target Group Camera", FeatureMode.Mode2D, ref groupExpanded, ref groupEditor);
        DrawFeature<CameraPixelMove2D>("Pixel Move", FeatureMode.Mode2D, ref pixelMoveExpanded, ref pixelMoveEditor);
    }

    private void DrawBackgroundSettings()
    {
        MainCamera controller = (MainCamera)target;
        Camera attachedCamera = controller.GetComponent<Camera>();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Background Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Color backgroundColor = EditorGUILayout.ColorField(
            new GUIContent("Background Color", "Camera background color. Changing it enables Solid Color clearing."),
            controller.BackgroundColor);

        if (EditorGUI.EndChangeCheck())
        {
            // Renk Camera üzerinde saklanır; Undo ve prefab kaydı gerçek bileşene uygulanır.
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
        AddFeatureMenuItem<CameraViewExpansion2D>(menu, "View Expansion", "Expands the view left, right, or on all sides.", FeatureMode.Mode2D);
        AddFeatureMenuItem<CameraLookAtTarget2D>(menu, "Look At Target", "Rotates the camera toward a target.", FeatureMode.Mode3D);
        AddFeatureMenuItem<CameraDeadSoftZone2D>(menu, "Dead Zone & Soft Zone", "Adds free-movement and smooth-follow regions.", FeatureMode.Mode2D);
        AddFeatureMenuItem<CameraTargetGroup2D>(menu, "Target Group Camera", "Keeps multiple targets in the same frame.", FeatureMode.Mode2D);
        AddFeatureMenuItem<CameraPixelMove2D>(menu, "Pixel Move", "Snaps 2D camera movement to the pixel grid.", FeatureMode.Mode2D);
        menu.ShowAsContext();
    }

    private void AddFeatureMenuItem<T>(GenericMenu menu, string title, string description, FeatureMode requiredMode)
        where T : MonoBehaviour
    {
        MainCamera controller = (MainCamera)target;
        if (controller.GetComponent<T>() != null)
        {
            menu.AddDisabledItem(new GUIContent(title + " (Added)", description), true);
            return;
        }

        if (!IsCompatible(controller, requiredMode))
        {
            string modeLabel = requiredMode == FeatureMode.Mode2D ? " (2D Only)" : " (3D Only)";
            menu.AddDisabledItem(new GUIContent(title + modeLabel, description));
            return;
        }

        menu.AddItem(new GUIContent(title, description), false, () =>
        {
            // Menü açıkken seçim veya nesne durumu değişmiş olabilir; tekrar kontrol et.
            if (controller == null || controller.GetComponent<T>() != null)
            {
                return;
            }

            // Undo üzerinden ekle; sahne/prefab kaydı ve geri alma Unity tarafından yönetilir.
            Undo.AddComponent<T>(controller.gameObject);
            Repaint();
            SceneView.RepaintAll();
        });
    }

    private void DrawFeature<T>(string title, FeatureMode requiredMode, ref bool expanded, ref Editor cachedEditor)
        where T : MonoBehaviour
    {
        T feature = ((MainCamera)target).GetComponent<T>();
        if (feature == null)
        {
            ReleaseEditor(ref cachedEditor);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool compatible = IsCompatible((MainCamera)target, requiredMode);
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, title, true);

                using (new EditorGUI.DisabledScope(!compatible))
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = GUILayout.Toggle(feature.enabled, new GUIContent("Enabled", "Enable or disable this feature."));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(feature, "Toggle Camera Feature");
                        feature.enabled = enabled;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
                        EditorUtility.SetDirty(feature);
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button(new GUIContent("Remove", "Remove this feature. Use Ctrl+Z to undo."), GUILayout.Width(65f)))
                {
                    ReleaseEditor(ref cachedEditor);
                    Undo.DestroyObjectImmediate(feature);
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                }
            }

            if (expanded)
            {
                if (!compatible)
                {
                    string requiredModeName = requiredMode == FeatureMode.Mode2D ? "2D" : "3D";
                    EditorGUILayout.HelpBox(
                        $"This feature is available only in {requiredModeName} mode and is currently inactive.",
                        MessageType.Info);
                }

                // Aynı bileşenin verilerini düzenle; ayrı bir ayar kopyası oluşturma.
                using (new EditorGUI.DisabledScope(!compatible))
                {
                    CreateCachedEditor(feature, null, ref cachedEditor);
                    cachedEditor.serializedObject.Update();
                    DrawPropertiesExcluding(cachedEditor.serializedObject, "m_Script");
                    if (cachedEditor.serializedObject.ApplyModifiedProperties())
                    {
                        SceneView.RepaintAll();
                    }
                }
            }
        }
    }

    private static bool IsCompatible(MainCamera controller, FeatureMode requiredMode)
    {
        return requiredMode == FeatureMode.Any ||
               (requiredMode == FeatureMode.Mode2D && controller.Is2D) ||
               (requiredMode == FeatureMode.Mode3D && !controller.Is2D);
    }

    private void OnDisable()
    {
        ReleaseEditor(ref expansionEditor);
        ReleaseEditor(ref lookAtEditor);
        ReleaseEditor(ref zonesEditor);
        ReleaseEditor(ref groupEditor);
        ReleaseEditor(ref pixelMoveEditor);
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
