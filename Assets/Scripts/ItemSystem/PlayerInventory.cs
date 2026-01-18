using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private float switchCooldown = 0.25f; // 250 мс
    private float lastSwitchTime = -1f;
    private int currentItemIndex = -1; // -1 если ничего не выбрано
    private readonly string[] itemOrder = { "Crowbar", "Flashlight", "Hammer" };
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
    public float hitDistance = 2.5f;     // Дистанция удара
    public LayerMask interactableLayer;  // Слой объектов (Interactable)
    public float damageDelay = 0.2f;    // Задержка луча под анимацию

    private Animator crowbarAnim;

    void Start()
    {
        if (crowbarInHand != null)
            crowbarAnim = crowbarInHand.GetComponent<Animator>();

        // Выключаем всё при старте, только если объекты назначены
        if (crowbarInHand != null) crowbarInHand.SetActive(false);
        if (flashlightInHand != null) flashlightInHand.SetActive(false);
        if (hammerInHand != null) hammerInHand.SetActive(false);
        if (flashlightLightSource != null) flashlightLightSource.enabled = false;
    }

    void Update()
    {
        // 1. Переключение предметов на клавиши 1, 2, 3 с кулдауном
        if (Time.time - lastSwitchTime >= switchCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && hasCrowbar)
            {
                ActivateItem("Crowbar");
                currentItemIndex = 0;
                lastSwitchTime = Time.time;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && hasFlashlight)
            {
                ActivateItem("Flashlight");
                currentItemIndex = 1;
                lastSwitchTime = Time.time;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) && hasHammer)
            {
                ActivateItem("Hammer");
                currentItemIndex = 2;
                lastSwitchTime = Time.time;
            }
        }

        // 1.1 Переключение предметов колесом мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && Time.time - lastSwitchTime >= switchCooldown)
        {
            int itemsCount = 0;
            bool[] hasItems = { hasCrowbar, hasFlashlight, hasHammer };
            for (int i = 0; i < hasItems.Length; i++)
                if (hasItems[i]) itemsCount++;
            if (itemsCount > 0)
            {
                // Найти текущий активный предмет
                int prevIndex = currentItemIndex;
                for (int i = 0; i < itemOrder.Length; i++)
                {
                    if ((itemOrder[i] == "Crowbar" && crowbarInHand != null && crowbarInHand.activeSelf) ||
                        (itemOrder[i] == "Flashlight" && flashlightInHand != null && flashlightInHand.activeSelf) ||
                        (itemOrder[i] == "Hammer" && hammerInHand != null && hammerInHand.activeSelf))
                    {
                        currentItemIndex = i;
                        break;
                    }
                }
                // Если ничего не выбрано, начать с первого доступного
                if (currentItemIndex == -1)
                {
                    for (int i = 0; i < hasItems.Length; i++)
                    {
                        if (hasItems[i])
                        {
                            currentItemIndex = i;
                            break;
                        }
                    }
                }
                // Прокрутка
                int dir = scroll > 0 ? 1 : -1;
                int nextIndex = currentItemIndex;
                for (int i = 0; i < itemOrder.Length; i++)
                {
                    nextIndex = (nextIndex + dir + itemOrder.Length) % itemOrder.Length;
                    if (hasItems[nextIndex])
                        break;
                }
                if (nextIndex != currentItemIndex)
                {
                    ActivateItem(itemOrder[nextIndex]);
                    currentItemIndex = nextIndex;
                    lastSwitchTime = Time.time;
                }
            }
        }
        // 2. Логика ЛКМ (Действие)
        if (Input.GetMouseButtonDown(0))
        {
            // Если в руках ЛОМ
            if (crowbarInHand != null && crowbarInHand.activeSelf)
            {
                PerformCrowbarAttack();
            }
            // Если в руках ФОНАРИК
            else if (flashlightInHand != null && flashlightInHand.activeSelf)
            {
                ToggleFlashlight();
            }
        }
    }

    // --- ЛОГИКА ПРЕДМЕТОВ ---

    public void ActivateItem(string itemName)
    {
        // Выключаем свет, только если он назначен
        if (flashlightLightSource != null)
        {
            flashlightLightSource.enabled = false;
        }

        // ПРОВЕРКА: Выключаем только те объекты, которые реально существуют
        if (crowbarInHand != null)
        {
            crowbarInHand.SetActive(false);
        }
        if (flashlightInHand != null)
        {
            flashlightInHand.SetActive(false);
        }
        if (hammerInHand != null)
        {
            hammerInHand.SetActive(false);
        }

        // Включаем нужный предмет (тоже с проверкой на null)
        if (itemName == "Crowbar" && hasCrowbar && crowbarInHand != null)
        {
            crowbarInHand.SetActive(true);
        }
        else if (itemName == "Flashlight" && hasFlashlight && flashlightInHand != null)
        {
            flashlightInHand.SetActive(true);
        }
        else if (itemName == "Hammer" && hasHammer && hammerInHand != null)
        {
            hammerInHand.SetActive(true);
        }
    }

    void ToggleFlashlight()
    {
        if (flashlightLightSource != null)
        {
            flashlightLightSource.enabled = !flashlightLightSource.enabled;

            // Звук щелчка
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
            crowbarAnim.SetTrigger("Attack"); // Запуск анимации
            
            // Запускаем "луч урона" с небольшой задержкой, чтобы совпало с замахом
            Invoke("CheckHit", damageDelay);
        }
    }

    void CheckHit()
    {
        // Пускаем луч из центра экрана
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, hitDistance, interactableLayer))
        {
            // Проверка на разрушаемый объект
            BreakableObject breakable = hit.collider.GetComponent<BreakableObject>();
            if (breakable != null)
            {
                breakable.Break();
            }

            // Проверка на старую логику пола
            FloorLogic floor = hit.collider.GetComponent<FloorLogic>();
            if (floor != null)
            {
                floor.Break();
            }
        }
    }
}