using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{
    // День 2, первый спуск: без монстра и резких звуков. Игрок сам идёт по лестнице, находит глухую стену и широкую
    // вентиляционную решётку, понимает, что подвал намного меньше дома, поднимается — и стук продолжается как ни в чём не бывало.
    public sealed class BasementDayVisit : MonoBehaviour
    {
        private const string DescendQuest = "day-descend", InspectQuest = "day-inspect", AscendQuest = "day-ascend";
        private const float UndergroundY = -1.5f, SurfaceY = 0.2f;

        private Transform player;
        private Action<string, int, UnityAction, string> createQuest;
        private Action<string[], Action> say, sayImportant;
        private Action knockBelow, finished;
        private Func<bool> tapeAvailable;
        private bool wallChecked, ventChecked;

        public bool Running { get; private set; }

        public static bool IsUnderground(Vector3 position) => position.y < UndergroundY;
        public static bool IsAboveGround(Vector3 position) => position.y > SurfaceY;

        public void Initialize(Transform playerTransform, Action<string, int, UnityAction, string> createQuestCallback,
            Action<string[], Action> quickThoughts, Action<string[], Action> importantThoughts, Action knockBelowCallback, Action onFinished,
            Func<bool> tapeAvailableCallback = null)
        {
            tapeAvailable = tapeAvailableCallback;
            player = playerTransform;
            createQuest = createQuestCallback;
            say = quickThoughts;
            sayImportant = importantThoughts;
            knockBelow = knockBelowCallback;
            finished = onFinished;
        }

        public void Begin()
        {
            BasementDepths depths = BasementDepths.Get();
            if (depths == null) { finished?.Invoke(); return; }
            Running = true;
            wallChecked = ventChecked = false;
            depths.sealedWall.Initialize(() => QuestActive(InspectQuest) && !wallChecked, CheckWall, () => Loc.T("Осмотреть стену"));
            depths.vent.Initialize(() => QuestActive(InspectQuest) && !ventChecked, CheckVent, () => Loc.T("Осмотреть решётку"));
            createQuest?.Invoke(Loc.T("Спуститься в подвал"), 1, OnDescended, DescendQuest);
        }

        private void Update()
        {
            if (!Running || player == null) return;
            if (QuestActive(DescendQuest) && IsUnderground(player.position)) QuestManager.Instance.AddProgress(1);
            else if (QuestActive(AscendQuest) && IsAboveGround(player.position)) QuestManager.Instance.AddProgress(1);
        }

        private void OnDescended()
        {
            createQuest?.Invoke(Loc.T("Осмотреть подвал"), 2, OnInspected, InspectQuest);
            say?.Invoke(new[] { Loc.T("Котёл, верстак, полки. Подвал как подвал."), Loc.T("И тихо. Здесь стука не слышно.") }, null);
        }

        private void CheckWall()
        {
            wallChecked = true;
            QuestManager.Instance?.AddProgress(1);
            say?.Invoke(new[] { Loc.T("Здесь был проём. Его заложили, и недавно: кладка свежее остальной."), Loc.T("Десять шагов от стены до стены. А дом наверху раза в полтора длиннее. Где остальное?") }, null);
        }

        private void CheckVent()
        {
            ventChecked = true;
            QuestManager.Instance?.AddProgress(1);
            say?.Invoke(new[] { Loc.T("Вентиляционный короб шириной с человека. Зачем такой маленькому подвалу?"), Loc.T("Решётка на болтах. Из короба тянет холодом.") }, null);
        }

        private void OnInspected()
        {
            say?.Invoke(new[] { Loc.T("Никого. Стучать здесь некому."), Loc.T("И всё равно что-то не сходится.") }, null);
            createQuest?.Invoke(Loc.T("Подняться наверх"), 1, () => StartCoroutine(KnockAgain()), AscendQuest);
        }

        // Наверху стук идёт снова, тем же ритмом: у подвала, в котором только что никого не было.
        private IEnumerator KnockAgain()
        {
            yield return new WaitForSeconds(3f);
            knockBelow?.Invoke();
            yield return new WaitForSeconds(2.4f);
            Running = false;
            // Про запись говорим только если она есть: иначе реплика обещает просмотр, а следом идёт квест про ужин.
            bool tape = tapeAvailable == null || tapeAvailable();
            sayImportant?.Invoke(new[]
            {
                Loc.T("Опять. Тот же стук — будто я и не спускался."),
                Loc.T("Внизу никого нет. Я только что оттуда."),
                tape ? Loc.T("Камера писала всю ночь. Пора посмотреть запись.") : Loc.T("Голова гудит. Сначала поем, потом буду думать.")
            }, finished);
        }

        private static bool QuestActive(string tag) => QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(tag);
    }
}
