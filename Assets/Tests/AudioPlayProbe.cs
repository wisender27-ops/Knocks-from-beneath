#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка A-01 (звук): AudioPlayProbe.BeginDay() / BeginNightTwo() из Editor-команды.
// День (StartBoxQuest): маршрутизация, улица/дом, двери, выключатели, пауза, задания, затемнение, уровни.
// Ночь 2 (StartKitchenNoise): дрон и дождь, пугалка → сердцебиение и приглушение, отключение света, подвал.
// Отчёт — .codex/verification/audio.
[DefaultExecutionOrder(-100)]
public sealed class AudioPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string startedUtc, completedUtc, stage;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public List<string> notes = new List<string>();
    }
    private readonly Report report = new Report();
    private static readonly Vector3 Outside = new Vector3(-39.3f, 1.2f, 122f);
    private static readonly Vector3 Inside = new Vector3(-42.5f, 1.2f, 106f);
    private IntroSequence intro;
    private PlayerController player;
    private string output;
    private bool nightTwo;
    private readonly float[] buffer = new float[2048];

    public static void BeginDay() => Create(false);
    public static void BeginNightTwo() => Create(true);

    private static void Create(bool nightTwo)
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Audio verification").AddComponent<AudioPlayProbe>().nightTwo = nightTwo;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/audio");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        Stage("wait-gameplay");
        yield return Until(() => HorrorMomentDirector.Instance != null && HorrorMomentDirector.Instance.GameplayAvailable, 25f);
        Check(GameAudio.Instance != null, "GameAudio создан");
        Check(AmbienceDirector.Instance != null, "AmbienceDirector создан в игровой сцене");
        Check(AudioRouting.Config != null && GameSounds.Library != null, "Конфиг маршрутизации и библиотека звуков загружены из Resources");
        int orphans = 0;
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (s.outputAudioMixerGroup == null) { orphans++; Note("без шины: " + s.name); }
        Check(orphans == 0, "Все источники сцены на шинах микшера (без шины: " + orphans + ")");
        yield return nightTwo ? NightTwo() : Day();
        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator Day()
    {
        var amb = AmbienceDirector.Instance;
        Stage("outside");
        Teleport(Outside);
        yield return Wait(3f);
        Check(!AmbienceDirector.ListenerInside, "Во дворе — снаружи");
        Check(Bed("Day Outside").volume > .3f && Bed("Day Outside").isPlaying, "Днём во дворе играет дневной фон");
        Check(Bed("Room Tone").volume < .05f, "Во дворе тон комнаты выключен");
        Check(Bed("Drone").volume < .01f, "Днём дрона нет");
        Level("день, двор", 2f);

        Stage("inside");
        Teleport(Inside);
        yield return Wait(3f);
        Check(AmbienceDirector.ListenerInside, "В доме — внутри");
        Check(Bed("Room Tone").volume > .3f, "В доме слышен тон комнаты");
        Level("день, дом", 2f);

        Stage("door");
        Door door = null;
        foreach (Door d in FindObjectsByType<Door>(FindObjectsSortMode.None))
            if (d.name.StartsWith("Door_00") && !d.isOpen) { door = d; break; }
        if (door != null)
        {
            Teleport(door.transform.position + Vector3.up * .2f + door.transform.right * 1.2f);
            var audio = door.GetComponent<DoorAudio>();
            Check(audio != null, "У двери " + door.name + " есть DoorAudio");
            var latch = audio != null ? audio.transform.Find("Door Latch Audio")?.GetComponent<AudioSource>() : null;
            door.OpenDoor();
            yield return Until(() => latch != null && latch.isPlaying, 2f);
            Check(latch != null && latch.isPlaying, "Открытие двери — щелчок защёлки");
            yield return Wait(1.5f);
            door.CloseDoor();
            yield return Until(() => latch != null && latch.isPlaying, 3f);
            Check(latch != null && latch.isPlaying, "Закрытие двери — стук/щелчок");
            Check(latch != null && latch.outputAudioMixerGroup != null && latch.outputAudioMixerGroup.name == "SFX", "Двери на шине SFX (World)");
        }
        else Check(false, "Не нашлась закрытая дверь Door_00x");

        Stage("switch");
        LightSwitch sw = FindFirstObjectByType<LightSwitch>();
        if (sw != null && sw.switchSound != null)
        {
            sw.ToggleLight();
            yield return null;
            Check(sw.switchSound.isPlaying, "Выключатель щёлкает (" + (sw.isOn ? "вкл" : "выкл") + ")");
            sw.ToggleLight();
        }

        Stage("pause");
        var pause = FindFirstObjectByType<PauseMenuController>();
        var ui = GameAudio.Instance.GetComponent<AudioSource>();
        if (pause != null)
        {
            pause.SendMessage("Pause", SendMessageOptions.DontRequireReceiver);
            yield return null;
            Check(PauseMenuController.IsPaused && AudioListener.pause, "Пауза: мир стоит (AudioListener.pause)");
            Check(ui.isPlaying && ui.ignoreListenerPause, "Пауза: звук открытия играет через UI-шину, несмотря на паузу слушателя");
            Check(FindObjectsByType<UiSoundHook>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0, "Кнопки меню паузы получили звук наведения/нажатия");
            yield return WaitRealtime(.6f);
            pause.SendMessage("Resume", SendMessageOptions.DontRequireReceiver);
            yield return null;
            Check(!AudioListener.pause && ui.isPlaying, "Выход из паузы: звук закрытия, мир снова звучит");
        }

        Stage("quest");
        yield return WaitRealtime(1.3f);
        QuestManager.Instance.CreateQuest("A-01 тест", 1);
        yield return null;
        Check(ui.isPlaying, "Новое задание — короткий сигнал");

        Stage("fade");
        float before = AudioListener.volume;
        intro.fadeScreen.alpha = 1f;
        yield return WaitRealtime(.6f);
        Check(AudioListener.volume < .02f, $"Затемнение плавно глушит звук ({before:0.00} → {AudioListener.volume:0.00})");
        intro.fadeScreen.alpha = 0f;
        yield return WaitRealtime(.15f);
        Check(AudioListener.volume > .01f && AudioListener.volume < before, "После затемнения звук возвращается плавно, а не рывком");
        yield return WaitRealtime(.6f);
        Check(Mathf.Abs(AudioListener.volume - SettingsStore.GetMasterVolume()) < .01f, "После затемнения громкость = настройке");

        Stage("settings");
        float music = SettingsStore.GetMusicVolume();
        SettingsStore.SetMusicVolume(0f);
        yield return null;
        AudioRouting.Config.mixer.GetFloat("MusicVol", out float db);
        Check(db <= -79f, "Ползунок «Музыка» 0 → MusicVol −80 дБ");
        SettingsStore.SetMusicVolume(music);
    }

    private IEnumerator NightTwo()
    {
        Stage("night-inside");
        Teleport(Inside);
        yield return Wait(6f);
        Check(Bed("Drone").isPlaying && Bed("Drone").clip == GameSounds.Library.droneNight2, "Ночь 2: дрон второй ночи");
        Check(Bed("Night Outside").volume > .05f || Bed("Rain").volume > .05f, "Ночь 2: уличный ночной фон/дождь");
        Level("ночь 2, дом", 2f);

        Stage("scare");
        var director = HorrorMomentDirector.Instance;
        yield return Until(() => director.CanPresent && !director.HasOwner, 40f);
        yield return Wait(1f);
        if (!director.CanPresent) Note("CanPresent=false: ждали 40 с, на экране реплика или идёт сцена");
        AudioRouting.Config.mixer.GetFloat("MusicVol", out float musicBefore);
        bool began = director.TryBegin(this, true) || director.IsOwner(this);
        Check(began, "Пугалка взята пробой");
        yield return Wait(1.2f);
        AudioRouting.Config.mixer.GetFloat("MusicVol", out float musicDuck);
        Check(musicDuck < musicBefore - 5f, $"Пугалка приглушает музыку ({musicBefore:0.0} → {musicDuck:0.0} дБ)");
        Check(Bed("Heartbeat").isPlaying, "Во время пугалки слышно сердцебиение");
        director.End(this, 0f);
        yield return Wait(1.2f);
        AudioRouting.Config.mixer.GetFloat("MusicVol", out float musicBack);
        Check(musicBack > musicDuck + 4f, "После пугалки музыка возвращается");

        Stage("blackout");
        float droneBefore = Bed("Drone").volume;
        GameEvents.OnPowerOutageSwitchAttempt?.Invoke();
        yield return null;
        Check(Bed("Drone").volume < droneBefore * .5f + .01f, $"Отключение света обрывает дрон ({droneBefore:0.00} → {Bed("Drone").volume:0.00})");
        bool fridge = false;
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (s.clip == GameSounds.Library.fridgeHum && s.isPlaying) fridge = true;
        Check(!fridge, "Холодильник замолкает при отключении света");

        Stage("basement");
        var hatch = FindFirstObjectByType<BasementHatch>(FindObjectsInactive.Include);
        if (hatch != null)
        {
            Teleport(new Vector3(hatch.transform.position.x, hatch.transform.position.y - 3.2f, hatch.transform.position.z));
            yield return Wait(4f);
            Check(AmbienceDirector.ListenerInBasement, "Ниже люка — подвал");
            Check(Bed("Basement").volume > .2f, "В подвале свой фон");
            Level("подвал", 2f);
        }
    }

    private AudioSource Bed(string name) => AmbienceDirector.Instance.transform.Find(name).GetComponent<AudioSource>();

    private void Level(string label, float seconds)
    {
        StartCoroutine(Measure(label, seconds));
    }

    private IEnumerator Measure(string label, float seconds)
    {
        float peak = 0f; double sum = 0; int n = 0;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            AudioListener.GetOutputData(buffer, 0);
            foreach (float v in buffer) { peak = Mathf.Max(peak, Mathf.Abs(v)); sum += v * v; }
            n += buffer.Length;
            yield return null;
        }
        float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, n)));
        Note($"уровень «{label}»: RMS {20 * Mathf.Log10(Mathf.Max(rms, 1e-6f)):0.0} дБ, пик {20 * Mathf.Log10(Mathf.Max(peak, 1e-6f)):0.0} дБ (общая громкость {AudioListener.volume:0.00})");
    }

    private static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds) yield return null;
    }

    private void Teleport(Vector3 position)
    {
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = position;
        capsule.enabled = true;
        player.StopMotion();
    }

    private void Check(bool passed, string message) { (passed ? report.passed : report.failed).Add(message); Write(); }
    private void Note(string message) { report.notes.Add(message); Write(); }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
    private void OnDisable() => Time.timeScale = 1f;
}
#endif
