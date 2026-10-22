using UnityEditor;
using UnityEngine;

/// <summary>MainCamera Inspector'ından ayrı kamera özelliklerini eklemeyi ve düzenlemeyi sağlar.</summary>
[CustomEditor(typeof(MainCamera))]
public class MainCameraEditor : Editor
{
    private Editor expansionEditor;
    private Editor lookAtEditor;
    private Editor zonesEditor;
    private Editor groupEditor;
    private bool expansionExpanded = true;
    private bool lookAtExpanded = true;
    private bool zonesExpanded = true;
    private bool groupExpanded = true;

    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector())
        {
            SceneView.RepaintAll();
        }

        DrawBackgroundSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Features", EditorStyles.boldLabel);

        if (GUILayout.Button(new GUIContent("Add Feature", "Bu kameraya isteğe bağlı bir davranış ekle.")))
        {
            ShowAddFeatureMenu();
        }

        DrawFeature<CameraViewExpansion2D>("View Expansion", ref expansionExpanded, ref expansionEditor);
        DrawFeature<CameraLookAtTarget2D>("Look At Target", ref lookAtExpanded, ref lookAtEditor);
        DrawFeature<CameraDeadSoftZone2D>("Dead Zone & Soft Zone", ref zonesExpanded, ref zonesEditor);
        DrawFeature<CameraTargetGroup2D>("Target Group Camera", ref groupExpanded, ref groupEditor);
    }

    private void DrawBackgroundSettings()
    {
        MainCamera controller = (MainCamera)target;
        Camera attachedCamera = controller.GetComponent<Camera>();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Background Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Color backgroundColor = EditorGUILayout.ColorField(
            new GUIContent("Background Color", "Kameranın arka plan rengi. Değiştirildiğinde düz renk arka plan modu açılır."),
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
        AddFeatureMenuItem<CameraViewExpansion2D>(menu, "View Expansion", "Sağa, sola veya her yöne görüş genişletme.");
        AddFeatureMenuItem<CameraLookAtTarget2D>(menu, "Look At Target", "Kamerayı bakış hedefine döndürme.");
        AddFeatureMenuItem<CameraDeadSoftZone2D>(menu, "Dead Zone & Soft Zone", "Serbest hareket ve yumuşak takip bölgeleri.");
        AddFeatureMenuItem<CameraTargetGroup2D>(menu, "Target Group Camera", "Birden fazla hedefi aynı kadrajda tutma.");
        menu.ShowAsContext();
    }

    private void AddFeatureMenuItem<T>(GenericMenu menu, string title, string description) where T : MonoBehaviour
    {
        MainCamera controller = (MainCamera)target;
        if (controller.GetComponent<T>() != null)
        {
            menu.AddDisabledItem(new GUIContent(title + " (Added)", description), true);
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

    private void DrawFeature<T>(string title, ref bool expanded, ref Editor cachedEditor) where T : MonoBehaviour
    {
        T feature = ((MainCamera)target).GetComponent<T>();
        if (feature == null)
        {
            ReleaseEditor(ref cachedEditor);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, title, true);

                EditorGUI.BeginChangeCheck();
                bool enabled = GUILayout.Toggle(feature.enabled, new GUIContent("Enabled", "Özelliği aç veya kapat."));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(feature, "Toggle Camera Feature");
                    feature.enabled = enabled;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
                    EditorUtility.SetDirty(feature);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button(new GUIContent("Remove", "Özelliği kaldır. Ctrl+Z ile geri alabilirsin."), GUILayout.Width(65f)))
                {
                    ReleaseEditor(ref cachedEditor);
                    Undo.DestroyObjectImmediate(feature);
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                }
            }

            if (expanded)
            {
                // Aynı bileşenin verilerini düzenle; ayrı bir ayar kopyası oluşturma.
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

    private void OnDisable()
    {
        ReleaseEditor(ref expansionEditor);
        ReleaseEditor(ref lookAtEditor);
        ReleaseEditor(ref zonesEditor);
        ReleaseEditor(ref groupEditor);
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
