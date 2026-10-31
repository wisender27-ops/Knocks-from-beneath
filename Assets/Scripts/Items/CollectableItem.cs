using UnityEngine;

namespace KnocksFromBeneath
{

public class CollectableItem : MonoBehaviour
{
    public enum ItemType { Trash, Box, Grocery } // Список типов
    public ItemType currentItemType; // Выбранный тип для этого объекта
    private void Awake()
    {
        QuestItemMarker.Ensure(gameObject);
        // Коробки показывает только квестовая метка.
        if (currentItemType == ItemType.Box) QuestItemMarker.SuppressLegacyParticles(transform);
    }
}
}
