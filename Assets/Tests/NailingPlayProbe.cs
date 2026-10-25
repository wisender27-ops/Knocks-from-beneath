#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using TMPro;
using UnityEngine;

// Play Mode проверка M-03 («Гвозди» v2: прицел + замах, толчки снизу): NailingPlayProbe.Begin(mode) из
// Editor-команды, старт — DebugStoryStage.StartFinale. Режимы: "success" — промах по металлу, пауза,
// толчок выдёргивает недобитый гвоздь, все гвозди → перестук хозяина → E1; "fail" — ничего не делать →
// доски сорваны → E2; "expired" — дедлайн истёк до люка → без мини-игры, E2; "look" — скриншот прицела.
// Отчёт — .codex/verification/nailing.
[DefaultExecutionOrder(-100)]
public sealed class NailingPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string startedUtc, completedUtc, mode, stage;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public List<string> notes = new List<string>();
    }
    private readonly Report report = new Report();
    private IntroSequence intro;
    private PlayerController player;
    private BasementHatch hatch;
    private FinaleController finale;
    private string output, mode;
    private bool keepPaused, previousAuto;
    private NarrativeReadingPanel panel;
    private Camera view;

    public static void Begin(string mode)
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Nailing verification").AddComponent<NailingPlayProbe>().mode = mode;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/nailing");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        report.mode = mode;
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        hatch = FindFirstObjectByType<BasementHatch>();
        finale = FindFirstObjectByType<FinaleController>(FindObjectsInactive.Include);
        view = player.GetComponentInChildren<Camera>();
        // Финальные реплики ждут «ДАЛЕЕ» — листаем их сами, как NeighborhoodPlayProbe.
        panel = ThoughtManager.Instance.GetComponent<NarrativeReadingPanel>();
        previousAuto = AutoAdvance;
        AutoAdvance = true;

        Stage("wait-board-quest");
        yield return Until(() => intro.CanBoardBasement(hatch), 30f);
        Check(intro.CanBoardBasement(hatch), "Квест «Заколотить люк» активен, молоток в руках");
        yield return Until(() => HorrorMomentDirector.Instance.GameplayAvailable, 10f);
        StandAtHatch();
        yield return Wait(0.3f);

        if (mode == "expired") yield return Expired();
        else yield return Nailing();

        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator Nailing()
    {
        var timer = MonsterTimer.Instance;
        Stage("begin");
        Check(intro.TryBoardBasement(hatch), "Взаимодействие с люком запустило заколачивание");
        yield return Until(() => HatchNailingGame.Active != null && HatchNailingGame.Active.IsPlaying, 5f);
        var game = HatchNailingGame.Active;
        Check(game != null, "Мини-игра «Гвозди» запущена");
        if (game == null) yield break;
        yield return Until(() => game.Logic.Available >= 2 && game.BaseAimSettled, 5f);
        Check(player.isCameraLocked, "Ходьба и обычный обзор заблокированы");
        Check(GameObject.Find("HatchNailing") != null, "Точка прицела и подсказка на экране");
        Check(timer == null || timer.timerText == null || timer.timerText.text == "", "Текста таймера на экране нет");
        Check(PlankCount() == 1 && Nails().Count == 2, $"Легла одна доска с двумя гвоздями ({PlankCount()}, {Nails().Count})");

        if (mode == "look")
        {
            game.AimAt(game.NailHead(0));
            yield return Wait(0.6f);
            float angle = Vector3.Angle(view.transform.forward, game.NailHead(0) - view.transform.position);
            Check(angle < 1.5f, $"Прицел мышью наводится на гвоздь: отклонение {angle:0.00}°");
            Capture("aim");
            yield break;
        }
        if (mode == "fail")
        {
            yield return Fail(game);
            yield break;
        }

        Stage("hit");
        yield return Hit(game, 0, 1f);
        Check(game.LastStrike == NailStrike.Drove, $"Полный замах по шляпке — гвоздь пошёл ({game.LastStrike})");
        float half = game.Logic.Depth(0);
        Check(half > 0.6f && half < 1f, $"Наполовину: глубина {half:0.00}");

        Stage("metal");
        game.AimAt(hatch.transform.position + hatch.transform.forward * 0.42f);
        yield return Wait(0.3f);
        float untilBefore = game.Logic.UntilHeave;
        game.RequestStrike(1f);
        yield return Wait(0.05f);
        Check(game.LastStrike == NailStrike.Miss, $"Удар мимо досок — промах ({game.LastStrike})");
        Check(game.Logic.UntilHeave < untilBefore - 0.5f || game.Logic.Heaves > 0,
            $"Звон по металлу — толчок раньше: {untilBefore:0.0} → {game.Logic.UntilHeave:0.0} с");

        Stage("heave");
        int heaves = game.Logic.Heaves;
        bool warned = false;
        float maxLift = 0f;
        float start = Time.realtimeSinceStartup;
        while (game.Logic.Heaves == heaves && Time.realtimeSinceStartup - start < 8f)
        {
            warned |= game.Logic.HeaveWarning;
            yield return null;
        }
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) { maxLift = Mathf.Max(maxLift, LeafLift()); yield return null; }
        Check(warned, "Перед толчком снизу — скрежет-предупреждение");
        Check(game.Logic.Heaves > heaves, "Снизу толкнули люк");
        Check(maxLift > 1.5f, $"Створки подлетели на {maxLift:0.0}°");
        Check(game.Logic.Depth(0) < half, $"Толчок выдернул недобитый гвоздь: {half:0.00} → {game.Logic.Depth(0):0.00}");

        Stage("pause");
        keepPaused = true;
        var pause = FindFirstObjectByType<PauseMenuController>();
        pause.Pause();
        float elapsed = game.Logic.Elapsed;
        float depth = game.Logic.Depth(0);
        game.AimAt(game.NailHead(0));
        game.RequestStrike(1f);
        yield return WaitRealtime(1f);
        Check(PauseMenuController.IsPaused, "Пауза открылась поверх мини-игры");
        Check(Mathf.Approximately(game.Logic.Elapsed, elapsed), "На паузе толчки не идут");
        Check(Mathf.Approximately(game.Logic.Depth(0), depth), "На паузе удар не засчитан");
        pause.Resume();
        keepPaused = false;
        yield return null;
        Check(player.isCameraLocked, "После паузы камера по-прежнему у мини-игры");

        Stage("rest");
        for (int guard = 0; guard < 20 && game.IsPlaying && !game.Logic.Finished; guard++)
        {
            int nail = 0;
            while (nail < NailBoarding.NailCount - 1 && game.Logic.IsDone(nail)) nail++;
            if (nail >= game.Logic.Available)
            {
                yield return Until(() => game.Logic.Available > nail && game.BaseAimSettled, 3f);
                Check(PlankCount() == 2, "Первая доска забита — легла вторая");
            }
            yield return Hit(game, nail, 1f);
        }
        Check(game.Logic.Succeeded, $"Все гвозди забиты: прочность {game.Logic.Integrity:0.00}, толчков {game.Logic.Heaves}, {game.Logic.Elapsed:0.0} с");
        Capture("boarded");
        yield return Until(() => !game.IsPlaying, 6f);
        Check(game.OwnerKnocksPlayed == 3, $"После последнего гвоздя снизу перестук «2·1» ({game.OwnerKnocksPlayed})");
        Check(hatch.IsBoarded && !hatch.IsOpen, "Люк заколочен");
        Check(GameObject.Find("HatchNailing") == null, "Прицел и подсказка убраны");
        Stage("ending");
        yield return Until(() => EndingTitle().Contains("КОНЕЦ"), 25f);
        Check(EndingTitle().Contains("КОНЕЦ"), "После реплик — экран «КОНЕЦ» (E1)");
    }

    private IEnumerator Fail(HatchNailingGame game)
    {
        Stage("idle");
        yield return Until(() => game.Logic.Finished, 70f);
        Check(game.Logic.Failed, $"Ничего не делал — доски сорвало за {game.Logic.Elapsed:0.0} с ({game.Logic.Heaves} толчков)");
        yield return Until(() => !game.IsPlaying, 3f);
        yield return Until(() => hatch.IsOpen, 4f);
        Check(hatch.IsOpen, "Створки распахнулись");
        yield return CheckPushed();
    }

    private IEnumerator Expired()
    {
        var timer = MonsterTimer.Instance;
        Stage("timer");
        timer.timerDuration = 1.2f;
        timer.StartTimer();
        bool textSeen = false;
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            if (timer.timerText != null && timer.timerText.text != "") textSeen = true;
            yield return null;
        }
        Check(!textSeen, "Пока шёл дедлайн, текста на экране не было ни кадра");
        Check(!timer.IsRunning, "Дедлайн истёк");
        Check(intro.TryBoardBasement(hatch), "Подошёл к люку после дедлайна");
        yield return Wait(0.5f);
        Check(HatchNailingGame.Active == null, "После дедлайна мини-игры нет — люк открывается сразу");
        yield return Until(() => hatch.IsOpen, 4f);
        yield return CheckPushed();
    }

    private IEnumerator CheckPushed()
    {
        Transform bottom = finale.holeBottomSpot;
        yield return Until(() => bottom != null && Vector3.Distance(player.transform.position, bottom.position) < 0.2f, 6f);
        Check(bottom != null && Vector3.Distance(player.transform.position, bottom.position) < 0.2f,
            "Монстр столкнул героя на дно подвала (E2)");
        yield return Until(() => EndingTitle().Contains("КОНЕЦ"), 25f);
        Check(EndingTitle().Contains("КОНЕЦ"), "Экран «КОНЕЦ» после E2");
    }

    // Навести прицел на шляпку и ударить тем же путём, что отпущенная ЛКМ.
    private IEnumerator Hit(HatchNailingGame game, int nail, float power)
    {
        game.AimAt(game.NailHead(nail));
        yield return Wait(0.25f);
        game.AimAt(game.NailHead(nail));
        yield return null;
        game.RequestStrike(power);
        yield return Wait(0.05f);
    }

    private void StandAtHatch()
    {
        Vector3 spot = hatch.transform.position + new Vector3(0f, 1.5f, 2.4f);
        if (Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            spot = ground.point + Vector3.up * 0.1f;
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = spot;
        player.transform.rotation = Quaternion.LookRotation(Vector3.back);
        capsule.enabled = true;
        player.StopMotion();
        report.notes.Add($"Игрок у люка: {spot}");
    }

    private float LeafLift()
    {
        Transform left = hatch.transform.Find("LeftLeaf");
        if (left == null) return 0f;
        return Mathf.DeltaAngle(0f, left.localEulerAngles.z);
    }

    private int PlankCount()
    {
        int count = 0;
        Transform boards = hatch.transform.Find("FinaleBoards");
        if (boards == null || !boards.gameObject.activeInHierarchy) return 0;
        foreach (Transform plank in boards) if (plank.name.StartsWith("Board") && plank.gameObject.activeSelf) count++;
        return count;
    }

    private List<GameObject> Nails()
    {
        var nails = new List<GameObject>();
        Transform boards = hatch.transform.Find("FinaleBoards");
        if (boards != null) foreach (Transform child in boards) if (child.name == "Nail") nails.Add(child.gameObject);
        return nails;
    }

    private static string EndingTitle()
    {
        var screen = EndingScreen.Instance;
        if (screen == null) return "";
        var field = typeof(EndingScreen).GetField("titleText", BindingFlags.Instance | BindingFlags.NonPublic);
        var text = field?.GetValue(screen) as TextMeshProUGUI;
        return text != null ? text.text : "";
    }

    // Editor в фоне теряет фокус — PauseMenuController ставит паузу; для пробы её снимаем.
    private void Update()
    {
        if (!keepPaused && PauseMenuController.IsPaused)
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

    private void Check(bool passed, string message) { (passed ? report.passed : report.failed).Add(message); Write(); }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report-" + mode + ".json"), JsonUtility.ToJson(report, true));
    private bool AutoAdvance
    {
        get => panel != null && (bool)AutoField.GetValue(panel);
        set { if (panel != null) AutoField.SetValue(panel, value); }
    }
    private static FieldInfo AutoField =>
        typeof(NarrativeReadingPanel).GetField("autoAdvance", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Capture(string name) => ProtagonistPlayProbe.Capture(view, Path.Combine(output, mode + "-" + name + ".png"));

    private void OnDisable()
    {
        AutoAdvance = previousAuto;
        Time.timeScale = 1f;
    }
}
#endif
