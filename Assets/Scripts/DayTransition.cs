using UnityEngine;
using System.Collections;

public class DayTransition : MonoBehaviour
{
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject messageText;

    [Header("Night Setup")]
    public GameObject nightTrigger;    // Тот самый куб у кровати
    public Transform playerTransform;  // Ссылка на трансформ игрока
    public Vector3 nightSpawnPosition; // Координаты спавна (введи в инспекторе)
    public Vector3 nightSpawnRotation; // Поворот головы/тела (опционально)

    public void StartNightSequence()
    {
        StartCoroutine(NightRoutine());
    }

    IEnumerator NightRoutine()
    {
        // 1. Fade Out (Экран темнеет)
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        // --- МОМЕНТ ТЕМНОТЫ ---
        messageText.SetActive(true);

        // Смена неба
        if (skySwitcher != null) skySwitcher.isDayTime = false;

        // Перемещение игрока
        if (playerTransform != null)
        {
            // Отключаем CharacterController на время перемещения (важно для Unity!)
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = nightSpawnPosition;
            playerTransform.eulerAngles = nightSpawnRotation;

            if (cc != null) cc.enabled = true;
        }

        yield return new WaitForSeconds(3f);
        // -----------------------

        // 2. Включаем триггер стука (он ждет, пока игрок выйдет из него)
        if (nightTrigger != null)
        {
            nightTrigger.SetActive(true);
        }

        // 3. Fade In (Экран светлеет)
        messageText.SetActive(false);
        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }
    }
}