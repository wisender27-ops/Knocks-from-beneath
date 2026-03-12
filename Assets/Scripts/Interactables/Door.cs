using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private float angleRotation = 90f;
    [SerializeField] private float openSpeed = 10f;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float noiseThreshold = 45f; // Порог придется настроить заново

    public AudioSource sfxSource;
    public AudioClip creakClip;

    public bool isOpen;
    private float baseRotationY;
    private float targetRotationY;
    private float startX, startZ;
    private bool isBeingHeld = false;

    // Для расчета реальной скорости двери
    private float previousRotationY;
    private float currentDoorVelocity;

    void Start()
    {
        startX = transform.localEulerAngles.x;
        baseRotationY = transform.localEulerAngles.y;
        startZ = transform.localEulerAngles.z;

        targetRotationY = baseRotationY;
        previousRotationY = baseRotationY;

        if (sfxSource && creakClip)
        {
            sfxSource.clip = creakClip;
            sfxSource.loop = true; // Обязательно зацикливаем звук скрипа!
        }
    }

    void Update()
    {
        if (isBeingHeld)
        {
            HandleManualOpen();
        }

        // 1. Плавно вращаем дверь
        Quaternion targetQuaternion = Quaternion.Euler(startX, targetRotationY, startZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetQuaternion, openSpeed * Time.deltaTime);

        // 2. Считаем реальную скорость самой двери (а не мышки!)
        float currentRotY = transform.localEulerAngles.y;
        float deltaRot = Mathf.DeltaAngle(previousRotationY, currentRotY);
        currentDoorVelocity = Mathf.Abs(deltaRot) / Time.deltaTime;
        previousRotationY = currentRotY;

        // 3. Управляем звуком на основе реальной скорости
        ManageSound(currentDoorVelocity);

        // 4. Триггерим монстра, если дверь захлопнули/открыли слишком сильно
        if (currentDoorVelocity > noiseThreshold)
        {
            TriggerMonsterEvent();
        }

        isOpen = Mathf.Abs(Mathf.DeltaAngle(transform.localEulerAngles.y, baseRotationY)) > 5f;
    }

    private void HandleManualOpen()
    {
        // Берем и X, и Y. Это делает открытие более интуитивным, 
        // так как игрок может тянуть мышь по диагонали или вбок.
        float mouseMove = Input.GetAxis("Mouse X") + Input.GetAxis("Mouse Y");

        if (Mathf.Abs(mouseMove) > 0.01f)
        {
            float moveStep = mouseMove * mouseSensitivity;
            // Примечание: Mathf.Clamp работает хорошо, если baseRotationY не пересекает отметку 360 градусов (например, не переходит с 350 на 10).
            targetRotationY = Mathf.Clamp(targetRotationY + moveStep, baseRotationY, baseRotationY + angleRotation);
        }
    }

    private void ManageSound(float velocity)
    {
        if (sfxSource == null) return;

        // Если дверь движется
        if (velocity > 1f)
        {
            if (!sfxSource.isPlaying) sfxSource.Play();

            // Плавно меняем громкость в зависимости от скорости
            float targetVolume = Mathf.Clamp(velocity * 0.02f, 0.1f, 1.0f);
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, targetVolume, Time.deltaTime * 10f);

            // Бонус: легкое изменение тона (Pitch) для реалистичности
            sfxSource.pitch = Mathf.Clamp(0.8f + (velocity * 0.005f), 0.8f, 1.2f);
        }
        else
        {
            // Если дверь остановилась, плавно затухаем звук, а не обрываем его
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, 0f, Time.deltaTime * 10f);

            if (sfxSource.volume < 0.05f && sfxSource.isPlaying)
            {
                sfxSource.Pause();
            }
        }
    }

    private void TriggerMonsterEvent()
    {
        // Ограничитель, чтобы монстр не спавнился каждый кадр
        if (MonsterWatcherManager.Instance != null)
            MonsterWatcherManager.Instance.SpawnWatcher(Camera.main.transform.position);
    }

    public void StartHolding() => isBeingHeld = true;

    public void StopHolding()
    {
        isBeingHeld = false;
        // Звук сам плавно затухнет благодаря ManageSound
    }

    public void OpenDoor() { targetRotationY = baseRotationY + angleRotation; }
    public void CloseDoor() { targetRotationY = baseRotationY; }
    public void ToggleDoor() { if (isOpen) CloseDoor(); else OpenDoor(); }
}