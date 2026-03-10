using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private float switchCooldown = 0.25f; // 250 мс
    private float lastSwitchTime = -1f;
    private int currentItemIndex = 0; // 0 — пустая рука (не используется), 1 — пустая рука, 2+ — предметы
    private readonly string[] itemOrder = { "None", "None", "Crowbar", "Flashlight", "Hammer" }; // 1 — пустая рука, 2 — лом, 3 — фонарик, 4 — молоток
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

        // По умолчанию — пустая рука (слот 1)
        ActivateItem("None");
        currentItemIndex = 1;
    }

    void Update()
    {
        // 1. Переключение предметов на клавиши 1, 2, 3, 4 с кулдауном
        if (Time.time - lastSwitchTime >= switchCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ActivateItem("None");
                currentItemIndex = 1;
                lastSwitchTime = Time.time;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && hasCrowbar)
            {
                ActivateItem("Crowbar");
                currentItemIndex = 2;
                lastSwitchTime = Time.time;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) && hasFlashlight)
            {
                ActivateItem("Flashlight");
                currentItemIndex = 3;
                lastSwitchTime = Time.time;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) && hasHammer)
            {
                ActivateItem("Hammer");
                currentItemIndex = 4;
                lastSwitchTime = Time.time;
            }
        }

        // 1.1 Переключение предметов колесом мыши (с учётом пустой руки)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && Time.time - lastSwitchTime >= switchCooldown)
        {
            // Индексы: 1 — None, 2 — Crowbar, 3 — Flashlight, 4 — Hammer
            bool[] hasItems = { false, true, hasCrowbar, hasFlashlight, hasHammer }; // 0 не используется, 1 — пустая рука всегда доступна
            int itemsCount = 0;
            for (int i = 1; i < hasItems.Length; i++)
                if (hasItems[i]) itemsCount++;
            if (itemsCount > 0)
            {
                // Найти текущий активный предмет
                if (crowbarInHand != null && crowbarInHand.activeSelf) currentItemIndex = 2;
                else if (flashlightInHand != null && flashlightInHand.activeSelf) currentItemIndex = 3;
                else if (hammerInHand != null && hammerInHand.activeSelf) currentItemIndex = 4;
                else currentItemIndex = 1; // Пустая рука

                // Прокрутка
                int dir = scroll > 0 ? 1 : -1;
                int nextIndex = currentItemIndex;
                for (int i = 0; i < itemOrder.Length; i++)
                {
                    nextIndex = (nextIndex + dir + itemOrder.Length) % itemOrder.Length;
                    if (nextIndex == 0) nextIndex = (dir > 0) ? 1 : itemOrder.Length - 1; // пропуск 0
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
            // Если пустая рука — ничего не делаем
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
        // Если None — ничего не включаем (всё выключено)
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