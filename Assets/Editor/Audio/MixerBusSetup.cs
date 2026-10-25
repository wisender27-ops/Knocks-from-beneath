#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// A-01: перестройка MainMixer — шины, открытые параметры громкости, реверберация дома.
// API микшера в редакторе internal, поэтому через рефлексию. Существующие группы SFX/Ambience и снапшоты
// не пересоздаются: их fileID/GUID остаются, ссылки сцен и RainZoneSwitcher не ломаются. Повторный запуск безопасен.
public static class MixerBusSetup
{
    public const string MixerPath = "Assets/Audio/MainMixer.mixer";
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly EditorAsm = typeof(EditorWindow).Assembly;

    public static string Apply()
    {
        var log = new StringBuilder();
        var mixer = AssetDatabase.LoadMainAssetAtPath(MixerPath);
        Undo.RecordObject(mixer, "A-01 mixer buses");
        object master = Get(mixer, "masterGroup");
        var sfx = FindGroup(mixer, "SFX");
        var ambience = FindGroup(mixer, "Ambience");

        var music = EnsureGroup(mixer, master, "Music", log);
        EnsureGroup(mixer, sfx, "Foley", log);
        EnsureGroup(mixer, sfx, "Horror", log);
        EnsureGroup(mixer, sfx, "Voice", log);
        var ui = EnsureGroup(mixer, master, "UI", log);

        // Громкость эмбиента в обоих снапшотах была -24.9 дБ; теперь её задаёт пользователь (открытый параметр),
        // а уровни — сами источники. Lowpass «внутри» остаётся за снапшотами.
        foreach (var snapshot in Snapshots(mixer))
        {
            Call(ambience, "SetValueForVolume", mixer, snapshot, 0f);
            Call(master, "SetValueForVolume", mixer, snapshot, 0f);
        }

        Expose(mixer, master, "MasterVol", log);
        Expose(mixer, music, "MusicVol", log);
        Expose(mixer, ambience, "AmbienceVol", log);
        Expose(mixer, sfx, "SfxVol", log);
        Expose(mixer, ui, "UiVol", log);

        EnsureReverb(mixer, sfx, log);
        EnsureLimiter(mixer, master, log);

        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static object EnsureGroup(UnityEngine.Object mixer, object parent, string name, StringBuilder log)
    {
        var existing = ((IEnumerable)Get(parent, "children")).Cast<object>().FirstOrDefault(g => ((UnityEngine.Object)g).name == name);
        if (existing != null) return existing;
        var group = Call(mixer, "CreateNewGroup", name, false);
        Call(mixer, "AddChildToParent", group, parent);
        Call(mixer, "AddGroupToCurrentView", group);
        log.AppendLine("group+ " + name + " under " + ((UnityEngine.Object)parent).name);
        return group;
    }

    static void Expose(UnityEngine.Object mixer, object group, string name, StringBuilder log)
    {
        var parameters = (Array)Get(mixer, "exposedParameters");
        foreach (var p in parameters)
            if ((string)Field(p, "name") == name) return;
        var guid = Call(group, "GetGUIDForVolume");
        if ((bool)Call(mixer, "ContainsExposedParameter", guid)) { Rename(mixer, guid, name); return; }
        var pathType = EditorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        var path = Activator.CreateInstance(pathType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { group, guid }, null);
        Call(mixer, "AddExposedParameter", path);
        Rename(mixer, guid, name);
        log.AppendLine("exposed " + name + " (" + ((UnityEngine.Object)group).name + ")");
    }

    static void Rename(UnityEngine.Object mixer, object guid, string name)
    {
        var parameters = (Array)Get(mixer, "exposedParameters");
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters.GetValue(i);
            if (!Field(p, "guid").Equals(guid)) continue;
            p.GetType().GetField("name", All).SetValue(p, name);
            parameters.SetValue(p, i);
        }
        Set(mixer, "exposedParameters", parameters);
    }

    // Реверберация небольшой комнаты на SFX: «внутри» — слышна, «снаружи» — выключена (mix level -80 дБ).
    static void EnsureReverb(UnityEngine.Object mixer, object sfx, StringBuilder log)
    {
        var effects = (Array)Get(sfx, "effects");
        object reverb = effects.Cast<object>().FirstOrDefault(e => (string)Get(e, "effectName") == "SFX Reverb");
        if (reverb == null)
        {
            var effectType = EditorAsm.GetType("UnityEditor.Audio.AudioMixerEffectController");
            reverb = Activator.CreateInstance(effectType, All, null, new object[] { "SFX Reverb" }, null);
            Call(reverb, "PreallocateGUIDs");
            AssetDatabase.AddObjectToAsset((UnityEngine.Object)reverb, mixer);
            Call(sfx, "InsertEffect", reverb, effects.Length);
            log.AppendLine("effect+ SFX Reverb on SFX");
        }
        foreach (var snapshot in Snapshots(mixer))
        {
            bool inside = ((UnityEngine.Object)snapshot).name == "Inside";
            Call(reverb, "SetValueForMixLevel", mixer, snapshot, 0f);
            void P(string n, float v) => Call(reverb, "SetValueForParameter", mixer, snapshot, n, v);
            P("Dry Level", 0f);
            P("Room", inside ? -1400f : -10000f);
            P("Room HF", -900f);
            P("Decay Time", 0.65f);
            P("Decay HF Ratio", 0.55f);
            P("Reflections", -1100f);
            P("Reflect Delay", 0.006f);
            P("Reverb", inside ? -900f : -10000f);
            P("Reverb Delay", 0.011f);
            P("Diffusion", 100f);
            P("Density", 100f);
            P("HF Reference", 4000f);
            P("Room LF", 0f);
            P("LF Reference", 250f);
        }
    }

    // Защита от перегруза на мастере: сложенные громкие слои (срыв стука, скримеры) не клиппуют.
    static void EnsureLimiter(UnityEngine.Object mixer, object master, StringBuilder log)
    {
        var effects = (Array)Get(master, "effects");
        object comp = effects.Cast<object>().FirstOrDefault(e => (string)Get(e, "effectName") == "Compressor");
        if (comp == null)
        {
            var effectType = EditorAsm.GetType("UnityEditor.Audio.AudioMixerEffectController");
            comp = Activator.CreateInstance(effectType, All, null, new object[] { "Compressor" }, null);
            Call(comp, "PreallocateGUIDs");
            AssetDatabase.AddObjectToAsset((UnityEngine.Object)comp, mixer);
            Call(master, "InsertEffect", comp, effects.Length);
            log.AppendLine("effect+ Compressor on Master");
        }
        foreach (var snapshot in Snapshots(mixer))
        {
            Call(comp, "SetValueForMixLevel", mixer, snapshot, 0f);
            foreach (var (n, v) in new[] { ("Threshold", -5f), ("Attack", 1f), ("Release", 80f), ("Make up gain", 0f) })
            {
                try { Call(comp, "SetValueForParameter", mixer, snapshot, n, v); }
                catch (Exception e) { log.AppendLine("compressor param " + n + ": " + e.InnerException?.Message); }
            }
        }
    }

    static object FindGroup(UnityEngine.Object mixer, string name) =>
        ((IEnumerable)Call(mixer, "GetAllAudioGroupsSlow")).Cast<object>().First(g => ((UnityEngine.Object)g).name == name);

    static object[] Snapshots(UnityEngine.Object mixer) => ((Array)Get(mixer, "snapshots")).Cast<object>().ToArray();

    static object Get(object target, string property) => target.GetType().GetProperty(property, All).GetValue(target);
    static void Set(object target, string property, object value) => target.GetType().GetProperty(property, All).SetValue(target, value);
    static object Field(object target, string field) => target.GetType().GetField(field, All).GetValue(target);

    static object Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethods(All).First(x => x.Name == method && x.GetParameters().Length == args.Length);
        return m.Invoke(target, args);
    }

    public static string Describe()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var sb = new StringBuilder();
        foreach (var g in mixer.FindMatchingGroups("")) sb.AppendLine("group " + g.name);
        foreach (var p in (Array)Get(mixer, "exposedParameters")) sb.AppendLine("param " + Field(p, "name"));
        foreach (var s in Snapshots(mixer)) sb.AppendLine("snapshot " + ((UnityEngine.Object)s).name);
        return sb.ToString();
    }
}
#endif
