using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
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
    public float standingCameraY = 1.6f;
    public float crouchingCameraY = 0.9f;

    [Header("Stamina")]
    public float maxStamina = 5f;
    public float staminaRegenRate = 1f;
    public float minStaminaToRun = 2f;

    private CharacterController controller;
    private Camera cam;
    private float yRotation;
    private float verticalVelocity;
    private float currentStamina;
    private float currentCameraY;
    private bool isExhausted;
    private bool wasRunningLastFrame;

    // Переменная для хранения текущей скорости (горизонтальной)
    private Vector3 horizontalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        currentStamina = maxStamina;
        currentCameraY = standingCameraY;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yRotation -= mouseY;
        yRotation = Mathf.Clamp(yRotation, minY, maxY);

        cam.transform.localRotation = Quaternion.Euler(yRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        // Используем GetAxisRaw для мгновенного считывания нажатий
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        
        bool isMoving = Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f;
        bool wantsToCrouch = Input.GetKey(KeyCode.LeftControl) || IsCeilingBlocking();
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);

        // Стамина и усталость
        if (currentStamina <= 0f) isExhausted = true;
        if (isExhausted && currentStamina >= minStaminaToRun) isExhausted = false;

        bool canStartRun = shiftHeld && isMoving && !wantsToCrouch && !isExhausted && currentStamina >= minStaminaToRun && !wasRunningLastFrame;
        bool canContinueRun = wasRunningLastFrame && shiftHeld && isMoving && !wantsToCrouch && !isExhausted && currentStamina > 0f;
        bool canRun = canStartRun || canContinueRun;

        // Определяем целевую скорость
        float targetSpeed = walkSpeed;
        if (wantsToCrouch) targetSpeed = crouchSpeed;
        else if (canRun)
        {
            targetSpeed = runSpeed;
            currentStamina -= Time.deltaTime;
        }
        else
        {
            if (currentStamina < maxStamina)
                currentStamina += staminaRegenRate * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // Состояние бега для следующего кадра
        wasRunningLastFrame = canRun && isMoving;

        // --- РАСЧЕТ ДВИЖЕНИЯ ---

        // Куда игрок хочет двигаться (целевой вектор)
        Vector3 targetDirection = (transform.right * x + transform.forward * z).normalized;
        Vector3 targetMoveVelocity = targetDirection * targetSpeed;

        // Плавно приближаем текущую горизонтальную скорость к целевой
        // Это убирает системную инерцию Unity и дает полный контроль
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetMoveVelocity, movementSmoothness * Time.deltaTime);

        // Гравитация
        if (controller.isGrounded && verticalVelocity < 0f) 
            verticalVelocity = -2f;
        
        verticalVelocity += gravity * Time.deltaTime;

        // Сборка финального вектора
        Vector3 finalVelocity = horizontalVelocity;
        finalVelocity.y = verticalVelocity;

        // Движение
        controller.Move(finalVelocity * Time.deltaTime);

        ApplyCrouchVisuals(wantsToCrouch);
    }

    private void ApplyCrouchVisuals(bool isCrouching)
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCameraY = isCrouching ? crouchingCameraY : standingCameraY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchSmooth);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);

        currentCameraY = Mathf.Lerp(currentCameraY, targetCameraY, Time.deltaTime * crouchSmooth);
        Vector3 camPos = cam.transform.localPosition;
        camPos.y = currentCameraY;
        cam.transform.localPosition = camPos;
    }

    private bool IsCeilingBlocking()
    {
        float radius = controller.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * crouchingHeight;
        return Physics.SphereCast(origin, radius, Vector3.up, out _, standingHeight - crouchingHeight);
    }
}