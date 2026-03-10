using UnityEngine;

public class PickableItem : MonoBehaviour
{
    public PlacementZone activeZone; // Зона, в которой сейчас находится эта коробка

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