using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
// A-01: общий звук игры. Живёт всю сессию (DontDestroyOnLoad), создаётся сам.
// - громкости категорий → открытые параметры MainMixer (MusicVol/AmbienceVol/SfxVol/UiVol);
// - затемнения приглушают звук плавно (SettingsStore.FadeGain), а не обрывают его;
// - пугалка (HorrorMomentDirector.HasOwner) приглушает музыку и фон, чтобы она читалась;
// - источники сцены без группы микшера получают шину по имени (страховка для старых объектов);
// - звуки интерфейса: наведение/нажатие любых Selectable, открытие/закрытие экранов.
public sealed class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const float MinDb = -80f;
    const float FadeSeconds = .35f;
    const float DuckMusicDb = -9f, DuckAmbienceDb = -5f, DuckSeconds = .6f;

    float fadeGain = 1f;
    float duck;
    AudioSource uiSource;
    readonly List<AudioSource> pool = new List<AudioSource>();
    int poolCursor;
    int settingsRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; lastQuestCue = -10f; nextThoughtTick = 0f; lastThoughtTick = -1; audibleThroughFadeUntil = 0f; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("Game Audio");
        DontDestroyOnLoad(go);
        go.AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        AudioRouting.Assign(uiSource, AudioBus.UI);
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        fadeGain = SettingsStore.FadeGain;
        RouteOrphans(scene);
        HookSelectables(null);
        UpgradeSceneAudio();
        ApplyVolumes(true);
        if (scene.GetRootGameObjects().Length > 0 && FindFirstObjectByType<SkyboxSwitcher>() != null &&
            FindFirstObjectByType<AmbienceDirector>() == null)
            new GameObject("Ambience Director").AddComponent<AmbienceDirector>();
        else if (FindFirstObjectByType<MenuAmbience>() == null && FindFirstObjectByType<MainMenuController>() != null)
            new GameObject("Menu Ambience").AddComponent<MenuAmbience>();
    }

    // Звук, который должен прозвучать на чёрном экране (грохот будит героя): затемнение
    // на это время не приглушает звук.
    static float audibleThroughFadeUntil;

    public static void HearThroughFade(float seconds) =>
        audibleThroughFadeUntil = Mathf.Max(audibleThroughFadeUntil, Time.unscaledTime + seconds);

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float fadeTarget = FadeTransitionPresentation.IsActive && Time.unscaledTime >= audibleThroughFadeUntil ? 0f : 1f;
        fadeGain = Mathf.MoveTowards(fadeGain, fadeTarget, dt / FadeSeconds);
        if (!Mathf.Approximately(fadeGain, SettingsStore.FadeGain)) SettingsStore.FadeGain = fadeGain;

        var director = HorrorMomentDirector.Instance;
        float duckTarget = director != null && director.HasOwner ? 1f : 0f;
        float newDuck = Mathf.MoveTowards(duck, duckTarget, dt / DuckSeconds);
        bool changed = !Mathf.Approximately(newDuck, duck);
        duck = newDuck;
        ApplyVolumes(changed);
    }

    void ApplyVolumes(bool force)
    {
        if (!force && settingsRevision == SettingsStore.Revision) return;
        settingsRevision = SettingsStore.Revision;
        var config = AudioRouting.Config;
        if (config == null || config.mixer == null) return;
        config.mixer.SetFloat("MusicVol", ToDb(SettingsStore.GetMusicVolume()) + DuckMusicDb * duck);
        config.mixer.SetFloat("AmbienceVol", ToDb(SettingsStore.GetAmbienceVolume()) + DuckAmbienceDb * duck);
        config.mixer.SetFloat("SfxVol", ToDb(SettingsStore.GetSfxVolume()));
        config.mixer.SetFloat("UiVol", ToDb(SettingsStore.GetSfxVolume()));
    }

    // Новые звуки для старых объектов сцены — без правки сцены: двери, предметы, фонарик.
    static void UpgradeSceneAudio()
    {
        if (GameSounds.Library == null) return;
        foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None)) DoorAudio.Attach(door);
        foreach (ItemFoley foley in FindObjectsByType<ItemFoley>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ItemAudio.Apply(foley);
        foreach (ImpactSounds impact in FindObjectsByType<ImpactSounds>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ItemAudio.Apply(impact);
        foreach (StoveInteractable stove in FindObjectsByType<StoveInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Плита: ручка и розжиг газа, а не щелчок выключателя; готово — тарелка на стол.
            if (GameSounds.Library.stoveIgnite != null) stove.cookStartSfx = GameSounds.Library.stoveIgnite;
            if (GameSounds.Library.plateSetDown != null) stove.cookDoneSfx = GameSounds.Library.plateSetDown;
        }
        foreach (PlayerInventory inventory in FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GameSounds.Library.flashlightOn != null) inventory.soundOn = GameSounds.Library.flashlightOn;
            if (GameSounds.Library.flashlightOff != null) inventory.soundOff = GameSounds.Library.flashlightOff;
            if (inventory.flashlightAudioSource != null) AudioRouting.Assign(inventory.flashlightAudioSource, AudioBus.Foley);
        }
    }

    static float ToDb(float linear) => linear <= .0001f ? MinDb : Mathf.Max(MinDb, 20f * Mathf.Log10(linear));

    // ---------- маршрутизация ----------

    static void RouteOrphans(Scene scene)
    {
        if (AudioRouting.Config == null || !scene.IsValid()) return;
        var sources = new List<AudioSource>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            root.GetComponentsInChildren(true, sources);
            foreach (AudioSource source in sources)
                if (source.outputAudioMixerGroup == null) AudioRouting.Assign(source, GuessBus(source));
        }
    }

    // Старые объекты сцены не знают про шины — угадываем по имени объекта и его родителей.
    public static AudioBus GuessBus(AudioSource source)
    {
        Transform t = source.transform;
        string path = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; path += "/" + t.name; }
        path = path.ToLowerInvariant();
        if (path.Contains("thought") || path.Contains("voice")) return AudioBus.Voice;
        if (path.Contains("monster") || path.Contains("hole") || path.Contains("basement") || path.Contains("finale") ||
            path.Contains("knock") || path.Contains("scare") || path.Contains("creature")) return AudioBus.Horror;
        if (path.Contains("rain") || path.Contains("ambien") || path.Contains("hum")) return AudioBus.Ambience;
        if (path.Contains("foley") || path.Contains("player") || path.Contains("footstep") || path.Contains("flashlight") ||
            path.Contains("item")) return AudioBus.Foley;
        if (path.Contains("canvas") || path.Contains("menu") || path.Contains("button") || path.Contains("title")) return AudioBus.UI;
        return AudioBus.World;
    }

    // ---------- звуки интерфейса ----------

    public static void PlayUi(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || Instance == null || Instance.uiSource == null) return;
        Instance.uiSource.pitch = pitch;
        Instance.uiSource.PlayOneShot(clip, volume);
    }

    static float lastQuestCue = -10f;
    static float nextThoughtTick;
    static int lastThoughtTick = -1;

    // Печать реплики: тихий щелчок не на каждую букву, а примерно раз в 80 мс.
    public static void PlayThoughtTick()
    {
        if (Time.unscaledTime < nextThoughtTick) return;
        nextThoughtTick = Time.unscaledTime + Random.Range(.07f, .1f);
        PlayUi(GameSounds.Pick(GameSounds.Library?.thoughtTicks, ref lastThoughtTick), .35f, Random.Range(.9f, 1.1f));
    }

    // Новое задание / задание выполнено. Выполнение и следующее задание обычно идут в одном кадре —
    // звучит только первое; дебаг-прыжки, создающие пачку заданий, не устраивают перезвон.
    public static void PlayQuestCue(bool completed)
    {
        if (Time.unscaledTime - lastQuestCue < 1.2f || FadeTransitionPresentation.IsActive) return;
        lastQuestCue = Time.unscaledTime;
        PlayUi(completed ? UiSound.QuestDone : UiSound.QuestNew);
    }

    public static void PlayUi(UiSound sound) => PlayUi(GameSounds.Ui(sound), GameSounds.UiVolume(sound), GameSounds.UiPitch(sound));

    // Добавляет звук наведения/нажатия всем кнопкам, слайдерам и переключателям под root (или во всех сценах).
    public static void HookSelectables(Transform root)
    {
        var selectables = root != null
            ? root.GetComponentsInChildren<Selectable>(true)
            : FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Selectable selectable in selectables)
            if (selectable.GetComponent<UiSoundHook>() == null) selectable.gameObject.AddComponent<UiSoundHook>();
    }

    // ---------- одноразовые 3D-звуки (замена PlayClipAtPoint с маршрутом) ----------

    public static AudioSource PlayAt(AudioClip clip, Vector3 position, AudioBus bus, float volume = 1f, float pitch = 1f, float maxDistance = 14f)
    {
        if (clip == null || Instance == null) return null;
        AudioSource source = Instance.NextPooled();
        source.transform.position = position;
        AudioRouting.Assign(source, bus);
        source.maxDistance = maxDistance;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
        return source;
    }

    AudioSource NextPooled()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            poolCursor = (poolCursor + 1) % pool.Count;
            if (pool[poolCursor] != null && !pool[poolCursor].isPlaying) return pool[poolCursor];
        }
        if (pool.Count >= 12) { poolCursor = (poolCursor + 1) % pool.Count; return pool[poolCursor]; }
        AudioSource source = AudioRouting.Create(transform, "OneShot " + pool.Count, AudioBus.World);
        source.transform.SetParent(null);
        DontDestroyOnLoad(source.gameObject);
        pool.Add(source);
        poolCursor = pool.Count - 1;
        return source;
    }
}

public sealed class UiSoundHook : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    Selectable selectable;
    float lastHover;

    void Awake() => selectable = GetComponent<Selectable>();

    bool Interactable => selectable != null && selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData) => Hover();
    public void OnSelect(BaseEventData eventData) { if (!(eventData is PointerEventData)) Hover(); }

    void Hover()
    {
        if (!Interactable || Time.unscaledTime - lastHover < .06f) return;
        lastHover = Time.unscaledTime;
        GameAudio.PlayUi(UiSound.Hover);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Interactable && !(selectable is Slider)) Click();
    }

    public void OnSubmit(BaseEventData eventData) { if (Interactable) Click(); }

    void Click() => GameAudio.PlayUi(selectable is Toggle ? UiSound.Toggle : UiSound.Click);
}
}
