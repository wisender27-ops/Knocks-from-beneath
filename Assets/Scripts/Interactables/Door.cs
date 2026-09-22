using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class Door : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Настройки двери")]
    [SerializeField] private float angleRotation = 90f;
    [SerializeField] private float openSpeed = 10f;
    [SerializeField] private float automaticOpenSpeed = 4f;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [SerializeField] private bool interactionLocked = false;
    [Header("Петля створки")]
    [SerializeField] private bool useOuterEdgeHinge;
    [SerializeField] private bool hingeAtMaximumWorldX;
    [SerializeField] private bool addGateCollider;

    [Header("Настройки звука (Скрип)")]
    public AudioSource sfxSource;
    public AudioClip doorCreakClip;

    [Range(0.1f, 10f)]
    [SerializeField] private float volumeMultiplier = 1.5f;
    [SerializeField] private float minVelocityThreshold = 2f;

    [Header("Заперто (T-21)")]
    [Tooltip("Звук, когда игрок пытается открыть запертую дверь. Если не задан — просто дёргается без звука.")]
    [SerializeField] private AudioClip lockedRattleClip;
    [SerializeField] private float rattleAngle = 6f;
    [SerializeField] private float rattleDuration = 0.3f;

    // --- НОВАЯ ЛОГИКА IS_OPEN ---
    [SerializeField] private bool _isOpen; // Внутренняя переменная
    public bool isOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value) return; // Если значение не изменилось — ничего не делаем
            _isOpen = value;

            // Если мы НЕ держим дверь руками, реагируем на смену галочки
            if (!isBeingHeld)
            {
                if (_isOpen) OpenDoor();
                else CloseDoor();
            }
        }
    }

    private Vector3 baseLocalEuler;
    private float baseAxisAngle;
    private float targetAxisAngle;
    private bool isBeingHeld = false;

    private float currentOffset = 0f;
    private float previousRotationY;
    private float smoothDoorVelocity;
    private Vector3 closedWorldPosition;
    private Quaternion closedWorldRotation;
    private Vector3 hingeWorldPosition;
    private Quaternion previousWorldRotation;

    void Awake()
    {
        if (addGateCollider && !TryGetComponent<Collider>(out _))
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
        }

        if (useOuterEdgeHinge && sfxSource == null && doorCreakClip != null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 1f;
            sfxSource.minDistance = 1f;
            sfxSource.maxDistance = 12f;
        }
    }

    void Start()
    {
        closedWorldPosition = transform.position;
        closedWorldRotation = transform.rotation;
        previousWorldRotation = closedWorldRotation;
        if (useOuterEdgeHinge)
        {
            Renderer gateRenderer = GetComponent<Renderer>();
            if (gateRenderer != null)
            {
                Bounds bounds = gateRenderer.bounds;
                hingeWorldPosition = new Vector3(
                    hingeAtMaximumWorldX ? bounds.max.x : bounds.min.x,
                    bounds.center.y, bounds.center.z);
            }
            else
            {
                hingeWorldPosition = closedWorldPosition;
            }
        }
        baseLocalEuler = transform.localEulerAngles;
        baseAxisAngle = GetAxisAngle(baseLocalEuler);

        targetAxisAngle = baseAxisAngle;
        previousRotationY = baseAxisAngle;

        if (sfxSource && doorCreakClip)
        {
            sfxSource.clip = doorCreakClip;
            sfxSource.loop = true;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0f;
        }
    }

    void Update()
    {
        if (isBeingHeld)
        {
            HandleManualOpen();
        }

        // Плавный поворот к целевому смещению
        targetAxisAngle = baseAxisAngle + currentOffset;
        if (useOuterEdgeHinge)
        {
            Quaternion swing = Quaternion.AngleAxis(currentOffset, Vector3.up);
            Quaternion targetRotation = swing * closedWorldRotation;
            Vector3 targetPosition = hingeWorldPosition + swing * (closedWorldPosition - hingeWorldPosition);
            float speed = isBeingHeld ? openSpeed : automaticOpenSpeed;
            float smoothing = 1f - Mathf.Exp(-speed * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPosition, smoothing),
                Quaternion.Slerp(transform.rotation, targetRotation, smoothing));
        }
        else
        {
            Quaternion targetQuaternion = BuildRotation(targetAxisAngle);
            float speed = isBeingHeld ? openSpeed : automaticOpenSpeed;
            float smoothing = 1f - Mathf.Exp(-speed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetQuaternion, smoothing);
        }

        // Расчет скорости для звука
        float currentAxisAngle = GetAxisAngle(transform.localEulerAngles);
        float deltaRot = Mathf.DeltaAngle(previousRotationY, currentAxisAngle);
        float rawVelocity = useOuterEdgeHinge
            ? Quaternion.Angle(previousWorldRotation, transform.rotation) / Mathf.Max(Time.deltaTime, 0.0001f)
            : Mathf.Abs(deltaRot) / Mathf.Max(Time.deltaTime, 0.0001f);

        smoothDoorVelocity = Mathf.Lerp(smoothDoorVelocity, rawVelocity, Time.deltaTime * 10f);
        previousRotationY = currentAxisAngle;
        previousWorldRotation = transform.rotation;

        ManageCreakSound(smoothDoorVelocity);

        // Обновляем состояние галочки без вызова сеттера, чтобы просто видеть статус в инспекторе
        // Но лучше оставить управление за пользователем через свойство выше.
        // Чтобы галочка в инспекторе сама не "прыгала", пока ты тянешь дверь:
        if (isBeingHeld) _isOpen = Mathf.Abs(currentOffset) > 5f;
    }

    // Это позволит видеть изменения в инспекторе Unity в реальном времени
    private void OnValidate()
    {
        // Если ты в редакторе клацнул галочку — дверь среагирует
        if (Application.isPlaying)
        {
            if (_isOpen) OpenDoor();
            else CloseDoor();
        }
    }

    private void HandleManualOpen()
    {
        float mouseMove = Input.GetAxis("Mouse Y");

        if (Mathf.Abs(mouseMove) > 0.01f)
        {
            float directionMultiplier = (angleRotation < 0) ? -1f : 1f;
            float moveStep = mouseMove * mouseSensitivity * 5f * directionMultiplier;

            float min = Mathf.Min(0, angleRotation);
            float max = Mathf.Max(0, angleRotation);

            currentOffset = Mathf.Clamp(currentOffset + moveStep, min, max);
        }
    }

    private void ManageCreakSound(float velocity)
    {
        if (sfxSource == null || doorCreakClip == null) return;

        if (velocity > minVelocityThreshold)
        {
            if (!sfxSource.isPlaying)
            {
                sfxSource.Play();
                TriggerMonsterEvent();
            }

            float targetVolume = Mathf.Clamp((velocity / 100f) * volumeMultiplier, 0f, 1f);
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, targetVolume, Time.deltaTime * 12f);
            sfxSource.pitch = Mathf.Clamp(0.85f + (velocity * 0.003f), 0.85f, 1.15f);
        }
        else
        {
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, 0f, Time.deltaTime * 15f);
            if (sfxSource.volume < 0.01f && sfxSource.isPlaying)
                sfxSource.Pause();
        }
    }

    public void CloseDoor()
    {
        currentOffset = 0f;
        _isOpen = false;
    }

    public void OpenDoor()
    {
        currentOffset = angleRotation;
        _isOpen = true;
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen; // Используем свойство
    }

    private void OnEnable()
    {
        GameEvents.OnNightStarted += ForceClose; // Подписываемся
    }

    private void OnDisable()
    {
        GameEvents.OnNightStarted -= ForceClose; // Отписываемся (обязательно для защиты от утечек памяти)
    }

    private void ForceClose()
    {
        isOpen = false; // Используем твое свойство, оно само запустит звук и анимацию
    }

    private void TriggerMonsterEvent()
    {
        Camera camera = Camera.main;
        if (MonsterWatcherManager.Instance != null && camera != null)
            MonsterWatcherManager.Instance.SpawnWatcher(camera.transform.position);
    }

    private float GetAxisAngle(Vector3 euler)
    {
        switch (rotationAxis)
        {
            case RotationAxis.X: return euler.x;
            case RotationAxis.Z: return euler.z;
            default: return euler.y;
        }
    }

    private Quaternion BuildRotation(float axisAngle)
    {
        Vector3 euler = baseLocalEuler;
        switch (rotationAxis)
        {
            case RotationAxis.X:
                euler.x = axisAngle;
                break;
            case RotationAxis.Z:
                euler.z = axisAngle;
                break;
            default:
                euler.y = axisAngle;
                break;
        }
        return Quaternion.Euler(euler);
    }

    public bool IsInteractionLocked => interactionLocked;

    public void SetInteractionLocked(bool isLocked)
    {
        interactionLocked = isLocked;
        if (interactionLocked)
        {
            isBeingHeld = false;
            CloseDoor();
        }
    }

    public void StartHolding()
    {
        if (interactionLocked) return;
        isBeingHeld = true;
    }

    public void StopHolding() => isBeingHeld = false;

    // T-21: раньше попытка открыть запертую дверь не давала игроку никакой реакции — тишина,
    // будто дверь просто не заметила клик. Дёргаем ручку и проигрываем звук, чтобы "заперто"
    // читалось физически, а не только по отсутствию отклика.
    public void PlayLockedFeedback()
    {
        if (!interactionLocked) return;
        if (sfxSource != null && lockedRattleClip != null)
            sfxSource.PlayOneShot(lockedRattleClip);
        StopCoroutine(nameof(RattleRoutine));
        StartCoroutine(RattleRoutine());
    }

    IEnumerator RattleRoutine()
    {
        float t = 0f;
        while (t < rattleDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - t / rattleDuration;
            currentOffset = Mathf.Sin(t * 40f) * rattleAngle * decay;
            yield return null;
        }
        currentOffset = 0f;
    }
}
}
