#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка K-01 (ночь 2, стук активен): KnockDirectorPlayProbe.Begin() из Editor-команды.
// Игрок ходит туда-обратно по первому этажу с заданной скоростью; отчёт — .codex/verification/knock-director.
[DefaultExecutionOrder(-100)]
public sealed class KnockDirectorPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string startedUtc, completedUtc, stage;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public List<string> notes = new List<string>();
    }
    private readonly Report report = new Report();
    private static readonly Vector3 WalkCenter = new Vector3(-42.5f, 0.9f, 106f); // кухня/гостиная над подвалом, не гараж
    private IntroSequence intro;
    private PlayerController player;
    private CharacterController capsule;
    private KnockDirector knock;
    private RandomKnock randomKnock;
    private string output;
    private float speed, phase;

    public static void Begin()
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Knock director verification").AddComponent<KnockDirectorPlayProbe>();
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/knock-director");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        capsule = player.GetComponent<CharacterController>();
        knock = intro.knockController.GetComponent<KnockDirector>();
        randomKnock = intro.knockController.GetComponent<RandomKnock>();
        var director = HorrorMomentDirector.Instance;
        float waitStart = Time.realtimeSinceStartup;
        while (director != null && !director.GameplayAvailable && Time.realtimeSinceStartup - waitStart < 20f) yield return null;
        report.notes.Add($"gameplayAvailable={director != null && director.GameplayAvailable} quest={QuestManager.Instance?.GetActiveQuestTag()}");
        Check(knock != null && knock.isActiveAndEnabled, "KnockDirector живёт на активном Knock Controller");
        Vector3 home = intro.knockController.transform.position;
        Check(home.y < player.transform.position.y - 0.5f || home.y < 0f, "Источник под полом");

        Stage("still");
        yield return Restart();
        Vector3 start = knock.SourcePosition;
        yield return Move(0f, 4f);
        Check(!knock.HasPlayer && Flat(knock.SourcePosition - start) < 0.01f, "Стоящий игрок не слышен, источник на месте");

        Stage("slow");
        yield return Move(1.0f, 5f);
        Check(!knock.HasPlayer, "Медленный шаг (1 м/с) не слышен");

        Stage("walk");
        yield return Restart();
        start = knock.SourcePosition;
        float walkHeard = -1f;
        yield return Move(2f, 4f, t => { if (walkHeard < 0f && knock.HasPlayer) walkHeard = t; });
        float walkDistance = Flat(knock.SourcePosition - WalkCenter);
        Check(walkHeard > 1f, $"Шаг слышен с задержкой ({walkHeard:0.00} с)");
        Check(Flat(knock.SourcePosition - start) > 0.2f, "После шага источник пополз к игроку");

        Stage("run");
        yield return Restart();
        float runHeard = -1f;
        yield return Move(4f, 4f, t => { if (runHeard < 0f && knock.HasPlayer) runHeard = t; });
        float runDistance = Flat(knock.SourcePosition - WalkCenter);
        Check(runHeard >= 0f && (walkHeard < 0f || runHeard < walkHeard), $"Бег слышен быстрее ({runHeard:0.00} с против {walkHeard:0.00} с)");
        Check(runDistance < walkDistance, $"Бег — источник ближе ({runDistance:0.0} м против {walkDistance:0.0} м)");

        Stage("lose");
        yield return Move(0f, 6f);
        Vector3 lostAt = knock.SourcePosition;
        Check(!knock.HasPlayer, "Затаился — стучащий теряет игрока");
        Teleport(WalkCenter + new Vector3(2f, 0f, -3f));
        yield return Move(0f, 3f, center: WalkCenter + new Vector3(2f, 0f, -3f));
        Check(Flat(knock.SourcePosition - lostAt) < 0.5f, "Потеряв игрока, не идёт за ним");

        Stage("pause");
        Vector3 beforePause = knock.SourcePosition;
        Time.timeScale = 0f;
        for (int i = 0; i < 10; i++) yield return null;
        Time.timeScale = 1f;
        Check(Flat(knock.SourcePosition - beforePause) < 0.01f, "На паузе источник не двигается");

        Stage("random-knock");
        int knocks = randomKnock.PlayedKnockCount;
        yield return Move(2f, 50f); // серия RandomKnock или тишина между сериями до 40 с + клип до 5 с
        Check(randomKnock.PlayedKnockCount > knocks, "RandomKnock продолжает стучать из движущегося источника");
        Check(knock.Occluders >= 1 && knock.CutoffFrequency < 2500f, $"Стук глушится перекрытиями ({knock.Occluders} шт., {knock.CutoffFrequency:0} Гц)");
        report.notes.Add($"home={home} final={knock.SourcePosition} knocks={randomKnock.PlayedKnockCount}");

        intro.knockController.SetActive(false);
        Check(Vector3.Distance(intro.knockController.transform.position, home + Vector3.up * 1.2f) < 0.01f, "Выключение возвращает объект на исходную точку сцены");
        intro.knockController.SetActive(true);

        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
        Destroy(gameObject);
    }

    private IEnumerator Restart()
    {
        speed = 0f;
        Teleport(WalkCenter);
        intro.knockController.SetActive(false);
        yield return null;
        intro.knockController.SetActive(true);
        yield return null;
    }

    private IEnumerator Move(float metersPerSecond, float seconds, Action<float> onFrame = null, Vector3? center = null)
    {
        Vector3 origin = center ?? WalkCenter;
        speed = metersPerSecond;
        phase = 0f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Time.timeScale > 0f)
            {
                elapsed += Time.deltaTime;
                if (speed > 0f)
                {
                    phase += speed * Time.deltaTime;
                    Teleport(origin + Vector3.right * (Mathf.PingPong(phase, 3f) - 1.5f));
                }
                onFrame?.Invoke(elapsed);
            }
            yield return null;
        }
        speed = 0f;
    }

    private void Teleport(Vector3 position)
    {
        capsule.enabled = false;
        player.transform.position = position;
        capsule.enabled = true;
        player.StopMotion();
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    private void Check(bool passed, string message) { (passed ? report.passed : report.failed).Add(message); Write(); }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
    private void OnDisable() => Time.timeScale = 1f;
}
#endif
