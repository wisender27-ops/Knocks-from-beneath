using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

// A-01: логика звуковой системы, не требующая Play Mode.
public class AudioSystemTests
{
    [Test]
    public void Pick_NeverRepeatsPreviousClip()
    {
        var clips = new[] { AudioClip.Create("a", 10, 1, 44100, false), AudioClip.Create("b", 10, 1, 44100, false), AudioClip.Create("c", 10, 1, 44100, false) };
        int last = -1;
        AudioClip previous = null;
        for (int i = 0; i < 200; i++)
        {
            AudioClip clip = GameSounds.Pick(clips, ref last);
            Assert.AreNotSame(previous, clip);
            previous = clip;
        }
        foreach (var c in clips) Object.DestroyImmediate(c);
    }

    [Test]
    public void Pick_HandlesEmptyAndSingle()
    {
        int last = -1;
        Assert.IsNull(GameSounds.Pick(null, ref last));
        Assert.IsNull(GameSounds.Pick(new AudioClip[0], ref last));
        var one = AudioClip.Create("one", 10, 1, 44100, false);
        Assert.AreSame(one, GameSounds.Pick(new[] { one }, ref last));
        Assert.AreSame(one, GameSounds.Pick(new[] { one }, ref last));
        Object.DestroyImmediate(one);
    }

    [TestCase("ThoughtAudio", null, AudioBus.Voice)]
    [TestCase("BasementCreatureVoice", "HoleEventController", AudioBus.Voice)]
    [TestCase("Monster escape AS", "Main Plot", AudioBus.Horror)]
    [TestCase("Knock Controller", "Main Plot", AudioBus.Horror)]
    [TestCase("Fridge Hum Audio", "Refrigerator.001", AudioBus.Ambience)]
    [TestCase("flashlightInHand", "ItemHolder", AudioBus.Foley)]
    [TestCase("lightswitchKitchen", "House", AudioBus.World)]
    public void GuessBus_ByObjectPath(string name, string parent, AudioBus expected)
    {
        var root = new GameObject(parent ?? "root");
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        var source = go.AddComponent<AudioSource>();
        Assert.AreEqual(expected, GameAudio.GuessBus(source));
        Object.DestroyImmediate(root);
    }

    [TestCase("MoveInBox_1", ItemAudio.Material.Cardboard)]
    [TestCase("Sarten_01", ItemAudio.Material.Metal)]
    [TestCase("Pot_lid_02", ItemAudio.Material.Metal)]
    [TestCase("Coffee_maker_cup", ItemAudio.Material.Plate)]
    [TestCase("Cake_01.001", ItemAudio.Material.Plate)]
    [TestCase("TrashBag", ItemAudio.Material.Soft)]
    [TestCase("NeighborNote", ItemAudio.Material.Paper)]
    public void ItemMaterial_ByName(string name, ItemAudio.Material expected)
    {
        var go = new GameObject(name);
        Assert.AreEqual(expected, ItemAudio.Guess(go.transform));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void FadeGain_ScalesMasterVolumeWithoutChangingSetting()
    {
        float master = SettingsStore.GetMasterVolume();
        try
        {
            SettingsStore.FadeGain = .5f;
            Assert.AreEqual(master * .5f, AudioListener.volume, 1e-4f);
            Assert.AreEqual(master, SettingsStore.GetMasterVolume(), 1e-4f);
            SettingsStore.FadeGain = 0f;
            Assert.AreEqual(0f, AudioListener.volume, 1e-4f);
        }
        finally { SettingsStore.FadeGain = 1f; }
        Assert.AreEqual(master, AudioListener.volume, 1e-4f);
    }
}
