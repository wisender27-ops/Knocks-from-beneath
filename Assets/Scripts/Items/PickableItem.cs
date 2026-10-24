using UnityEngine;

namespace KnocksFromBeneath
{

public class PickableItem : MonoBehaviour
{
    public PlacementZone activeZone; // Зона, в которой сейчас находится эта коробка
    public string placementQuestTag;

    [Tooltip("Зона, которой ПРЕДНАЗНАЧЕН этот предмет. Для квеста с разными предметами (декор дня 2) у каждого своя: лампа — в одну комнату, цветок — в другую. Пусто = предмет подходит любой зоне этого квеста (коробки дня 1).")]
    public PlacementZone designatedZone;

    [Header("Подсказка при переноске")]
    public string displayName = "предмет";
    public string destinationHint;
    public Transform scatteredPose;

    [Header("Хват при переноске")]
    [Tooltip("Необязательные центры ладоней: +Z внутрь поверхности, +Y вдоль пальцев. Без точек хват вычисляется по коллайдерам любого предмета.")]
    public Transform primaryHandGrip;
    public Transform secondaryHandGrip;
    private bool _preparedForQuest;

    public void PrepareForPlacementQuest()
    {
        if (_preparedForQuest || scatteredPose == null) return;
        _preparedForQuest = true;
        transform.SetParent(null, true);
        gameObject.SetActive(true);
        transform.SetPositionAndRotation(scatteredPose.position, scatteredPose.rotation);
        tag = "Pickable";
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) return;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

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
