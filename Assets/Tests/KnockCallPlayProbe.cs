#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка K-03 («Перестук»): KnockCallPlayProbe.Begin() из Editor-команды.
// Ночь 2 (StartKitchenNoise): игрок над подвалом — снизу сам начинается ритм уровня 3; верный ответ →
// тихий удар, доверие +1, мысль; ошибка → ритм ещё раз → молчание → доверие −1; пауза останавливает раунд.
// Ночь 1 (StartNightOne): BeginNightOne() — пока сосед не ушёл, раунда нет; потом уровень 1.
// Отчёт — .codex/verification/knock-call.
[DefaultExecutionOrder(-100)]
public sealed class KnockCallPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string startedUtc, completedUtc, stage;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public List<string> notes = new List<string>();
    }
    private readonly Report report = new Report();
    private static readonly Vector3 Center = new Vector3(-42.5f, 0.9f, 106f);
    private IntroSequence intro;
    private PlayerController player;
    private PlayerFloorKnock floorKnock;
    private KnockResponder responder;
    private KnockCallGame game;
    private RandomKnock randomKnock;
    private string output;
    private bool nightOne;

    public static void Begin() => Create(false);
    public static void BeginNightOne() => Create(true);

    private static void Create(bool nightOne)
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Knock call verification").AddComponent<KnockCallPlayProbe>().nightOne = nightOne;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/knock-call");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        floorKnock = intro.GetComponent<PlayerFloorKnock>();
        responder = intro.GetComponent<KnockResponder>();
        game = intro.GetComponent<KnockCallGame>();
        randomKnock = intro.knockController.GetComponent<RandomKnock>();
        Check(game != null, "KnockCallGame добавлен IntroSequence");
        if (game != null) yield return nightOne ? NightOne() : NightTwo();
        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator NightTwo()
    {
        Stage("wait-gameplay");
        yield return Until(() => HorrorMomentDirector.Instance.GameplayAvailable, 20f);
        Teleport(Center);
        player.CrouchDown();
        // Центр первого этажа — в триггере кухни: квест шума закрывается и стук выключается. Как в
        // пробе K-02, включаем его обратно — уровень 3 живёт, пока стучащий «говорит».
        yield return null;
        intro.knockController.SetActive(true);
        Check(intro.knockController.activeInHierarchy, "Стук ночи 2 активен");
        int trust = StoryFlags.KnockTrust;

        Stage("round-1-auto");
        yield return Until(() => game.IsActive, 40f);
        Check(game.IsActive, "Игрок стоит над подвалом — снизу сам начался ритм");
        if (!game.IsActive) yield break;
        KnockPattern pattern = game.CurrentPattern;
        Check(pattern.Level == 3, $"Ночь 2 — уровень 3 (групп: {pattern.GroupCount}, ударов: {pattern.TapCount})");
        int randomBefore = randomKnock.PlayedKnockCount;
        yield return Until(() => game.PatternKnocksPlayed >= pattern.TapCount, 10f);
        yield return Wait(0.3f);
        Check(floorKnock.HintVisible, "После ритма — подсказка «Q», хотя игрок уже стучал раньше");
        int responderBefore = responder.ReplyKnocksPlayed;
        yield return Answer(pattern, 1f);
        yield return Until(() => !game.IsActive, 8f);
        Check(game.LastOutcome == KnockCallOutcome.Correct, $"Верный ритм распознан: {game.LastOutcome}");
        Check(StoryFlags.KnockTrust == trust + 1, $"Доверие +1: {trust} → {StoryFlags.KnockTrust}");
        Check(responder.ReplyKnocksPlayed == responderBefore, "Удары игрока в раунде не ушли в обычный ответ K-02");
        Check(randomKnock.PlayedKnockCount == randomBefore, "RandomKnock молчал, пока шёл раунд");
        Check(!responder.Suspended, "После раунда K-02 снова слушает");
        report.notes.Add($"Ритм 1: {pattern.TapCount} ударов, {pattern.GroupCount} групп");

        Stage("round-2-wrong-then-ignore");
        yield return Wait(6f); // восстановление HorrorMomentDirector после раунда
        yield return Until(() => HorrorMomentDirector.Instance.CanPresent, 20f); // мысль героя дочитана
        trust = StoryFlags.KnockTrust;
        Check(game.TryStartRound(), "Второй раунд стартовал");
        pattern = game.CurrentPattern;
        int played = game.PatternKnocksPlayed;
        yield return Until(() => game.PatternKnocksPlayed >= played + pattern.TapCount, 10f);
        yield return Wait(0.3f);
        for (int i = 0; i < pattern.TapCount; i++) // ровно, без длинных пауз — неверно на уровне 3
        {
            floorKnock.TryKnock();
            yield return Wait(0.4f);
        }
        yield return Until(() => game.PatternKnocksPlayed >= played + 2 * pattern.TapCount, 12f);
        Check(game.PatternKnocksPlayed == played + 2 * pattern.TapCount, "После ошибки хозяин простучал ритм ещё раз");
        Check(game.IsActive, "Раунд ждёт второй попытки");
        yield return Until(() => !game.IsActive, 15f);
        Check(game.LastOutcome == KnockCallOutcome.Ignored, $"Молчание после повтора — игнор: {game.LastOutcome}");
        Check(StoryFlags.KnockTrust == trust - 1, $"Доверие −1: {trust} → {StoryFlags.KnockTrust}");

        Stage("round-3-pause");
        yield return Wait(6f);
        yield return Until(() => HorrorMomentDirector.Instance.CanPresent, 20f);
        trust = StoryFlags.KnockTrust;
        Check(game.TryStartRound(), "Третий раунд стартовал (проба обходит лимит раундов)");
        pattern = game.CurrentPattern;
        played = game.PatternKnocksPlayed;
        yield return Until(() => game.PatternKnocksPlayed >= played + pattern.TapCount, 10f);
        Time.timeScale = 0f;
        yield return WaitRealtime(9f);
        Check(game.IsActive, "На паузе 9 с (больше окна ответа 7 с) раунд не закончился");
        Time.timeScale = 1f;
        yield return Answer(pattern, 1.4f); // медленный игрок
        yield return Until(() => !game.IsActive, 8f);
        Check(game.LastOutcome == KnockCallOutcome.Correct, $"После паузы — медленный верный ответ принят: {game.LastOutcome}");
        Check(StoryFlags.KnockTrust == trust + 1, $"Доверие +1: {trust} → {StoryFlags.KnockTrust}");

        Stage("round-limit");
        yield return Wait(6f);
        bool natural = false;
        for (float t = 0f; t < 6f; t += Time.deltaTime) { natural |= game.IsActive; yield return null; }
        Check(!natural, "Лимит раундов ночи 2 исчерпан — сам по себе раунд больше не начинается");
        player.StandUp();
    }

    private IEnumerator NightOne()
    {
        Stage("wait-scare");
        var scare = intro.GetComponent<NightOneScare>();
        yield return Until(() => scare.IsPlaying, 25f);
        yield return Until(() => !scare.IsPlaying, 25f);
        NeighborVisitSequence neighbor = intro.neighborVisit;
        Check(neighbor != null, "Визит соседа настроен");
        if (neighbor == null) yield break;
        yield return Until(() => neighbor.IsPlaying, 20f);

        Stage("neighbor-at-door");
        Teleport(Center);
        player.CrouchDown();
        bool started = false;
        for (float t = 0f; t < 6f; t += Time.deltaTime) { started |= game.IsActive; yield return null; }
        Check(!started, "Пока сосед не ушёл, «Перестука» нет");

        Stage("neighbor-gone");
        typeof(NeighborVisitSequence).GetProperty(nameof(NeighborVisitSequence.HasVisited)).SetValue(neighbor, true);
        int trust = StoryFlags.KnockTrust;
        yield return Until(() => game.IsActive, 15f);
        Check(game.IsActive, "Сосед ушёл, игрок над подвалом — снизу ритм");
        if (!game.IsActive) yield break;
        KnockPattern pattern = game.CurrentPattern;
        Check(pattern.Level == 1, $"Ночь 1 — уровень 1 ({pattern.TapCount} удара)");
        yield return Until(() => game.PatternKnocksPlayed >= pattern.TapCount, 10f);
        yield return Wait(0.3f);
        yield return Answer(pattern, 1f);
        yield return Until(() => !game.IsActive, 8f);
        Check(game.LastOutcome == KnockCallOutcome.Correct, $"Повтор принят: {game.LastOutcome}");
        Check(StoryFlags.KnockTrust == trust + 1, $"Доверие +1: {trust} → {StoryFlags.KnockTrust}");
        player.StandUp();
    }

    // Отстучать ритм так, как его простучал хозяин (scale — темп игрока).
    private IEnumerator Answer(KnockPattern pattern, float scale)
    {
        for (int i = 0; i < pattern.TapCount; i++)
        {
            if (i > 0) yield return Wait(pattern.GapBefore(i) * scale);
            Check(floorKnock.TryKnock(), $"Удар {i + 1} засчитан");
        }
    }

    // Editor в фоне теряет фокус — PauseMenuController ставит паузу; для пробы её снимаем.
    private void Update()
    {
        if (PauseMenuController.IsPaused)
            FindFirstObjectByType<PauseMenuController>()?.SendMessage("Resume", SendMessageOptions.DontRequireReceiver);
    }

    private static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds) yield return null;
    }

    private void Teleport(Vector3 position)
    {
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = position;
        capsule.enabled = true;
        player.StopMotion();
    }

    private void Check(bool passed, string message) { (passed ? report.passed : report.failed).Add(message); Write(); }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
    private void OnDisable() => Time.timeScale = 1f;
}
#endif
