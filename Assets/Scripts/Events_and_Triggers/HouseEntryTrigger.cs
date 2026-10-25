using UnityEngine;

namespace KnocksFromBeneath
{

// Зона сразу за входной дверью: игрок зашёл в дом — квест "Зайти в дом" выполнен.
public class HouseEntryTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameEvents.OnHouseEntered?.Invoke();
        gameObject.SetActive(false);
    }
}
}
