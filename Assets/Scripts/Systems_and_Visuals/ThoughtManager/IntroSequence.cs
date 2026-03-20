using UnityEngine;
using System.Collections;

public enum DebugStoryStage
{
    None,
    StartBoxQuest,
    StartNight,
    StartKitchenNoise,
    StartCrowbarQuest,
    StartBreakFloor,
    StartFlashlightQuest,
    StartLookInHole,
    StartHammerQuest,
    StartFinale
}

public class IntroSequence : MonoBehaviour
{
    // =====================================================================
    // INSPECTOR
    // =====================================================================

    [Header("Дебаг Сюжета")]
    public bool useDebugSkip = false;
    public DebugStoryStage startStage = DebugStoryStage.None;

    [Header("Игрок")]
    public Transform playerTransform;
    public PlayerInventory playerInventory;

    [Header("Переход ночи")]
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject nightTrigger;
    public Vector3 nightSpawnPosition;
    public Vector3 nightSpawnRotation;

    [Header("Сюжетные триггеры")]
    public GameObject trashZone;
    public GameObject garageZone;
    public GameObject kitchenNoiseTrigger;
    public GameObject finaleTrigger;

    [Header("Контроллеры")]
    public HoleEventController holeEventController;

    // =====================================================================
    // ИНИЦИАЛИЗАЦИЯ
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
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        if (finaleTrigger != null) finaleTrigger.SetActive(false);
    }

    // =====================================================================
    // ДЕБАГ
    // =====================================================================

    void ApplyDebugSkip()
    {
        Debug.LogWarning($"[DEBUG] Быстрый старт с этапа: {startStage}");

        if (startStage >= DebugStoryStage.StartKitchenNoise)
        {
            if (skySwitcher != null) skySwitcher.isDayTime = false;
            RenderSettings.fog = false; // Добавь сюда тоже
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
            case DebugStoryStage.StartCrowbarQuest:
                SetupCrowbarQuest();
                break;
            case DebugStoryStage.StartBreakFloor:
                if (playerInventory != null) playerInventory.hasCrowbar = true;
                SetupBreakFloorQuest();
                break;
            case DebugStoryStage.StartFlashlightQuest:
                SetupFlashlightQuest();
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
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
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

    void CreateQuest(string title, int amount, UnityEngine.Events.UnityAction callback = null)
    {
        QuestManager.Instance.CreateQuest(title, amount, callback);
    }

    // =====================================================================
    // ДЕНЬ 1 — ОБУСТРОЙСТВО
    // =====================================================================

    // --- 1. Мусор ---
    void StartIntro()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Наконец-то я дома...",
            "Нужно обживаться... И воняет знатно, надо бы вынести этот мусор."
        }, SetupTrashQuest);
    }

    void SetupTrashQuest()
    {
        CreateQuest("Собрать мусор по дому", 3, OnTrashCollected);
    }

    // Весь мусор собран — появился мешок, теперь нести
    void OnTrashCollected()
    {
        if (trashZone != null) trashZone.SetActive(true);
        CreateQuest("Вынести мусорный мешок", 1, OnTrashFinished);
    }

    // Мешок вынесен
    public void OnTrashFinished()
    {
        if (trashZone != null) trashZone.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
        "Фух, одной проблемой меньше.",
        "Так, теперь та коробка у входа... Нужно перетащить её в гараж."
    }, SetupBoxQuest);
    }

    // --- 2. Коробка в гараж ---
    void SetupBoxQuest()
    {
        if (garageZone != null) garageZone.SetActive(true);
        CreateQuest("Отнести коробку в гараж", 1, OnBoxFinished);
    }

    public void OnBoxFinished()
    {
        if (garageZone != null) garageZone.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Всё, на сегодня хватит.",
            "Смертельно устал. Пора ложиться спать."
        }, () => StartCoroutine(NightRoutine()));
    }

    // =====================================================================
    // НОЧЬ — ПЕРЕХОД И ПРОБУЖДЕНИЕ
    // =====================================================================

    IEnumerator NightRoutine()
    {
        // Затемнение
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        GameEvents.OnNightStarted?.Invoke();
        if (skySwitcher != null) skySwitcher.isDayTime = false;
        RenderSettings.fog = false; // Выключаем туман ночью
        TeleportPlayerToBed();

        yield return new WaitForSeconds(2f);
        if (nightTrigger != null) nightTrigger.SetActive(true);

        // Осветление
        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        ThoughtManager.Instance.ShowThoughts(new string[] {
            "...Что за скрежет?",
            "Звук идет с кухни. Похоже на крыс или старые трубы.",
            "Надо проверить, пока они мне пол не сожрали."
        }, SetupSearchNoiseQuest);
    }

    // =====================================================================
    // НОЧЬ — РАССЛЕДОВАНИЕ
    // =====================================================================

    // --- 3. Шум на кухне ---
    void SetupSearchNoiseQuest()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(true);
        CreateQuest("Проверить источник шума на кухне", 1, OnKitchenQuestCompleted);
    }

    public void OnKitchenTriggerReached()
    {
        QuestManager.Instance.AddProgress(1);
    }

    void OnKitchenQuestCompleted()
    {
        if (kitchenNoiseTrigger != null) kitchenNoiseTrigger.SetActive(false);
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Скребется прямо под досками. Звучит хреново.",
            "Придется вскрыть пол. Лом вроде оставался в гараже."
        }, SetupCrowbarQuest);
    }

    // --- 4. Найти лом ---
    void SetupCrowbarQuest()
    {
        CreateQuest("Найти лом в гараже", 1, OnCrowbarPickedUp);
    }

    public void OnCrowbarPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Нашел. Теперь вскроем эти доски."
        }, SetupBreakFloorQuest);
    }

    // --- 5. Сломать пол ---
    void SetupBreakFloorQuest()
    {
        CreateQuest("Вскрыть доски на кухне", 1, OnFloorBroken);
    }

    // Вызывается из FloorLogic.Break()
    public void OnFloorBroken()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Готово. Темно... Ничего не видно.",
            "Нужен фонарик чтобы разглядеть что там внутри."
        }, SetupFlashlightQuest);
    }

    // --- 6. Найти фонарик ---
    void SetupFlashlightQuest()
    {
        CreateQuest("Найти фонарик", 1, OnFlashlightPickedUp);
    }

    public void OnFlashlightPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Вот он. Посмотрим что там в дыре..."
        }, SetupLookInHoleQuest);
    }

    // --- 7. Заглянуть в дыру (скример) ---
    void SetupLookInHoleQuest()
    {
        CreateQuest("Заглянуть в дыру", 1, OnHoleEventFinished);

        if (holeEventController != null)
            holeEventController.enabled = true;
        else
            Debug.LogError("[DEBUG] HoleEventController не привязан в Inspector!");
    }

    // =====================================================================
    // НОЧЬ — ФИНАЛ
    // =====================================================================

    // --- 8. Реакция и поиск молотка ---
    public void OnHoleEventFinished()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "ТВОЮ МАТЬ! ЧТО ЭТО БЫЛО?!",
            "Там кто-то есть... Живой! Оно смотрело прямо на меня!",
            "Нужно заколотить это немедленно, пока оно не вылезло!",
            "На втором этаже в ящике был молоток и гвозди. СКОРЕЕ!"
        }, SetupHammerQuest);
    }

    void SetupHammerQuest()
    {
        CreateQuest("Найти молоток на втором этаже", 1, OnHammerPickedUp);
    }

    // --- 9. Финал у дыры ---
    // Дальше управление берет FinaleController
    public void OnHammerPickedUp()
    {
        ThoughtManager.Instance.ShowThoughts(new string[] {
            "Взял! Назад к дыре, быстро!"
        }, () => {
            if (finaleTrigger != null) finaleTrigger.SetActive(true);
            CreateQuest("Заколотить дыру", 1);
        });
    }
}