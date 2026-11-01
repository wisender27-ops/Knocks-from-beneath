using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // P-04: дедлайн у люка без цифр. Пока идёт MonsterTimer, всё нарастает по одной шкале: удары снизу в люк,
    // стук хозяина, сердце и дыхание героя, дрожь, фонарик, помехи. Последние секунды — тишина, потом люк
    // распахивается: заколотить уже нельзя (FinaleController пойдёт по ветке deadlineExpired → E2).
    // Логика чисел — DeadlineScale. Пауза, инвентарь и модальное чтение останавливают таймер, а с ним всё здесь.
    public sealed class DiegeticDeadline : MonoBehaviour
    {
        private const int TapeWidth = 160, TapeHeight = 90;
        private const float TapeInterval = 0.08f;
        private const float TapeAlpha = 0.34f, VignetteAlpha = 0.8f;
        private const float DimmedLight = 0.3f;

        private static readonly string[] FrontLines = { "Быстрее…" };
        private static readonly string[] BackLines = { "Оно уже у створки." };
        private static readonly string[] TooLateLines = { "Поздно. Люк открыт.", "Оно вышло." };

        private BasementHatch _hatch;
        private PlayerInventory _inventory;
        private PhoneFlashlight _phone;
        private Transform _player;
        private MonsterTimer _timer;

        private AudioSource _blowSource, _ownerSource, _bodySource;
        private AudioClip _slam, _thud, _rust, _knock, _heart;
        private AudioClip[] _breaths;
        private ParticleSystem _dust;
        private Material _dustMaterial;

        private bool _running, _silent, _frontShown, _backShown, _tooLatePending;
        private float _nextBlow, _nextKnock, _nextHeart, _nextBreath, _nextTremble, _nextTape, _nextBlackout, _blackoutLeft;
        private int _knockStep;

        private Light _flashlight, _phoneBeam;
        private float _flashlightBase, _phoneBase;

        private GameObject _overlay;
        private RawImage _vignette, _tape;
        private Texture2D _vignetteTexture, _tapeTexture;
        private Color32[] _tapePixels;

        public bool IsPresenting => _running;
        public bool IsSilent => _silent;
        public int Blows { get; private set; }
        public int Knocks { get; private set; }
        public float LastTension { get; private set; }

        public void Initialize(BasementHatch hatch, PlayerInventory inventory, PhoneFlashlight phone, Transform player)
        {
            _hatch = hatch;
            _inventory = inventory;
            _phone = phone;
            _player = player;
        }

        private void OnEnable() => GameEvents.OnMonsterTimerExpired += HandleExpired;

        private void OnDisable()
        {
            GameEvents.OnMonsterTimerExpired -= HandleExpired;
            Finish();
        }

        private void OnDestroy()
        {
            if (_timer != null) _timer.Stopped -= Finish;
            if (_dustMaterial != null) Destroy(_dustMaterial);
            if (_vignetteTexture != null) Destroy(_vignetteTexture);
            if (_tapeTexture != null) Destroy(_tapeTexture);
        }

        private void Update()
        {
            if (_tooLatePending && ThoughtManager.Instance != null &&
                ThoughtManager.Instance.TryShowInteractionThoughts("deadline-too-late", TooLateLines))
                _tooLatePending = false;

            // MonsterTimer живёт на своём объекте сцены: в Awake IntroSequence его Instance может быть ещё пуст.
            if (_timer == null)
            {
                _timer = MonsterTimer.Instance;
                if (_timer == null) return;
                _timer.Stopped += Finish;
            }
            if (!_timer.IsRunning || _hatch == null) return;
            if (!_running) Begin();
            if (!_timer.IsTicking || Time.deltaTime <= 0f) return;

            float tension = _timer.Progress;
            LastTension = tension;
            float now = Time.time;
            bool silent = DeadlineScale.IsSilent(_timer.Remaining);
            if (silent && !_silent) EnterSilence();

            UpdateLight(tension, now);
            UpdateOverlay(tension, now);
            if (_silent) return;

            if (now >= _nextBlow) Blow(tension, now);
            if (now >= _nextKnock) OwnerKnock(tension, now);
            UpdateBody(tension, now);
            ShowThoughts(tension);
        }

        private void Begin()
        {
            _running = true;
            _silent = false;
            _frontShown = _backShown = false;
            _knockStep = 0;
            Blows = Knocks = 0;
            float now = Time.time;
            _nextBlow = now + 1.5f;
            _nextKnock = now + 3f;
            _nextHeart = now + 1f;
            _nextBreath = now + 6f;
            _nextTremble = 0f;
            _nextBlackout = 0f;
            _blackoutLeft = 0f;
            EnsureWorldPresentation();
            // Оно ушло вниз и захлопнуло люк за собой: только закрытые створки могут подпрыгивать от ударов.
            _hatch.CloseForNailing();
            BuildOverlay();
            CaptureLight();
        }

        private void EnsureWorldPresentation()
        {
            if (_blowSource != null) return;
            _slam = Resources.Load<AudioClip>("Audio/Horror/impactMetal_heavy_001");
            _thud = Resources.Load<AudioClip>("Audio/Horror/impactPunch_heavy_000");
            _rust = Resources.Load<AudioClip>("Audio/Horror/PlateScrape");
            var library = GameSounds.Library;
            _heart = library != null ? library.heartbeat : null;
            _breaths = library != null ? library.breathScared : null;
            var intro = GetComponent<IntroSequence>();
            var knock = intro != null && intro.knockController != null ? intro.knockController.GetComponent<RandomKnock>() : null;
            _knock = knock != null ? knock.ShortestClip : null;
            if (_knock == null) _knock = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");

            // Слышно с любой точки участка и из дома: люк — главный «циферблат».
            _blowSource = HatchSource("Deadline Blows");
            _ownerSource = HatchSource("Deadline Owner Knock");
            _ownerSource.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900f;
            _bodySource = AudioRouting.Create(transform, "Deadline Body", AudioBus.Horror, 0f);
            CreateDust();
        }

        private AudioSource HatchSource(string sourceName)
        {
            AudioSource source = AudioRouting.Create(_hatch.transform, sourceName, AudioBus.Horror, 0.7f, 70f);
            source.minDistance = 8f;
            return source;
        }

        private void CreateDust()
        {
            var obj = new GameObject("Deadline Dust");
            obj.transform.SetParent(_hatch.transform, false);
            obj.transform.position = _hatch.transform.position + Vector3.up * 0.2f;
            _dust = obj.AddComponent<ParticleSystem>();
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.2f, 0.12f, 0.9f), new Color(0.55f, 0.5f, 0.42f, 0.7f));
            main.gravityModifier = 0.7f;
            main.maxParticles = 120;
            var emission = _dust.emission;
            emission.enabled = false;
            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.8f, 0.05f, 1.4f);
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _dustMaterial = new Material(shader);
                obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = _dustMaterial;
            }
        }

        // Удар снизу: редкий и глухой → частый и сильный, створки подпрыгивают, сыплется ржавчина.
        private void Blow(float tension, float now)
        {
            float strength = DeadlineScale.BlowStrength(tension);
            _nextBlow = now + DeadlineScale.BlowInterval(tension) * Random.Range(0.85f, 1.15f);
            Blows++;
            Play(_blowSource, _thud, Random.Range(0.7f, 0.85f), 0.45f + 0.5f * strength);
            if (tension > 0.3f) Play(_blowSource, _slam, Random.Range(0.75f, 0.92f), strength);
            if (tension > 0.55f && Random.value < 0.5f) Play(_blowSource, _rust, Random.Range(0.6f, 0.8f), 0.5f * strength);
            _hatch.Jolt(1.5f + 11f * strength);
            if (_dust != null) _dust.Emit(Mathf.RoundToInt(Mathf.Lerp(4f, 34f, strength)));
            float distance = _player != null ? Vector3.Distance(_player.position, _hatch.transform.position) : 0f;
            PlayerFloorKnock.Instance?.Jolt((0.2f + 1.1f * strength) * Mathf.Clamp(1f - distance / 40f, 0.35f, 1f));
            _nextTremble = now + 0.45f;
        }

        // Стук хозяина: перестук «2·1» → беспорядочный → отчаянный.
        private void OwnerKnock(float tension, float now)
        {
            OwnerKnockMood mood = DeadlineScale.Mood(tension);
            _nextKnock = now + DeadlineScale.KnockGap(mood, _knockStep, Random.value);
            _knockStep++;
            Knocks++;
            float pitch = mood == OwnerKnockMood.Ordered ? 0.95f : Random.Range(1f, 1.3f);
            Play(_ownerSource, _knock, pitch, mood == OwnerKnockMood.Desperate ? 1f : 0.7f);
        }

        private void UpdateBody(float tension, float now)
        {
            if (_heart != null && now >= _nextHeart)
            {
                _nextHeart = now + DeadlineScale.HeartInterval(tension);
                Play(_bodySource, _heart, 1f, Mathf.Lerp(0.2f, 0.75f, tension));
            }
            if (tension > 0.3f && now >= _nextBreath && _breaths != null)
            {
                _nextBreath = now + DeadlineScale.BreathInterval(tension);
                Play(_bodySource, GameSounds.Pick(_breaths), Mathf.Lerp(1f, 1.2f, tension), Mathf.Lerp(0.3f, 0.6f, tension));
            }
            if (tension >= DeadlineScale.TrembleFrom && now >= _nextTremble)
            {
                _nextTremble = now + 0.4f;
                PlayerFloorKnock.Instance?.Jolt(Mathf.Lerp(0.15f, 0.5f, Mathf.InverseLerp(DeadlineScale.TrembleFrom, 1f, tension)));
            }
        }

        private void ShowThoughts(float tension)
        {
            if (ThoughtManager.Instance == null) return;
            if (!_frontShown && tension >= DeadlineScale.FrontThought)
                _frontShown = ThoughtManager.Instance.TryShowInteractionThoughts("deadline-front", FrontLines);
            if (!_backShown && tension >= DeadlineScale.BackThought)
                _backShown = ThoughtManager.Instance.TryShowInteractionThoughts("deadline-back", BackLines);
        }

        private void EnterSilence()
        {
            _silent = true;
            RestoreLight();
            if (_tape != null) _tape.enabled = false;
            _blowSource?.Stop();
            _ownerSource?.Stop();
            _bodySource?.Stop();
            AmbienceDirector.Instance?.Hush(DeadlineScale.SilenceSeconds + 0.5f);
        }

        private void HandleExpired()
        {
            if (!_running || _hatch == null) return;
            // Вот теперь ясно, чем кончится: люк распахнут, заколачивать нечего.
            _hatch.OpenForCinematic();
            Play(_blowSource, _slam, 0.7f, 1f);
            Play(_blowSource, _thud, 0.65f, 1f);
            var library = GameSounds.Library;
            if (library != null)
            {
                Play(_bodySource, GameSounds.Pick(library.subHits), 1f, 0.9f);
                Play(_bodySource, GameSounds.Pick(library.darkHits), 1f, 0.8f);
            }
            if (_dust != null) _dust.Emit(60);
            PlayerFloorKnock.Instance?.Jolt(1.2f);
            _tooLatePending = true;
            Finish();
        }

        private void Finish()
        {
            if (!_running) return;
            _running = false;
            _silent = false;
            RestoreLight();
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            _tape = _vignette = null;
        }

        // Свет: фонарик и телефон мигают; на последней трети — короткие провалы в темноту.
        private void CaptureLight()
        {
            _flashlight = _inventory != null ? _inventory.flashlightLightSource : null;
            if (_flashlight != null) _flashlightBase = _flashlight.intensity;
            _phoneBeam = null;
        }

        private void UpdateLight(float tension, float now)
        {
            if (_silent) return;
            Light phone = _phone != null ? _phone.Beam : null;
            if (phone != _phoneBeam)
            {
                _phoneBeam = phone;
                if (phone != null) _phoneBase = phone.intensity;
            }
            float level = 1f;
            if (tension >= DeadlineScale.FlickerFrom &&
                Mathf.PerlinNoise(now * 11f, 0.37f) > DeadlineScale.FlickerThreshold(tension))
                level = DimmedLight;
            if (tension >= DeadlineScale.BlackoutsFrom)
            {
                if (_blackoutLeft > 0f)
                {
                    _blackoutLeft -= Time.deltaTime;
                    level = 0f;
                }
                else if (now >= _nextBlackout)
                {
                    _nextBlackout = now + Random.Range(2.2f, 3.8f);
                    _blackoutLeft = Random.Range(0.2f, 0.45f);
                }
            }
            if (_flashlight != null) _flashlight.intensity = _flashlightBase * level;
            if (_phoneBeam != null) _phoneBeam.intensity = _phoneBase * level;
        }

        private void RestoreLight()
        {
            if (_flashlight != null) _flashlight.intensity = _flashlightBase;
            if (_phoneBeam != null) _phoneBeam.intensity = _phoneBase;
        }

        // Картинка: виньетка и плёночный снег поверх кадра. Профиль RetroPSX общий ассет, его не трогаем.
        private void BuildOverlay()
        {
            if (_overlay != null) return;
            Canvas canvas = UiStyle.OverlayCanvas(null, "DeadlineTape", 40, new Vector2(1280f, 720f), false);
            _overlay = canvas.gameObject;
            _tape = FullScreen("Snow", TapeTexture());
            _vignette = FullScreen("Vignette", VignetteTexture());
        }

        private RawImage FullScreen(string imageName, Texture texture)
        {
            RectTransform rect = UiStyle.Rect(imageName, _overlay.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return image;
        }

        private Texture2D TapeTexture()
        {
            if (_tapeTexture != null) return _tapeTexture;
            _tapeTexture = new Texture2D(TapeWidth, TapeHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _tapePixels = new Color32[TapeWidth * TapeHeight];
            return _tapeTexture;
        }

        private Texture2D VignetteTexture()
        {
            if (_vignetteTexture != null) return _vignetteTexture;
            const int size = 64;
            _vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.3f, Mathf.Sqrt(u * u + v * v)));
                    pixels[x + y * size] = new Color32(0, 0, 0, (byte)(edge * 255f));
                }
            _vignetteTexture.SetPixels32(pixels);
            _vignetteTexture.Apply();
            return _vignetteTexture;
        }

        private void UpdateOverlay(float tension, float now)
        {
            if (_vignette != null) _vignette.color = new Color(1f, 1f, 1f, VignetteAlpha * tension);
            if (_tape == null || _silent) return;
            _tape.color = new Color(1f, 1f, 1f, TapeAlpha * tension);
            if (now < _nextTape) return;
            _nextTape = now + TapeInterval;
            int band = Random.Range(0, TapeHeight), bandHeight = 3 + Mathf.RoundToInt(tension * 6f);
            for (int y = 0; y < TapeHeight; y++)
            {
                bool inBand = y >= band && y < band + bandHeight;
                for (int x = 0; x < TapeWidth; x++)
                {
                    byte value = (byte)Random.Range(30, 255);
                    byte alpha = (byte)(inBand ? 200 : Random.value < 0.25f ? 90 : 0);
                    _tapePixels[x + y * TapeWidth] = new Color32(value, value, value, alpha);
                }
            }
            _tapeTexture.SetPixels32(_tapePixels);
            _tapeTexture.Apply();
        }

        private static void Play(AudioSource source, AudioClip clip, float pitch, float volume)
        {
            if (source == null || clip == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
