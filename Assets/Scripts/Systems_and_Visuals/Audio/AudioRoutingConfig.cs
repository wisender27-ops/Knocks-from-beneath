using UnityEngine;
using UnityEngine.Audio;

namespace KnocksFromBeneath
{
// Ссылки на микшер лежат в Resources/Audio/AudioRouting — так их получают и источники, созданные кодом.
[CreateAssetMenu(menuName = "Knocks/Audio Routing Config")]
public sealed class AudioRoutingConfig : ScriptableObject
{
    public AudioMixer mixer;
    public AudioMixerGroup world, foley, horror, voice, ambience, music, ui;
    public AudioMixerSnapshot inside, outside;

    public AudioMixerGroup Group(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Foley: return foley;
            case AudioBus.Horror: return horror;
            case AudioBus.Voice: return voice;
            case AudioBus.Ambience: return ambience;
            case AudioBus.Music: return music;
            case AudioBus.UI: return ui;
            default: return world;
        }
    }
}
}
