using UnityEngine;

namespace KnocksFromBeneath
{

public class PickableItem : MonoBehaviour
{
    public PlacementZone activeZone; // Зона, в которой сейчас находится эта коробка
    public string placementQuestTag;

    [Tooltip("Зона, которой ПРЕДНАЗНАЧЕН этот предмет. Для квеста с разными предметами (декор дня 2) у каждого своя: лампа — в одну комнату, цветок — в другую. Пусто = предмет подходит любой зоне этого квеста (коробки дня 1).")]
    public PlacementZone designatedZone;

    private void OnTriggerEnter(Collider other)
    {
        PlacementZone zone = other.GetComponent<PlacementZone>();
        if (zone != null)
        {
            activeZone = zone;
            Debug.Log("Коробка готова к установке в: " + other.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlacementZone>())
        {
            activeZone = null;
        }
    }
}
}
