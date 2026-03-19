using UnityEngine;

public class KitchenNoiseTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Находим IntroSequence и говорим, что мы пришли
            FindObjectOfType<IntroSequence>().OnKitchenTriggerReached();
            gameObject.SetActive(false); // Выключаем триггер
        }
    }
}