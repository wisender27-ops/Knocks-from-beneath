using UnityEngine;

namespace KnocksFromBeneath
{
// A-01: фон игровой сцены. Создаётся GameAudio, если в сцене есть SkyboxSwitcher.
// Слои (2D, плавные переходы): день снаружи, ночь снаружи, дождь, тон комнаты, подвал, дрон главы.
// «Внутри/снаружи» — по потолку над слушателем; снапшоты Inside/Outside MainMixer глушат уличный фон в доме.
// Редкие одиночные звуки: скрип дома ночью, гром во вторую ночь, собака вдалеке.
// После отключения света дрон и гул замолкают на несколько секунд — самая тихая точка ночи 2.
// Во время пугалки (HorrorMomentDirector.HasOwner) — сердцебиение, после неё — дыхание героя.
public sealed class AmbienceDirector : MonoBehaviour
{
    public static AmbienceDirector Instance { get; private set; }
    public static bool ListenerInside { get; private set; }
    public static bool ListenerInBasement { get; private set; }

    const float CheckInterval = .25f;
    const float CeilingProbe = 7f;

    sealed class Layer
    {
        public AudioSource source;
        public float target, max, speed;
        public Layer(AudioSource s, float max, float speed) { source = s; this.max = max; this.speed = speed; }
    }

    Layer day, night, rain, room, basement, drone, heartbeatLayer;
    AudioClip currentDrone;
    SkyboxSwitcher sky;
    ParticleSystem rainParticles;
    Transform listener;
    BasementHatch hatch;
    float basementTopY = float.NegativeInfinity;
    float nextCheck, nextCreak, nextThunder, nextDog, silenceUntil, nextHeartbeat;
    bool nightTwo, powerOut, inside, wasScare;
    int lastCreak = -1;
    readonly RaycastHit[] hits = new RaycastHit[4];
    AudioSource oneShot2D;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; ListenerInside = false; ListenerInBasement = false; }

    void Awake()
    {
        Instance = this;
        var lib = GameSounds.Library;
        day = Bed("Day Outside", lib?.dayOutside, AudioBus.Ambience, .55f, .35f);
        night = Bed("Night Outside", lib?.nightOutside, AudioBus.Ambience, .6f, .3f);
        rain = Bed("Rain", lib?.rain, AudioBus.Ambience, .5f, .3f);
        room = Bed("Room Tone", lib?.roomTone, AudioBus.Ambience, .9f, .5f);
        basement = Bed("Basement", lib?.basement, AudioBus.Ambience, .75f, .4f);
        drone = Bed("Drone", null, AudioBus.Music, .42f, .12f);
        heartbeatLayer = Bed("Heartbeat", null, AudioBus.Horror, .7f, 1.2f);
        heartbeatLayer.source.loop = false;
        oneShot2D = AudioRouting.Create(transform, "Ambience OneShot", AudioBus.Ambience, 0f);
        float now = Time.time;
        nextCreak = now + Random.Range(25f, 45f);
        nextThunder = now + Random.Range(20f, 40f);
        nextDog = now + Random.Range(40f, 80f);
    }

    void OnEnable()
    {
        GameEvents.OnNightTwoStarted += HandleNightTwo;
        GameEvents.OnDayStarted += HandleDay;
        GameEvents.OnPowerOutageSwitchAttempt += HandlePowerOut;
    }

    void OnDisable()
    {
        GameEvents.OnNightTwoStarted -= HandleNightTwo;
        GameEvents.OnDayStarted -= HandleDay;
        GameEvents.OnPowerOutageSwitchAttempt -= HandlePowerOut;
        if (Instance == this) Instance = null;
    }

    void HandleNightTwo() => nightTwo = true;
    void HandleDay() { nightTwo = false; powerOut = false; }

    void HandlePowerOut()
    {
        if (powerOut) return;
        powerOut = true;
        silenceUntil = Time.time + 7f;
        var breaker = GameSounds.Library?.breakerOff;
        if (breaker != null && listener != null)
            GameAudio.PlayAt(breaker, listener.position + listener.forward * -3f + Vector3.down * 1.5f, AudioBus.World, .8f, 1f, 20f);
        // Тишина обрывается сразу, а не плавно — так отключение читается телом.
        drone.source.volume *= .15f;
        room.source.volume *= .4f;
        foreach (var hum in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (hum != null && hum.loop && hum.isPlaying && hum.clip != null && hum.clip == GameSounds.Library?.fridgeHum) hum.Stop();
    }

    Layer Bed(string name, AudioClip clip, AudioBus bus, float max, float speed)
    {
        var source = AudioRouting.Create(transform, name, bus, 0f);
        source.loop = true;
        source.clip = clip;
        source.volume = 0f;
        source.priority = 40;
        return new Layer(source, max, speed);
    }

    void Update()
    {
        if (Time.time >= nextCheck) { nextCheck = Time.time + CheckInterval; Sense(); }
        bool isDay = sky == null || sky.isDayTime;
        bool raining = rainParticles != null && rainParticles.gameObject.activeInHierarchy && rainParticles.isEmitting;
        bool silent = Time.time < silenceUntil;

        day.target = isDay && !ListenerInBasement ? 1f : 0f;
        night.target = !isDay && !ListenerInBasement ? (raining ? .55f : 1f) : 0f;
        rain.target = raining && !ListenerInBasement ? (nightTwo ? 1f : .8f) : 0f;
        room.target = inside && !ListenerInBasement ? (powerOut ? .45f : 1f) : 0f;
        basement.target = ListenerInBasement ? 1f : 0f;

        AudioClip wantDrone = ListenerInBasement ? GameSounds.Library?.droneBasement
            : isDay ? null : nightTwo ? GameSounds.Library?.droneNight2 : GameSounds.Library?.droneNight1;
        UpdateDrone(wantDrone, silent);

        Fade(day); Fade(night); Fade(rain); Fade(room); Fade(basement); Fade(drone);
        UpdateScareLayers();
        if (!isDay && !ListenerInBasement) OneShots();
    }

    void UpdateDrone(AudioClip want, bool silent)
    {
        if (want != currentDrone)
        {
            // Сначала уводим старый дрон в ноль, потом меняем клип.
            drone.target = 0f;
            if (drone.source.volume <= .01f)
            {
                currentDrone = want;
                drone.source.clip = want;
                if (want != null) { drone.source.time = Random.Range(0f, want.length * .8f); drone.source.Play(); }
            }
            return;
        }
        drone.target = want != null && !silent ? 1f : 0f;
    }

    void Fade(Layer layer)
    {
        AudioSource s = layer.source;
        if (s.clip == null) return;
        float goal = layer.target * layer.max;
        s.volume = Mathf.MoveTowards(s.volume, goal, layer.speed * layer.max * Time.unscaledDeltaTime);
        if (s.volume > .001f && !s.isPlaying) { s.time = Random.Range(0f, s.clip.length * .9f); s.Play(); }
        else if (s.volume <= .001f && s.isPlaying && goal <= 0f) s.Stop();
    }

    void Sense()
    {
        if (sky == null) sky = FindFirstObjectByType<SkyboxSwitcher>();
        if (rainParticles == null)
        {
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ps.name == "Rain System") { rainParticles = ps; break; }
        }
        if (hatch == null)
        {
            hatch = FindFirstObjectByType<BasementHatch>(FindObjectsInactive.Include);
            if (hatch != null) basementTopY = hatch.transform.position.y - 1.2f;
        }
        if (listener == null || !listener.gameObject.activeInHierarchy)
        {
            var l = FindFirstObjectByType<AudioListener>();
            listener = l != null ? l.transform : null;
        }
        if (listener == null) return;
        Vector3 p = listener.position;
        ListenerInBasement = p.y < basementTopY;
        int count = Physics.RaycastNonAlloc(p + Vector3.up * .2f, Vector3.up, hits, CeilingProbe, ~0, QueryTriggerInteraction.Ignore);
        bool covered = false;
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(listener.root)) { covered = true; break; }
        bool nowInside = covered || ListenerInBasement;
        if (nowInside != inside || ListenerInside != nowInside)
        {
            inside = nowInside;
            ListenerInside = nowInside;
            var config = AudioRouting.Config;
            var snapshot = config == null ? null : inside ? config.inside : config.outside;
            if (snapshot != null) snapshot.TransitionTo(1.2f);
        }
    }

    void OneShots()
    {
        var lib = GameSounds.Library;
        if (lib == null || listener == null || HorrorMomentDirector.Instance != null && HorrorMomentDirector.Instance.HasOwner) return;
        float now = Time.time;
        if (inside && now >= nextCreak)
        {
            nextCreak = now + Random.Range(nightTwo ? 18f : 30f, nightTwo ? 40f : 70f);
            var clip = GameSounds.Pick(lib.houseCreaks, ref lastCreak);
            if (clip != null)
            {
                Vector3 offset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 7f);
                offset.y = Random.value < .5f ? 2.6f : 0f; // сверху (второй этаж) или где-то рядом
                GameAudio.PlayAt(clip, listener.position + offset, AudioBus.World, Random.Range(.25f, .45f), Random.Range(.8f, 1.05f), 16f);
            }
        }
        if (nightTwo && now >= nextThunder)
        {
            nextThunder = now + Random.Range(35f, 75f);
            var clip = GameSounds.Pick(lib.thunder);
            if (clip != null) { oneShot2D.pitch = Random.Range(.9f, 1.05f); oneShot2D.PlayOneShot(clip, Random.Range(.45f, .8f)); }
        }
        if (!nightTwo && now >= nextDog)
        {
            nextDog = now + Random.Range(70f, 150f);
            var clip = GameSounds.Pick(lib.distantDogs);
            if (clip != null) { oneShot2D.pitch = Random.Range(.95f, 1.05f); oneShot2D.PlayOneShot(clip, Random.Range(.12f, .22f)); }
        }
    }

    void UpdateScareLayers()
    {
        var lib = GameSounds.Library;
        var director = HorrorMomentDirector.Instance;
        bool scare = director != null && director.HasOwner && Time.timeScale > 0f;
        if (scare && lib != null && lib.heartbeat != null && Time.time >= nextHeartbeat)
        {
            nextHeartbeat = Time.time + 60f / 96f;
            heartbeatLayer.source.PlayOneShot(lib.heartbeat, .55f);
        }
        if (wasScare && !scare && lib != null)
        {
            var breath = GameSounds.Pick(lib.breathScared);
            if (breath != null) heartbeatLayer.source.PlayOneShot(breath, .5f);
        }
        heartbeatLayer.source.volume = 1f;
        wasScare = scare;
    }
}

// Главное меню: тихий дрон и дождь за окном.
public sealed class MenuAmbience : MonoBehaviour
{
    AudioSource drone, rain;

    void Start()
    {
        var lib = GameSounds.Library;
        if (lib == null) return;
        drone = Loop("Menu Drone", lib.droneMenu, AudioBus.Music);
        rain = Loop("Menu Rain", lib.rain, AudioBus.Ambience);
    }

    AudioSource Loop(string name, AudioClip clip, AudioBus bus)
    {
        if (clip == null) return null;
        var source = AudioRouting.Create(transform, name, bus, 0f);
        source.clip = clip; source.loop = true; source.volume = 0f;
        source.Play();
        return source;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (drone != null) drone.volume = Mathf.MoveTowards(drone.volume, .5f, dt * .15f);
        if (rain != null) rain.volume = Mathf.MoveTowards(rain.volume, .28f, dt * .1f);
    }
}
}
