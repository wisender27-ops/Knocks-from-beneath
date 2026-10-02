using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public enum DebugStoryStage
{
    None,
    StartBoxQuest,
    // День 2 (T-14..T-17) — стадии стоят до StartNight намеренно: ApplyDebugSkip
    // включает ночь только начиная со StartKitchenNoise, а день 2 идёт при свете.
    StartDay2Decoration,
    StartDay2Dinner,
    StartDay2Evening,
    StartNight,
    StartKitchenNoise,
    StartFlashlightQuest,
    StartCrowbarQuest,
    StartBreakFloor,
    StartLookInHole,
    StartHammerQuest,
    StartFinale,
    // Развилка ночи 2 (T-19/T-20) — сразу после выхода монстра из дыры,
    // все три пути открыты.
    StartBranchEndings,
    StartPieMicrowaveQuest,
    StartTrashQuest,
    StartTrashDeliveryQuest,
    StartPieQuest,
    StartPieEatQuest,
    StartGoToBed,
    StartNightOne,
    StartDinnerCook,
    StartDinnerEat
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

    [Header("День 2 — разбросанные вещи на кухне (T-14)")]
    [Tooltip("У каждой кухонной вещи одна зона возврата на её прежнее место.")]
    public GameObject[] decorationZones;

    [Header("День 2 — вечерний обход (T-17)")]
    [Tooltip("Входная дверь дома — EveningRoundController запирает её через Door.SetInteractionLocked, если игрок выберет 'запереть'.")]
    public Door frontDoor;

    [Header("Ночь 2 — развилка концовок (T-19)")]
    [Tooltip("Триггер у входной двери — концовка 'побег'. Включается BranchEndingController, если дверь не заперта.")]
    public GameObject escapeDoorTrigger;

    [Header("Ночь 2 — укрытия (T-20)")]
    [Tooltip("3 зоны укрытия (кровать/шкаф/корзина). Индекс должен совпадать с hidingSpotAmbushPoints.")]
    public GameObject[] hidingSpots;
    [Tooltip("Точка засады монстра для каждой зоны укрытия — тот же индекс, что и в hidingSpots. Расставляется автором в редакторе рядом с дверью каждой комнаты.")]
    public Transform[] hidingSpotAmbushPoints;

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
    private BranchEndingController _branchEnding;
    private HideEndingController _hideEnding;

    // =====================================================================
    // Процедуры и инициализация
    // =====================================================================

    void Awake()
    {
        FadeTransitionPresentation.Ensure(fadeScreen);
        _pieQuest = new PieQuestController(
            pieObject, microwaveZone,
            CreateQuest, ShowThoughts, IsQuestActive,
            onDinnerFinished: SetupGoToBedQuest,
            recordCompletion: tag => { QuestManager.Instance?.RecordQuestCompletion(tag); });

        _moveInChores = new MoveInChoresController(
            trashZone, roomZones,
            CreateQuest, ShowThoughts,
            onChoresFinished: _pieQuest.SetupTakePieQuest);

        _eveningRound = new EveningRoundController(
            frontDoor, CreateQuest, ShowThoughts,
            onEveningFinished: StartNightTwo); // T-18: fade+teleport+знакомая цепочка расследования, как в старом NightRoutine()

        _dinner = new DinnerCookingController(
            CreateQuest, ShowThoughts,
            onDinnerFinished: _eveningRound.SetupEveningWalkQuest,
            recordCompletion: tag => { QuestManager.Instance?.RecordQuestCompletion(tag); });

        _decoration = new RoomDecorationController(
            decorationZones, CreateQuest, ShowThoughts,
            onDecorationFinished: _dinner.SetupIngredientQuest);

        _hideEnding = new HideEndingController(
            hidingSpots, hidingSpotAmbushPoints, playerTransform,
            StartCoroutine, ShowThoughts, LockPlayerCamera,
            tryClaimEnding: () => _branchEnding.TryResolve(), // общая защёлка всех концовок ночи 2
            endGame: EndGame);

        _branchEnding = new BranchEndingController(
            SetupHammerQuest, escapeDoorTrigger, _hideEnding.SetZonesActive,
            playerTransform, StartCoroutine, ShowThoughts, LockPlayerCamera, EndGame);

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
        GameEvents.OnMonsterTimerExpired += _branchEnding.HandleTimerExpired;
    }

    void OnDisable()
    {
        GameEvents.OnBedTriggerReached -= OnBedTriggerReached;
        GameEvents.OnBedTriggerReached -= _eveningRound.HandleBedReached;
        GameEvents.OnKitchenNoiseHeard -= OnKitchenTriggerReached;
        GameEvents.OnTrashDeliveryReady -= _moveInChores.StartTrashDeliveryQuest;
        GameEvents.OnPieGrabbed -= _pieQuest.HandlePieGrabbed;
        GameEvents.OnPieEaten -= _pieQuest.OnPieEaten;
        GameEvents.OnMonsterTimerExpired -= _branchEnding.HandleTimerExpired;
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
        if (escapeDoorTrigger != null) escapeDoorTrigger.SetActive(false);
        if (hidingSpots != null)
        {
            for (int i = 0; i < hidingSpots.Length; i++)
            {
                if (hidingSpots[i] != null) hidingSpots[i].SetActive(false);
            }
        }
        // Items now stay visible on the map - they're controlled by quest requirements instead
        // if (flashlightItem != null) flashlightItem.SetActive(false);
        // if (crowbarItem != null) crowbarItem.SetActive(false);
        // if (hammerItem != null) hammerItem.SetActive(false);
        // nightStartTrigger.SetActive(false) убрано — кровать всегда видна
        if (knockController != null) knockController.SetActive(false);

        // Кучи мусора (garbagepile 1-3) теперь видны с самого начала — не прячем их
        // здесь, TrashManager больше не скрывает их сам. Недоступность подбора до
        // старта квеста 'trash-collect' обеспечивает TrashPile.Collect().
    }

    // =====================================================================
    // Отладка
    // =====================================================================

    void ApplyDebugSkip()
    {
        Debug.LogWarning($"[DEBUG] Начальная стадия: {startStage}");

        if (startStage != DebugStoryStage.None && startStage != DebugStoryStage.StartTrashQuest &&
            startStage != DebugStoryStage.StartTrashDeliveryQuest && startStage != DebugStoryStage.StartBoxQuest && roomZones != null)
        {
            if (TrashManager.Instance != null && TrashManager.Instance.trashPiles != null)
                foreach (GameObject pile in TrashManager.Instance.trashPiles)
                    if (pile != null) pile.SetActive(false);
            foreach (GameObject zone in roomZones)
                if (zone != null) zone.GetComponent<PlacementZone>()?.RevealItemsForDebug();
        }

        if (IsNightDebugStage(startStage))
        {
            if (skySwitcher != null) skySwitcher.isDayTime = false;
            RenderSettings.fog = false;
            if (knockController != null) knockController.SetActive(true);
            TeleportPlayerToBed();
        }
        if (IsNightDebugStage(startStage) && startStage != DebugStoryStage.StartNight)
            GameEvents.OnNightStarted?.Invoke();

        switch (startStage)
        {
            case DebugStoryStage.StartTrashQuest:
                _moveInChores.SetupTrashQuest();
                break;
            case DebugStoryStage.StartTrashDeliveryQuest:
                _moveInChores.StartTrashDeliveryQuest();
                break;
            case DebugStoryStage.StartBoxQuest:
                _moveInChores.SetupBoxQuest();
                break;
            case DebugStoryStage.StartPieQuest:
                _pieQuest.SetupTakePieQuest();
                break;
            case DebugStoryStage.StartPieMicrowaveQuest:
                _pieQuest.SetupMicrowaveQuest();
                break;
            case DebugStoryStage.StartPieEatQuest:
                _pieQuest.SetupEatQuest();
                break;
            case DebugStoryStage.StartGoToBed:
                SetupGoToBedQuest();
                break;
            case DebugStoryStage.StartNightOne:
                _nightOne.Run();
                break;
            case DebugStoryStage.StartDay2Decoration:
                _decoration.SetupDecorationQuest();
                break;
            case DebugStoryStage.StartDay2Dinner:
                _dinner.SetupIngredientQuest();
                break;
            case DebugStoryStage.StartDinnerCook:
                _dinner.SetupCookDinnerDebug();
                FindFirstObjectByType<StoveInteractable>()?.ResetForCookDebug();
                break;
            case DebugStoryStage.StartDinnerEat:
                _dinner.SetupEatDinnerDebug();
                FindFirstObjectByType<StoveInteractable>()?.SetReadyForDebug();
                break;
            case DebugStoryStage.StartDay2Evening:
                _eveningRound.SetupEveningWalkQuest();
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
                GrantDebugItem("Flashlight");
                SetupCrowbarQuest();
                break;
            case DebugStoryStage.StartBreakFloor:
                // Раньше эта стадия ломала пол на кухне (FloorLogic.SetCompletedForDebug).
                // Кухонной дыры в сюжете больше нет — стадия теперь прыгает к замку люка.
                GrantDebugItem("Flashlight");
                SetupPryLockQuest();
                break;
            case DebugStoryStage.StartLookInHole:
                GrantDebugItem("Flashlight");
                GrantDebugItem("Crowbar");
                SetupLookInHoleQuest();
                break;
            case DebugStoryStage.StartHammerQuest:
                GrantDebugItem("Flashlight");
                GrantDebugItem("Crowbar");
                FindFirstObjectByType<BasementHatch>()?.DebugUnlock();
                SetupHammerQuest();
                break;
            case DebugStoryStage.StartFinale:
                GrantDebugItem("Flashlight");
                GrantDebugItem("Crowbar");
                GrantDebugItem("Hammer");
                FindFirstObjectByType<BasementHatch>()?.DebugUnlock();
                OnHammerPickedUp();
                break;
            case DebugStoryStage.StartBranchEndings:
                GrantDebugItem("Flashlight");
                GrantDebugItem("Crowbar");
                FindFirstObjectByType<BasementHatch>()?.DebugUnlock();
                if (MonsterTimer.Instance != null)
                    MonsterTimer.Instance.StartTimer();
                _branchEnding.Activate();
                break;
        }
    }

    static bool IsNightDebugStage(DebugStoryStage stage)
    {
        switch (stage)
        {
            case DebugStoryStage.StartNight:
            case DebugStoryStage.StartKitchenNoise:
            case DebugStoryStage.StartFlashlightQuest:
            case DebugStoryStage.StartCrowbarQuest:
            case DebugStoryStage.StartBreakFloor:
            case DebugStoryStage.StartLookInHole:
            case DebugStoryStage.StartHammerQuest:
            case DebugStoryStage.StartFinale:
            case DebugStoryStage.StartBranchEndings:
                return true;
            default:
                return false;
        }
    }

    void GrantDebugItem(string itemId)
    {
        if (playerInventory == null || playerInventory.HasItem(itemId)) return;
        if (playerInventory.AddItem(itemId)) return;

        // Keep debug entry usable in scenes where the inventory database is not assigned.
        if (itemId == "Flashlight") playerInventory.hasFlashlight = true;
        else if (itemId == "Crowbar") playerInventory.hasCrowbar = true;
        else if (itemId == "Hammer") playerInventory.hasHammer = true;
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
            "Ну вот. Свой дом. Может, хоть здесь будет спокойно.",
            "Семья съехала, а срач оставила мне. Спасибо."
        }, _moveInChores.SetupTrashQuest);
    }

    void SetupGoToBedQuest()
    {
        // nightStartTrigger.SetActive(true) убрано — кровать всегда видна
        CreateQuest("Лечь спать", 1, OnGoToBedFinished, "go-to-bed");
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
            "Опять этот стук.",
            "Да чтоб тебя... Я же уехал.",
            "Ладно. На этот раз выясню, откуда он."
        }, SetupSearchNoiseQuest);
    }

    // =====================================================================
    // Расследование шума и поиск снаряжения
    // =====================================================================

    // --- 3. Поиск источника шума ---
    void SetupSearchNoiseQuest()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(true);
        CreateQuest("Проверить шум на кухне", 1, OnKitchenQuestCompleted);
    }

    public void OnKitchenTriggerReached()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.AddProgress(1);
    }

    void OnKitchenQuestCompleted()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        // T-21: источник шума найден — RandomKnock (knockController) раньше продолжал
        // стучать всю оставшуюся ночь, включая сцену у дыры и развилку концовок.
        if (knockController != null) knockController.SetActive(false);
        ShowThoughts(new string[] {
            "Стук идёт снизу. Из подвала.",
            "Только бы не та же история...",
            "Без фонаря я туда не полезу."
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
        // Раньше здесь стояло «Теперь бы лом» — реплика была верна для старого порядка,
        // где лом искался сразу после фонаря. Теперь лом нужен только ПОСЛЕ отказа на
        // запертом люке, поэтому реплика ведёт наружу, а не к лому.
        ShowThoughts(new string[] {
            "Хоть не в темноте шариться.",
            "Вход в подвал снаружи. Пойду проверю люк."
        }, SetupHatchQuest);
    }

    // --- 5. Идём к подвалу и пробуем открыть ---
    // Ночь 2 перестроена вокруг уличного люка: игрок берёт фонарик, выходит на улицу,
    // пробует E на створках и получает отказ — замок. Только после этого он идёт за ломом.
    // Прежний шаг «вскрыть доски на кухне» (break-floor) удалён целиком вместе с
    // FloorLogic: он вёл в кухонную дыру, а скример обслуживает подвал, и игрок ломал пол
    // ради квеста, который засчитывался в другом конце карты.
    void SetupHatchQuest()
    {
        CreateQuest("Проверить люк подвала снаружи", 1, OnHatchLockedFound, "open-basement");
    }

    // Игрок нажал E на запертом люке — засчитываем шаг и отправляем за ломом.
    public void OnHatchLockedFound()
    {
        ShowThoughts(new string[] {
            "Ещё и замок.",
            "Ключа нет. Придётся найти лом."
        }, SetupCrowbarQuest);
    }

    // --- 6. Поиск лома ---
    void SetupCrowbarQuest()
    {
        if (crowbarItem != null) crowbarItem.SetActive(true);
        CreateQuest("Найти лом", 1, OnCrowbarPickedUp, "crowbar-find");
    }

    public void OnCrowbarPickedUp()
    {
        ShowThoughts(new string[] {
            "Этим замок можно сорвать.",
            "Ночью вскрывать собственный подвал. Отличный переезд."
        }, SetupPryLockQuest);
    }

    // --- 7. Вскрыть замок люка ---
    void SetupPryLockQuest()
    {
        CreateQuest("Сорвать замок с люка подвала", 1, OnHatchLockPried, "pry-hatch-lock");
    }

    public void OnHatchLockPried()
    {
        ShowThoughts(new string[] {
            "Теперь можно открыть люк.",
            "Сперва посветить на лестницу. Вслепую туда не полезу."
        }, SetupLookInHoleQuest);
    }

    // --- 8. Открыть створки и посветить вниз ---
    void SetupLookInHoleQuest()
    {
        CreateQuest("Открыть подвал и осветить лестницу", 1, OnHoleEventFinished);
        if (holeEventController != null)
        {
            holeEventController.gameObject.SetActive(true);
            holeEventController.enabled = true;
        }
    }

    // =====================================================================
    // Монстр и молоток
    // =====================================================================

    // --- 8. Завершить событие в дыры ---
    // Таймер запускается ПОСЛЕ монолога (в момент, когда реально открывается развилка), а
    // не до него: раньше StartTimer() вызывался прямо тут и все ~16 сек монолога (4 реплики,
    // typingSpeed 0.05 / displayDuration 2.5) утекали из отведённых 30 сек до того, как
    // игрок вообще получал контроль — на решение оставалось ~14 сек, ещё и с тикающей
    // надписью таймера поверх непропускаемого текста. Монолог сохранён целиком, тревога
    // тоже никуда не делась (сама сцена достаточно жуткая), а честные 30 секунд на выбор
    // игрок теперь получает именно тогда, когда видит развилку и может действовать.
    public void OnHoleEventFinished()
    {
        ShowThoughts(new string[] {
            "Оно исчезло.",
            "Нет. Я не стану опять от него бежать.",
            "Заколочу люк. И всё. Больше не вылезет.",
            "Нужен молоток."
        }, () =>
        {
            if (MonsterTimer.Instance != null)
                MonsterTimer.Instance.StartTimer();
            _branchEnding.Activate();
        });
    }

    // --- Развилка ночи 2 (T-19): побег через дверь — тонкий проброс, EscapeDoorTrigger
    // зовёт напрямую тем же паттерном, что Stove/Microwave/FrontDoorLock. ---

    public void OnEscapedThroughDoor()
    {
        _branchEnding.HandleDoorEscape();
    }

    // --- Развилка ночи 2 (T-20): прятки — тонкий проброс, HidingSpotTrigger зовёт напрямую. ---

    public void OnHidingSpotEntered(GameObject zone)
    {
        _hideEnding.HandleZoneEntered(zone);
    }

    public void OnHidingSpotExited(GameObject zone)
    {
        _hideEnding.HandleZoneExited(zone);
    }

    void LockPlayerCamera(bool locked)
    {
        if (playerTransform == null) return;
        var pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null) pc.isCameraLocked = locked;
    }

    // Общая защёлка развилки ночи 2 — внешний контракт для FinaleController (концовка
    // молотка), который не знает про _branchEnding напрямую и зовёт через IntroSequence тем
    // же тонким проброс-паттерном, что и остальные интерактивы (T-21).
    public bool TryClaimEnding()
    {
        return _branchEnding.TryResolve();
    }

    // T-21: раньше тут был Application.Quit() сразу после последней реплики — в билде игра
    // просто закрывалась. Теперь показываем карточку концовки и возвращаемся в главное меню.
    void EndGame(string endingTitle)
    {
        if (EndingScreen.Instance != null)
            EndingScreen.Instance.ShowEnding(endingTitle);
    }

    void SetupHammerQuest()
    {
        if (hammerItem != null) hammerItem.SetActive(true);
        CreateQuest("Найти молоток", 1, OnHammerPickedUp, "hammer-find");
    }

    // --- 9. Молоток и финал ---
    public void OnHammerPickedUp()
    {
        MonsterTimer.Instance?.PauseTimer();
        ShowThoughts(new string[] {
            "Заколочу. И наконец всё закончится.",
            "Надо вернуться к люку."
        }, () =>
        {
            if (finaleTrigger != null) finaleTrigger.SetActive(true);
            CreateQuest("Заколотить люк подвала", 1, questTag: "board-basement");
            MonsterTimer.Instance?.ResumeTimer();
        });
    }

    public bool CanBoardBasement(BasementHatch hatch)
    {
        return hatch != null && holeEventController != null &&
            hatch == holeEventController.basementHatch && playerInventory != null &&
            playerInventory.hasHammer && IsQuestActive("board-basement");
    }

    public bool TryBoardBasement(BasementHatch hatch)
    {
        if (!CanBoardBasement(hatch) || finaleTrigger == null || playerTransform == null) return false;
        FinaleController finale = finaleTrigger.GetComponent<FinaleController>();
        return finale != null && finale.BeginBoarding(
            playerTransform.GetComponent<PlayerController>(), _branchEnding.DeadlineExpired);
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

    public void OnDinnerCooked()
    {
        _dinner.OnDinnerCooked();
    }

    public void OnDinnerEaten()
    {
        _dinner.OnDinnerEaten();
    }

    public bool OnFrontDoorLocked()
    {
        return _eveningRound.LockFrontDoor();
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
