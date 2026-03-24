using UnityEngine;

public class PlayerDoorOpen : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;

    // Ссылка на твой контроллер
    [SerializeField] private PlayerController playerController;

    private float distance = 3.0f;
    private Door currentHeldDoor;

    void Start()
    {
        // Пытаемся найти контроллер автоматически, если забыли перетащить в инспекторе
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }
    }

    void Update()
    {
        // 1. НАЖАЛИ КНОПКУ: Ищем дверь, хватаем её и лочим камеру
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance))
            {
                Door door = hit.collider.GetComponent<Door>();
                if (door != null)
                {
                    if (door.IsInteractionLocked) return;

                    currentHeldDoor = door;
                    currentHeldDoor.StartHolding();

                    // Блокируем вращение камеры
                    if (playerController != null)
                        playerController.isCameraLocked = true;
                }
            }
        }

        // 2. ОТПУСТИЛИ КНОПКУ: Бросаем дверь и разблокируем камеру
        if (Input.GetKeyUp(KeyCode.E))
        {
            if (currentHeldDoor != null)
            {
                currentHeldDoor.StopHolding();
                currentHeldDoor = null;

                // Возвращаем управление камерой
                if (playerController != null)
                    playerController.isCameraLocked = false;
            }
        }
    }
}