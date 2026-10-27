using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class C01ManualPreview
{
    private const string Pending = "KFB.C01.ManualPreview.Pending";
    private const string PreviousSkip = "KFB.C01.ManualPreview.Skip";
    private const string PreviousStage = "KFB.C01.ManualPreview.Stage";
    private const string Path = "Assets/Editor/C01ManualPreview.cs";

    static C01ManualPreview() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    internal static void Prepare()
    {
        var intro = Object.FindAnyObjectByType<IntroSequence>();
        if (intro == null) throw new System.InvalidOperationException("IntroSequence not found");
        SessionState.SetBool(PreviousSkip, intro.useDebugSkip);
        SessionState.SetInt(PreviousStage, (int)intro.startStage);
        SessionState.SetBool(Pending, true);
        intro.useDebugSkip = true;
        intro.startStage = DebugStoryStage.StartBoxQuest;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
        var intro = Object.FindAnyObjectByType<IntroSequence>();
        if (intro != null)
        {
            intro.useDebugSkip = SessionState.GetBool(PreviousSkip, false);
            intro.startStage = (DebugStoryStage)SessionState.GetInt(PreviousStage, 0);
        }
        SessionState.SetBool(Pending, false);
        EditorApplication.delayCall += () => AssetDatabase.DeleteAsset(Path);
    }
}
