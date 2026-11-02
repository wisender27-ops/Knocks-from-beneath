using UnityEngine;

namespace KnocksFromBeneath
{
    // Подвал в сцене Main (дочерний объект Basement_Reference): ссылки на его объекты; логика — в BasementChapter.
    public sealed class BasementLevel : MonoBehaviour
    {
        public Transform cellDoor;
        public GameObject cellDoorCollider;
        public GameObject lockVisual;
        public BasementCellLock cellLock;
        public BasementCellLock hideSpot;
        public BasementOwner owner;
        public Light cellLamp;
        public Transform tapeView;
        public BasementRoomAtmosphere atmosphere;
    }
}
