using UnityEngine;
using UnityEngine.Audio;

namespace KnocksFromBeneath
{
// Шины MainMixer (A-01). Значения не менять — только дописывать в конец.
public enum AudioBus
{
    World = 0,    // SFX: двери, свет, техника, машины
    Foley = 1,    // шаги, предметы, руки героя
    Horror = 2,   // стук снизу, пугалки, существо
    Voice = 3,    // мысли, голоса
    Ambience = 4, // фон снаружи (глушится снапшотом «внутри»)
    Music = 5,    // дроны и музыка
    UI = 6        // интерфейс (играет и на паузе)
}

public static class AudioRouting
{
    const string ConfigPath = "Audio/AudioRouting";
    static AudioRoutingConfig config;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { config = null; loaded = false; }

    public static AudioRoutingConfig Config
    {
        get
        {
            if (!loaded) { config = Resources.Load<AudioRoutingConfig>(ConfigPath); loaded = true; }
            return config;
        }
    }

    public static AudioMixerGroup Group(AudioBus bus) => Config != null ? Config.Group(bus) : null;

    public static AudioSource Assign(AudioSource source, AudioBus bus)
    {
        if (source == null) return null;
        AudioMixerGroup group = Group(bus);
        if (group != null) source.outputAudioMixerGroup = group;
        if (bus == AudioBus.UI) source.ignoreListenerPause = true;
        return source;
    }

    // Новый источник с маршрутом; 3D по умолчанию, как HorrorMomentDirector.CreateSource.
    public static AudioSource Create(Transform parent, string name, AudioBus bus, float spatialBlend = 1f, float maxDistance = 12f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1.5f;
        source.maxDistance = maxDistance;
        return Assign(source, bus);
    }
}
}
