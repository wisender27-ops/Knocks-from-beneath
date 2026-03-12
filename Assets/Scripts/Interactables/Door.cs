using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Настройки двери")]
    [SerializeField] private float angleRotation = 90f;
    [SerializeField] private float openSpeed = 10f;
    [SerializeField] private float mouseSensitivity = 3f;

    [Header("Настройки звука (Скрип)")]
    public AudioSource sfxSource;
    public AudioClip doorCreakClip;

    [Range(0.1f, 10f)]
    [SerializeField] private float volumeMultiplier = 1.5f;
    [SerializeField] private float minVelocityThreshold = 2f;

    public bool isOpen;
    private float baseRotationY;
    private float targetRotationY;
    private float startX, startZ;
    private bool isBeingHeld = false;

    private float currentOffset = 0f;
    private float previousRotationY;
    private float smoothDoorVelocity;

    void Start()
    {
        startX = transform.localEulerAngles.x;
        baseRotationY = transform.localEulerAngles.y;
        startZ = transform.localEulerAngles.z;

        targetRotationY = baseRotationY;
        previousRotationY = baseRotationY;

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

        targetRotationY = baseRotationY + currentOffset;
        Quaternion targetQuaternion = Quaternion.Euler(startX, targetRotationY, startZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetQuaternion, openSpeed * Time.deltaTime);

        float currentRotY = transform.localEulerAngles.y;
        float deltaRot = Mathf.DeltaAngle(previousRotationY, currentRotY);
        float rawVelocity = Mathf.Abs(deltaRot) / Time.deltaTime;

        smoothDoorVelocity = Mathf.Lerp(smoothDoorVelocity, rawVelocity, Time.deltaTime * 10f);
        previousRotationY = currentRotY;

        // Управление звуком и ивентом
        ManageCreakSound(smoothDoorVelocity);

        isOpen = Mathf.Abs(currentOffset) > 5f;
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
            // Если звук еще не играет, значит это момент начала скрипа
            if (!sfxSource.isPlaying)
            {
                sfxSource.Play();
                TriggerMonsterEvent(); // Срабатывает один раз при старте звука
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
        isBeingHeld = false;
    }

    public void OpenDoor()
    {
        currentOffset = angleRotation;
    }

    public void ToggleDoor()
    {
        if (isOpen) CloseDoor();
        else OpenDoor();
    }

    private void TriggerMonsterEvent()
    {
        if (MonsterWatcherManager.Instance != null)
            MonsterWatcherManager.Instance.SpawnWatcher(Camera.main.transform.position);
    }

    public void StartHolding() => isBeingHeld = true;
    public void StopHolding() => isBeingHeld = false;
}