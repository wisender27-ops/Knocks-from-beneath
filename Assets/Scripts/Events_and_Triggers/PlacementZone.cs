using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

public class PlacementZone : MonoBehaviour
{
    public List<Transform> slots; // Сюда перетащи Slot_1, Slot_2, Slot_3

    [Tooltip("Срабатывает на каждую успешно установленную коробку — для внешних слушателей (звук, партиклы и т.п.).")]
    public UnityEvent onBoxPlaced = new UnityEvent();

    [Tooltip("Теги квестов, которым эта зона засчитывает прогресс. Коробки дня 1 — box-delivery/box-collect, декор дня 2 — room-decoration.")]
    public string[] progressQuestTags = { "box-delivery", "box-collect" };

    [Tooltip("Предметы комнаты — скрываются при старте сцены и появятся при установке коробки в эту зону.")]
    [SerializeField] private GameObject[] itemsToReveal;

    [Header("Постепенная распаковка")]
    [Tooltip("Пауза между появлением соседних предметов, сек. При 40+ предметах в зоне держи небольшой (0.1-0.15), иначе распаковка затянется.")]
    [SerializeField] private float revealInterval = 0.12f;

    [Tooltip("Длительность 'поп'-анимации одного предмета (рост от нуля с небольшим перехлёстом), сек.")]
    [SerializeField] private float revealPopDuration = 0.3f;

    [Tooltip("Звук 'поп' при появлении каждого предмета. Необязателен — без клипа просто тихо. Проигрывается с лёгким случайным питчем для разнообразия.")]
    [SerializeField] private AudioClip revealPopSound;

    [Range(0f, 1f)]
    [SerializeField] private float revealPopVolume = 0.5f;

    private bool[] isSlotOccupied;
    private bool _revealed;

    // Зоны в сцене изначально выключены (их включает MoveInChoresController/
    // RoomDecorationController по ходу сюжета) — Awake() выключенного GameObject не
    // вызывается, пока его не включат, поэтому HideItems() в Awake() одной зоны не
    // спрячет предметы сразу при запуске игры. Прогоняем скрытие по всем зонам сцены
    // (включая выключенные) один раз при загрузке — независимо от того, активна зона
    // или нет.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HideAllRevealItemsAtBoot()
    {
        var zones = FindObjectsByType<PlacementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
            zones[i].HideItems();
    }

    void Awake()
    {
        // Инициализируем массив "занятости" по количеству слотов
        isSlotOccupied = new bool[slots != null ? slots.Count : 0];
        HideItems();
    }

    void HideItems()
    {
        if (itemsToReveal == null) return;
        for (int i = 0; i < itemsToReveal.Length; i++)
        {
            if (itemsToReveal[i] != null)
                itemsToReveal[i].SetActive(false);
        }
    }

    public void RevealItems()
    {
        if (_revealed) return;
        _revealed = true;

        if (itemsToReveal == null) return;

        // Активируем все предметы сразу (важно для квестов/тестов, которые проверяют
        // activeSelf сразу после вызова), но с нулевым масштабом — видимый "рост" каждого
        // предмета запускаем с задержкой по очереди, чтобы 20+ вещей не выпрыгивали разом.
        // Целевой масштаб (в т.ч. отрицательный — у зеркальных мешей) запоминаем до обнуления.
        var toAnimate = new List<(Transform t, Vector3 targetScale)>(itemsToReveal.Length);
        for (int i = 0; i < itemsToReveal.Length; i++)
        {
            var go = itemsToReveal[i];
            if (go == null) continue;

            var t = go.transform;
            Vector3 targetScale = t.localScale;
            t.localScale = Vector3.zero;
            go.SetActive(true);
            toAnimate.Add((t, targetScale));
        }

        // Крутим анимацию на отдельном persistent-раннере, а не на StartCoroutine(this):
        // если это последняя коробка из трёх, MoveInChoresController.OnBoxFinished()
        // синхронно выключает GameObject этой же зоны прямо внутри AddProgress() —
        // ДО того, как мы сюда дошли (см. FinalizePlacement: AddProgress → RevealItems).
        // StartCoroutine на уже выключенном GameObject тихо ничего не запускает, и
        // предметы навсегда остаются с нулевым масштабом. Раннер никогда не выключается
        // вместе с зоной, так что анимация всегда доигрывает до конца независимо от того,
        // в каком порядке игрок расставил коробки.
        RevealRunner.StartCoroutine(RevealSequence(toAnimate));
    }

    IEnumerator RevealSequence(List<(Transform t, Vector3 targetScale)> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var (t, targetScale) = items[i];
            if (t != null)
            {
                RevealRunner.StartCoroutine(PopIn(t, targetScale));
                PlayPopSound(t.position);
            }

            if (i < items.Count - 1 && revealInterval > 0f)
                yield return new WaitForSeconds(revealInterval);
        }
    }

    private static PlacementZoneRevealRunner _revealRunner;
    private static PlacementZoneRevealRunner RevealRunner
    {
        get
        {
            if (_revealRunner == null)
            {
                var go = new GameObject("~PlacementZoneRevealRunner");
                go.hideFlags = HideFlags.HideAndDontSave;
                if (Application.isPlaying)
                    Object.DontDestroyOnLoad(go);
                _revealRunner = go.AddComponent<PlacementZoneRevealRunner>();
            }
            return _revealRunner;
        }
    }

    // Пустой MonoBehaviour-хост исключительно для StartCoroutine — сам по себе ничего не
    // делает и никогда не выключается вместе с конкретной зоной.
    private class PlacementZoneRevealRunner : MonoBehaviour { }

    IEnumerator PopIn(Transform t, Vector3 targetScale)
    {
        if (revealPopDuration <= 0f)
        {
            if (t != null) t.localScale = targetScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < revealPopDuration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / revealPopDuration);
            float eased = EaseOutBack(p);
            t.localScale = targetScale * eased;
            yield return null;
        }

        if (t != null) t.localScale = targetScale;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    void PlayPopSound(Vector3 position)
    {
        if (revealPopSound == null) return;

        var sfxGo = new GameObject("RevealPopSFX");
        sfxGo.transform.position = position;
        var src = sfxGo.AddComponent<AudioSource>();
        src.clip = revealPopSound;
        src.pitch = Random.Range(0.92f, 1.08f);
        src.volume = revealPopVolume;
        src.spatialBlend = 1f;
        src.Play();
        Destroy(sfxGo, revealPopSound.length / src.pitch + 0.1f);
    }

    // Тот самый метод, который вызывает игрок
    public bool TryPlaceBox(GameObject box)
    {
        if (box == null || slots == null)
            return false;

        // Защита на случай, если Awake() ещё не отработал (в EditMode-тестах AddComponent
        // не гарантирует немедленный вызов Awake — см. RoomRevealZoneTests). В реальной игре
        // это no-op, Awake уже отработал к моменту вызова TryPlaceBox игроком.
        if (isSlotOccupied == null || isSlotOccupied.Length != slots.Count)
            isSlotOccupied = new bool[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            if (!isSlotOccupied[i] && slots[i] != null) // Нашли свободный слот
            {
                FinalizePlacement(box, i);
                return true; // Говорим игроку: "Всё ок, я забрала!"
            }
        }
        Debug.Log("Зона: Мест нет!");
        return false; // Говорим игроку: "Мест нет, бросай на пол"
    }

    void FinalizePlacement(GameObject box, int index)
    {
        if (box == null || slots == null || index < 0 || index >= slots.Count || slots[index] == null)
            return;

        // 1. Убиваем физику
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 2. Ставим в слот
        box.transform.parent = null; 
        box.transform.position = slots[index].position;
        box.transform.rotation = slots[index].rotation;

        // 3. Слот теперь занят
        isSlotOccupied[index] = true;

        // --- ВОТ ЭТА СТРОЧКА ОТКЛЮЧАЕТ ВЗАИМОДЕЙСТВИЕ ---
        box.tag = "Untagged"; // Меняем тег на стандартный, который не "берется"
        
        // Если на коробке висит скрипт PickableItem, его тоже можно выключить для оптимизации
        if (box.GetComponent<PickableItem>()) {
            box.GetComponent<PickableItem>().enabled = false;
        }

        Debug.Log($"Зона: Коробка зафиксирована в слоте {index + 1}");

        // Сценарий: если активный квест - доставка коробок, считаем прогресс
        if (QuestManager.Instance != null &&
            QuestManager.Instance.currentQuestIndex >= 0 &&
            QuestManager.Instance.currentQuestIndex < QuestManager.Instance.questList.Count)
        {
            var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
            if (activeQuest != null && IsProgressQuest(activeQuest.questTag))
            {
                // Считаем как одно действие по доставке (ставим одну прогресс-единицу)
                QuestManager.Instance.AddProgress(1);
                Debug.Log("[PlacementZone] Прогресс доставки коробки добавлен.");
            }
        }

        onBoxPlaced?.Invoke();
        RevealItems();
    }

    bool IsProgressQuest(string questTag)
    {
        if (progressQuestTags == null || string.IsNullOrEmpty(questTag)) return false;
        for (int i = 0; i < progressQuestTags.Length; i++)
        {
            if (progressQuestTags[i] == questTag) return true;
        }
        return false;
    }
}
}
