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
    StartDinnerEat,
    // M-04: ночь 2, свет выбило, фонарик в руках — игрок у щитка в гараже.
    StartBreakerPanel,
    // M-07: ночь 2, игрок у стола с молотком в своей спальне — взял молоток, и начинаются прятки под кроватью.
    StartHidingSpots,
    // C-02: утро дня 2, камера стоит на штативе — сразу показывается ночная запись (до телевизора C-03).
    PlayNightRecording
}

public class IntroSequence : MonoBehaviour
{
    // =====================================================================
    // INSPECTOR
    // =====================================================================

    [Header("Настройки дебага")]
    public bool useDebugSkip = false;
    public DebugStoryStage startStage = DebugStoryStage.None;

    [Header("Начало игры")]
    [Tooltip("Чёрный экран с предысторией и катсцена приезда к дому. Без неё игра стартует сразу в доме.")]
    public ArrivalCutscene openingCutscene;

    [Header("Игровые объекты")]
    public Transform playerTransform;
    public PlayerInventory playerInventory;

    [Header("Ночные параметры")]
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject nightTrigger;
    public Vector3 nightSpawnPosition;
    public Vector3 nightSpawnRotation;

    [Header("Ночь 2 — точка пробуждения")]
    public Vector3 nightTwoBedSpawnPosition = new Vector3(-43f, 4.16f, 118.6f);
    public Vector3 nightTwoSpawnRotation = new Vector3(0f, 140f, 0f);

    [Header("Зоны активности")]
    public GameObject trashZone;
    [Tooltip("Зона для каждой из коробок дня 1 (T-12) — одна PlacementZone+RoomRevealZone на комнату.")]
    public GameObject[] roomZones;
    [Tooltip("Фургон, который после выноса мусора привозит коробки дня 1. Без него коробки просто стоят во дворе.")]
    public DeliveryTruckSequence boxDeliveryTruck;
    public GameObject kitchenNoiseTrigger;
    [Tooltip("Зона за входной дверью (HouseEntryTrigger) — квест 'Зайти в дом'.")]
    public GameObject houseEntryTrigger;
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

    [Header("Первая ночь — визит соседа")]
    public NeighborVisitSequence neighborVisit;

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
    private CornerPeekSequence _cornerPeek;
    private HideEndingController _hideEnding;
    private HidingBreathGame _hidingScene;
    private CameraPlacementController _cameraPlacement;
    // C-02: на ночной записи двери открываются от закрытого положения — снимаем его до того, как их тронут.
    private TapeDoor[] _tapeDoors;
    private NightTapeViewer _nightTape;
    private bool _hasReactedToPowerOutage;
    private AudioSource _powerOutageAudio;
    // K-02: хозяин в подвале отвечает на стук с первой ночи (до неё о нём никто не знает).
    private bool _knockerAwake;
    private bool _nightTwoStarted; // K-03: уровень «Перестука» по главе
    private RandomKnock _knockSound;
    // M-04 «Щиток»: после «Свет не работает» и фонарика — щиток в гараже.
    private BreakerPanel _breakerPanel;
    private bool _breakerPending;

    // =====================================================================
    // Процедуры и инициализация
    // =====================================================================

    void Awake()
    {
        FadeTransitionPresentation.Ensure(fadeScreen);
        _cameraPlacement = gameObject.AddComponent<CameraPlacementController>();
        _cameraPlacement.Initialize(roomZones, OnCameraPlaced, OnCameraRemoved);
        _tapeDoors = NightRecording.CaptureDoors(frontDoor);
        var horrorDirector = gameObject.AddComponent<HorrorMomentDirector>();
        horrorDirector.Initialize(playerInventory);
        var firstNightScare = gameObject.AddComponent<NightOneScare>();
        firstNightScare.Initialize(playerInventory);
        var floorKnock = gameObject.AddComponent<PlayerFloorKnock>(); // K-02: Q — стук в пол
        RandomKnock knockSound = _knockSound = knockController != null ? knockController.GetComponent<RandomKnock>() : null;
        floorKnock.Initialize(playerInventory, knockSound != null ? knockSound.ShortestClip : null);
        floorKnock.SetBasementArea(BasementMin, BasementMax, FirstFloorY);
        var followingSteps = gameObject.AddComponent<FollowingFootsteps>();
        followingSteps.Initialize(playerInventory);
        var kitchenDisturbance = gameObject.AddComponent<KitchenDisturbance>();
        kitchenDisturbance.Initialize(this, playerInventory);
        kitchenDisturbance.ConfigureAudio(Resources.Load<AudioClip>("Audio/Horror/PlateScrape"),
            Resources.Load<AudioClip>("Audio/Horror/PlateImpact"));
        _pieQuest = new PieQuestController(
            pieObject, microwaveZone,
            CreateQuest, ShowThoughts, IsQuestActive,
            onDinnerFinished: SetupGoToBedQuest,
            recordCompletion: tag => { QuestManager.Instance?.RecordQuestCompletion(tag); });

        _moveInChores = new MoveInChoresController(
            trashZone, roomZones,
            CreateQuest, ShowThoughts,
            onChoresFinished: _pieQuest.SetupTakePieQuest,
            boxDelivery: boxDeliveryTruck);

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

        // M-07: укрытие ночи 2 — под кроватью героя, в спальне с молотком (старые зоны hidingSpots не используются).
        _hidingScene = gameObject.AddComponent<HidingBreathGame>();
        _hidingScene.Initialize(playerInventory, () => _hideEnding.HandleHideRequested(), () => _hideEnding.HandleCaughtInOpen());
        _hideEnding = new HideEndingController(
            new[] { _hidingScene.Zone }, new Transform[1], playerTransform,
            StartCoroutine, ShowThoughts, LockPlayerCamera,
            tryClaimEnding: () => _branchEnding.TryResolve(), // общая защёлка всех концовок ночи 2
            endGame: EndGame,
            search: _hidingScene,
            onHideStarted: () => { if (IsQuestActive("hide-bed")) QuestManager.Instance?.AddProgress(1); },
            onSurvived: ReturnToHatchAfterHiding);

        _branchEnding = new BranchEndingController(
            SetupHammerQuest, escapeDoorTrigger, _hideEnding.SetZonesActive,
            playerTransform, StartCoroutine, ShowThoughts, LockPlayerCamera, EndGame);

        _nightOne = new NightOneController(
            fadeScreen, skySwitcher, knockController,
            TeleportPlayerToBed, StartCoroutine, ShowImportantThoughts,
            onNightFinished: () => { SaveChapter(StoryChapter.DayTwo); _decoration.SetupDecorationQuest(); },
            nightScare: firstNightScare.Play,
            neighborVisit: neighborVisit != null ? neighborVisit.Play : (System.Func<IEnumerator>)null,
            didMeetNeighbor: () => neighborVisit != null && neighborVisit.HasVisited);

        SetupBedsideLamp();
        SetupPowerOutageAudio();
        SetupKnockDirector();
        var responder = gameObject.AddComponent<KnockResponder>();
        responder.Initialize(floorKnock, knockController != null ? knockController.GetComponent<KnockDirector>() : null,
            knockSound != null ? knockSound.ShortestClip : null, () => _knockerAwake);
        responder.Replied += (kind, count) =>
        {
            // Флаг ответа ставится после события — здесь он ещё говорит, был ли ответ раньше.
            string[] lines = KnockResponder.HeroReaction(kind, !StoryFlags.KnockAnswered, responder.LastRageTime == Time.time);
            if (lines != null) ShowThoughts(lines, null);
        };
        gameObject.AddComponent<KnockCallGame>().Initialize(floorKnock, responder, KnockCallLevel, lines => ShowThoughts(lines, null));
        _cornerPeek = gameObject.AddComponent<CornerPeekSequence>();
        _cornerPeek.Initialize(this, playerInventory, knockController, frontDoor);
        SetupBreakerPanel();
    }

    void OnEnable()
    {
        GameEvents.OnBedTriggerReached += OnBedTriggerReached;
        GameEvents.OnBedTriggerReached += _eveningRound.HandleBedReached;
        GameEvents.OnKitchenNoiseHeard += OnKitchenTriggerReached;
        GameEvents.OnHouseEntered += OnHouseEntered;
        GameEvents.OnNightStarted += WakeKnocker;
        GameEvents.OnNightTwoStarted += MarkNightTwo;
        GameEvents.OnTrashDeliveryReady += _moveInChores.StartTrashDeliveryQuest;
        GameEvents.OnPieGrabbed += _pieQuest.HandlePieGrabbed;
        GameEvents.OnPieEaten += _pieQuest.OnPieEaten;
        GameEvents.OnMonsterTimerExpired += _branchEnding.HandleTimerExpired;
        GameEvents.OnPowerOutageSwitchAttempt += HandlePowerOutageSwitchAttempt;
        GameEvents.OnNightTwoStarted += ResetPowerOutageThought;
    }

    void OnDisable()
    {
        if (_bedroomDoor != null)
        {
            _bedroomDoor.LockedAttempted -= OnBedroomDoorAttempt;
            _bedroomDoor.Opened -= OnBedroomDoorOpened;
            if (_checkingLight) _bedroomDoor.SetInteractionLocked(_bedroomDoorWasLocked);
        }
        GameEvents.OnBedTriggerReached -= OnBedTriggerReached;
        GameEvents.OnBedTriggerReached -= _eveningRound.HandleBedReached;
        GameEvents.OnKitchenNoiseHeard -= OnKitchenTriggerReached;
        GameEvents.OnHouseEntered -= OnHouseEntered;
        GameEvents.OnNightStarted -= WakeKnocker;
        GameEvents.OnNightTwoStarted -= MarkNightTwo;
        GameEvents.OnTrashDeliveryReady -= _moveInChores.StartTrashDeliveryQuest;
        GameEvents.OnPieGrabbed -= _pieQuest.HandlePieGrabbed;
        GameEvents.OnPieEaten -= _pieQuest.OnPieEaten;
        GameEvents.OnMonsterTimerExpired -= _branchEnding.HandleTimerExpired;
        GameEvents.OnPowerOutageSwitchAttempt -= HandlePowerOutageSwitchAttempt;
        GameEvents.OnNightTwoStarted -= ResetPowerOutageThought;
    }

    void Start()
    {
        ResetUI();
        ResetTriggers();

        if (ChapterCheckpoints.TryTakeLaunch(out StoryChapter chapter))
            StartChapter(chapter);
        else if (useDebugSkip && startStage != DebugStoryStage.None)
        {
            ChapterCheckpoints.DisableSaves(); // проверки с прыжка не открывают главы игроку
            ApplyDebugSkip(startStage);
        }
        else
            StartNewGame();
    }

    void StartNewGame()
    {
        SaveChapter(StoryChapter.DayOne);
        if (openingCutscene != null)
            openingCutscene.Play(StartIntro); // предыстория + приезд на такси, затем первые мысли
        else
            Invoke(nameof(StartIntro), 1.0f);
    }

    // =====================================================================
    // Чекпойнты по главам (F-04)
    // =====================================================================

    // «Продолжить»/«Выбор главы»: флаги и инвентарь главы, затем тот же прыжок, что у дебага.
    void StartChapter(StoryChapter chapter)
    {
        ChapterCheckpoints.ApplyLaunchState(playerInventory);
        _cameraPlacement?.RestoreFromCheckpoint();
        _eveningRound.ApplyRestoredLock();
        FindFirstObjectByType<FrontDoorLockInteractable>()?.SnapToState();
        DebugStoryStage stage = ChapterCheckpoints.Info(chapter).Stage;
        if (stage == DebugStoryStage.None)
        {
            StartNewGame();
            return;
        }
        SaveChapter(chapter);
        ApplyDebugSkip(stage);
        // Второй день начинается с пробуждения в кровати, как после первой ночи.
        if (chapter == StoryChapter.DayTwo) TeleportPlayerToBed();
    }

    void SaveChapter(StoryChapter chapter) => ChapterCheckpoints.Save(chapter, playerInventory);

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
        if (houseEntryTrigger != null) houseEntryTrigger.SetActive(false);
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

    void ApplyDebugSkip(DebugStoryStage startStage)
    {
        Debug.LogWarning($"[DEBUG] Начальная стадия: {startStage}");

        if (startStage == DebugStoryStage.StartPieQuest || startStage == DebugStoryStage.StartPieMicrowaveQuest ||
            startStage == DebugStoryStage.StartPieEatQuest || startStage == DebugStoryStage.StartGoToBed)
            _cameraPlacement?.RevealForDebug();

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
            TeleportPlayerToNightTwoBed();
        }
        if (IsNightDebugStage(startStage) && startStage != DebugStoryStage.StartNight)
        {
            GameEvents.OnNightStarted?.Invoke();
            GameEvents.OnNightTwoStarted?.Invoke();
            GameEvents.OnNightTwoAwake?.Invoke();
        }

        if (IsDayTwoDebugStage(startStage)) _knockerAwake = true; // ночь 1 уже была
        if (IsPastKitchenDebugStage(startStage)) _cornerPeek?.Retire(); // пугалка кухни уже позади
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
            case DebugStoryStage.PlayNightRecording:
                StoryFlags.SetCameraPlacementSlot(1);
                _cameraPlacement?.RestoreFromCheckpoint();
                _decoration.SetupDecorationQuest();
                StartCoroutine(PlayNightRecordingWhenFree());
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
                SetupBoardBasementQuest();
                break;
            case DebugStoryStage.StartBreakerPanel:
                GrantDebugItem("Flashlight");
                playerInventory?.Equip("Flashlight");
                if (playerInventory != null && playerInventory.EquippedItemId != "Flashlight") playerInventory.ActivateItem("Flashlight");
                if (playerInventory != null && playerInventory.flashlightLightSource != null)
                    playerInventory.flashlightLightSource.enabled = true;
                LightSwitch.CutHousePower(false);
                _hasReactedToPowerOutage = true;
                _breakerPending = _breakerPanel != null;
                if (_bedsideBulb != null) _bedsideBulb.Break();
                TeleportPlayerToBreakerPanel();
                SetupBreakerQuest();
                break;
            case DebugStoryStage.StartBranchEndings:
            case DebugStoryStage.StartHidingSpots:
                GrantDebugItem("Flashlight");
                GrantDebugItem("Crowbar");
                FindFirstObjectByType<BasementHatch>()?.DebugUnlock();
                if (MonsterTimer.Instance != null)
                    MonsterTimer.Instance.StartTimer();
                _branchEnding.Activate();
                if (startStage == DebugStoryStage.StartHidingSpots) TeleportPlayerToHammer();
                break;
        }
    }

    static bool IsPastKitchenDebugStage(DebugStoryStage stage)
    {
        switch (stage)
        {
            case DebugStoryStage.StartCrowbarQuest:
            case DebugStoryStage.StartBreakFloor:
            case DebugStoryStage.StartLookInHole:
            case DebugStoryStage.StartHammerQuest:
            case DebugStoryStage.StartFinale:
            case DebugStoryStage.StartBranchEndings:
            case DebugStoryStage.StartHidingSpots:
                return true;
            default:
                return false;
        }
    }

    static bool IsNightDebugStage(DebugStoryStage stage)
    {
        switch (stage)
        {
            case DebugStoryStage.StartBreakerPanel:
            case DebugStoryStage.StartNight:
            case DebugStoryStage.StartKitchenNoise:
            case DebugStoryStage.StartFlashlightQuest:
            case DebugStoryStage.StartCrowbarQuest:
            case DebugStoryStage.StartBreakFloor:
            case DebugStoryStage.StartLookInHole:
            case DebugStoryStage.StartHammerQuest:
            case DebugStoryStage.StartFinale:
            case DebugStoryStage.StartBranchEndings:
            case DebugStoryStage.StartHidingSpots:
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
        playerTransform.GetComponent<PlayerController>()?.StandUp();
    }

    // M-04: в гараже, в паре шагов от щитка, лицом к нему.
    void TeleportPlayerToBreakerPanel()
    {
        if (playerTransform == null) return;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        float feet = 1f;
        if (Physics.Raycast(playerTransform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            feet = playerTransform.position.y - floor.point.y;
        Vector3 stand = BreakerPanelPosition + BreakerPanelRotation * Vector3.forward * 1.6f;
        if (Physics.Raycast(stand, Vector3.down, out RaycastHit standFloor, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            stand.y = standFloor.point.y;
        if (cc != null) cc.enabled = false;
        playerTransform.position = stand + Vector3.up * feet;
        playerTransform.rotation = Quaternion.LookRotation(-(BreakerPanelRotation * Vector3.forward));
        if (cc != null) cc.enabled = true;
    }

    // M-07 (дебаг): у стола с молотком в спальне героя, фонарик в руке. Задание «Найти молоток» уже поставила
    // развилка (BranchEndingController.Activate) — второе такое же застряло бы перед «Спрятаться под кровать».
    void TeleportPlayerToHammer()
    {
        playerInventory?.Equip("Flashlight");
        if (playerInventory != null && playerInventory.flashlightLightSource != null)
            playerInventory.flashlightLightSource.enabled = true;
        if (playerTransform == null) return;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(-42.4f, 4.18f, 114.3f);
        playerTransform.rotation = Quaternion.LookRotation(Vector3.right);
        if (cc != null) cc.enabled = true;
    }

    void TeleportPlayerToNightTwoBed()
    {
        if (playerTransform == null) return;

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = nightTwoBedSpawnPosition;
        playerTransform.rotation = Quaternion.Euler(nightTwoSpawnRotation);
        if (cc != null) cc.enabled = true;
        playerTransform.GetComponent<PlayerController>()?.StandUp();
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
        ShowImportantThoughts(new string[] {
            "Ну вот. Свой дом. Может, хоть здесь будет спокойно.",
            "Семья съехала, а срач оставила мне. Спасибо."
        }, SetupEnterHouseQuest);
    }

    // Уборка начинается только когда игрок зашёл внутрь.
    void SetupEnterHouseQuest()
    {
        if (houseEntryTrigger != null) houseEntryTrigger.SetActive(true);
        CreateQuest("Зайти в дом", 1, _moveInChores.SetupTrashQuest, "enter-house");
    }

    void OnHouseEntered()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("enter-house"))
            QuestManager.Instance.AddProgress(1);
    }

    void SetupGoToBedQuest()
    {
        if (_cameraPlacement != null && _cameraPlacement.Available && !_cameraPlacement.IsPlaced)
        {
            CreateQuest("Установить видеокамеру на штатив", 1, SetupGoToBedQuest, "camera-place");
            return;
        }
        // nightStartTrigger.SetActive(true) убрано — кровать всегда видна
        CreateQuest("Лечь спать", 1, OnGoToBedFinished, "go-to-bed");
    }

    void OnCameraPlaced()
    {
        if (IsQuestActive("camera-place")) QuestManager.Instance?.AddProgress(1);
    }

    void OnCameraRemoved()
    {
        if (IsQuestActive("go-to-bed"))
            CreateQuest("Установить видеокамеру на штатив", 1, SetupGoToBedQuest, "camera-place");
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
        SaveChapter(StoryChapter.NightOne);
        _nightOne.Run();
    }

    // Финал дня 2 (T-18) — переиспользует существующую цепочку fade->teleport->стук->
    // расследование дословно, только точка входа теперь после EveningRoundController,
    // а не сразу после пирога дня 1.
    void StartNightTwo()
    {
        SaveChapter(StoryChapter.NightTwo);
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
        GameEvents.OnNightTwoStarted?.Invoke();
        if (skySwitcher != null) skySwitcher.isDayTime = false;
        RenderSettings.fog = false;
        TeleportPlayerToNightTwoBed();

        if (knockController != null) knockController.SetActive(false);

        yield return new WaitForSeconds(2f);
        if (nightTrigger != null) nightTrigger.SetActive(true);

        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            if (fadeScreen != null)
                fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        GameEvents.OnNightTwoAwake?.Invoke();

        SetupCheckLightQuest(false);
        RandomKnock firstKnock = knockController != null ? knockController.GetComponentInChildren<RandomKnock>(true) : null;
        int knockCount = firstKnock != null ? firstKnock.PlayedKnockCount : 0;
        if (knockController != null) knockController.SetActive(true);
        if (firstKnock == null) yield break;
        while (firstKnock != null && firstKnock.PlayedKnockCount == knockCount) yield return null;
        if (firstKnock == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, firstKnock.LastKnockTime + 1f - Time.time));
        ShowThoughts(new[] {
            "Опять этот стук.",
            _hasReactedToPowerOutage ? "Нужно выяснить, откуда он." : "Сначала проверю свет, потом пойду разбираться."
        }, () => {
            if (_checkingLight) CreateQuest("Проверить выключатель света", 1, questTag: "night-two-light");
        });
    }

    // =====================================================================
    // Расследование шума и поиск снаряжения
    // =====================================================================

    // --- 3. Поиск источника шума ---
    void SetupSearchNoiseQuest()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(true);
        CreateQuest("Проверить шум на кухне", 1, OnKitchenQuestCompleted, "search-kitchen-noise");
    }

    public void PrepareCornerScare()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
    }

    public void ContinueAfterCornerScare()
    {
        PrepareCornerScare();
        QuestManager quests = QuestManager.Instance;
        if (quests == null) return;
        // Interrupted speech callbacks never run; continue the existing story explicitly.
        string tag = quests.GetActiveQuestTag();
        if (!string.IsNullOrEmpty(tag) && tag != "search-kitchen-noise" && tag != "flashlight-find") return;
        if (!string.IsNullOrEmpty(tag)) quests.RecordQuestCompletion(tag);
        SetupHatchQuest();
    }

    public void OnKitchenTriggerReached()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("search-kitchen-noise"))
            QuestManager.Instance.AddProgress(1);
    }

    void OnKitchenQuestCompleted()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        // T-21: источник шума найден — RandomKnock (knockController) раньше продолжал
        // стучать всю оставшуюся ночь, включая сцену у дыры и развилку концовок.
        if (knockController != null) knockController.SetActive(false);
        bool hasFlashlight = playerInventory != null && playerInventory.hasFlashlight;
        ShowImportantThoughts(new string[] {
            "Стук идёт снизу. Из подвала.",
            "Только бы не та же история...",
            hasFlashlight ? "Вход в подвал снаружи. Пойду проверю люк." : "Стоило бы взять фонарик. Без него я туда не полезу."
        }, hasFlashlight ? (System.Action)SetupHatchQuest : SetupFlashlightQuest);
    }

    // --- 4. Найти фонарик ---
    void SetupFlashlightQuest()
    {
        CreateQuest("Найти фонарик", 1, OnFlashlightPickedUp, "flashlight-find");
        ShowThoughts(new[] { "Пойду возьму фонарик." }, ShowFlashlightOutline);
    }

    void ShowFlashlightOutline()
    {
        if (playerInventory != null && playerInventory.hasFlashlight) return;
        if (flashlightItem != null)
        {
            PixelItemOutline outline = flashlightItem.GetComponent<PixelItemOutline>();
            if (outline == null) outline = flashlightItem.AddComponent<PixelItemOutline>();
            outline.Show();
        }
    }

    // Подвал — под первым этажом дома (пол на 0.8 м). Гараж (бетон на 0.3 м, x > -38.5) и второй
    // этаж — не над ним: там стучащий не ходит и герой в пол не стучит.
    static readonly Vector2 BasementMin = new Vector2(-46f, 101f);
    static readonly Vector2 BasementMax = new Vector2(-38.6f, 118.5f);
    const float FirstFloorY = 0.8f;

    // K-01: источник RandomKnock ползёт под полом к игроку. Границы — первый этаж дома.
    void SetupKnockDirector()
    {
        if (knockController == null || playerInventory == null) return;
        PlayerController player = playerInventory.GetComponentInParent<PlayerController>();
        if (player == null) return;
        KnockDirector director = knockController.GetComponent<KnockDirector>();
        if (director == null) director = knockController.AddComponent<KnockDirector>();
        director.Initialize(player, () => skySwitcher == null || !skySwitcher.isDayTime,
            BasementMin, BasementMax);
        director.SetPlayerKnock(GetComponent<PlayerFloorKnock>());
    }

    void WakeKnocker() => _knockerAwake = true;
    // Первая ночь началась или пройдена (в т.ч. прыжком на день 2) — улики дня 2 (M-05) доступны.
    public bool FirstNightPassed => _knockerAwake;
    void MarkNightTwo() => _nightTwoStarted = true;

    // K-03: сложность «Перестука» — ночь 1 → 1, день 2 → 2, ночь 2 → 3; 0 — сейчас не стучат.
    // Ночь 1: только после ухода соседа (в кровати на 2-м этаже стучать нельзя). Ночь 2: пока
    // стучащий ещё «говорит» — до кухни и до пугалки из-за угла (дальше люк, монстр, финал).
    int KnockCallLevel()
    {
        if (!_knockerAwake) return 0;
        if (neighborVisit != null && neighborVisit.IsPlaying && !neighborVisit.HasVisited) return 0;
        if (skySwitcher == null || skySwitcher.isDayTime) return _nightTwoStarted ? 0 : 2;
        if (!_nightTwoStarted) return 1;
        if (knockController == null || !knockController.activeInHierarchy) return 0;
        return _knockSound != null && !_knockSound.IsSilencedPermanently ? 3 : 0; // после пугалки из-за угла стук затих навсегда
    }

    static bool IsDayTwoDebugStage(DebugStoryStage stage) =>
        stage == DebugStoryStage.StartDay2Decoration || stage == DebugStoryStage.StartDay2Dinner ||
        stage == DebugStoryStage.StartDay2Evening || stage == DebugStoryStage.StartDinnerCook ||
        stage == DebugStoryStage.StartDinnerEat || stage == DebugStoryStage.PlayNightRecording;

    // C-02: ночная запись с точки штатива во весь экран. Без установленной камеры записи нет.
    public bool PlayNightRecording()
    {
        Transform view = _cameraPlacement != null ? _cameraPlacement.RecordingView : null;
        if (view == null) return false;
        if (_nightTape == null) _nightTape = gameObject.AddComponent<NightTapeViewer>();
        return _nightTape.Open(view, _tapeDoors, null);
    }

    // Окно не откроется, пока камера занята (пробуждение, мысли, пауза) — пробуем, пока не освободится.
    IEnumerator PlayNightRecordingWhenFree()
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            yield return new WaitForSeconds(0.5f);
            if (PlayNightRecording()) yield break;
        }
    }

    void SetupBedsideLamp()
    {
        GameObject lampObject = GameObject.Find("Desk_lamp_01.002");
        if (lampObject == null) return;

        LightSwitch lampSwitch = lampObject.GetComponent<LightSwitch>();
        if (lampSwitch == null)
            lampSwitch = lampObject.AddComponent<LightSwitch>();

        Light lampLight = lampObject.GetComponentInChildren<Light>();
        if (lampLight == null)
        {
            GameObject bulb = new GameObject("BedsideLampLight");
            bulb.transform.SetParent(lampObject.transform, false);
            bulb.transform.position = lampObject.transform.position + Vector3.up * 0.35f;
            lampLight = bulb.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1f, 0.78f, 0.55f);
            lampLight.intensity = 2.5f;
            lampLight.range = 3.5f;
            lampLight.shadows = LightShadows.None;
        }

        lampSwitch.lightsToControl = new[] { lampLight };
        lampSwitch.lampRenderers = new[] { lampObject.GetComponent<Renderer>() };
        lampSwitch.turnOnAfterNightTwoWake = true;
        lampSwitch.isOn = false;
        lampLight.enabled = false;

        if (lampObject.GetComponent<Collider>() == null)
        {
            SphereCollider collider = lampObject.AddComponent<SphereCollider>();
            collider.radius = 0.01f;
        }

        AudioSource lampAudio = lampObject.AddComponent<AudioSource>();
        AudioRouting.Assign(lampAudio, AudioBus.World);
        lampAudio.playOnAwake = false;
        lampAudio.spatialBlend = 1f;
        lampAudio.minDistance = 1.5f;
        lampAudio.maxDistance = 8f;
        lampAudio.volume = 0.45f;
        lampAudio.clip = Resources.Load<AudioClip>("Audio/DeskLampToggle");
        lampSwitch.switchSound = lampAudio;
        _bedsideBulb = lampObject.AddComponent<BedsideBulb>();
        _bedsideBulb.Initialize(lampLight);
    }

    public void OnFlashlightPickedUp()
    {
        if (flashlightItem != null && flashlightItem.TryGetComponent<PixelItemOutline>(out var outline)) outline.Hide();
        if (_breakerPending && _breakerPanel != null)
        {
            // Квест ставится до мыслей: мысли может оборвать пугалка, а квест держит сюжет.
            SetupBreakerQuest();
            ShowThoughts(new[] { "Фонарик есть.", "Наверное, выбило автоматы. Щиток в гараже." }, null);
            return;
        }
        ShowThoughts(new[] { "Фонарик есть. Теперь проверю, откуда шум." }, SetupSearchNoiseQuest);
    }

    // =====================================================================
    // M-04 «Щиток»
    // =====================================================================

    // Гараж: стена, общая с домом, слева от двери из дома (за стеллажом). За ней подвал — стук подходит к щитку из-за стены.
    static readonly Vector3 BreakerPanelPosition = new Vector3(-38.25f, 1.85f, 106.6f);
    static readonly Quaternion BreakerPanelRotation = Quaternion.LookRotation(Vector3.right);

    void SetupBreakerPanel()
    {
        GameObject prefab = Resources.Load<GameObject>("Props/BreakerPanel");
        if (prefab == null) return;
        GameObject panel = Instantiate(prefab, BreakerPanelPosition, BreakerPanelRotation);
        panel.name = "Breaker Panel";
        _breakerPanel = panel.GetComponent<BreakerPanel>();
        if (_breakerPanel == null) return;
        _breakerPanel.Configure(() => IsQuestActive("breaker-panel"), OnBreakerPanelFinished);
        QuestItemMarker.Ensure(panel).ConfigureCondition(() => _breakerPanel != null && _breakerPanel.CanInteract);
    }

    // Пока герой идёт к щитку, пугалка из-за угла ждёт: по сюжету она после щитка, на пути к кухне.
    public bool HoldsCornerScare => _breakerPending || BreakerPanel.IsPlaying;

    void SetupBreakerQuest()
    {
        CreateQuest("Проверить щиток", 1, null, "breaker-panel");
    }

    void OnBreakerPanelFinished(bool restored)
    {
        _breakerPending = false;
        if (restored)
        {
            // Ток есть везде, кроме кухни: её автомат не держит (и техника на кухне тоже стоит). В гараже загорается свет.
            LightSwitch.RestoreHousePower(IsKitchenLine, IsKitchenAppliance);
            StartCoroutine(GarageLightComesOn());
        }
        QuestManager.Instance?.AddProgress(1);
        ShowThoughts(restored
            ? new[] { "Есть. Свет.", "А кухня не держит. Автомат сам выбивает.", "Стук ведь тоже оттуда... Проверю кухню." }
            : new[] { "Оно прямо за стеной...", "К чёрту щиток. Обойдусь фонариком.", "Надо понять, откуда этот стук." },
            SetupSearchNoiseQuest);
    }

    static bool IsKitchenLine(LightSwitch lightSwitch) =>
        lightSwitch != null && lightSwitch.name.IndexOf("Kitchen", System.StringComparison.OrdinalIgnoreCase) >= 0;

    // Свет без выключателя в доме — только техника кухни (микроволновка, холодильник).
    static bool IsKitchenAppliance(Light light) => light != null && light.transform.position.x < -43f;

    IEnumerator GarageLightComesOn()
    {
        GameObject garage = GameObject.Find("lightswitchGarage");
        LightSwitch garageSwitch = garage != null ? garage.GetComponent<LightSwitch>() : null;
        if (garageSwitch == null || garageSwitch.lightsToControl == null) yield break;
        AudioClip flickerClip = Resources.Load<AudioClip>("Audio/Breaker/LightsFlickerOn");
        Light first = garageSwitch.lightsToControl.Length > 0 ? garageSwitch.lightsToControl[0] : null;
        if (flickerClip != null && first != null)
        {
            AudioSource source = HorrorMomentDirector.CreateSource(first.transform, "Garage Light Flicker", 8f);
            AudioRouting.Assign(source, AudioBus.World);
            source.PlayOneShot(flickerClip, 0.7f);
            Destroy(source.gameObject, flickerClip.length + 0.5f);
        }
        float[] blinks = { 0.15f, 0.08f, 0.35f, 0.06f, 0.5f };
        bool on = false;
        foreach (float wait in blinks)
        {
            on = !on;
            foreach (Light l in garageSwitch.lightsToControl) if (l != null) l.enabled = on;
            yield return new WaitForSeconds(wait);
        }
        garageSwitch.ForceTurnOn();
    }

    void HandlePowerOutageSwitchAttempt()
    {
        if (_hasReactedToPowerOutage) return;
        _hasReactedToPowerOutage = true;
        _breakerPending = _breakerPanel != null;
        if (_bedsideBulb != null) _bedsideBulb.Break();
        if (_checkingLight)
        {
            _checkingLight = false;
            if (_bedroomDoor != null) _bedroomDoor.SetInteractionLocked(_bedroomDoorWasLocked);
            QuestManager.Instance?.RecordQuestCompletion("night-two-light");
        }
        if (_powerOutageAudio != null)
            _powerOutageAudio.PlayOneShot(_powerOutageAudio.clip);
        ShowThoughts(new[] { "Странно. Свет не работает." }, null);
        SetupFlashlightQuest();
    }

    void SetupPowerOutageAudio()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/GeneratorShutdown");
        if (clip == null)
        {
            Debug.LogWarning("[IntroSequence] Generator shutdown sound not found in Resources/Audio/GeneratorShutdown.");
            return;
        }

        _powerOutageAudio = gameObject.AddComponent<AudioSource>();
        AudioRouting.Assign(_powerOutageAudio, AudioBus.Horror);
        _powerOutageAudio.playOnAwake = false;
        _powerOutageAudio.spatialBlend = 0f;
        _powerOutageAudio.volume = 0.8f;
        _powerOutageAudio.clip = clip;
    }

    void ResetPowerOutageThought()
    {
        _hasReactedToPowerOutage = false;
        _breakerPending = false;
        _stairRunPlayed = false;
    }

    private BedsideBulb _bedsideBulb;
    private Door _bedroomDoor;
    private bool _checkingLight, _bedroomDoorWasLocked, _stairRunPlayed;
    private int _doorHintIndex;
    private readonly string[] _doorHints = { "Сначала бы свет включить.", "В темноте я никуда не пойду.", "Нужно проверить выключатель." };

    void SetupCheckLightQuest(bool showQuest = true)
    {
        _checkingLight = !_hasReactedToPowerOutage;
        if (!_checkingLight) { SetupSearchNoiseQuest(); return; }
        _doorHintIndex = 0;
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        _bedroomDoor = GameObject.Find("Door_001")?.GetComponent<Door>();
        if (_bedroomDoor != null)
        {
            _bedroomDoorWasLocked = _bedroomDoor.IsInteractionLocked;
            _bedroomDoor.SetInteractionLocked(true);
            _bedroomDoor.LockedAttempted -= OnBedroomDoorAttempt;
            _bedroomDoor.LockedAttempted += OnBedroomDoorAttempt;
            _bedroomDoor.Opened -= OnBedroomDoorOpened;
            _bedroomDoor.Opened += OnBedroomDoorOpened;
        }
        if (showQuest) CreateQuest("Проверить выключатель света", 1, questTag: "night-two-light");
    }

    void OnBedroomDoorAttempt()
    {
        if (!_checkingLight || _doorHintIndex >= _doorHints.Length || ThoughtManager.Instance == null) return;
        if (ThoughtManager.Instance.TryShowInteractionThoughts("bedroom-light-check", new[] { _doorHints[_doorHintIndex] }))
            _doorHintIndex++;
    }

    void OnBedroomDoorOpened()
    {
        if (_checkingLight || !_hasReactedToPowerOutage || _stairRunPlayed) return;
        _stairRunPlayed = true;
        GameObject stairs = GameObject.Find("Ladders");
        if (stairs != null) StartCoroutine(PlayStairRun(stairs));
    }

    IEnumerator PlayStairRun(GameObject stairs)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/StairRunDown");
        if (clip == null) yield break;
        GameObject emitter = new GameObject("NightTwoStairRun");
        emitter.transform.SetParent(stairs.transform, false);
        AudioSource source = emitter.AddComponent<AudioSource>();
        AudioRouting.Assign(source, AudioBus.Horror);
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 18f;
        source.volume = 0.9f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        source.clip = clip;
        // Both flights meet at the landing; the old path covered only the lower flight.
        Vector3 top = new Vector3(-41.9f, 4.25f, 113.08f);
        Vector3 landing = new Vector3(-39.4f, 2.6f, 113.08f);
        Vector3 bottom = new Vector3(-41.67f, 0.92f, 111.8f);
        float firstFlightLength = Vector3.Distance(top, landing);
        float totalLength = firstFlightLength + Vector3.Distance(landing, bottom);
        emitter.transform.position = top;
        if (knockController != null)
            foreach (RandomKnock knock in knockController.GetComponentsInChildren<RandomKnock>(true))
                knock.PauseFor(clip.length + 3f);
        source.Play();
        Destroy(emitter, clip.length + 1f);
        while (source != null && source.isPlaying)
        {
            float distance = Mathf.Clamp01(source.time / clip.length) * totalLength;
            emitter.transform.position = distance <= firstFlightLength
                ? Vector3.Lerp(top, landing, distance / firstFlightLength)
                : Vector3.Lerp(landing, bottom, (distance - firstFlightLength) / (totalLength - firstFlightLength));
            yield return null;
        }
        if (emitter != null) Destroy(emitter);
        if (!CornerPeekSequence.IsPlaying)
            ShowThoughts(new[] { "Кто-то только что пробежал по моей лестнице..." }, null);
    }

    // --- 5. Идём к подвалу и пробуем открыть ---
    // Ночь 2 перестроена вокруг уличного люка: игрок берёт фонарик, выходит на улицу,
    // пробует E на створках и получает отказ — замок. Только после этого он идёт за ломом.
    // Прежний шаг «вскрыть доски на кухне» (break-floor) удалён целиком вместе с
    // FloorLogic: он вёл в кухонную дыру, а скример обслуживает подвал, и игрок ломал пол
    // ради квеста, который засчитывался в другом конце карты.
    void SetupHatchQuest()
    {
        // Пугалка из-за угла ещё ждёт игрока по пути к входной двери (после щитка M-04 он приходит на
        // кухню из гаража, и реплики про люк кончаются раньше, чем он доходит до прихожей). Во время этого
        // квеста она ничего не откатывает: ContinueAfterCornerScare оставляет «open-basement» как есть.
        CreateQuest("Проверить люк подвала снаружи", 1, OnHatchLockedFound, "open-basement");
    }

    // Игрок нажал E на запертом люке — засчитываем шаг и отправляем за ломом.
    public void OnHatchLockedFound()
    {
        // Сюжет ушёл от дома: пугалка из-за угла больше не нужна. Иначе она могла сработать позже
        // (например, на репликах про молоток), оборвать их и откатить историю.
        _cornerPeek?.Retire();
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
        CreateQuest("Открыть подвал и осветить лестницу", 1, OnHoleEventFinished, "open-basement-after-lock");
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
        ShowImportantThoughts(new string[] {
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
    void EndGame(EndingId ending)
    {
        if (EndingScreen.Instance != null)
            EndingScreen.Instance.ShowEnding(ending);
    }

    void SetupHammerQuest()
    {
        if (hammerItem != null) hammerItem.SetActive(true);
        CreateQuest("Найти молоток", 1, OnHammerPickedUp, "hammer-find");
    }

    // --- 9. Молоток и финал ---
    // M-07: молоток взят — снизу открывается люк, оно поднимается в спальню. Спрятаться под кровать («Дыхание»):
    // переждал — ReturnToHatchAfterHiding, нашло или застало снаружи — E3 (HideEndingController).
    public void OnHammerPickedUp()
    {
        MonsterTimer.Instance?.PauseTimer();
        _hideEnding.ActivateZones();
        _hidingScene.BeginScene();
        CreateQuest("Спрятаться под кровать", 1, questTag: "hide-bed");
        // Обычные мысли, не «важные»: важные ставят игру на паузу до нажатия, а оно уже идёт.
        ShowThoughts(new string[] {
            "Люк… Оно вылезло.",
            "Идёт сюда. Под кровать!"
        }, null);
    }

    void ReturnToHatchAfterHiding()
    {
        ShowThoughts(new string[] {
            "Ушло вниз.",
            "Заколочу. И наконец всё закончится.",
            "Надо вернуться к люку."
        }, SetupBoardBasementQuest);
    }

    void SetupBoardBasementQuest()
    {
        if (finaleTrigger != null) finaleTrigger.SetActive(true);
        CreateQuest("Заколотить люк подвала", 1, questTag: "board-basement");
        MonsterTimer.Instance?.ResumeTimer();
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

    public bool CanLockFrontDoor => _eveningRound.CanLockFrontDoor;

    public bool OnFrontDoorLocked()
    {
        return _eveningRound.LockFrontDoor();
    }

    public bool OnFrontDoorUnlocked()
    {
        return _eveningRound.UnlockFrontDoor();
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

    private void ShowImportantThoughts(string[] lines, System.Action onComplete)
    {
        if (ThoughtManager.Instance != null)
            ThoughtManager.Instance.ShowImportantThoughts(lines, onComplete);
        else
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
