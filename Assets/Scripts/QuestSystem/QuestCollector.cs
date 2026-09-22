using UnityEngine;

namespace KnocksFromBeneath
{

public class QuestCollector : MonoBehaviour
{
    public CollectableItem.ItemType acceptedType;

    [Tooltip("Предметы, которые появятся после того как эта зона примет подходящий предмет — например, скрытые мешки мусора рядом с мусоркой, которые становятся видны после того как выбросили мусор.")]
    [SerializeField] private GameObject[] itemsToReveal;

    private bool _revealed;

    // Зоны в сцене иногда выключены при старте (см. PlacementZone) — Awake() выключенного
    // GameObject не вызывается, пока его не включат, поэтому скрытие не может полагаться
    // только на Awake() конкретного коллектора. Прогоняем по всем коллекторам сцены сразу
    // при загрузке, включая выключенные.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HideAllRevealItemsAtBoot()
    {
        var collectors = FindObjectsByType<QuestCollector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < collectors.Length; i++)
            collectors[i].HideItems();
    }

    void Awake()
    {
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
        for (int i = 0; i < itemsToReveal.Length; i++)
        {
            if (itemsToReveal[i] != null)
                itemsToReveal[i].SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CollectableItem item = other.GetComponent<CollectableItem>();
        if (item == null) return;

        if (item.currentItemType != acceptedType)
        {
            Debug.Log($"Зона ждет {acceptedType}, а принесли {item.currentItemType}");
            return;
        }

        if (QuestManager.Instance == null)
            return;

        // Проверяем что сейчас активен квест который ждёт этот тип предмета
        if (QuestManager.Instance.currentQuestIndex >= 0 &&
            QuestManager.Instance.currentQuestIndex < QuestManager.Instance.questList.Count)
        {
            var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
            if (activeQuest == null)
                return;

            // Проверяем по questTag, а не по названиям (устойчиво к локализации/кодировкам)
            bool isCorrectQuest =
                (acceptedType == CollectableItem.ItemType.Trash && activeQuest.questTag == "trash-delivery") ||
                (acceptedType == CollectableItem.ItemType.Box && (activeQuest.questTag == "box-delivery" || activeQuest.questTag == "box-collect"));

            if (isCorrectQuest)
            {
                QuestManager.Instance.AddProgress(1);
                Destroy(other.gameObject);
                RevealItems();
                Debug.Log($"Предмет {item.currentItemType} засчитан!");
            }
        }
    }
}
}
