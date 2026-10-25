using UnityEngine;

namespace KnocksFromBeneath
{

// Порядок 10010 — ПОСЛЕ PlayerController (10000) и ДО ProtagonistBody (10020).
// Раньше атрибута не было, поэтому Update() выполнялся в порядке 0, то есть ДО
// PlayerController, и компонент читал controller.velocity прошлого кадра — до того,
// как CharacterController.Move() её обновил. На беге (4 м/с) это давало постоянное
// отставание покачивания на кадр, и голова шла позади тела. 10010 ставит нас ровно
// между: скорость уже актуальна, но тело/руки ещё не пересчитали позу.
[DefaultExecutionOrder(10010)]
public class HeadBobbing : MonoBehaviour
{
    [Header("Настройки веса")]
    public float walkSpeed = 10f;

    // Высоту камеры задаёт PlayerController.
    [Range(0f, 0.3f)]
    public float bobIntensity = 0f;

    // Наклон задаётся в ГРАДУСАХ (Quaternion.Euler). Диапазон узкий сознательно:
    // это микропокачивание, а не крен.
    [Range(0f, 3f)]
    public float tiltIntensity = 0.45f;

    [Header("Инерция")]
    public float smoothness = 6f; // Насколько "вязко" движется голова

    private float _timer;
    private Vector3 _targetPos;
    private Quaternion _targetRot;
    private Vector3 _initialLocalPos;
    // Исходный наклон узла. Без него в покое камера возвращалась в Quaternion.identity,
    // то есть выравнивалась по мировым осям, и при остановке/приседании дёргалась,
    // а на наклонённой камере ещё и «отъезжала». Теперь покой — это возврат К ИСХОДНОЙ позе.
    private Quaternion _initialLocalRot;
    private CharacterController _controller;

    [Header("Звуки шагов")]
    public AudioSource footstepSource; // Сюда перетащи AudioSource с игрока
    public AudioClip[] concreteSteps;
    public AudioClip[] metalSteps;
    public AudioClip[] woodSteps;
    public AudioClip[] stairSteps;
    private AudioClip _lastStep;

    private bool _stepPlayed = false;
    private PlayerController _player;
    private bool _wasCrouching;

    // A-01: поверхность по тегу, затем по материалу коллайдера, затем по месту (дом/улица/подвал).
    // Клипы — из GameSoundLibrary; массивы в инспекторе остаются запасным вариантом.
    private AudioClip[] SelectSteps(RaycastHit hit, out float volume)
    {
        volume = 1f;
        var lib = GameSounds.Library;
        string tag = hit.collider.tag;
        if (lib == null)
            return tag == "Metal" ? metalSteps : tag == "Wood" ? woodSteps : tag == "Stairs" ? stairSteps : concreteSteps;
        switch (tag)
        {
            case "Metal": return lib.stepMetal;
            case "Wood": return lib.stepWood;
            case "Stairs": volume = 1.1f; return lib.stepStairs;
            case "Grass": volume = .9f; return lib.stepGrass;
            case "Concreate":
            case "Concrete": return lib.stepConcrete;
        }
        if (hit.collider is TerrainCollider) { volume = .9f; return lib.stepGrass; }
        var renderer = hit.collider.GetComponent<Renderer>();
        string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
        if (Contains(material, "grass") || Contains(material, "lawn")) { volume = .9f; return lib.stepGrass; }
        if (Contains(material, "carpet") || Contains(material, "rug")) { volume = .8f; return lib.stepCarpet; }
        if (Contains(material, "metal") || Contains(material, "iron") || Contains(material, "steel")) return lib.stepMetal;
        if (Contains(material, "concrete") || Contains(material, "asphalt") || Contains(material, "stone") || Contains(material, "tile"))
            return lib.stepConcrete;
        if (Contains(material, "dirt") || Contains(material, "ground") || Contains(material, "soil") || Contains(material, "gravel"))
            return lib.stepDirt;
        if (AmbienceDirector.ListenerInBasement) return lib.stepConcrete;
        return AmbienceDirector.ListenerInside ? lib.stepWood : lib.stepDirt;
    }

    private static bool Contains(string text, string part) => text.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

    // Шорох одежды при смене позы.
    private void PlayCrouchFoley()
    {
        bool crouching = _player != null && _player.IsCrouching;
        if (crouching == _wasCrouching) return;
        _wasCrouching = crouching;
        var clip = GameSounds.Pick(GameSounds.Library?.cloth);
        if (clip != null && footstepSource != null && Time.timeScale > 0f && !AudioListener.pause)
            footstepSource.PlayOneShot(clip, crouching ? .7f : .5f);
    }

    void Start()
    {
        _controller = GetComponentInParent<CharacterController>();
        _player = GetComponentInParent<PlayerController>();
        _wasCrouching = _player != null && _player.IsCrouching;
        if (footstepSource != null) AudioRouting.Assign(footstepSource, AudioBus.Foley);
        _initialLocalPos = transform.localPosition;
        _targetPos = _initialLocalPos;
        _initialLocalRot = transform.localRotation;
        _targetRot = _initialLocalRot;
    }

    void Update()
    {
        if (_controller == null)
            return;
        PlayCrouchFoley();

        Vector3 vel = new Vector3(_controller.velocity.x, 0, _controller.velocity.z);
        float speed = vel.magnitude;

        if (speed > 0.2f && _controller.isGrounded)
        {
            // Ускоряем таймер в зависимости от реальной скорости
            _timer += Time.deltaTime * (speed * walkSpeed * 0.5f);

            // Создаем движение "восьмеркой" (более физично, чем круг)
            float waveY = Mathf.Sin(_timer);

            // Если синусоида в нижней точке (шаг произошел)
            if (waveY < -0.9f && !_stepPlayed)
            {
                PlayFootstepSound();
                _stepPlayed = true;
            }

            // Сбрасываем флаг, когда голова пошла вверх
            if (waveY > 0) _stepPlayed = false;

            float waveX = Mathf.Cos(_timer * 0.5f);

            _targetPos = _initialLocalPos + new Vector3(waveX * bobIntensity * 0.5f, waveY * bobIntensity, 0);

            // Сильный наклон камеры (Z) при переносе веса
            _targetRot = Quaternion.Euler(0, 0, -waveX * tiltIntensity);
        }
        else
        {
            _timer = 0;
            _targetPos = _initialLocalPos;
            // К ИСХОДНОМУ наклону, а не в identity: иначе при остановке и смене позы
            // камера выравнивалась по мировым осям и дёргалась.
            _targetRot = _initialLocalRot;
        }

        // Экспоненциальное сглаживание вместо Mathf.Lerp(a, b, dt * k).
        // Вариант с dt в множителе НЕ инвариантен к частоте кадров: на 30 fps
        // t=0.267, на 144 fps t=0.056 — на слабой машине камера догоняла тело
        // заметно медленнее. 1 - exp(-k*dt) даёт одинаковую постоянную времени
        // при любом FPS (k = скорость реакции, 1/с).
        float smoothing = 1f - Mathf.Exp(-Mathf.Max(0f, smoothness) * Time.deltaTime);
        Vector3 position = Vector3.Lerp(transform.localPosition, _targetPos, smoothing);
        // Высоту CameraOffset задаёт контроллер, включая удержание двери.
        position.y = transform.localPosition.y;
        transform.localPosition = position;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, smoothing);
    }

    void PlayFootstepSound()
    {
        if (_controller == null || footstepSource == null || AudioListener.pause || Time.timeScale <= 0f) return;
        RaycastHit hit;
        // Стреляем лучом вниз, чтобы понять, на чем стоим
        // (слой-блокер исключён: калитка под ногами не должна менять звук шага)
        Bounds bodyBounds = _controller.bounds;
        if (Physics.Raycast(bodyBounds.center, Vector3.down, out hit, bodyBounds.extents.y + .4f,
            PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore))
        {
            AudioClip[] selectedArray = SelectSteps(hit, out float surfaceVolume);
            if (selectedArray != null && selectedArray.Length > 0)
            {
                // Берем случайный звук из выбранного массива
                int index = Random.Range(0, selectedArray.Length);
                if (selectedArray.Length > 1 && selectedArray[index] == _lastStep)
                    index = (index + 1 + Random.Range(0, selectedArray.Length - 1)) % selectedArray.Length;
                AudioClip clip = selectedArray[index];
                if (clip == null) return;
                _lastStep = clip;

                // Немного меняем высоту звука (Pitch), чтобы шаги не были одинаковыми
                if (footstepSource == null)
                    return;

                footstepSource.pitch = Random.Range(0.94f, 1.05f);
                float speed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
                float volume = Mathf.Lerp(.45f, 1f, Mathf.InverseLerp(1f, 4f, speed)) * surfaceVolume * Random.Range(.88f, 1f);
                if (_player != null && _player.IsCrouching) volume *= .45f;
                footstepSource.PlayOneShot(clip, volume);
            }
        }
    }
}
}
