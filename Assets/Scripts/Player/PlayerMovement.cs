using UnityEngine;

namespace KnocksFromBeneath
{

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private const float MaxCameraNearClip = 0.03f;
    private const float CameraWallMargin = 0.01f;
    // Load hitches (e.g. scene load from the main menu) give dt≈0.33 and let the controller tunnel through thin floors.
    private const float MaxMovementDeltaTime = 1f / 30f;
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float crouchSpeed = 1.2f;
    public float gravity = -9.81f;
    [Tooltip("Сила ускорения и торможения. Чем выше, тем резче управление.")]
    public float movementSmoothness = 15f; 

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float minY = -80f;
    public float maxY = 80f;

    [Header("Crouch")]
    public float standingHeight = 1.8f;
    public float crouchingHeight = 1.0f;
    public float crouchSmooth = 8f;

    [Header("Stamina")]
    public float maxStamina = 5f;
    public float staminaRegenRate = 1f;
    public float minStaminaToRun = 2f;

    private CharacterController controller;
    private Camera cam;
    private PickupController _pickupControl;
    private bool invertMouseY;
    private float defaultFieldOfView;
    private int settingsRevision = -1;
    private float yRotation;
    private float verticalVelocity;
    private float currentStamina;
    private float currentCameraY;
    private bool _cameraHeightNormalized;
    private bool isExhausted;
    private bool wasRunningLastFrame;
    private bool isCrouching; // логическое состояние приседа

    public bool isCameraLocked = false;

    public Vector3 PlanarVelocity => controller != null && controller.enabled
        ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up) : Vector3.zero;
    public bool IsCrouching => isCrouching;
    public bool IsRunning => wasRunningLastFrame;
    public void StopMotion()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        wasRunningLastFrame = false;
    }
    private Vector3 horizontalVelocity;
    private readonly Collider[] ceilingOverlaps = new Collider[16];

    void Start()
    {
        controller = GetComponent<CharacterController>();
        _pickupControl = GetComponent<PickupController>();
        cam = GetComponentInChildren<Camera>();
        if (cam != null)
            cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, MaxCameraNearClip);
        currentStamina = maxStamina;

        currentCameraY = standingHeight - 0.2f;
        mouseSensitivity = SettingsStore.GetMouseSensitivity(mouseSensitivity);
        defaultFieldOfView = cam != null ? cam.fieldOfView : 60f;
        ApplyUserSettings();
        SettingsStore.ApplyMasterVolume();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (settingsRevision != SettingsStore.Revision) ApplyUserSettings();
        if (PauseMenuController.InputBlocked || Time.timeScale <= 0f) return;
        HandleLook();
        HandleMovement();
    }

    private void ApplyUserSettings()
    {
        settingsRevision = SettingsStore.Revision;
        mouseSensitivity = SettingsStore.GetMouseSensitivity(mouseSensitivity);
        invertMouseY = SettingsStore.GetInvertY();
        float fov = SettingsStore.GetFieldOfView(defaultFieldOfView);
        if (cam != null) cam.fieldOfView = fov;
        if (_pickupControl != null) _pickupControl.SetBaseFieldOfView(fov);
    }

    void LateUpdate()
    {
        if (controller == null || !controller.enabled || cam == null || isCameraLocked)
            return;
        float halfFovTangent = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float near = cam.nearClipPlane;
        float frustumRadius = near * Mathf.Sqrt(1f + halfFovTangent * halfFovTangent * (1f + cam.aspect * cam.aspect));
        float safeRadius = Mathf.Max(0f, controller.radius - controller.skinWidth - frustumRadius - CameraWallMargin);

        Vector3 cameraLocal = transform.InverseTransformPoint(cam.transform.position);
        Vector3 center = controller.center;
        Vector2 offset = new Vector2(cameraLocal.x - center.x, cameraLocal.z - center.z);
        offset = Vector2.ClampMagnitude(offset, safeRadius);
        cam.transform.position = transform.TransformPoint(
            new Vector3(center.x + offset.x, cameraLocal.y, center.z + offset.y));
        UpdateCameraHeight();
    }

    private void HandleLook()
    {
        if (isCameraLocked || cam == null || (_pickupControl != null && _pickupControl.IsRotatingHeldObject)) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertMouseY ? -1f : 1f);

        yRotation -= mouseY;
        yRotation = Mathf.Clamp(yRotation, minY, maxY);

        cam.transform.localRotation = Quaternion.Euler(yRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        if (controller == null || !controller.enabled) return;

        if (isCameraLocked) return;

        float dt = Mathf.Min(Time.deltaTime, MaxMovementDeltaTime);
        float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        
        bool isMoving = Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f;
        bool crouchKeyHeld = Input.GetKey(KeyCode.LeftControl);
        bool ceilingBlocksStandUp = !crouchKeyHeld && isCrouching && IsCeilingBlocking();
        bool wantsToCrouch = crouchKeyHeld || ceilingBlocksStandUp;
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
        if (currentStamina <= 0f) isExhausted = true;
        if (isExhausted && currentStamina >= minStaminaToRun) isExhausted = false;

        bool canStartRun = shiftHeld && isMoving && !wantsToCrouch && !isExhausted && currentStamina >= minStaminaToRun && !wasRunningLastFrame;
        bool canContinueRun = wasRunningLastFrame && shiftHeld && isMoving && !wantsToCrouch && !isExhausted && currentStamina > 0f;
        bool canRun = canStartRun || canContinueRun;
        float targetSpeed = walkSpeed;
        if (wantsToCrouch) targetSpeed = crouchSpeed;
        else if (canRun)
        {
            targetSpeed = runSpeed;
            currentStamina -= dt;
        }
        else
        {
            if (currentStamina < maxStamina)
                currentStamina += staminaRegenRate * dt;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        wasRunningLastFrame = canRun && isMoving;
        Vector3 targetDirection = (transform.right * x + transform.forward * z).normalized;
        Vector3 targetMoveVelocity = targetDirection * targetSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetMoveVelocity, movementSmoothness * dt);
        if (controller.isGrounded && verticalVelocity < 0f) 
            verticalVelocity = -2f;
        
        verticalVelocity += gravity * dt;
        Vector3 finalVelocity = horizontalVelocity;
        finalVelocity.y = verticalVelocity;
        Vector3 step = finalVelocity * dt;
        // Коробка в руках упирается в стены и мебель и не даёт пройти сквозь неё.
        if (_pickupControl != null) step = _pickupControl.ClampCarryMovement(step);
        controller.Move(step);

        ApplyCrouchVisuals(wantsToCrouch);
        isCrouching = wantsToCrouch;
    }

    private void ApplyCrouchVisuals(bool isCrouching)
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float k = 1f - Mathf.Exp(-Mathf.Max(0f, crouchSmooth) * Time.deltaTime);
        controller.height = Mathf.Lerp(controller.height, targetHeight, k);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);
    }

    private void UpdateCameraHeight()
    {
        if (cam == null || cam.transform.parent == null) return;
        if (!_cameraHeightNormalized)
        {
            Vector3 local = cam.transform.localPosition;
            local.y = 0f;
            cam.transform.localPosition = local;
            _cameraHeightNormalized = true;
        }
        float target = isCrouching ? crouchingHeight - 0.1f : standingHeight - 0.2f;
        float k = 1f - Mathf.Exp(-Mathf.Max(0f, crouchSmooth) * Time.deltaTime);
        currentCameraY = Mathf.Lerp(currentCameraY, target, k);
        Vector3 position = cam.transform.parent.localPosition;
        position.y = currentCameraY;
        cam.transform.parent.localPosition = position;
    }
    private bool IsCeilingBlocking()
    {
        float checkRadius = controller.radius * 0.6f;
        float headY = transform.position.y + standingHeight - checkRadius;
        Vector3 headCheckPos = new Vector3(transform.position.x, headY, transform.position.z);

        int overlapCount = Physics.OverlapSphereNonAlloc(
            headCheckPos,
            checkRadius,
            ceilingOverlaps,
            PhysicsMasks.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = ceilingOverlaps[i];
            if (col == null) continue;
            if (col.transform.root == transform) continue;
            return true;
        }

        return false;
    }
}
[DefaultExecutionOrder(10000)]
public sealed class PlayerMovement : PlayerController
{
}
}
