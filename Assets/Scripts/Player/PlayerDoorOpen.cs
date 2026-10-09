using UnityEngine;

namespace KnocksFromBeneath
{

public class PlayerDoorOpen : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;

    // Ссылка на твой контроллер
    [SerializeField] private PlayerController playerController;

    private PlayerInteraction interaction;
    private Door currentHeldDoor;
    private Vector3 heldContactLocal;
    public Door HeldDoor => currentHeldDoor;
    public Vector3 HeldContactPoint => currentHeldDoor != null ? currentHeldDoor.transform.TransformPoint(heldContactLocal) : Vector3.zero;
    private float pressTime;
    private float mouseMovement;
    private const float TapDuration = 0.22f;
    private const float TapMouseTolerance = 0.1f;


    void Start()
    {
        interaction = GetComponentInParent<PlayerInteraction>();
        // Пытаемся найти контроллер автоматически, если забыли перетащить в инспекторе
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

    }

    void Update()
    {
        if (PauseMenuController.InputBlocked || Time.timeScale <= 0f) return;
        if (currentHeldDoor != null && !Input.GetKey(KeyCode.E)) EndDoorHold(true);
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

            RaycastHit hit;

            if (interaction != null && interaction.enabled && interaction.TryGetInteractionHit(out hit))
            {
                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null && interaction.ShouldUseDoorInput(door))
                {
                    if (door.IsInteractionLocked)
                    {
                        door.PlayLockedFeedback();
                        NotifyLockedFrontDoor(door);
                        return;
                    }

                    BeginDoorHold(door, hit.point + hit.normal * .006f);
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
                EndDoorHold(true);
            }
        }
    }

    public void Initialize(Camera camera, PlayerController controller, PlayerInteraction actor)
    {
        playerCamera = camera != null ? camera : GetComponentInChildren<Camera>();
        playerController = controller != null ? controller : GetComponentInParent<PlayerController>();
        interaction = actor != null ? actor : GetComponentInParent<PlayerInteraction>();
    }

    public bool BeginDoorHold(Door door, Vector3 contact)
    {
        if (interaction == null) interaction = GetComponentInParent<PlayerInteraction>();
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (door == null || currentHeldDoor != null || interaction == null || !interaction.isActiveAndEnabled ||
            !interaction.ShouldUseDoorInput(door) || (playerController != null && playerController.isCameraLocked)) return false;
        if (door.IsInteractionLocked) { door.PlayLockedFeedback(); NotifyLockedFrontDoor(door); return false; }
        currentHeldDoor = door;
        heldContactLocal = door.transform.InverseTransformPoint(contact);
        pressTime = Time.unscaledTime;
        mouseMovement = 0f;
        door.StartHolding();
        if (playerController != null) playerController.isCameraLocked = true;
        return true;
    }

    public void EndDoorHold(bool allowTap = false)
    {
        if (currentHeldDoor == null) return;
        Door released = currentHeldDoor;
        bool tap = allowTap && Time.unscaledTime - pressTime <= TapDuration && mouseMovement <= TapMouseTolerance;
        currentHeldDoor = null;
        released.StopHolding();
        if (tap && !released.IsInteractionLocked) released.ToggleDoor();
        if (playerController != null) playerController.isCameraLocked = false;
    }

    void OnDisable() => EndDoorHold();

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
            ThoughtManager.Instance.TryShowInteractionThoughts("front-door-locked",
                new[] { "Чёрт, заперто.", "Сам же перед сном задвинул засов." }, 5f);
    }
}
}
