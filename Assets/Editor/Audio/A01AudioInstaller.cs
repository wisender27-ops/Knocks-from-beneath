#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// A-01: заполняет Resources/Audio/GameSounds и Resources/Audio/AudioRouting из Assets/Audio/Game.
// Повторный запуск безопасен: ассеты обновляются на месте (GUID сохраняются). Сцены не трогает.
public static class A01AudioInstaller
{
    const string Game = "Assets/Audio/Game/";
    const string LibraryPath = "Assets/Resources/Audio/GameSounds.asset";
    const string RoutingPath = "Assets/Resources/Audio/AudioRouting.asset";

    public static string InstallAssets()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        var lib = LoadOrCreate<GameSoundLibrary>(LibraryPath, log);
        Undo.RecordObject(lib, "A-01 sound library");
        lib.ui = System.Enum.GetNames(typeof(UiSound)).Select(n => Clip("UI/" + n)).ToArray();
        lib.thoughtTicks = Clips("UI/ThoughtTick");
        lib.dayOutside = Clip("Ambience/DayOutside"); lib.nightOutside = Clip("Ambience/NightOutside");
        lib.rain = Clip("Ambience/Rain"); lib.basement = Clip("Ambience/Basement"); lib.roomTone = Clip("Ambience/RoomTone");
        lib.fridgeHum = Clip("Ambience/FridgeHum"); lib.lampBuzz = Clip("Ambience/LampBuzz");
        lib.droneMenu = Clip("Music/DroneMenu"); lib.droneNight1 = Clip("Music/DroneNight1");
        lib.droneNight2 = Clip("Music/DroneNight2"); lib.droneBasement = Clip("Music/DroneBasement");
        lib.houseCreaks = Clips("World/House/Creak");
        lib.thunder = Clips("Ambience/Thunder");
        lib.distantDogs = Clips("Ambience/Dog");
        lib.heartbeat = Clip("Horror/Heartbeat"); lib.tinnitus = Clip("Horror/Tinnitus"); lib.riser = Clip("Horror/Riser");
        lib.subHits = Clips("Horror/SubHit"); lib.darkHits = Clips("Horror/DarkHit");
        lib.breathScared = Clips("Horror/Breath");
        lib.stepWood = Clips("Foley/Footsteps/Wood"); lib.stepConcrete = Clips("Foley/Footsteps/Concrete");
        lib.stepGrass = Clips("Foley/Footsteps/Grass"); lib.stepCarpet = Clips("Foley/Footsteps/Carpet");
        lib.stepStairs = Clips("Foley/Footsteps/Stairs"); lib.stepDirt = Clips("Foley/Footsteps/Dirt");
        lib.stepMetal = Clips("Foley/Footsteps/Metal"); lib.cloth = Clips("Foley/Cloth");
        lib.doorCreaks = Clips("World/Doors/Creak"); lib.doorOpen = Clips("World/Doors/Open"); lib.doorClose = Clips("World/Doors/Close");
        lib.doorLatch = Clip("World/Doors/Latch"); lib.doorLock = Clip("World/Doors/Lock");
        lib.switchOn = Clip("World/SwitchOn"); lib.switchOff = Clip("World/SwitchOff");
        lib.flashlightOn = Clip("World/FlashlightOn"); lib.flashlightOff = Clip("World/FlashlightOff");
        lib.grab = Clips("Foley/Items/Grab"); lib.equip = new[] { Clip("Foley/Items/Equip") }.Where(c => c != null).ToArray();
        lib.paperGrab = Clips("Foley/Items/PaperGrab"); lib.metalGrab = Clips("Foley/Items/MetalGrab");
        lib.cardboardDrop = Clips("Foley/Items/CardboardDrop"); lib.paperDrop = Clips("Foley/Items/PaperDrop");
        lib.plateDrop = Clips("Foley/Items/PlateDrop"); lib.metalDrop = Clips("Foley/Items/MetalDrop");
        lib.glassDrop = Clips("Foley/Items/GlassDrop"); lib.woodDrop = Clips("Foley/Items/WoodDrop");
        lib.softDrop = Clips("Foley/Items/SoftDrop");
        lib.hammerNail = Clips("World/HammerNail"); lib.nailingAbove = Clip("Horror/NailingAbove");
        lib.doorKnock = Clip("World/DoorKnock"); lib.doorLanding = Clip("World/DoorLanding");
        lib.lampFlicker = Clip("World/LampFlicker"); lib.pryBreak = Clip("World/PryBreak");
        lib.stoveIgnite = Clip("World/StoveIgnite"); lib.breakerOff = Clip("World/BreakerOff"); lib.plateSetDown = Clip("World/PlateSetDown");
        EditorUtility.SetDirty(lib);

        var routing = LoadOrCreate<AudioRoutingConfig>(RoutingPath, log);
        Undo.RecordObject(routing, "A-01 routing");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerBusSetup.MixerPath);
        routing.mixer = mixer;
        AudioMixerGroup G(string n) => mixer.FindMatchingGroups("").First(g => g.name == n);
        routing.world = G("SFX"); routing.foley = G("Foley"); routing.horror = G("Horror"); routing.voice = G("Voice");
        routing.ambience = G("Ambience"); routing.music = G("Music"); routing.ui = G("UI");
        routing.inside = mixer.FindSnapshot("Inside"); routing.outside = mixer.FindSnapshot("Outside");
        EditorUtility.SetDirty(routing);
        AssetDatabase.SaveAssets();

        int missing = 0;
        foreach (var f in typeof(GameSoundLibrary).GetFields())
        {
            var v = f.GetValue(lib);
            if (v == null || v is AudioClip[] arr && arr.Length == 0) { log.AppendLine("empty: " + f.Name); missing++; }
        }
        log.AppendLine($"library ok, empty fields: {missing}");
        return log.ToString();
    }

    // Клипы, вытащенные из чужой игры (ClapClap), и неудачные замены → новые CC0-клипы из Assets/Audio/Game.
    static readonly (string from, string to)[] ClipMap =
    {
        ("Assets/Audio/ClapClap/toggle_on.wav", "World/SwitchOn"),
        ("Assets/Audio/ClapClap/toggle_off.wav", "World/SwitchOff"),
        ("Assets/Audio/ClapClap/Footsteps_Wood_Walk_01.wav", "Foley/Footsteps/Wood1"),
        ("Assets/Audio/ClapClap/Footsteps_Wood_Walk_02.wav", "Foley/Footsteps/Wood2"),
        ("Assets/Audio/ClapClap/Footsteps_Wood_Walk_03.wav", "Foley/Footsteps/Wood3"),
        ("Assets/Audio/ClapClap/Footsteps_Wood_Walk_04.wav", "Foley/Footsteps/Wood4"),
        ("Assets/Audio/ClapClap/Footsteps_Tile_Walk_01.wav", "Foley/Footsteps/Concrete1"),
        ("Assets/Audio/ClapClap/Footsteps_Tile_Walk_02.wav", "Foley/Footsteps/Concrete2"),
        ("Assets/Audio/ClapClap/Footsteps_Tile_Walk_03.wav", "Foley/Footsteps/Concrete3"),
        ("Assets/Audio/ClapClap/Footsteps_Tile_Walk_04.wav", "Foley/Footsteps/Concrete4"),
        ("Assets/Audio/ClapClap/Footsteps_Rock_Walk_01.wav", "Foley/Footsteps/Metal1"),
        ("Assets/Audio/ClapClap/Footsteps_Rock_Walk_02.wav", "Foley/Footsteps/Metal2"),
        ("Assets/Audio/ClapClap/Footsteps_Rock_Walk_03.wav", "Foley/Footsteps/Metal3"),
        ("Assets/Audio/ClapClap/Footsteps_Rock_Walk_04.wav", "Foley/Footsteps/Metal4"),
        ("Assets/Audio/ClapClap/book-grab.wav", "Foley/Items/Grab1"),
        ("Assets/Audio/ClapClap/book-drop-01.wav", "Foley/Items/CardboardDrop1"),
        ("Assets/Audio/ClapClap/book-drop-02.wav", "Foley/Items/CardboardDrop2"),
        ("Assets/Audio/ClapClap/book-drop-03.wav", "Foley/Items/CardboardDrop3"),
        ("Assets/Audio/ClapClap/drop-bowl.wav", "Foley/Items/PlateDrop1"),
        ("Assets/Audio/ClapClap/grab-bowl.wav", "Foley/Items/Grab2"),
        ("Assets/Audio/ClapClap/drop-meat.wav", "Foley/Items/SoftDrop1"),
        ("Assets/Audio/ClapClap/grab-meat.wav", "Foley/Items/Grab3"),
        ("Assets/Audio/ClapClap/throw.wav", "Foley/Cloth1"),
        ("Assets/Audio/ClapClap/fridge-loop.wav", "Ambience/FridgeHum"),
        ("Assets/Audio/ClapClap/locked_door.wav", "World/Doors/LockedRattle"),
        ("Assets/Audio/ClapClap/unlock_door.wav", "World/Doors/Lock"),
        ("Assets/Audio/ClapClap/table-smash-47690.wav", "World/PryBreak"),
    };

    // Сцена Main: ClapClap → CC0, старый источник дождя выключен (дождь ведёт AmbienceDirector),
    // снапшоты RainZoneSwitcher сняты (Inside/Outside переключает AmbienceDirector), гул холодильника слышен.
    public static string UpgradeMainScene(bool dryRun)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying) return "stop Play Mode first";
        if (scene.path != "Assets/Scenes/Main Scene/Main.unity") return "open Main first (active: " + scene.path + ")";
        if (scene.isDirty && !dryRun) return "Main has unsaved edits — refusing";
        var log = new StringBuilder();
        var map = new Dictionary<Object, Object>();
        foreach (var (from, to) in ClipMap)
        {
            var a = AssetDatabase.LoadAssetAtPath<AudioClip>(from);
            var b = Clip(to);
            if (a == null || b == null) { log.AppendLine($"skip map {from} -> {to} (a={a != null} b={b != null})"); continue; }
            map[a] = b;
        }
        int swaps = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (!map.TryGetValue(it.objectReferenceValue, out Object replacement)) continue;
                    log.AppendLine($"swap {PathOf(c.transform)}.{c.GetType().Name}.{it.propertyPath}: {it.objectReferenceValue.name} -> {replacement.name}");
                    if (!dryRun) { it.objectReferenceValue = replacement; changed = true; }
                    swaps++;
                }
                if (changed) { Undo.RecordObject(c, "A-01 clip swap"); so.ApplyModifiedProperties(); }
            }

        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.gameObject.scene != scene) continue;
            if (s.name == "Rain" && s.transform.parent != null && s.transform.parent.name == "Main Plot" && s.enabled)
            {
                log.AppendLine("disable old rain source " + PathOf(s.transform));
                if (!dryRun) { Undo.RecordObject(s, "A-01 rain"); s.enabled = false; }
            }
            if (s.name == "Fridge Hum Audio")
            {
                log.AppendLine($"fridge hum volume {s.volume} -> 0.45, max {s.maxDistance} -> 9");
                if (!dryRun) { Undo.RecordObject(s, "A-01 fridge"); s.volume = .45f; s.maxDistance = 9f; s.minDistance = 1f; }
            }
        }
        foreach (RainZoneSwitcher zone in Object.FindObjectsByType<RainZoneSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zone.enterSnapshot == null && zone.exitSnapshot == null) continue;
            log.AppendLine("clear snapshots on " + PathOf(zone.transform));
            if (!dryRun) { Undo.RecordObject(zone, "A-01 zones"); zone.enterSnapshot = null; zone.exitSnapshot = null; }
        }
        log.AppendLine($"swaps: {swaps}, dryRun={dryRun}");
        if (!dryRun)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            log.AppendLine("saved");
        }
        return log.ToString();
    }

    static string PathOf(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static T LoadOrCreate<T>(string path, StringBuilder log) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        log.AppendLine("created " + path);
        return asset;
    }

    static AudioClip Clip(string rel) => AssetDatabase.LoadAssetAtPath<AudioClip>(Game + rel + ".ogg");

    // Все клипы вида <prefix>N.ogg, по возрастанию N.
    static AudioClip[] Clips(string prefix)
    {
        string dir = Path.GetDirectoryName(Game + prefix).Replace('\\', '/');
        string name = Path.GetFileName(prefix);
        if (!AssetDatabase.IsValidFolder(dir)) return new AudioClip[0];
        var list = new List<(int, AudioClip)>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != dir) continue;
            string file = Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith(name) || !int.TryParse(file.Substring(name.Length), out int n)) continue;
            list.Add((n, AssetDatabase.LoadAssetAtPath<AudioClip>(path)));
        }
        return list.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
    }
}
#endif
