using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private float switchCooldown = 0.25f;
    private float lastSwitchTime = -1f;
    private int currentItemIndex = 0;

    [Header("Наличие предметов (Логика)")]
    public bool hasCrowbar = false;
    public bool hasFlashlight = false;
    public bool hasHammer = false;

    [Header("Объекты в руках (Визуал)")]
    public GameObject crowbarInHand;
    public GameObject flashlightInHand;
    public GameObject hammerInHand;

    [Header("Настройки фонарика")]
    public Light flashlightLightSource;
    public AudioSource flashlightAudioSource;
    public AudioClip soundOn;
    public AudioClip soundOff;

    [Header("Настройки удара ломом")]
    public float hitDistance = 2.5f;
    public LayerMask interactableLayer;
    public float damageDelay = 0.2f;

    private Animator crowbarAnim;

    void Start()
    {
        if (crowbarInHand != null)
            crowbarAnim = crowbarInHand.GetComponent<Animator>();

        if (crowbarInHand != null) crowbarInHand.SetActive(false);
        if (flashlightInHand != null) flashlightInHand.SetActive(false);
        if (hammerInHand != null) hammerInHand.SetActive(false);
        if (flashlightLightSource != null) flashlightLightSource.enabled = false;

        ActivateItem("None");
        currentItemIndex = 0;
    }

    void Update()
    {
        // --- ПЕРЕКЛЮЧЕНИЕ КЛАВИШАМИ ---
        if (Time.time - lastSwitchTime >= switchCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToSlot(3);
        }

        // --- СКРОЛЛ МЫШИ ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && Time.time - lastSwitchTime >= switchCooldown)
        {
            int dir = scroll > 0 ? 1 : -1;
            int nextSlot = currentItemIndex + dir;

            int maxSlot = InventoryUI.Instance != null ?
                InventoryUI.Instance.GetActiveSlotCount() - 1 : 0;

            nextSlot = Mathf.Clamp(nextSlot, 0, maxSlot);

            if (nextSlot != currentItemIndex)
                SwitchToSlot(nextSlot);
        }

        // --- ЛКМ ---
        if (Input.GetMouseButtonDown(0))
        {
            if (crowbarInHand != null && crowbarInHand.activeSelf)
                PerformCrowbarAttack();
            else if (flashlightInHand != null && flashlightInHand.activeSelf)
                ToggleFlashlight();
        }
    }

    void SwitchToSlot(int slotIndex)
    {
        if (InventoryUI.Instance == null) return;

        string itemInSlot = InventoryUI.Instance.GetItemInSlot(slotIndex);
        if (itemInSlot == null) return;

        ActivateItem(itemInSlot);
        currentItemIndex = slotIndex;
        lastSwitchTime = Time.time;
    }

    // --- ЛОГИКА ПРЕДМЕТОВ ---

    public void ActivateItem(string itemName)
    {
        if (flashlightLightSource != null)
            flashlightLightSource.enabled = false;

        if (crowbarInHand != null) crowbarInHand.SetActive(false);
        if (flashlightInHand != null) flashlightInHand.SetActive(false);
        if (hammerInHand != null) hammerInHand.SetActive(false);

        if (itemName == "Crowbar" && hasCrowbar && crowbarInHand != null)
            crowbarInHand.SetActive(true);
        else if (itemName == "Flashlight" && hasFlashlight && flashlightInHand != null)
            flashlightInHand.SetActive(true);
        else if (itemName == "Hammer" && hasHammer && hammerInHand != null)
            hammerInHand.SetActive(true);

        // Обновляем UI — подсвечиваем активный слот
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.SetActiveSlot(itemName);
    }

    void ToggleFlashlight()
    {
        if (flashlightLightSource != null)
        {
            flashlightLightSource.enabled = !flashlightLightSource.enabled;

            if (flashlightAudioSource != null)
            {
                AudioClip clip = flashlightLightSource.enabled ? soundOn : soundOff;
                flashlightAudioSource.PlayOneShot(clip);
            }
        }
    }

    void PerformCrowbarAttack()
    {
        if (crowbarAnim != null)
        {
            crowbarAnim.SetTrigger("Attack");
            Invoke("CheckHit", damageDelay);
        }
    }

    public void CheckHit()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, hitDistance, interactableLayer))
        {
            BreakableObject breakable = hit.collider.GetComponent<BreakableObject>();
            if (breakable != null) breakable.Break();

            FloorLogic floor = hit.collider.GetComponent<FloorLogic>();
            if (floor != null) floor.Break();
        }
    }
}