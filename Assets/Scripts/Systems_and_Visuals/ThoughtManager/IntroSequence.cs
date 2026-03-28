using UnityEngine;
using System.Collections;

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
    public GameObject garageZone;
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

    private bool _isPieTaken = false;
    private bool _isPieHeated = false;
    private bool _trashDeliveryQuestStarted = false;

    // =====================================================================
    // Процедуры и инициализация
    // =====================================================================

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
        if (QuestManager.Instance.questUiText != null)
            QuestManager.Instance.questUiText.text = "";
    }

    void ResetTriggers()
    {
        if (holeEventController != null) holeEventController.enabled = false;
        if (trashZone != null) trashZone.SetActive(false);
        if (garageZone != null) garageZone.SetActive(false);
        if (microwaveZone != null) microwaveZone.SetActive(false);
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
    // �����
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
                SetupBoxQuest();
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
    // ��������������� ������
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
        QuestManager.Instance.CreateQuest(title, amount, callback, questTag);
    }

    // =====================================================================
    // Этап 1 и сюжетные задания
    // =====================================================================

    // --- 1. Начальный монолог ---
    void StartIntro()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Наконец-то - своё жильё.",
            "Хоть и прошлые хозяева его конечно засрали."
        }, SetupTrashQuest);
    }

    void SetupTrashQuest()
    {
        TrashManager.Instance.Initialize();
        CreateQuest("Собрать мусор по дому", 3, OnTrashCollected, "trash-collect");
    }

    void OnTrashCollected()
    {
        // Delivery quest is started after TrashManager finishes its thought sequence,
        // to prevent sequence breaks from completing it mid-dialogue.
        _trashDeliveryQuestStarted = false;
    }

    public void StartTrashDeliveryQuest()
    {
        if (_trashDeliveryQuestStarted) return;
        _trashDeliveryQuestStarted = true;

        if (trashZone != null) trashZone.SetActive(true);
        CreateQuest("Вынести мусорный мешок", 1, OnTrashFinished, "trash-delivery");
    }

    public void OnTrashFinished()
    {
        if (trashZone != null) trashZone.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Коробки всё ещё у входа...",
            "Как будто на каторгу приехал."
        }, SetupBoxQuest);
    }

    // --- 2. Собирать коробки ---
    void SetupBoxQuest()
    {
        if (garageZone != null) garageZone.SetActive(true);
        QuestManager.Instance.CreateQuest("Отнести коробки в гараж", 1, OnBoxFinished, "box-delivery");
    }

    public void OnBoxFinished()
    {
        if (garageZone != null) garageZone.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Спина отваливается.",
            "Надо хоть что-то поесть перед сном.",
            "Достану пирог из холодильника."
        }, SetupTakePieQuest);
    }

    void SetupTakePieQuest()
    {
        _isPieTaken = false;
        _isPieHeated = false;

        if (pieObject != null) pieObject.SetActive(true);
        if (microwaveZone != null) microwaveZone.SetActive(false);

        CreateQuest("Достать пирог из холодильника", 1, null, "pie-take");
    }

    public void OnPieTaken()
    {
        _isPieTaken = true;
        if (microwaveZone != null) microwaveZone.SetActive(true);
        CreateQuest("Поставить пирог в микроволновку", 1, OnPiePlacedInMicrowave, "pie-microwave");
    }

    public void OnPiePlacedInMicrowave()
    {
        _isPieHeated = true;
        if (microwaveZone != null) microwaveZone.SetActive(false);
        CreateQuest("Съесть пирог", 1, OnPieEaten, "pie-eat");
    }

    public void OnPieEaten()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Уже легче.",
            "Теперь можно поспать."
        }, SetupGoToBedQuest);
    }

    void SetupGoToBedQuest()
    {
        // nightStartTrigger.SetActive(true) убрано — кровать всегда видна
        CreateQuest("Пойти к кровати", 1, OnGoToBedFinished, "go-to-bed");
    }

    public void OnBedTriggerReached()
    {
        if (QuestManager.Instance == null) return;
        if (QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return;

        var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
        bool isBedQuest = activeQuest.questTag == "go-to-bed" || activeQuest.questTitle.Contains("кровати");
        if (!isBedQuest) return;

        // nightStartTrigger.SetActive(false) убрано — кровать всегда видна
        QuestManager.Instance.AddProgress(1); // Завершить квест
    }

    void OnGoToBedFinished()
    {
        StartCoroutine(NightRoutine());
    }


    // =====================================================================
    // ���� � ������� � �����������
    // =====================================================================

    IEnumerator NightRoutine()
    {
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        GameEvents.OnNightStarted?.Invoke();
        if (skySwitcher != null) skySwitcher.isDayTime = false;
        RenderSettings.fog = false;
        TeleportPlayerToBed();

        // IntroSequence ��� �������� ���� � NightStartTrigger ������ �� �����
        if (knockController != null) knockController.SetActive(true);

        yield return new WaitForSeconds(2f);
        if (nightTrigger != null) nightTrigger.SetActive(true);

        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        ThoughtManager.Instance.ShowThoughts(new string[] {
            "...?",
            "Что это было?..",
            "Я только въехал. Почему оно не пропало."
        }, SetupSearchNoiseQuest);
    }

    // =====================================================================
    // ���� � �������������
    // =====================================================================

    // --- 3. Поиск источника шума ---
    void SetupSearchNoiseQuest()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(true);
        CreateQuest("Проверить источник шума", 1, OnKitchenQuestCompleted);
    }

    public void OnKitchenTriggerReached()
    {
        QuestManager.Instance.AddProgress(1);
    }

    void OnKitchenQuestCompleted()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Под полом.",
            "Прямо подо мной.",
            "Без света я туда не полезу."
        }, SetupFlashlightQuest);
    }

    // --- 4. Найти фонарик ---
    void SetupFlashlightQuest()
    {
        if (flashlightItem != null) flashlightItem.SetActive(true);
        CreateQuest("Найти фонарик", 1, OnFlashlightPickedUp);
    }

    public void OnFlashlightPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Свет есть.",
            "Теперь бы лом."
        }, SetupCrowbarQuest);
    }

    // --- 5. Поиск лома ---
    void SetupCrowbarQuest()
    {
        if (crowbarItem != null) crowbarItem.SetActive(true);
        CreateQuest("Найти лом", 1, OnCrowbarPickedUp);
    }

    public void OnCrowbarPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Нашёл.",
            "Ненавижу это чувство."
        }, SetupBreakFloorQuest);
    }

    // --- 6. Пробить пол ---
    void SetupBreakFloorQuest()
    {
        CreateQuest("Вскрыть доски на кухне", 1, OnFloorBroken);
    }

    public void OnFloorBroken()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
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
    // ���� � �����
    // =====================================================================

    // --- 8. Завершить событие в дыры ---
    public void OnHoleEventFinished()
    {
        if (MonsterTimer.Instance != null)
            MonsterTimer.Instance.StartTimer();
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "НЕТ.",
            "Эта тварь выскочила прямо на меня.",
            "Нужно заколотить её.",
            "Прямо сейчас."
        }, SetupHammerQuest);
    }

    void SetupHammerQuest()
    {
        if (hammerItem != null) hammerItem.SetActive(true);
        CreateQuest("Найти молоток", 1, OnHammerPickedUp);
    }

    // --- 9. Молоток и финал ---
    public void OnHammerPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Взял.",
            "Я всё исправлю."
        }, () =>
        {
            if (finaleTrigger != null) finaleTrigger.SetActive(true);
            CreateQuest("Закрыть дыру", 1);
        });
    }

    public bool IsPieTakeQuestActive()
    {
        return IsQuestActive("pie-take");
    }

    public bool CanPlacePieInMicrowave()
    {
        return _isPieTaken && !_isPieHeated && IsQuestActive("pie-microwave");
    }

    public bool CanEatPie()
    {
        return _isPieHeated && IsQuestActive("pie-eat");
    }

    bool IsQuestActive(string questTag)
    {
        if (QuestManager.Instance == null) return false;
        if (QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return false;
        return QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex].questTag == questTag;
    }
}
