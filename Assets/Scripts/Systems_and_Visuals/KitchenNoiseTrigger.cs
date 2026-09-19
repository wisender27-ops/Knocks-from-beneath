using UnityEngine;

namespace KnocksFromBeneath
{

public class KitchenNoiseTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameEvents.OnKitchenNoiseHeard?.Invoke();
            gameObject.SetActive(false); // Выключаем триггер
        }
    }
}
}
