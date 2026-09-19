using UnityEngine;

public class KitchenNoiseTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Находим IntroSequence и говорим, что мы пришли
            IntroSequence intro = FindObjectOfType<IntroSequence>();
            if (intro != null)
                intro.OnKitchenTriggerReached();
            gameObject.SetActive(false); // Выключаем триггер
        }
    }
}