#if UNITY_EDITOR
using System;
using System.Linq;
using KnocksFromBeneath;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioPolishInstaller
{
    private const string Folder = "Assets/Audio/ClapClap/";
    private static AudioMixerGroup sfx;
    private static AudioMixerGroup ambience;

    public static string Apply(bool resume = false)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main Scene/Main.unity" || (scene.isDirty && !resume))
            throw new InvalidOperationException("Main must be open with no unsaved edits.");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/MainMixer.mixer");
        sfx = mixer.FindMatchingGroups("").First(g => g.name == "SFX");
        ambience = mixer.FindMatchingGroups("").First(g => g.name == "Ambience");
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder.TrimEnd('/') }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var settings = importer.defaultSampleSettings;
            settings.loadType = importer.assetPath.Contains("ambience") || importer.assetPath.Contains("fridge-loop")
                ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .8f;
            settings.preloadAudioData = settings.loadType != AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !importer.assetPath.Contains("ambience");
            importer.SaveAndReimport();
        }

        int items = 0;
        foreach (var bob in Find<HeadBobbing>())
        {
            Undo.RecordObject(bob, "Improve footsteps");
            bob.woodSteps = Steps("Wood"); bob.concreteSteps = Steps("Tile");
            bob.stairSteps = Steps("Wood");
            if (bob.metalSteps == null || bob.metalSteps.Length == 0) bob.metalSteps = Steps("Rock");
            if (bob.footstepSource == null) bob.footstepSource = Source(bob.transform, "Footstep Audio", false, .5f);
            else { Undo.RecordObject(bob.footstepSource, "Balance footsteps"); bob.footstepSource.volume = .5f; }
            EditorUtility.SetDirty(bob);
        }
        foreach (var door in Find<Door>())
        {
            var feedback = Source(door.transform, "Door Feedback Audio", true, .65f);
            Set(door, "feedbackSource", feedback);
            Set(door, "lockedRattleClip", Clip("locked_door"));
        }
        foreach (var light in Find<LightSwitch>())
        {
            if (light.switchSound == null) continue;
            Undo.RecordObject(light.switchSound, "Improve switch click");
            light.switchSound.clip = Clip("toggle_on"); light.switchSound.volume = .35f;
            light.switchSound.playOnAwake = false;
        }
        foreach (var inventory in Find<PlayerInventory>())
        {
            Undo.RecordObject(inventory, "Improve flashlight clicks");
            inventory.soundOn = Clip("toggle_on"); inventory.soundOff = Clip("toggle_off");
            EditorUtility.SetDirty(inventory);
        }
        foreach (var latch in Find<FrontDoorLockInteractable>())
        {
            Set(latch, "lockAudioSource", Source(latch.transform, "Latch Audio", true, .5f));
            Set(latch, "lockSfx", Clip("unlock_door"));
        }
        foreach (var latch in Find<BasementHatchLock>())
        {
            Set(latch, "audioSource", Source(latch.transform, "Padlock Audio", true, .6f));
            Set(latch, "rattleSfx", Clip("locked_door"));
            Set(latch, "prySfx", Clip("table-smash-47690"));
        }
        foreach (var floor in Find<FloorLogic>())
        {
            Set(floor, "audioSource", Source(floor.transform, "Board Audio", true, .65f));
            Set(floor, "breakClip", Clip("table-smash-47690"));
        }
        var candidates = Find<PickableItem>().Select(i => i.gameObject)
            .Concat(Find<CollectableItem>().Select(i => i.gameObject))
            .Concat(Find<PieQuestItem>().Select(i => i.gameObject)).Distinct();
        foreach (var item in candidates)
        {
            var collectable = item.GetComponent<CollectableItem>();
            bool trash = collectable != null && collectable.currentItemType == CollectableItem.ItemType.Trash;
            bool box = collectable != null && collectable.currentItemType == CollectableItem.ItemType.Box;
            bool food = item.GetComponent<PieQuestItem>() != null;
            string grab = trash ? "trash-grab" : box ? "book-grab" : food ? "grab-meat" : "grab-bowl";
            string drop = trash ? "trash-impact" : box ? "book-drop-01" : food ? "drop-meat" : "drop-bowl";
            var foley = item.GetComponent<ItemFoley>();
            if (foley == null) foley = Undo.AddComponent<ItemFoley>(item);
            Set(foley, "source", Source(item.transform, "Item Foley Audio", true, .7f));
            Set(foley, "grabClip", Clip(grab)); Set(foley, "placeClip", Clip(drop));
            Set(foley, "throwClip", Clip("throw"));
            var impact = item.GetComponent<ImpactSounds>();
            if (impact == null && item.GetComponent<Rigidbody>() != null) impact = Undo.AddComponent<ImpactSounds>(item);
            if (impact != null)
            {
                Undo.RecordObject(impact, "Improve item impact");
                impact.impactSource = Source(item.transform, "Item Impact Audio", true, .65f);
                impact.clips = box ? new[] { Clip("book-drop-01"), Clip("book-drop-02"), Clip("book-drop-03") }
                    : new[] { Clip(drop) };
                EditorUtility.SetDirty(impact);
            }
            items++;
        }
        foreach (var stove in Find<StoveInteractable>())
        {
            Undo.RecordObject(stove, "Add stove feedback");
            stove.stoveAudioSource = Source(stove.transform, "Stove Audio", true, .4f);
            stove.cookStartSfx = Clip("toggle_on"); stove.cookDoneSfx = Clip("drop-bowl");
        }
        var fridge = Find<Transform>().FirstOrDefault(t => t.name == "Refrigerator.001");
        if (fridge != null)
        {
            var source = Source(fridge, "Fridge Hum Audio", true, .08f);
            source.outputAudioMixerGroup = ambience;
            source.clip = Clip("fridge-loop"); source.loop = true; source.playOnAwake = true;
            source.maxDistance = 7f;
        }
        foreach (var rain in Find<AudioSource>().Where(a => a.name == "Rain"))
        { Undo.RecordObject(rain, "Balance rain"); rain.volume = .3f; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"Configured {items} physical items, {Find<Door>().Length} doors; scene saved.";
    }

    private static T[] Find<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.gameObject.scene ==
        UnityEngine.SceneManagement.SceneManager.GetActiveScene()).ToArray();

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav")
        ?? throw new InvalidOperationException("Missing sound: " + name);

    private static AudioClip[] Steps(string surface) => Enumerable.Range(1, 4)
        .Select(i => Clip($"Footsteps_{surface}_Walk_{i:00}")).ToArray();

    private static AudioSource Source(Transform parent, string name, bool spatial, float volume)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add audio emitter");
            child = go.transform; child.SetParent(parent, false);
        }
        var source = child.GetComponent<AudioSource>();
        if (source == null) source = Undo.AddComponent<AudioSource>(child.gameObject);
        Undo.RecordObject(source, "Configure emitter");
        source.playOnAwake = false; source.spatialBlend = spatial ? 1f : 0f;
        source.volume = volume; source.minDistance = 1f; source.maxDistance = 12f;
        source.rolloffMode = AudioRolloffMode.Logarithmic; source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = sfx;
        return source;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        Undo.RecordObject(target, "Assign audio");
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(field);
        property.objectReferenceValue = value; serialized.ApplyModifiedProperties();
    }
}
#endif
