using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public enum DebugStoryStage
{
    None,
    StartBoxQuest,
    StartNight,
    StartKitchenNoise,
    StartFlashlightQuest,
    StartCrowbarQuest,
    StartBreakFloor,
    StartLookInHole,
    StartHammerQuest,
    StartFinale
}

public class IntroSequence : MonoBehaviour
{
    // =====================================================================
    // INSPECTOR
    // =====================================================================

    [Header("Настройки дебага")]
    public bool useDebugSkip = false;
    public DebugStoryStage startStage = DebugStoryStage.None;

    [Header("Игровые объекты")]
    public Transform playerTransform;
    public PlayerInventory playerInventory;

    [Header("Ночные параметры")]
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject nightTrigger;
    public Vector3 nightSpawnPosition;
    public Vector3 nightSpawnRotation;

    [Header("Зоны активности")]
    public GameObject trashZone;
    [Tooltip("Зона для каждой из коробок дня 1 (T-12) — одна PlacementZone+RoomRevealZone на комнату.")]
    public GameObject[] roomZones;
    public GameObject kitchenNoiseTrigger;
    public GameObject finaleTrigger;
    public GameObject nightStartTrigger;
    public GameObject knockController; // Звуки удара и RandomKnock

    [Header("Контроллеры событий")]
    public HoleEventController holeEventController;

    [Header("Предметы для квестов")]
    public GameObject flashlightItem;
    public GameObject crowbarItem;
    public GameObject hammerItem;

    [Header("Квест с пирогом")]
    public GameObject pieObject;
    public GameObject microwaveZone;

    [Header("День 2 — расстановка вещей (T-14)")]
    [Tooltip("Зоны для мелких декоративных предметов дня 2 — та же PlacementZone+RoomRevealZone, что и коробки дня 1, отдельный набор.")]
    public GameObject[] decorationZones;

    [Header("День 2 — вечерний обход (T-17)")]
    [Tooltip("Входная дверь дома — EveningRoundController запирает её через Door.SetInteractionLocked, если игрок выберет 'запереть'.")]
    public Door frontDoor;

    // Быт после переезда (мусор -> коробки) и квест с пирогом — самодостаточные
    // под-сюжеты, вынесены в отдельные классы (T-08). IntroSequence координирует
    // порядок и общие для всех этапов помощники (CreateQuest/ShowThoughts/IsQuestActive),
    // но не хранит их внутреннее состояние.
    private MoveInChoresController _moveInChores;
    private PieQuestController _pieQuest;
    // Ночь 1 (спокойная) — T-13. Пока ведёт напрямую в старую цепочку расследования
    // (SetupSearchNoiseQuest); когда день 2 будет собран (T-14..T-17), тот же
    // колбэк-параметр переключится на старт дня 2 — сама NightOneController не изменится.
    private NightOneController _nightOne;
    // День 2 (T-14..T-17) — тоже сиблинги, не части друг друга. Финальная цепочка
    // собирается по кускам: каждый новый тикет только переставляет одну стрелку
    // "куда идти дальше" на предыдущем звене, остальные не трогает.
    private RoomDecorationController _decoration;
    private DinnerCookingController _dinner;
    private EveningRoundController _eveningRound;

    // =====================================================================
    // Процедуры и инициализация
    // =====================================================================

    void Awake()
    {
        _pieQuest = new PieQuestController(
            pieObject, microwaveZone,
            CreateQuest, ShowThoughts, IsQuestActive,
            onDinnerFinished: SetupGoToBedQuest);

        _moveInChores = new MoveInChoresController(
            trashZone, roomZones,
            CreateQuest, ShowThoughts,
            onChoresFinished: _pieQuest.SetupTakePieQuest);

        _eveningRound = new EveningRoundController(
            frontDoor, CreateQuest, ShowThoughts,
            onEveningFinished: StartNightTwo); // T-18: fade+teleport+знакомая цепочка расследования, как в старом NightRoutine()

        _dinner = new DinnerCookingController(
            CreateQuest, ShowThoughts,
            onDinnerFinished: _eveningRound.SetupEveningWalkQuest);

        _decoration = new RoomDecorationController(
            decorationZones, CreateQuest, ShowThoughts,
            onDecorationFinished: _dinner.SetupIngredientQuest);

        _nightOne = new NightOneController(
            fadeScreen, skySwitcher, knockController,
            TeleportPlayerToBed, StartCoroutine, ShowThoughts,
            onNightFinished: _decoration.SetupDecorationQuest);
    }

    void OnEnable()
    {
        GameEvents.OnBedTriggerReached += OnBedTriggerReached;
        GameEvents.OnBedTriggerReached += _eveningRound.HandleBedReached;
        GameEvents.OnKitchenNoiseHeard += OnKitchenTriggerReached;
        GameEvents.OnTrashDeliveryReady += _moveInChores.StartTrashDeliveryQuest;
        GameEvents.OnPieGrabbed += _pieQuest.HandlePieGrabbed;
        GameEvents.OnPieEaten += _pieQuest.OnPieEaten;
    }

    void OnDisable()
    {
        GameEvents.OnBedTriggerReached -= OnBedTriggerReached;
        GameEvents.OnBedTriggerReached -= _eveningRound.HandleBedReached;
        GameEvents.OnKitchenNoiseHeard -= OnKitchenTriggerReached;
        GameEvents.OnTrashDeliveryReady -= _moveInChores.StartTrashDeliveryQuest;
        GameEvents.OnPieGrabbed -= _pieQuest.HandlePieGrabbed;
        GameEvents.OnPieEaten -= _pieQuest.OnPieEaten;
    }

    void Start()
    {
        ResetUI();
        ResetTriggers();

        if (useDebugSkip && startStage != DebugStoryStage.None)
            ApplyDebugSkip();
        else
            Invoke(nameof(StartIntro), 1.0f);
    }

    void ResetUI()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.questUiText != null)
            QuestManager.Instance.questUiText.text = "";
    }

    void ResetTriggers()
    {
        GameState.frontDoorLocked = false;
        if (holeEventController != null) holeEventController.enabled = false;
        if (trashZone != null) trashZone.SetActive(false);
        if (roomZones != null)
        {
            for (int i = 0; i < roomZones.Length; i++)
            {
                if (roomZones[i] != null) roomZones[i].SetActive(false);
            }
        }
        if (microwaveZone != null) microwaveZone.SetActive(false);
        if (decorationZones != null)
        {
            for (int i = 0; i < decorationZones.Length; i++)
            {
                if (decorationZones[i] != null) decorationZones[i].SetActive(false);
            }
        }
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        if (finaleTrigger != null) finaleTrigger.SetActive(false);
        // Items now stay visible on the map - they're controlled by quest requirements instead
        // if (flashlightItem != null) flashlightItem.SetActive(false);
        // if (crowbarItem != null) crowbarItem.SetActive(false);
        // if (hammerItem != null) hammerItem.SetActive(false);
        // nightStartTrigger.SetActive(false) убрано — кровать всегда видна
        if (knockController != null) knockController.SetActive(false);

        if (TrashManager.Instance != null)
            TrashManager.Instance.HideAll();
    }

    // =====================================================================
    // Отладка
    // =====================================================================

    void ApplyDebugSkip()
    {
        Debug.LogWarning($"[DEBUG] Начальная стадия: {startStage}");

        if (startStage >= DebugStoryStage.StartKitchenNoise)
        {
            if (skySwitcher != null) skySwitcher.isDayTime = false;
            RenderSettings.fog = false;
            if (knockController != null) knockController.SetActive(true);
            TeleportPlayerToBed();
        }

        switch (startStage)
        {
            case DebugStoryStage.StartBoxQuest:
                _moveInChores.SetupBoxQuest();
                break;
            case DebugStoryStage.StartNight:
                StartCoroutine(NightRoutine());
                break;
            case DebugStoryStage.StartKitchenNoise:
                if (nightTrigger != null) nightTrigger.SetActive(true);
                SetupSearchNoiseQuest();
                break;
            case DebugStoryStage.StartFlashlightQuest:
                SetupFlashlightQuest();
                break;
            case DebugStoryStage.StartCrowbarQuest:
                if (playerInventory != null) playerInventory.hasFlashlight = true;
                SetupCrowbarQuest();
                break;
            case DebugStoryStage.StartBreakFloor:
                if (playerInventory != null) playerInventory.hasFlashlight = true;
                if (playerInventory != null) playerInventory.hasCrowbar = true;
                SetupBreakFloorQuest();
                break;
            case DebugStoryStage.StartLookInHole:
                if (playerInventory != null) playerInventory.hasFlashlight = true;
                SetupLookInHoleQuest();
                break;
            case DebugStoryStage.StartHammerQuest:
                SetupHammerQuest();
                break;
            case DebugStoryStage.StartFinale:
                OnHammerPickedUp();
                break;
        }
    }

    // =====================================================================
    // Вспомогательные методы
    // =====================================================================

    void TeleportPlayerToBed()
    {
        if (playerTransform == null) return;

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = nightSpawnPosition;
        playerTransform.rotation = Quaternion.Euler(nightSpawnRotation);
        if (cc != null) cc.enabled = true;
    }

    void CreateQuest(string title, int amount, UnityEngine.Events.UnityAction callback = null, string questTag = "")
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.CreateQuest(title, amount, callback, questTag);
    }

    // =====================================================================
    // Этап 1 и сюжетные задания
    // =====================================================================

    // --- 1. Начальный монолог ---
    void StartIntro()
    {
        ShowThoughts(new string[] {
            "Наконец-то - своё жильё.",
            "Хоть и прошлые хозяева его конечно засрали."
        }, _moveInChores.SetupTrashQuest);
    }

    void SetupGoToBedQuest()
    {
        // nightStartTrigger.SetActive(true) убрано — кровать всегда видна
        CreateQuest("Пойти к кровати", 1, OnGoToBedFinished, "go-to-bed");
    }

    public void OnBedTriggerReached()
    {
        if (QuestManager.Instance == null) return;
        if (QuestManager.Instance.currentQuestIndex < 0 ||
            QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return;

        if (!QuestManager.Instance.IsQuestActive("go-to-bed")) return;

        // nightStartTrigger.SetActive(false) убрано — кровать всегда видна
        QuestManager.Instance.AddProgress(1); // Завершить квест
    }

    void OnGoToBedFinished()
    {
        _nightOne.Run();
    }

    // Финал дня 2 (T-18) — переиспользует существующую цепочку fade->teleport->стук->
    // расследование дословно, только точка входа теперь после EveningRoundController,
    // а не сразу после пирога дня 1.
    void StartNightTwo()
    {
        StartCoroutine(NightRoutine());
    }


    // =====================================================================
    // Наступление ночи
    // =====================================================================

    IEnumerator NightRoutine()
    {
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            if (fadeScreen != null)
                fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        GameEvents.OnNightStarted?.Invoke();
        if (skySwitcher != null) skySwitcher.isDayTime = false;
        RenderSettings.fog = false;
        TeleportPlayerToBed();

        // IntroSequence сама включает стук; отдельный NightStartTrigger для этого больше не нужен
        if (knockController != null) knockController.SetActive(true);

        yield return new WaitForSeconds(2f);
        if (nightTrigger != null) nightTrigger.SetActive(true);

        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            if (fadeScreen != null)
                fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        ShowThoughts(new string[] {
            "...?",
            "Что это было?..",
            "Я только въехал. Почему оно не пропало."
        }, SetupSearchNoiseQuest);
    }

    // =====================================================================
    // Расследование шума и поиск снаряжения
    // =====================================================================

    // --- 3. Поиск источника шума ---
    void SetupSearchNoiseQuest()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(true);
        CreateQuest("Проверить источник шума", 1, OnKitchenQuestCompleted);
    }

    public void OnKitchenTriggerReached()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.AddProgress(1);
    }

    void OnKitchenQuestCompleted()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        ShowThoughts(new string[] {
            "Под полом.",
            "Прямо подо мной.",
            "Без света я туда не полезу."
        }, SetupFlashlightQuest);
    }

    // --- 4. Найти фонарик ---
    void SetupFlashlightQuest()
    {
        if (flashlightItem != null) flashlightItem.SetActive(true);
        CreateQuest("Найти фонарик", 1, OnFlashlightPickedUp, "flashlight-find");
    }

    public void OnFlashlightPickedUp()
    {
        ShowThoughts(new string[] {
            "Свет есть.",
            "Теперь бы лом."
        }, SetupCrowbarQuest);
    }

    // --- 5. Поиск лома ---
    void SetupCrowbarQuest()
    {
        if (crowbarItem != null) crowbarItem.SetActive(true);
        CreateQuest("Найти лом", 1, OnCrowbarPickedUp, "crowbar-find");
    }

    public void OnCrowbarPickedUp()
    {
        ShowThoughts(new string[] {
            "Нашёл.",
            "Ненавижу это чувство."
        }, SetupBreakFloorQuest);
    }

    // --- 6. Пробить пол ---
    void SetupBreakFloorQuest()
    {
        CreateQuest("Вскрыть доски на кухне", 1, OnFloorBroken, "break-floor");
    }

    public void OnFloorBroken()
    {
        ShowThoughts(new string[] {
            "Ничего не видно.",
            "Ладно хоть фонарик взял."
        }, SetupLookInHoleQuest);
    }

    // --- 7. Заглянуть в дыру ---
    void SetupLookInHoleQuest()
    {
        CreateQuest("Заглянуть в дыру", 1, OnHoleEventFinished);
        if (holeEventController != null)
            holeEventController.enabled = true;
    }

    // =====================================================================
    // Монстр и молоток
    // =====================================================================

    // --- 8. Завершить событие в дыры ---
    public void OnHoleEventFinished()
    {
        if (MonsterTimer.Instance != null)
            MonsterTimer.Instance.StartTimer();
        ShowThoughts(new string[] {
            "НЕТ.",
            "Эта тварь выскочила прямо на меня.",
            "Нужно заколотить её.",
            "Прямо сейчас."
        }, SetupHammerQuest);
    }

    void SetupHammerQuest()
    {
        if (hammerItem != null) hammerItem.SetActive(true);
        CreateQuest("Найти молоток", 1, OnHammerPickedUp, "hammer-find");
    }

    // --- 9. Молоток и финал ---
    public void OnHammerPickedUp()
    {
        ShowThoughts(new string[] {
            "Взял.",
            "Я всё исправлю."
        }, () =>
        {
            if (finaleTrigger != null) finaleTrigger.SetActive(true);
            CreateQuest("Закрыть дыру", 1);
        });
    }

    // --- Квест с пирогом: внешний контракт (MicrowaveInteractable, CrosshairJuice,
    // PlayerInteraction зовут эти методы напрямую) — тонкие проброс-методы в PieQuestController.

    public bool CanPlacePieInMicrowave()
    {
        return _pieQuest.CanPlacePieInMicrowave();
    }

    public bool CanEatPie()
    {
        return _pieQuest.CanEatPie();
    }

    public void OnPiePlacedInMicrowave()
    {
        _pieQuest.OnPiePlacedInMicrowave();
    }

    // --- Ужин дня 2: тот же тонкий проброс-паттерн, StoveInteractable зовёт напрямую. ---

    public bool CanCookDinner()
    {
        return _dinner.CanCook;
    }

    public void OnDinnerEaten()
    {
        _dinner.OnDinnerEaten();
    }

    public void OnFrontDoorLocked()
    {
        _eveningRound.LockFrontDoor();
    }

    private void ShowThoughts(string[] lines, System.Action onComplete)
    {
        if (ThoughtManager.Instance != null)
        {
            ThoughtManager.Instance.ShowThoughts(lines, onComplete);
            return;
        }

        onComplete?.Invoke();
    }

    bool IsQuestActive(string questTag)
    {
        if (QuestManager.Instance == null) return false;
        if (QuestManager.Instance.currentQuestIndex < 0 ||
            QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return false;
        QuestManager.QuestData activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
        return activeQuest != null && activeQuest.questTag == questTag;
    }
}
}
