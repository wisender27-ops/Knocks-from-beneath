using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float distance = 3f;
    public LayerMask layerMask;
    public PlayerInventory inventory; // Ссылка на наш новый инвентарь

    void Update()
    {
        // Кнопка E для взаимодействия
        if (Input.GetKeyDown(KeyCode.E))
        {
            PerformRaycast();
        }
    }

    void PerformRaycast()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, distance, layerMask))
        {
            // 1. Пробуем найти скрипт предмета
            SimpleItem item = hit.collider.GetComponent<SimpleItem>();
            if (item != null)
            {
                PickUp(item);
                return;
            }

            // 2. Пробуем найти логику пола (если нужно оставить взаимодействие на E)
            FloorLogic floor = hit.collider.GetComponent<FloorLogic>();
            if (floor != null)
            {
                if (inventory.hasCrowbar) floor.Break();
                else Debug.Log("Нужен лом в инвентаре!");
            }
        }
    }

    void PickUp(SimpleItem item)
    {
        if (item.itemType == ItemType.Crowbar)
        {
            inventory.hasCrowbar = true;
            inventory.ActivateItem("Crowbar"); // Берем в руки сразу
        }
        else if (item.itemType == ItemType.Flashlight)
        {
            inventory.hasFlashlight = true;
            inventory.ActivateItem("Flashlight"); // Берем в руки сразу
        }
        else if (item.itemType == ItemType.Hammer)
        {
            inventory.hasHammer = true;
            inventory.ActivateItem("Hammer");
        }

        Debug.Log("Подобрали: " + item.itemType);
        Destroy(item.gameObject); // Удаляем предмет с пола
    }
}