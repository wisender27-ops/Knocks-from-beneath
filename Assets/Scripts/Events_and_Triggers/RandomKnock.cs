using UnityEngine;
using System.Collections;

public class RandomKnock : MonoBehaviour
{
    [Header("Компоненты")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] knockClips; // Массив звуков для разнообразия

    [Header("Настройки времени")]
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 5f;

    private void OnEnable()
    {
        // Запускаем корутину, как только объект активируется триггером
        StartCoroutine(KnockRoutine());
    }

    private void OnDisable()
    {
        // Останавливаем все процессы, если объект выключен (например, пол вскрыт)
        StopAllCoroutines();
    }

    private IEnumerator KnockRoutine()
    {
        while (true)
        {
            // Ждем случайное количество времени
            float waitTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(waitTime);

            // Воспроизводим случайный звук из массива
            if (knockClips.Length > 0)
            {
                int randomIndex = Random.Range(0, knockClips.Length);
                audioSource.PlayOneShot(knockClips[randomIndex]);

                Debug.Log("Раздался стук из-под пола...");
            }
        }
    }
}