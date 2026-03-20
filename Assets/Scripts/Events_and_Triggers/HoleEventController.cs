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
    public float minTimeToTrigger = 0.8f;  // Уменьшили — меньше времени
    public float maxTimeToTrigger = 2f;

    [Header("Звуки напряжения (до скримера)")]
    public AudioSource tensionAudioSource;
    public AudioClip[] tensionSounds;

    [Header("Настройки камеры")]
    public float cameraShakeIntensity = 0.08f; // Сильнее
    public float cameraDipAmount = 0.6f;        // Сильнее

    private float _lookTimer = 0f;
    private float _triggerTime;
    private bool _eventStarted = false;
    private bool _tensionSoundPlayed = false;
    private bool _committed = false; // Порог — скример уже не отменить
    private Light _flashlightLight;
    private Camera _playerCam;

    void Start()
    {
        if (monsterFace != null) monsterFace.SetActive(false);
        if (inventory != null) _flashlightLight = inventory.flashlightLightSource;
        _playerCam = Camera.main;
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
                // Не сбрасываем если уже committed
                if (!_committed) _lookTimer = 0f;
                return;
            }

            _lookTimer += Time.deltaTime;

            // Перешли половину — скример уже не отменить
            if (!_committed && _lookTimer >= _triggerTime * 0.5f)
            {
                _committed = true;
            }

            // Звук напряжения на 70% таймера — позже и короче
            if (!_tensionSoundPlayed && _lookTimer >= _triggerTime * 0.7f)
            {
                PlayRandomTensionSound();
                _tensionSoundPlayed = true;
            }

            if (_lookTimer >= _triggerTime)
                StartCoroutine(TheVoidSequence());
        }
        else
        {
            // Если committed — продолжаем таймер даже без взгляда
            if (_committed)
            {
                _lookTimer += Time.deltaTime;
                if (_lookTimer >= _triggerTime)
                    StartCoroutine(TheVoidSequence());
                return;
            }

            // Не committed — сбрасываем
            _lookTimer = 0f;
            _tensionSoundPlayed = false;
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

        // 1. ТОЛЬКО БЫСТРОЕ МИГАНИЕ — без раскачки
        yield return StartCoroutine(FlickerFlashlight(3, 0.04f));

        // 2. СВЕТ В ДОМЕ ГАСНЕТ + ФОНАРИК ГАСНЕТ
        if (LightingManager.Instance != null)
            LightingManager.Instance.TurnOffAllLamps();

        _flashlightLight.enabled = false;
        PlayFlashlightSound(inventory.soundOff);

        // Короткая пауза в темноте — нагнетание
        yield return new WaitForSeconds(0.5f);

        // 3. МОНСТР ПОЯВЛЯЕТСЯ — резко
        monsterFace.SetActive(true);
        _flashlightLight.enabled = true;
        PlayFlashlightSound(inventory.soundOn);

        if (jumpscareAudio != null) jumpscareAudio.Play();

        // 4. ВСЕ ЭФФЕКТЫ ОДНОВРЕМЕННО
        StartCoroutine(ZoomToMonster());
        StartCoroutine(CameraDip());
        StartCoroutine(CameraShake(1.5f));

        yield return new WaitForSeconds(1.5f);

        // 5. ПАРАЛИЧ
        yield return StartCoroutine(Paralyze(0.8f));

        // 6. ФОНАРИК ГАСНЕТ, МОНСТР ИСЧЕЗАЕТ
        _flashlightLight.enabled = false;
        PlayFlashlightSound(inventory.soundOff);

        yield return new WaitForSeconds(0.4f);
        monsterFace.SetActive(false);

        yield return new WaitForSeconds(0.3f);

        // 7. ФОНАРИК ВКЛЮЧАЕТСЯ
        _flashlightLight.enabled = true;
        PlayFlashlightSound(inventory.soundOn);

        // 8. ЗАВЕРШАЕМ КВЕСТ
        QuestManager.Instance.AddProgress(1);
        this.enabled = false;
    }

    IEnumerator ZoomToMonster()
    {
        if (_playerCam == null || monsterFace == null) yield break;

        Vector3 originalPos = _playerCam.transform.localPosition;
        float originalFov = _playerCam.fieldOfView;

        Vector3 dirToMonster = (monsterFace.transform.position - _playerCam.transform.position).normalized;
        Vector3 targetPos = originalPos + _playerCam.transform.InverseTransformDirection(dirToMonster) * 0.6f;
        float targetFov = originalFov - 25f;

        float t = 0;
        // Максимально резкий зум — 0.05 секунды
        while (t < 0.05f)
        {
            t += Time.deltaTime;
            float progress = t / 0.05f;
            _playerCam.transform.localPosition = Vector3.Lerp(originalPos, targetPos, progress);
            _playerCam.fieldOfView = Mathf.Lerp(originalFov, targetFov, progress);
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

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

    IEnumerator CameraDip()
    {
        if (_playerCam == null) yield break;

        Vector3 originalPos = _playerCam.transform.localPosition;
        Vector3 dipPos = originalPos + Vector3.down * cameraDipAmount;

        float t = 0;
        while (t < 0.05f) // Резче вниз
        {
            t += Time.deltaTime;
            _playerCam.transform.localPosition = Vector3.Lerp(originalPos, dipPos, t / 0.05f);
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            _playerCam.transform.localPosition = Vector3.Lerp(dipPos, originalPos, t / 0.3f);
            yield return null;
        }

        _playerCam.transform.localPosition = originalPos;
    }

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