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
    private int _doorRaycastMask;

    void Start()
    {
        // Пытаемся найти контроллер автоматически, если забыли перетащить в инспекторе
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        // Исключаем слой HeldItem — иначе предмет в руках (или триггер-зона установки)
        // перехватывает луч раньше двери, стояющей за ним.
        int heldItemLayer = LayerMask.NameToLayer("HeldItem");
        int mask = heldItemLayer >= 0 ? ~(1 << heldItemLayer) : ~0;
        // И слой DoorNoRaycast: калитка garden_gate_01 (и подобные блокеры) должна
        // держать физику, но не перехватывать луч — иначе она висела бы перед
        // настоящей дверью и E открывал бы пустоту вместо створки.
        _doorRaycastMask = PhysicsMasks.WithoutNoRaycast(mask);
    }

    void Update()
    {
        if (playerCamera == null)
            return;

        // Не хватаемся за новую дверь, если камера уже заблокирована кем-то другим
        // (финал, кат-сцена, открытый инвентарь) — иначе KeyUp ниже снимет чужую
        // блокировку и, например, во время концовки камера разблокируется на E двери,
        // а с открытым инвентарём начнёт вращаться на паузе.
        bool cameraLockedByOther = currentHeldDoor == null && playerController != null && playerController.isCameraLocked;

        // 1. НАЖАЛИ КНОПКУ: Ищем дверь, хватаем её и лочим камеру
        if (!cameraLockedByOther && Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance, _doorRaycastMask, QueryTriggerInteraction.Ignore))
            {
                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null)
                {
                    if (door.IsInteractionLocked)
                    {
                        door.PlayLockedFeedback();
                        NotifyLockedFrontDoor(door);
                        return;
                    }

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
        // Снимаем блокировку камеры только если её поставили мы (держали дверь) — иначе
        // чужая блокировка (финал, инвентарь) снимается тем, что этот компонент выключили.
        if (currentHeldDoor != null)
        {
            currentHeldDoor.StopHolding();
            currentHeldDoor = null;

            if (playerController != null)
                playerController.isCameraLocked = false;
        }
    }

    // T-21: Door.IsInteractionLocked обслуживает и входную дверь (заперта на вечернем обходе
    // дня 2), и дверцу микроволновки (заперта во время готовки, MicrowaveInteractable) — общий
    // рычащий звук/дёргание ручки (Door.PlayLockedFeedback) уместен для обеих, а вот сюжетная
    // мысль "сам вчера закрыл" — только для входной двери и только пока она реально заперта
    // игроком, поэтому сверяемся с IntroSequence.frontDoor, а не просто с любой запертой дверью.
    void NotifyLockedFrontDoor(Door door)
    {
        if (!GameState.frontDoorLocked) return;

        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null || door != intro.frontDoor) return;

        if (ThoughtManager.Instance != null)
            ThoughtManager.Instance.ShowThoughts(new string[] { "Заперто.", "Я сам вчера закрыл." });
    }
}
}
