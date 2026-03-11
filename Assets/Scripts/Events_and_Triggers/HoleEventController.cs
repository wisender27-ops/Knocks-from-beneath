using UnityEngine;
using System.Collections;

public class HoleEventController : MonoBehaviour
{
    [Header("Ссылки")]
    public GameObject monsterFace;      // Модель лица под полом
    public AudioSource jumpscareAudio;  // Скример-звук
    public PlayerInventory inventory;   // Ссылка на инвентарь для проверки фонарика

    [Header("Настройки")]
    public float lookDistance = 3f;     // Дистанция, с которой нужно смотреть
    public float timeToTrigger = 2.5f;  // Сколько секунд нужно смотреть

    private float _lookTimer = 0f;
    private bool _eventStarted = false;
    private Light _flashlightLight;

    void Start()
    {
        if (monsterFace != null) monsterFace.SetActive(false);

        // Находим свет фонарика через инвентарь
        if (inventory != null) _flashlightLight = inventory.flashlightLightSource;
    }

    void Update()
    {
        if (_eventStarted) return;

        // 1. Проверяем: включен ли фонарик и в руках ли он
        bool isFlashlightOn = inventory.hasFlashlight && _flashlightLight != null && _flashlightLight.enabled;

        if (isFlashlightOn && IsPlayerLookingAtHole())
        {
            _lookTimer += Time.deltaTime;
            if (_lookTimer >= timeToTrigger)
            {
                StartCoroutine(TheVoidSequence());
            }
        }
        else
        {
            _lookTimer = 0f; // Сбрасываем таймер, если игрок отвел взгляд
        }
    }

    bool IsPlayerLookingAtHole()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // Рисуем луч в окне Scene (красный - не попал, зеленый - попал)
        Debug.DrawRay(ray.origin, ray.direction * lookDistance, Color.red);

        if (Physics.Raycast(ray, out hit, lookDistance))
        {
            // ВАЖНО: Смотрим в консоль, что там написано
            // Debug.Log("Смотрю на: " + hit.collider.gameObject.name);

            // Проверяем, есть ли на объекте, в который попали, этот же скрипт
            if (hit.collider.gameObject == gameObject || hit.collider.GetComponent<HoleEventController>() != null)
            {
                Debug.DrawRay(ray.origin, ray.direction * lookDistance, Color.green);
                return true;
            }
        }
        return false;
    }

    IEnumerator TheVoidSequence()
    {
        _eventStarted = true;

        // 1. Предвестник: Фонарик мерцает
        yield return StartCoroutine(FlickerFlashlight(3, 0.1f));

        // 2. Темнота перед появлением
        _flashlightLight.enabled = false;
        if (inventory.flashlightAudioSource != null)
            inventory.flashlightAudioSource.PlayOneShot(inventory.soundOff);

        yield return new WaitForSeconds(1f);

        // 3. Появление монстра
        monsterFace.SetActive(true);

        // 4. Включаем свет + Скример (Игрок видит ЭТО)
        _flashlightLight.enabled = true;
        if (inventory.flashlightAudioSource != null)
            inventory.flashlightAudioSource.PlayOneShot(inventory.soundOn);

        if (jumpscareAudio != null) jumpscareAudio.Play();

        // 5. ПАУЗА: Игрок смотрит на монстра (например, 1.5 секунды)
        yield return new WaitForSeconds(1.5f);

        // 6. ФИНАЛЬНЫЙ БЛЭКАУТ: Фонарик гаснет, и в этот момент монстр исчезает
        _flashlightLight.enabled = false;
        if (inventory.flashlightAudioSource != null)
            inventory.flashlightAudioSource.PlayOneShot(inventory.soundOff);

        yield return new WaitForSeconds(0.5f); // Короткая вспышка темноты

        monsterFace.SetActive(false); // Монстр исчез, пока было темно

        // 7. Автоматическое включение фонарика (опционально, создаёт эффект "глюка")
        yield return new WaitForSeconds(0.3f);
        _flashlightLight.enabled = true;
        if (inventory.flashlightAudioSource != null)
            inventory.flashlightAudioSource.PlayOneShot(inventory.soundOn);

        Debug.Log("Этап 3 завершен: монстр исчез в темноте.");

        // Подготовка к Этапу 4 (можно включить другой объект или звук)
        this.enabled = false;
    }

    IEnumerator FlickerFlashlight(int counts, float speed)
    {
        for (int i = 0; i < counts; i++)
        {
            _flashlightLight.enabled = false;
            yield return new WaitForSeconds(speed);
            _flashlightLight.enabled = true;
            yield return new WaitForSeconds(speed);
        }
    }
}