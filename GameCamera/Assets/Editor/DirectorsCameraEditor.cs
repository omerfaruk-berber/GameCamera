using UnityEditor;
using UnityEngine;

/// <summary>Director's Camera için arka plan ayarlarını ve özellik alanını gösterir.</summary>
[CustomEditor(typeof(DirectorsCamera))]
public class DirectorsCameraEditor : Editor
{
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

        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Button(new GUIContent("Add Feature", "Bu kamera için henüz eklenebilir bir özellik bulunmuyor."));
        }

        EditorGUILayout.HelpBox("No features available yet.", MessageType.Info);
    }

    private void DrawBackgroundSettings()
    {
        DirectorsCamera controller = (DirectorsCamera)target;
        Camera attachedCamera = controller.ControlledCamera;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Background Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Color backgroundColor = EditorGUILayout.ColorField(
            new GUIContent("Background Color", "Kameranın arka plan rengi. Değiştirildiğinde düz renk arka plan modu açılır."),
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
}
