using System;
using UnityEditor;
using UnityEngine;
using KnocksFromBeneath;

[CustomPropertyDrawer(typeof(DebugStoryStage))]
public sealed class DebugStoryStageDrawer : PropertyDrawer
{
    private static readonly DebugStoryStage[] StoryOrder =
    {
        DebugStoryStage.None,
        DebugStoryStage.StartTrashQuest,
        DebugStoryStage.StartTrashDeliveryQuest,
        DebugStoryStage.StartBoxQuest,
        DebugStoryStage.StartPieQuest,
        DebugStoryStage.StartPieMicrowaveQuest,
        DebugStoryStage.StartPieEatQuest,
        DebugStoryStage.StartGoToBed,
        DebugStoryStage.StartNightOne,
        DebugStoryStage.StartDay2Decoration,
        DebugStoryStage.StartDay2Dinner,
        DebugStoryStage.StartDinnerCook,
        DebugStoryStage.StartDinnerEat,
        DebugStoryStage.StartDay2Evening,
        DebugStoryStage.StartNight,
        DebugStoryStage.StartKitchenNoise,
        DebugStoryStage.StartFlashlightQuest,
        DebugStoryStage.StartCrowbarQuest,
        DebugStoryStage.StartBreakFloor,
        DebugStoryStage.StartLookInHole,
        DebugStoryStage.StartBranchEndings,
        DebugStoryStage.StartHammerQuest,
        DebugStoryStage.StartFinale
    };

    private static readonly string[] Labels =
    {
        "None",
        "Start Trash Quest",
        "Start Trash Delivery",
        "Start Box Quest",
        "Start Pie Quest",
        "Start Pie Microwave Quest",
        "Start Eat Pie Quest",
        "Start Go To Bed Quest",
        "Start Night 1",
        "Start Day 2 Kitchen Cleanup",
        "Start Day 2 Dinner Ingredients",
        "Start Day 2 Cook Dinner",
        "Start Day 2 Eat Dinner",
        "Start Day 2 Evening",
        "Start Night 2",
        "Start Kitchen Noise Quest",
        "Start Flashlight Quest",
        "Start Crowbar Quest",
        "Start Break Floor Quest",
        "Start Look In Hole Quest",
        "Start Branch Endings",
        "Start Hammer Quest",
        "Start Finale"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        int selectedIndex = Array.IndexOf(StoryOrder, (DebugStoryStage)property.intValue);
        if (selectedIndex < 0) selectedIndex = 0;

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, Labels);
        if (EditorGUI.EndChangeCheck())
            property.intValue = (int)StoryOrder[selectedIndex];
        EditorGUI.EndProperty();
    }
}
