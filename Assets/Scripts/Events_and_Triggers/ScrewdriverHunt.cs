using UnityEngine;

namespace KnocksFromBeneath
{
    // Поиск отвёртки для болтов вентиляции: ключ появляется на верстаке в подвале вместе с квестом, отвёртка — в захламлённой кладовой
    // за запертой дверью наверху (бывшая верхняя ванная). Подсказки героя зависят от того, видел ли игрок запертую дверь.
    [DisallowMultipleComponent]
    public sealed class ScrewdriverHunt : MonoBehaviour
    {
        public const string KeyId = "HouseKey", ScrewdriverId = "Screwdriver", QuestTag = "screwdriver-find";
        private const float ThoughtCooldown = 5f;

        private static readonly string[] LockedLines = { Loc.Key("Заперто. Про запертые комнаты мне никто не говорил.") };
        private static readonly string[] KeyDirectLines = { Loc.Key("Похоже, от той запертой двери наверху."), Loc.Key("Если отвёртка в доме есть, то там.") };
        private static readonly string[] KeyVagueLines = { Loc.Key("Старый ключ. Что-то в этом доме он да открывает.") };
        private static readonly string[] NeedLines = { Loc.Key("Болты руками не открутить. Нужна отвёртка."), Loc.Key("Где-то в доме она должна быть.") };
        private static readonly string[] OpenedLines = { Loc.Key("Открыто. Кладовка, забитая хламом.") };
        private static readonly string[] FoundLines = { Loc.Key("Вот и отвёртка. Теперь вниз, к вентиляции.") };

        [Tooltip("Запертая дверь кладовой.")]
        public Door storageDoor;
        [Tooltip("Ключ на верстаке в подвале.")]
        public ToolPickup key;
        [Tooltip("Отвёртка в кладовой.")]
        public ToolPickup screwdriver;

        private PlayerInventory inventory;
        private bool doorSeen, doorOpened, started;

        public bool IsDoorOpened => doorOpened;

        private void Start()
        {
            inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (storageDoor != null)
            {
                storageDoor.SetInteractionLocked(true);
                storageDoor.LockedAttempted += OnLockedAttempt;
            }
            ToolPickup.Taken += OnTaken;
            // Ключ появляется на верстаке только вместе с квестом: иначе его берут днём и сразу открывают кладовую.
            if (key != null && !Has(KeyId) && !Has(ScrewdriverId)) key.gameObject.SetActive(false);
            if (key != null) QuestItemMarker.Ensure(key.gameObject).ConfigureCondition(() => started && !doorOpened && !Has(KeyId));
            if (screwdriver != null) QuestItemMarker.Ensure(screwdriver.gameObject).ConfigureCondition(() => doorOpened && !Has(ScrewdriverId));
        }

        private void OnDestroy()
        {
            ToolPickup.Taken -= OnTaken;
            if (storageDoor != null) storageDoor.LockedAttempted -= OnLockedAttempt;
        }

        private bool Has(string id) => inventory != null && inventory.HasItem(id);

        // Игрок у вентиляции без отвёртки: без этого вызова квест не стартует.
        public void NeedScrewdriver()
        {
            if (Has(ScrewdriverId)) return;
            ThoughtManager.Instance?.TryShowInteractionThoughts("need-screwdriver", Loc.T(NeedLines), ThoughtCooldown);
            StartQuest();
        }

        private void StartQuest()
        {
            if (started || Has(ScrewdriverId)) return;
            // Днём в подвале ключ можно взять до ночной главы: квест рано ставить нельзя.
            BasementChapter chapter = FindFirstObjectByType<BasementChapter>();
            if (chapter == null || !chapter.IsActive) return;
            started = true;
            if (key != null && !Has(KeyId) && !doorOpened) key.gameObject.SetActive(true);
            QuestManager.Instance?.CreateQuest(Loc.T("Найти отвёртку"), 1, null, QuestTag);
        }

        private void OnLockedAttempt()
        {
            if (Has(KeyId)) { Unlock(); return; }
            doorSeen = true;
            ThoughtManager.Instance?.TryShowInteractionThoughts("storage-locked", Loc.T(LockedLines), ThoughtCooldown);
        }

        private void Unlock()
        {
            doorOpened = true;
            inventory.RemoveItem(KeyId);
            storageDoor.SetInteractionLocked(false);
            storageDoor.OpenDoor();
            ThoughtManager.Instance?.ShowThoughts(Loc.T(OpenedLines));
        }

        private void OnTaken(string id)
        {
            if (id == KeyId)
            {
                StartQuest();
                ThoughtManager.Instance?.ShowThoughts(Loc.T(doorSeen ? KeyDirectLines : KeyVagueLines));
            }
            else if (id == ScrewdriverId)
            {
                if (QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(QuestTag))
                    QuestManager.Instance.RecordQuestCompletion(QuestTag);
                ThoughtManager.Instance?.ShowThoughts(Loc.T(FoundLines));
            }
        }
    }
}
