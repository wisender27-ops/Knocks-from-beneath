using RetroPSX;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RetroPSXCameraSettings))]
public sealed class RetroPSXCameraSettingsEditor : Editor
{
    private bool rasterExpanded = true;
    private bool geometryExpanded;
    private bool colorExpanded = true;
    private bool lightingExpanded;
    private bool fogExpanded;
    private bool volumetricsExpanded;
    private bool displayExpanded = true;
    private bool uiExpanded;
    private bool debugExpanded;

    private UnityEditor.Editor rasterEditor;
    private UnityEditor.Editor geometryEditor;
    private UnityEditor.Editor colorEditor;
    private UnityEditor.Editor lightingEditor;
    private UnityEditor.Editor fogEditor;
    private UnityEditor.Editor volumetricsEditor;
    private UnityEditor.Editor displayEditor;
    private UnityEditor.Editor uiEditor;
    private UnityEditor.Editor debugEditor;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
        serializedObject.ApplyModifiedProperties();

        RetroPSXPipelineProfile profile = ((RetroPSXCameraSettings)target).Profile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox("Assign the RetroPSX Pipeline profile.", MessageType.Warning);
            return;
        }

        DrawRootSettings(profile);

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("Apply Reference PSX Look"))
            ApplyReferenceLook(profile);

        EditorGUILayout.Space(6f);
        EditorGUI.BeginChangeCheck();
        DrawProfile("Raster", profile.Raster, ref rasterExpanded, ref rasterEditor);
        DrawProfile("Geometry", profile.Geometry, ref geometryExpanded, ref geometryEditor);
        DrawProfile("Color", profile.Color, ref colorExpanded, ref colorEditor);
        DrawProfile("Lighting", profile.Lighting, ref lightingExpanded, ref lightingEditor);
        DrawProfile("Fog", profile.Fog, ref fogExpanded, ref fogEditor);
        DrawProfile("Volumetrics", profile.Volumetrics, ref volumetricsExpanded, ref volumetricsEditor);
        DrawProfile("Display / CRT", profile.Display, ref displayExpanded, ref displayEditor);
        DrawProfile("UI", profile.UI, ref uiExpanded, ref uiEditor);
        DrawProfile("Debug", profile.Debug, ref debugExpanded, ref debugEditor);

        if (EditorGUI.EndChangeCheck())
            RepaintRendering();
    }

    private static void DrawRootSettings(RetroPSXPipelineProfile profile)
    {
        SerializedObject root = new(profile);
        root.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(root.FindProperty("enabled"));
        EditorGUILayout.PropertyField(root.FindProperty("sceneViewPreview"));
        if (EditorGUI.EndChangeCheck())
        {
            root.ApplyModifiedProperties();
            RepaintRendering();
        }
    }

    private static void DrawProfile(
        string title,
        Object profile,
        ref bool expanded,
        ref UnityEditor.Editor cachedEditor)
    {
        expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
        if (expanded)
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox($"{title} profile is not assigned.", MessageType.Warning);
            }
            else
            {
                EditorGUI.indentLevel++;
                UnityEditor.Editor.CreateCachedEditor(profile, null, ref cachedEditor);
                cachedEditor.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void ApplyReferenceLook(RetroPSXPipelineProfile profile)
    {
        Object[] assets =
        {
            profile,
            profile.Raster,
            profile.Color,
            profile.Display
        };
        Undo.RecordObjects(assets, "Apply Reference PSX Look");

        Set(profile, "enabled", true);
        Set(profile, "sceneViewPreview", (int)RetroSceneViewMode.FullPipeline);

        Set(profile.Raster, "mode", (int)RetroRasterMode.InternalHeight);
        Set(profile.Raster, "internalHeight", 240);
        Set(profile.Raster, "presentation", (int)RetroPresentationMode.Stretch);

        Set(profile.Color, "mode", (int)RetroColorMode.RGB555);
        Set(profile.Color, "materialDither", (int)RetroDitherMode.PSX);
        Set(profile.Color, "materialDitherStrength", 1f);
        Set(profile.Color, "quantizeFinalImage", true);
        Set(profile.Color, "finalImageDither", (int)RetroDitherMode.PSX);
        Set(profile.Color, "finalImageDitherStrength", 0.6f);

        Set(profile.Display, "enabled", true);
        Set(profile.Display, "scanlines", 0.16f);
        Set(profile.Display, "maskStrength", 0.14f);
        Set(profile.Display, "maskMode", (int)RetroCRTMaskMode.ApertureGrille);
        Set(profile.Display, "curvature", 0.015f);
        Set(profile.Display, "overscan", 0.01f);
        Set(profile.Display, "vignette", 0.08f);
        Set(profile.Display, "horizontalBleed", 0.12f);
        Set(profile.Display, "chromaBleed", 0.05f);
        Set(profile.Display, "chromaticMisalignment", 0.08f);
        Set(profile.Display, "signalNoise", 0.012f);
        Set(profile.Display, "brightness", 1.04f);
        Set(profile.Display, "interlacing", 0f);
        Set(profile.Display, "pixelBloom", 0.08f);

        foreach (Object asset in assets)
            EditorUtility.SetDirty(asset);

        AssetDatabase.SaveAssets();
        RepaintRendering();
    }

    private static void Set(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, float value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RepaintRendering()
    {
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
