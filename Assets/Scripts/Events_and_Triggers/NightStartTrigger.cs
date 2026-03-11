using UnityEngine;

public class NightStartTrigger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject knockController; // Объект, на котором висит RandomKnock и AudioSource
    [SerializeField] private string playerTag = "Player";

    private bool _hasTriggered = false;

    private void OnTriggerExit(Collider other)
    {
        // Проверяем, что это игрок, что событие еще не срабатывало 
        // и что объект со стуком вообще назначен
        if (!_hasTriggered && other.CompareTag(playerTag))
        {
            if (knockController != null)
            {
                ActivateNightEvents();
            }
        }
    }

    private void ActivateNightEvents()
    {
        _hasTriggered = true;

        // Включаем контроллер стука
        knockController.SetActive(true);

        Debug.Log("Ночные события активированы: Стук начался.");

        // Если нужно, здесь можно добавить другие действия:
        // например, запереть входную дверь или выключить свет в коридоре.
    }
}