using UnityEngine;

public class HeadBobbing : MonoBehaviour
{
    [Header("Настройки веса")]
    public float walkSpeed = 10f;
    public float bobIntensity = 0.15f; // Увеличил, чтобы было заметно
    public float tiltIntensity = 2.0f; // Наклон в стороны

    [Header("Инерция")]
    public float smoothness = 6f; // Насколько "вязко" движется голова

    private float _timer;
    private Vector3 _targetPos;
    private Quaternion _targetRot;
    private Vector3 _initialLocalPos;
    private CharacterController _controller;

    [Header("Звуки шагов")]
    public AudioSource footstepSource; // Сюда перетащи AudioSource с игрока
    public AudioClip[] concreteSteps;
    public AudioClip[] metalSteps;
    public AudioClip[] woodSteps;
    public AudioClip[] stairSteps;

    private bool _stepPlayed = false;

    void Start()
    {
        _controller = GetComponentInParent<CharacterController>();
        _initialLocalPos = transform.localPosition;
    }

    void Update()
    {
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
            _targetRot = Quaternion.identity;
        }

        // Плавное следование за целью (вязкость)
        transform.localPosition = Vector3.Lerp(transform.localPosition, _targetPos, Time.deltaTime * smoothness);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, Time.deltaTime * smoothness);
    }

    void PlayFootstepSound()
    {
        RaycastHit hit;
        // Стреляем лучом вниз, чтобы понять, на чем стоим
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 2.0f))
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
                AudioClip clip = selectedArray[Random.Range(0, selectedArray.Length)];

                // Немного меняем высоту звука (Pitch), чтобы шаги не были одинаковыми
                footstepSource.pitch = Random.Range(0.9f, 1.1f);
                footstepSource.PlayOneShot(clip);
            }
        }
    }
}