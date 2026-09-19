using UnityEngine;

namespace KnocksFromBeneath
{

public class CollectableItem : MonoBehaviour
{
    public enum ItemType { Trash, Box, Grocery } // Список типов
    public ItemType currentItemType; // Выбранный тип для этого объекта
}
}
