#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка P-04 (диегетический дедлайн). DeadlinePlayProbe.Arm(mode) из Editor-команды до входа в Play
// Mode: сцена не меняется, этап прыжка выставляется в рантайме до IntroSequence.Start. Режимы: "expire" — весь
// дедлайн с паузой посередине, тишина, люк распахнут, E2; "board" — успел к люку, всё гаснет, начались «Гвозди»;
// "hammer" — пока ищешь молоток, дедлайн не идёт. Отчёт и кадры — .codex/verification/deadline.
[DefaultExecutionOrder(-100)]
public sealed class DeadlinePlayProbe : MonoBehaviour
{
    private const string ArmKey = "kfb.deadline.probe";

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
    private DiegeticDeadline deadline;
    private MonsterTimer timer;
    private NarrativeReadingPanel panel;
    private Camera view;
    private Light beam;
    private string output, mode;

    public static void Arm(string mode) => UnityEditor.SessionState.SetString(ArmKey, mode);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        string armed = UnityEditor.SessionState.GetString(ArmKey, "");
        if (armed == "") return;
        UnityEditor.SessionState.EraseString(ArmKey);
        var sequence = FindFirstObjectByType<IntroSequence>();
        if (sequence == null) return;
        sequence.useDebugSkip = true;
        sequence.startStage = armed == "hammer" ? DebugStoryStage.StartHammerQuest : DebugStoryStage.StartNightTwoExits;
        if (armed == "manual") return; // только прыжок к концу пряток: дальше играет автор
        new GameObject("Deadline verification").AddComponent<DeadlinePlayProbe>().mode = armed;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/deadline");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        report.mode = mode;
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        hatch = FindFirstObjectByType<BasementHatch>();
        finale = FindFirstObjectByType<FinaleController>(FindObjectsInactive.Include);
        deadline = intro.GetComponent<DiegeticDeadline>();
        timer = MonsterTimer.Instance;
        view = player.GetComponentInChildren<Camera>();
        beam = intro.playerInventory.flashlightLightSource;
        panel = ThoughtManager.Instance.GetComponent<NarrativeReadingPanel>();
        SetAuto(true);

        if (mode == "hammer")
        {
            yield return Wait(4f);
            Check(!timer.IsRunning, "Пока ищешь молоток, дедлайна нет");
        }
        else
        {
            Stage("wait-board-quest");
            yield return Until(() => intro.CanBoardBasement(hatch) && timer.IsRunning, 30f);
            Check(timer.IsRunning, "Дедлайн запущен квестом «Заколотить люк»");
            if (beam != null) { beam.enabled = true; }
            StandBack();
            if (mode == "board") yield return Board();
            else yield return Expire();
        }
        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator Expire()
    {
        Stage("early");
        yield return null;
        yield return Until(() => !hatch.IsMoving, 3f);
        Check(!hatch.IsOpen, "Люк захлопнут: створкам есть от чего подпрыгивать");
        Check(GameObject.Find("DeadlineTape") != null, "Помехи/виньетка подняты");
        float intensity = beam != null ? beam.intensity : 0f;
        yield return Until(() => timer.Progress >= 0.2f, 12f);
        Capture("early");
        int earlyBlows = deadline.Blows, earlyKnocks = deadline.Knocks;
        Note($"t=0.2: ударов {earlyBlows}, стуков {earlyKnocks}");

        Stage("pause");
        yield return Until(() => timer.Progress >= 0.35f, 12f);
        var pause = FindFirstObjectByType<PauseMenuController>();
        pause.Pause();
        float remaining = timer.Remaining;
        int blows = deadline.Blows, knocks = deadline.Knocks;
        yield return WaitRealtime(1.5f);
        Check(PauseMenuController.IsPaused && Mathf.Approximately(timer.Remaining, remaining), "На паузе таймер стоит");
        Check(deadline.Blows == blows && deadline.Knocks == knocks, "На паузе нет ни ударов, ни стука");
        pause.Resume();
        yield return null;

        Stage("inventory");
        var inventoryScreen = FindFirstObjectByType<RetroInventoryScreen>();
        Check(inventoryScreen != null, "Экран инвентаря найден");
        if (inventoryScreen != null)
        {
            Time.timeScale = 0f; // экран инвентаря останавливает игру этим же способом
            remaining = timer.Remaining;
            blows = deadline.Blows;
            yield return WaitRealtime(1f);
            Check(Mathf.Approximately(timer.Remaining, remaining) && deadline.Blows == blows, "При timeScale 0 (инвентарь) всё стоит");
            Time.timeScale = 1f;
        }

        Stage("late");
        yield return Until(() => timer.Progress >= 0.5f, 12f);
        int midFrom = deadline.Blows, midBlows = 0;
        bool dimmed = false, dark = false;
        while (timer.IsRunning && timer.Progress < 0.88f)
        {
            if (midBlows == 0 && timer.Progress >= 0.69f) midBlows = deadline.Blows - midFrom;
            if (beam != null && intensity > 0f)
            {
                dimmed |= beam.intensity < intensity * 0.5f && beam.intensity > 0f;
                dark |= beam.intensity <= 0f;
            }
            yield return null;
        }
        Capture("late");
        int lateBlows = deadline.Blows - midFrom - midBlows;
        Check(lateBlows > midBlows, $"Удары учащаются: {midBlows} за отрезок 0.5–0.69 → {lateBlows} за 0.69–0.88");
        Check(deadline.Knocks > earlyKnocks + 5, $"Стук хозяина учащается: всего {deadline.Knocks}");
        if (beam != null && beam.enabled)
        {
            Check(dimmed, "Фонарик мигает");
            Check(dark, "На последней трети фонарик проваливается в темноту");
        }

        Stage("silence");
        yield return Until(() => deadline.IsSilent, 8f);
        Check(deadline.IsSilent, "Наступила тишина");
        blows = deadline.Blows;
        knocks = deadline.Knocks;
        Capture("silence");
        float silenceStart = Time.time;
        yield return Until(() => !timer.IsRunning, 5f);
        Check(Time.time - silenceStart > 2.6f, $"Тишина длится {Time.time - silenceStart:0.0} с");
        Check(deadline.Blows == blows && deadline.Knocks == knocks, "В тишине ни удара, ни стука");

        Stage("expired");
        yield return Until(() => hatch.IsOpen, 3f);
        Check(hatch.IsOpen, "Время вышло — люк распахнулся");
        Check(GameObject.Find("DeadlineTape") == null, "Помехи сняты");
        Check(beam == null || Mathf.Approximately(beam.intensity, intensity), "Свет фонарика восстановлен");
        Capture("expired");
        yield return Wait(4f);
        StandAtHatch();
        yield return Wait(0.3f);
        Check(intro.TryBoardBasement(hatch), "Подошёл к люку после дедлайна");
        yield return Wait(0.5f);
        Check(HatchNailingGame.Active == null, "Мини-игры нет — концовка E2");
        Transform bottom = finale.holeBottomSpot;
        yield return Until(() => bottom != null && Vector3.Distance(player.transform.position, bottom.position) < 0.2f, 8f);
        Check(bottom != null && Vector3.Distance(player.transform.position, bottom.position) < 0.2f, "Монстр столкнул героя вниз (E2)");
    }

    private IEnumerator Board()
    {
        Stage("board");
        float intensity = beam != null ? beam.intensity : 0f;
        StandAtHatch();
        yield return Until(() => timer.Progress >= 0.65f, 25f);
        int blows = deadline.Blows;
        float closed = LeafAngle();
        float lift = 0f;
        yield return Until(() => deadline.Blows > blows, 4f);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime) { lift = Mathf.Max(lift, Mathf.Abs(Mathf.DeltaAngle(closed, LeafAngle()))); yield return null; }
        view.transform.LookAt(hatch.transform.position);
        Capture("blow");
        Check(lift > 2f, $"От удара створки подпрыгнули на {lift:0.0}°");
        Check(intro.TryBoardBasement(hatch), "Успел к люку до конца дедлайна");
        yield return null;
        Check(!timer.IsRunning && !deadline.IsPresenting, "Дедлайн остановлен");
        Check(GameObject.Find("DeadlineTape") == null, "Помехи сняты");
        Check(beam == null || Mathf.Approximately(beam.intensity, intensity), "Свет фонарика восстановлен");
        yield return Until(() => HatchNailingGame.Active != null && HatchNailingGame.Active.IsPlaying, 5f);
        Check(HatchNailingGame.Active != null, "Начались «Гвозди» (путь к E1)");
    }

    private float LeafAngle()
    {
        var leaf = typeof(BasementHatch).GetField("leftLeaf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hatch) as Transform;
        return leaf != null ? leaf.localEulerAngles.z : 0f;
    }

    private void StandBack() => Place(hatch.transform.position + new Vector3(4f, 1.5f, -2f));

    private void StandAtHatch() => Place(hatch.transform.position + new Vector3(0f, 1.5f, 2.4f));

    private void Place(Vector3 spot)
    {
        if (Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            spot = ground.point + Vector3.up * 0.1f;
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = spot;
        Vector3 look = hatch.transform.position - spot;
        look.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(look);
        capsule.enabled = true;
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
    private void Note(string message) { report.notes.Add(message); Write(); }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report-" + mode + ".json"), JsonUtility.ToJson(report, true));
    private void SetAuto(bool value)
    {
        if (panel != null)
            typeof(NarrativeReadingPanel).GetField("autoAdvance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, value);
    }
    // Камера без UI — мир; снимок экрана — с виньеткой и помехами.
    private void Capture(string name)
    {
        ProtagonistPlayProbe.Capture(view, Path.Combine(output, mode + "-" + name + ".png"));
        ScreenCapture.CaptureScreenshot(Path.Combine(output, mode + "-" + name + "-screen.png"));
    }

    private void OnDisable() => Time.timeScale = 1f;
}
#endif
