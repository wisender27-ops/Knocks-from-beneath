using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float distance = 3f;
    public LayerMask layerMask;
    public PlayerInventory inventory; // Ссылка на наш новый инвентарь
    public new Camera camera;

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
        // Создаем луч из центра экрана
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, distance, layerMask))
        {
            // 1. Пробуем найти скрипт выключателя света
            LightSwitch lightSwitch = hit.collider.GetComponent<LightSwitch>();
            if (lightSwitch != null)
            {
                lightSwitch.ToggleLight();
                return; // Завершаем метод, чтобы не проверять другие условия
            }

            // 2. Пробуем найти скрипт предмета
            SimpleItem item = hit.collider.GetComponent<SimpleItem>();
            if (item != null)
            {
                PickUp(item);
                return;
            }

            // 3. Пробуем найти логику пола
            FloorLogic floor = hit.collider.GetComponent<FloorLogic>();
            if (floor != null)
            {
                if (inventory.hasCrowbar) floor.Break();
                else Debug.Log("Нужен лом в инвентаре!");
                return;
            }
        }
    }

    void PickUp(SimpleItem item)
    {
        // Логика подбора предметов в инвентарь
        if (item.itemType == ItemType.Crowbar)
        {
            inventory.hasCrowbar = true;
            inventory.ActivateItem("Crowbar"); 
        }
        else if (item.itemType == ItemType.Flashlight)
        {
            inventory.hasFlashlight = true;
            inventory.ActivateItem("Flashlight"); 
        }
        else if (item.itemType == ItemType.Hammer)
        {
            inventory.hasHammer = true;
            inventory.ActivateItem("Hammer");
        }

        Debug.Log("Подобрали: " + item.itemType);
        Destroy(item.gameObject); // Удаляем предмет из мира
    }
}