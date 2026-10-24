using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Installs the shot blur pass on the camera's existing renderer without replacing pipeline settings.</summary>
[InitializeOnLoad]
public static class CameraShotBlurEditorSetup
{
    private const string ShaderPath = "Assets/Shaders/CameraShotBlur.shader";
    private const string UndoLabel = "Set Up Camera Shot Blur";
    private static readonly List<ScriptableRendererData> touchedRenderers = new List<ScriptableRendererData>();
    private static bool repairingUndo;

    static CameraShotBlurEditorSetup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Undo.undoRedoPerformed += RepairUndoRedoFeatures;
    }

    [MenuItem("Tools/Camera/Set Up Shot Blur")]
    public static void InstallForCurrentRenderer()
    {
        EnsureInstalled(null);
    }

    /// <summary>Adds or repairs this feature only. Existing renderer features and camera settings are preserved.</summary>
    public static bool EnsureInstalled(Camera camera, bool recordUndo = true)
    {
        bool success = TryEnsureInstalled(camera, out string error, recordUndo);
        if (!success)
        {
            Debug.LogWarning(error, camera);
        }
        return success;
    }

    /// <summary>Reports a setup error to the Inspector without repeatedly logging it.</summary>
    public static bool TryEnsureInstalled(Camera camera, out string error, bool recordUndo = true)
    {
        error = null;
        ScriptableRendererData renderer = ResolveRenderer(camera);
        if (renderer == null)
        {
            error = "Shot Blur requires an active Universal Render Pipeline renderer. Cut, Gentle and Custom remain available.";
            return false;
        }
        if (!EditorUtility.IsPersistent(renderer) || !AssetDatabase.GetAssetPath(renderer).StartsWith("Assets/", System.StringComparison.Ordinal))
        {
            error = "Shot Blur requires a renderer asset in this project's Assets folder.";
            return false;
        }
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            error = "Shot Blur shader is missing: " + ShaderPath;
            return false;
        }
        if (!touchedRenderers.Contains(renderer))
        {
            touchedRenderers.Add(renderer);
        }

        CameraShotBlurFeature feature = null;
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            if (renderer.rendererFeatures[i] is CameraShotBlurFeature existing)
            {
                feature = existing;
                break;
            }
        }
        bool created = feature == null;
        if (!created && feature.isActive && feature.BlurShader != null &&
            TryGetFeatureLocalId(feature, out long featureLocalId) && FeatureMapMatches(renderer, feature, featureLocalId))
        {
            return true;
        }

        int undoGroup = -1;
        if (recordUndo)
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);
            Undo.RecordObject(renderer, UndoLabel);
        }
        if (created)
        {
            feature = ScriptableObject.CreateInstance<CameraShotBlurFeature>();
            feature.name = "Camera Shot Blur";
            if (recordUndo)
            {
                Undo.RegisterCreatedObjectUndo(feature, UndoLabel);
            }
            AssetDatabase.AddObjectToAsset(feature, renderer);

            // Keep URP's persistent local-ID map aligned with its feature list, as its native Inspector does.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            SerializedObject rendererData = new SerializedObject(renderer);
            SerializedProperty features = rendererData.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = rendererData.FindProperty("m_RendererFeatureMap");
            int index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            featureMap.arraySize = index + 1;
            featureMap.GetArrayElementAtIndex(index).longValue = localId;
            if (recordUndo)
            {
                rendererData.ApplyModifiedProperties();
            }
            else
            {
                rendererData.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else
        {
            if (recordUndo)
            {
                Undo.RecordObject(feature, UndoLabel);
            }
            // Unity can recreate an undone sub-asset as a transient object on Redo.
            // A surviving list reference alone does not make that object persist in the renderer asset.
            if (!TryGetFeatureLocalId(feature, out _))
            {
                AssetDatabase.AddObjectToAsset(feature, renderer);
            }
            SynchronizeFeatureMap(renderer, recordUndo);
        }

        SerializedObject featureData = new SerializedObject(feature);
        SerializedProperty shaderProperty = featureData.FindProperty("blurShader");
        if (shaderProperty.objectReferenceValue == null)
        {
            shaderProperty.objectReferenceValue = shader;
        }
        featureData.FindProperty("m_Active").boolValue = true;
        if (recordUndo)
        {
            featureData.ApplyModifiedProperties();
        }
        else
        {
            featureData.ApplyModifiedPropertiesWithoutUndo();
        }
        feature.Create();
        renderer.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssetIfDirty(renderer);
        if (recordUndo)
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
        return true;
    }

    private static bool TryGetFeatureLocalId(ScriptableRendererFeature feature, out long localId)
    {
        localId = 0;
        return feature != null && EditorUtility.IsPersistent(feature) &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out localId) && localId != 0;
    }

    private static bool FeatureMapMatches(ScriptableRendererData renderer, CameraShotBlurFeature feature, long localId)
    {
        SerializedProperty featureMap = new SerializedObject(renderer).FindProperty("m_RendererFeatureMap");
        if (featureMap.arraySize != renderer.rendererFeatures.Count)
        {
            return false;
        }
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            if (renderer.rendererFeatures[i] == feature)
            {
                return featureMap.GetArrayElementAtIndex(i).longValue == localId;
            }
        }
        return false;
    }

    private static bool SynchronizeFeatureMap(ScriptableRendererData renderer, bool recordUndo)
    {
        SerializedObject rendererData = new SerializedObject(renderer);
        SerializedProperty featureMap = rendererData.FindProperty("m_RendererFeatureMap");
        bool changed = featureMap.arraySize != renderer.rendererFeatures.Count;
        featureMap.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            if (TryGetFeatureLocalId(renderer.rendererFeatures[i], out long localId))
            {
                SerializedProperty entry = featureMap.GetArrayElementAtIndex(i);
                if (entry.longValue != localId)
                {
                    entry.longValue = localId;
                    changed = true;
                }
            }
        }
        if (!changed)
        {
            return false;
        }
        if (recordUndo)
        {
            rendererData.ApplyModifiedProperties();
        }
        else
        {
            rendererData.ApplyModifiedPropertiesWithoutUndo();
        }
        return true;
    }

    private static void RepairUndoRedoFeatures()
    {
        if (repairingUndo)
        {
            return;
        }
        repairingUndo = true;
        try
        {
            for (int rendererIndex = touchedRenderers.Count - 1; rendererIndex >= 0; rendererIndex--)
            {
                ScriptableRendererData renderer = touchedRenderers[rendererIndex];
                if (renderer == null)
                {
                    touchedRenderers.RemoveAt(rendererIndex);
                    continue;
                }
                bool hasBlurFeature = false;
                bool changed = false;
                for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                {
                    if (!(renderer.rendererFeatures[i] is CameraShotBlurFeature feature) || feature == null)
                    {
                        continue;
                    }
                    hasBlurFeature = true;
                    if (!TryGetFeatureLocalId(feature, out _))
                    {
                        AssetDatabase.AddObjectToAsset(feature, renderer);
                        EditorUtility.SetDirty(feature);
                        changed = true;
                    }
                }
                // Undoing installation must leave the feature absent. Never create or enable one here.
                if (!hasBlurFeature)
                {
                    continue;
                }
                changed |= SynchronizeFeatureMap(renderer, false);
                if (changed)
                {
                    renderer.SetDirty();
                    EditorUtility.SetDirty(renderer);
                    AssetDatabase.SaveAssetIfDirty(renderer);
                }
            }
        }
        finally
        {
            repairingUndo = false;
        }
    }

    private static ScriptableRendererData ResolveRenderer(Camera camera)
    {
        RenderPipelineAsset pipeline = QualitySettings.renderPipeline;
        if (pipeline == null)
        {
            pipeline = GraphicsSettings.defaultRenderPipeline;
        }
        if (!(pipeline is UniversalRenderPipelineAsset universal))
        {
            return null;
        }
        SerializedObject pipelineData = new SerializedObject(universal);
        SerializedProperty renderers = pipelineData.FindProperty("m_RendererDataList");
        int defaultIndex = pipelineData.FindProperty("m_DefaultRendererIndex").intValue;
        int selectedIndex = defaultIndex;
        if (camera != null && camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            int cameraIndex = new SerializedObject(cameraData).FindProperty("m_RendererIndex").intValue;
            if (cameraIndex >= 0)
            {
                selectedIndex = cameraIndex;
            }
        }
        if (selectedIndex < 0 || selectedIndex >= renderers.arraySize ||
            renderers.GetArrayElementAtIndex(selectedIndex).objectReferenceValue == null)
        {
            selectedIndex = defaultIndex;
        }
        return selectedIndex >= 0 && selectedIndex < renderers.arraySize
            ? renderers.GetArrayElementAtIndex(selectedIndex).objectReferenceValue as ScriptableRendererData
            : null;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }
        // This discovery happens once before play, never in the runtime update or rendering loops.
        CameraShotDirector[] directors = Object.FindObjectsByType<CameraShotDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < directors.Length; i++)
        {
            CameraShotDirector director = directors[i];
            if (director.enabled && director.gameObject.scene.IsValid() && director.Transition == CameraShotTransition.Blur)
            {
                EnsureInstalled(director.GetComponent<Camera>());
            }
        }
    }
}
