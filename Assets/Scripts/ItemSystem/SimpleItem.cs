using UnityEngine;

// Перечисление типов предметов (всего 3)
public enum ItemType
{
    Crowbar,
    Flashlight,
    Hammer
}

public class SimpleItem : MonoBehaviour
{
    public ItemType itemType; // Выбираем тип в инспекторе Unity
}