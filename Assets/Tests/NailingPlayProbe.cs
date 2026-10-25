#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using TMPro;
using UnityEngine;

// Play Mode проверка M-03 («Гвозди»): NailingPlayProbe.Begin(mode) из Editor-команды, старт —
// DebugStoryStage.StartFinale. Режимы: "success" — все гвозди (с одним промахом и паузой) → E1;
// "fail" — одни промахи → доски выбиты → E2; "expired" — дедлайн истёк до люка → без мини-игры, E2.
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
        else if (mode == "look") yield return Look();
        else yield return Nailing(mode == "success");

        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator Nailing(bool succeed)
    {
        var timer = MonsterTimer.Instance;
        Stage("begin");
        Check(intro.TryBoardBasement(hatch), "Взаимодействие с люком запустило заколачивание");
        yield return Until(() => HatchNailingGame.Active != null && HatchNailingGame.Active.IsPlaying, 5f);
        var game = HatchNailingGame.Active;
        Check(game != null, "Вместо автоматических ударов — мини-игра «Гвозди»");
        if (game == null) yield break;
        Check(player.isCameraLocked, "Камера и ходьба заблокированы");
        yield return Until(() => GameObject.Find("HatchNailing") != null, 3f);
        Check(GameObject.Find("HatchNailing") != null, "Подсказка и индикатор наклона на экране");
        Check(timer == null || timer.timerText == null || timer.timerText.text == "", "Текста таймера на экране нет");
        yield return Until(() => PlankCount(true) >= 1 && Nails().Count >= 1, 3f);
        Check(PlankCount(true) == 1, $"Сначала легла одна доска (видно {PlankCount(true)})");
        yield return Wait(0.5f);
        Capture("nail");

        if (succeed)
        {
            // Первый гвоздь — оба удара точно.
            yield return HitUpright(game);
            Check(game.Logic.HitsOnNail == 1, "Ровный удар: гвоздь вошёл наполовину");
            yield return HitUpright(game);
            Check(game.Logic.NailsDone == 1, "Второй ровный удар: гвоздь забит");

            Stage("miss");
            yield return Wait(0.3f); // пока идёт мах молотка, новый удар не принимается
            yield return Until(() => Mathf.Abs(game.Logic.Angle) > 20f, 3f);
            float pressure = game.Logic.Pressure;
            game.RequestStrike();
            yield return null;
            yield return null;
            Check(game.Logic.Bent == 1, "Удар по наклонённому гвоздю гнёт его");
            Check(game.Logic.Pressure >= pressure + NailBoarding.MissCost - 0.01f,
                $"Промах съедает запас досок: {pressure:0.0} → {game.Logic.Pressure:0.0}");
            yield return Wait(0.05f);
            float leaf = LeafLift();
            Check(leaf > 0.5f, $"Снизу ударили — створки подпрыгнули на {leaf:0.0}°");
            Capture("bent");

            Stage("pause");
            keepPaused = true;
            var pause = FindFirstObjectByType<PauseMenuController>();
            pause.Pause();
            float before = game.Logic.Pressure;
            int hitsBefore = game.Logic.HitsOnNail + game.Logic.NailsDone * 10 + game.Logic.Bent * 100;
            game.RequestStrike();
            yield return WaitRealtime(1f);
            Check(PauseMenuController.IsPaused, "Пауза открылась поверх мини-игры");
            Check(Mathf.Approximately(game.Logic.Pressure, before), "На паузе доски не выбивают");
            Check(hitsBefore == game.Logic.HitsOnNail + game.Logic.NailsDone * 10 + game.Logic.Bent * 100,
                "На паузе удар не засчитан");
            pause.Resume();
            keepPaused = false;
            yield return null;
            Check(player.isCameraLocked, "После паузы камера по-прежнему в руках мини-игры");

            Stage("rest");
            while (game.IsPlaying && !game.Logic.Finished)
            {
                yield return HitUpright(game);
                if (game.Logic.NailsDone == 2) yield return Wait(0.1f);
            }
            Check(game.Logic.Succeeded, $"Все гвозди забиты (погнуто {game.Logic.Bent}, запас {game.Logic.Pressure:0.0}/{NailBoarding.Budget})");
            yield return Until(() => !game.IsPlaying, 3f);
            Check(PlankCount(true) == 2, "Обе доски на люке");
            Check(hatch.IsBoarded && !hatch.IsOpen, "Люк заколочен");
            Check(GameObject.Find("HatchNailing") == null, "Подсказка убрана");
            int flush = 0;
            foreach (var nail in Nails()) if (Vector3.Angle(nail.transform.up, hatch.transform.up) < 1f) flush++;
            Check(flush >= NailBoarding.NailCount, $"Забитые гвозди остались в досках: {flush}");
            Capture("boarded");
            Stage("ending");
            yield return Until(() => EndingTitle().Contains("КОНЕЦ"), 25f);
            Check(EndingTitle().Contains("КОНЕЦ"), "После реплик — экран «КОНЕЦ» (E1)");
        }
        else
        {
            int misses = 0;
            while (game.IsPlaying && !game.Logic.Finished && misses < 10)
            {
                yield return Until(() => !game.Logic.IsUpright, 2f);
                game.RequestStrike();
                misses++;
                yield return Wait(0.45f);
            }
            Check(game.Logic.Failed, $"Только промахи — доски выбиты после {game.Logic.Bent} погнутых гвоздей");
            yield return Until(() => !game.IsPlaying, 3f);
            yield return Until(() => hatch.IsOpen, 4f);
            Check(hatch.IsOpen, "Створки распахнулись");
            yield return CheckPushed();
        }
    }

    // Без ударов: куда смотрит камера и виден ли гвоздь (скриншот для глаза).
    private IEnumerator Look()
    {
        Check(intro.TryBoardBasement(hatch), "Взаимодействие с люком запустило заколачивание");
        yield return Until(() => Nails().Count > 0, 4f);
        yield return Wait(1.2f);
        var nails = Nails();
        if (nails.Count == 0) { Check(false, "Гвоздь появился"); yield break; }
        Vector3 head = nails[0].transform.TransformPoint(Vector3.up * 0.2f);
        Vector3 viewport = view.WorldToViewportPoint(head);
        float angle = Vector3.Angle(view.transform.forward, head - view.transform.position);
        Check(angle < 6f, $"Камера смотрит на гвоздь: отклонение {angle:0.0}°");
        Check(viewport.z > 0f && viewport.x > 0.3f && viewport.x < 0.7f && viewport.y > 0.3f && viewport.y < 0.7f,
            $"Гвоздь в центре кадра: {viewport}");
        Check(GameObject.Find("Nailing Lamp") != null, "Фонарик положен лучом на люк");
        report.notes.Add($"FOV {view.fieldOfView:0.0}, pitch {view.transform.eulerAngles.x:0.0}");
        Capture("look");
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

    // Ждём, пока гвоздь встанет ровно, и бьём тем же путём, что Space/ЛКМ.
    private IEnumerator HitUpright(HatchNailingGame game)
    {
        int done = game.Logic.NailsDone, hits = game.Logic.HitsOnNail;
        yield return Wait(0.3f); // мах молотка
        yield return Until(() => Mathf.Abs(game.Logic.Angle) > NailBoarding.UprightAngle + 2f, 3f);
        yield return Until(() => Mathf.Abs(game.Logic.Angle) < 4f || game.Logic.Finished, 3f);
        if (game.Logic.Finished) yield break;
        game.RequestStrike();
        yield return Until(() => game.Logic.NailsDone != done || game.Logic.HitsOnNail != hits, 1f);
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
        float z = left.localEulerAngles.z;
        return z > 180f ? z - 360f : z;
    }

    private int PlankCount(bool active)
    {
        int count = 0;
        Transform boards = hatch.transform.Find("FinaleBoards");
        if (boards == null || !boards.gameObject.activeInHierarchy) return 0;
        foreach (Transform plank in boards) if (plank.name.StartsWith("Board") && plank.gameObject.activeSelf == active) count++;
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
