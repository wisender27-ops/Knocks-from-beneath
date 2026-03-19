using UnityEngine;
using System.Collections;

public class HoleEventController : MonoBehaviour
{
    [Header("Ссылки")]
    public GameObject monsterFace;
    public AudioSource jumpscareAudio;
    public PlayerInventory inventory;

    [Header("Настройки")]
    public float lookDistance = 3f;
    public float minTimeToTrigger = 2f;  // Случайный таймер — минимум
    public float maxTimeToTrigger = 5f;  // Случайный таймер — максимум

    [Header("Звуки напряжения (до скримера)")]
    public AudioSource tensionAudioSource;
    public AudioClip[] tensionSounds;    // Дыхание, скрежет, шёпот

    [Header("Звук нарастания (перед вспышкой)")]
    public AudioClip buildupClip;        // Нарастающий звук перед скримером

    [Header("Настройки камеры")]
    public float cameraShakeIntensity = 0.05f;
    public float cameraDipAmount = 0.3f; // Насколько камера дёргается вниз

    private float _lookTimer = 0f;
    private float _triggerTime;          // Случайное время до скримера
    private bool _eventStarted = false;
    private bool _tensionSoundPlayed = false;
    private Light _flashlightLight;
    private Camera _playerCam;

    void Start()
    {
        if (monsterFace != null) monsterFace.SetActive(false);
        if (inventory != null) _flashlightLight = inventory.flashlightLightSource;

        // Находим камеру игрока
        _playerCam = Camera.main;

        // Генерируем случайное время до скримера
        _triggerTime = Random.Range(minTimeToTrigger, maxTimeToTrigger);
    }

    void Update()
    {
        if (_eventStarted) return;

        bool isFlashlightOn = inventory.hasFlashlight &&
                              _flashlightLight != null &&
                              _flashlightLight.enabled;

        if (IsPlayerLookingAtHole())
        {
            if (!isFlashlightOn)
            {
                Debug.Log("Здесь слишком темно. Нужен фонарик.");
                _lookTimer = 0f;
                return;
            }

            _lookTimer += Time.deltaTime;

            // На середине таймера — играем случайный звук напряжения
            if (!_tensionSoundPlayed && _lookTimer >= _triggerTime * 0.5f)
            {
                PlayRandomTensionSound();
                _tensionSoundPlayed = true;
            }

            if (_lookTimer >= _triggerTime)
                StartCoroutine(TheVoidSequence());
        }
        else
        {
            // Если игрок отвёл взгляд — сбрасываем всё
            _lookTimer = 0f;
            _tensionSoundPlayed = false;

            // Новое случайное время — каждый раз разное
            _triggerTime = Random.Range(minTimeToTrigger, maxTimeToTrigger);
        }
    }

    bool IsPlayerLookingAtHole()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, lookDistance))
            return hit.collider.CompareTag("FloorBoards");
        return false;
    }

    void PlayRandomTensionSound()
    {
        if (tensionAudioSource == null || tensionSounds.Length == 0) return;
        AudioClip clip = tensionSounds[Random.Range(0, tensionSounds.Length)];
        tensionAudioSource.PlayOneShot(clip);
    }

    IEnumerator TheVoidSequence()
    {
        _eventStarted = true;

        // 1. НАРАСТАЮЩИЙ ЗВУК
        if (tensionAudioSource != null && buildupClip != null)
            tensionAudioSource.PlayOneShot(buildupClip);

        yield return new WaitForSeconds(0.5f);

        // 2. МИГАНИЕ ФОНАРИКА — нарастающее
        yield return StartCoroutine(FlickerFlashlight(2, 0.15f));
        yield return StartCoroutine(FlickerFlashlight(2, 0.08f));
        yield return StartCoroutine(FlickerFlashlight(2, 0.04f));

        // 3. СВЕТ В ДОМЕ ГАСНЕТ + ФОНАРИК ГАСНЕТ ОДНОВРЕМЕННО
        if (LightingManager.Instance != null)
            LightingManager.Instance.TurnOffAllLamps();

        _flashlightLight.enabled = false;
        PlayFlashlightSound(inventory.soundOff);

        yield return new WaitForSeconds(0.8f);

        // 4. МОНСТР ПОЯВЛЯЕТСЯ
        monsterFace.SetActive(true);
        _flashlightLight.enabled = true;
        PlayFlashlightSound(inventory.soundOn);

        if (jumpscareAudio != null) jumpscareAudio.Play();

        // 5. РЕЗКИЙ ЗУМ К МОНСТРУ
        StartCoroutine(ZoomToMonster());

        // 6. КАМЕРА ДЁРГАЕТСЯ ВНИЗ + ТРЯСКА
        StartCoroutine(CameraDip());
        StartCoroutine(CameraShake(1.5f));

        yield return new WaitForSeconds(1.5f);

        // 7. ПАРАЛИЧ
        yield return StartCoroutine(Paralyze(0.8f));

        // 8. ФОНАРИК ГАСНЕТ, МОНСТР ИСЧЕЗАЕТ
        _flashlightLight.enabled = false;
        PlayFlashlightSound(inventory.soundOff);

        yield return new WaitForSeconds(0.4f);
        monsterFace.SetActive(false);

        yield return new WaitForSeconds(0.3f);

        // 9. ФОНАРИК ВКЛЮЧАЕТСЯ
        _flashlightLight.enabled = true;
        PlayFlashlightSound(inventory.soundOn);

        // 10. ЗАВЕРШАЕМ КВЕСТ
        QuestManager.Instance.AddProgress(1);
        this.enabled = false;
    }

    // Резкое приближение камеры к монстру
    IEnumerator ZoomToMonster()
    {
        if (_playerCam == null || monsterFace == null) yield break;

        Vector3 originalPos = _playerCam.transform.localPosition;
        float originalFov = _playerCam.fieldOfView;

        // Направление к монстру
        Vector3 dirToMonster = (monsterFace.transform.position - _playerCam.transform.position).normalized;

        // Целевая позиция — резко приближаемся к монстру
        Vector3 targetPos = originalPos + _playerCam.transform.InverseTransformDirection(dirToMonster) * 0.4f;
        float targetFov = originalFov - 15f; // Сужаем FOV — эффект зума

        float t = 0;
        // Резкий зум вперёд за 0.1 секунды
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float progress = t / 0.1f;
            _playerCam.transform.localPosition = Vector3.Lerp(originalPos, targetPos, progress);
            _playerCam.fieldOfView = Mathf.Lerp(originalFov, targetFov, progress);
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        // Плавный возврат обратно
        t = 0;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float progress = t / 0.4f;
            _playerCam.transform.localPosition = Vector3.Lerp(targetPos, originalPos, progress);
            _playerCam.fieldOfView = Mathf.Lerp(targetFov, originalFov, progress);
            yield return null;
        }

        _playerCam.transform.localPosition = originalPos;
        _playerCam.fieldOfView = originalFov;
    }

    // Камера резко дёргается вниз в сторону дыры
    IEnumerator CameraDip()
    {
        if (_playerCam == null) yield break;

        Vector3 originalPos = _playerCam.transform.localPosition;
        Vector3 dipPos = originalPos + Vector3.down * cameraDipAmount;

        float t = 0;
        // Резко вниз
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            _playerCam.transform.localPosition = Vector3.Lerp(originalPos, dipPos, t / 0.1f);
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        // Плавно обратно
        t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            _playerCam.transform.localPosition = Vector3.Lerp(dipPos, originalPos, t / 0.3f);
            yield return null;
        }

        _playerCam.transform.localPosition = originalPos;
    }

    // Тряска камеры
    IEnumerator CameraShake(float duration)
    {
        if (_playerCam == null) yield break;

        Vector3 originalPos = _playerCam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * cameraShakeIntensity;
            float y = Random.Range(-1f, 1f) * cameraShakeIntensity;
            _playerCam.transform.localPosition = new Vector3(
                originalPos.x + x,
                originalPos.y + y,
                originalPos.z
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        _playerCam.transform.localPosition = originalPos;
    }

    // Паралич — временно блокируем управление игрока
    IEnumerator Paralyze(float duration)
    {
        PlayerController pc = inventory.GetComponent<PlayerController>();
        if (pc == null) yield break;

        pc.isCameraLocked = true;
        yield return new WaitForSeconds(duration);
        pc.isCameraLocked = false;
    }

    IEnumerator FlickerFlashlight(int counts, float speed)
    {
        for (int i = 0; i < counts; i++)
        {
            if (_flashlightLight == null) yield break;
            _flashlightLight.enabled = false;
            yield return new WaitForSeconds(speed);
            _flashlightLight.enabled = true;
            yield return new WaitForSeconds(speed);
        }
    }

    void PlayFlashlightSound(AudioClip clip)
    {
        if (inventory.flashlightAudioSource != null && clip != null)
            inventory.flashlightAudioSource.PlayOneShot(clip);
    }
}