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

    void Start()
    {
        _controller = GetComponentInParent<CharacterController>();
        _initialLocalPos = transform.localPosition;
        _targetPos = _initialLocalPos;
        _initialLocalRot = transform.localRotation;
        _targetRot = _initialLocalRot;
    }

    void Update()
    {
        if (_controller == null)
            return;

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
            AudioClip[] selectedArray = null;

            // Проверяем тег поверхности
            switch (hit.collider.tag)
            {
                case "Metal":
                    selectedArray = metalSteps;
                    break;
                case "Wood":
                    selectedArray = woodSteps;
                    break;
                case "Stairs":
                    selectedArray = stairSteps;
                    break;
                default: // Если тег Concrete или любой другой
                    selectedArray = concreteSteps;
                    break;
            }

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

                footstepSource.pitch = Random.Range(0.96f, 1.04f);
                float speed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
                footstepSource.PlayOneShot(clip, Mathf.Lerp(.55f, 1f, Mathf.InverseLerp(1f, 4f, speed)));
            }
        }
    }
}
}
