using UnityEngine;

namespace KnocksFromBeneath
{
    // Поиск отвёртки для болтов вентиляции: ключ лежит на верстаке в подвале, отвёртка — в захламлённой кладовой
    // за запертой дверью наверху (бывшая верхняя ванная). Подсказки героя зависят от того, видел ли игрок запертую дверь.
    [DisallowMultipleComponent]
    public sealed class ScrewdriverHunt : MonoBehaviour
    {
        public const string KeyId = "HouseKey", ScrewdriverId = "Screwdriver", QuestTag = "screwdriver-find";
        private const float ThoughtCooldown = 5f;

        private static readonly string[] LockedLines = { "Почему она закрыта? Мне о таком не говорили." };
        private static readonly string[] KeyDirectLines = { "Ключ от той запертой двери наверху.", "Отвёртка наверняка там." };
        private static readonly string[] KeyVagueLines = { "Старый ключ. Он наверняка что-то открывает в этом доме." };
        private static readonly string[] NeedLines = { "Болты руками не открутить. Нужна отвёртка.", "В таком доме она должна быть." };
        private static readonly string[] OpenedLines = { "Замок поддался. Здесь свалено всё подряд." };
        private static readonly string[] FoundLines = { "Отвёртка. Теперь вниз, к вентиляции." };

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
            ThoughtManager.Instance?.TryShowInteractionThoughts("need-screwdriver", NeedLines, ThoughtCooldown);
            StartQuest();
        }

        private void StartQuest()
        {
            if (started || Has(ScrewdriverId)) return;
            // Днём в подвале ключ можно взять до ночной главы: квест рано ставить нельзя.
            BasementChapter chapter = FindFirstObjectByType<BasementChapter>();
            if (chapter == null || !chapter.IsActive) return;
            started = true;
            QuestManager.Instance?.CreateQuest("Найти отвёртку", 1, null, QuestTag);
        }

        private void OnLockedAttempt()
        {
            if (Has(KeyId)) { Unlock(); return; }
            doorSeen = true;
            ThoughtManager.Instance?.TryShowInteractionThoughts("storage-locked", LockedLines, ThoughtCooldown);
        }

        private void Unlock()
        {
            doorOpened = true;
            inventory.RemoveItem(KeyId);
            storageDoor.SetInteractionLocked(false);
            storageDoor.OpenDoor();
            ThoughtManager.Instance?.ShowThoughts(OpenedLines);
        }

        private void OnTaken(string id)
        {
            if (id == KeyId)
            {
                StartQuest();
                ThoughtManager.Instance?.ShowThoughts(doorSeen ? KeyDirectLines : KeyVagueLines);
            }
            else if (id == ScrewdriverId)
            {
                if (QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(QuestTag))
                    QuestManager.Instance.RecordQuestCompletion(QuestTag);
                ThoughtManager.Instance?.ShowThoughts(FoundLines);
            }
        }
    }
}
