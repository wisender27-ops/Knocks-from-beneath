using UnityEngine;

namespace KnocksFromBeneath
{

public class PlayerDoorOpen : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;

    // Ссылка на твой контроллер
    [SerializeField] private PlayerController playerController;

    private float distance = 3.0f;
    private Door currentHeldDoor;
    private float pressTime;
    private float mouseMovement;
    private const float TapDuration = 0.22f;
    private const float TapMouseTolerance = 0.1f;

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
        if (playerCamera == null)
            return;

        // 1. НАЖАЛИ КНОПКУ: Ищем дверь, хватаем её и лочим камеру
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance))
            {
                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null)
                {
                    if (door.IsInteractionLocked) return;

                    currentHeldDoor = door;
                    pressTime = Time.unscaledTime;
                    mouseMovement = 0f;
                    currentHeldDoor.StartHolding();

                    // Блокируем вращение камеры
                    if (playerController != null)
                        playerController.isCameraLocked = true;
                }
            }
        }

        if (currentHeldDoor != null && Input.GetKey(KeyCode.E))
            mouseMovement += Mathf.Abs(Input.GetAxis("Mouse Y"));

        // 2. ОТПУСТИЛИ КНОПКУ: Бросаем дверь и разблокируем камеру
        if (Input.GetKeyUp(KeyCode.E))
        {
            if (currentHeldDoor != null)
            {
                bool wasTap = Time.unscaledTime - pressTime <= TapDuration &&
                              mouseMovement <= TapMouseTolerance;
                currentHeldDoor.StopHolding();
                if (wasTap && !currentHeldDoor.IsInteractionLocked)
                    currentHeldDoor.ToggleDoor();
                currentHeldDoor = null;

                // Возвращаем управление камерой
                if (playerController != null)
                    playerController.isCameraLocked = false;
            }
        }
    }

    void OnDisable()
    {
        if (currentHeldDoor != null)
        {
            currentHeldDoor.StopHolding();
            currentHeldDoor = null;
        }

        if (playerController != null)
            playerController.isCameraLocked = false;
    }
}
}
