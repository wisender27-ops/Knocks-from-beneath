#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using TMPro;
using UnityEngine;

// Play Mode проверка S-08 (встреча с Хейлом). HaleMeetingPlayProbe.Arm() из Editor-команды до входа в Play Mode:
// сцена не меняется, этап прыжка (StartBasementTunnels) выставляется в рантайме до IntroSequence.Start.
// Реплики листаются автопрокруткой панели, сама настройка игрока не меняется. Отчёт и кадры — .codex/verification/hale-meeting.
[DefaultExecutionOrder(-100)]
public sealed class HaleMeetingPlayProbe : MonoBehaviour
{
    private const string ArmKey = "kfb.halemeeting.probe";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly string[][] Expected =
    {
        new[] { "ХЕЙЛ", "Кто ты? Четыре месяца стучу по трубе, а кто наверху — не знаю." },
        new[] { "ХЕЙЛ", "Погоди… Дэниел Росс?" },
        new[] { "Я", "Откуда вы меня знаете?" },
        new[] { "ХЕЙЛ", "В бумагах аренды была копия твоего документа с фото." },
        new[] { "ХЕЙЛ", "Он никогда не шёл за мной. Он шёл за тобой." },
        new[] { "ХЕЙЛ", "Зачем ты сбил замок?" },
        new[] { "ХЕЙЛ", "Сын запер меня по моей просьбе. Этот замок был не тюрьмой, а защитой." },
        new[] { "ХЕЙЛ", "Я стучал по трубе, чтобы удержать его внизу. Теперь он услышал грохот и идёт." },
        new[] { "ХЕЙЛ", "Камера на стене моя. Пусть пишет. Хоть раз он попадёт на плёнку там, где надо." }
    };

    private readonly List<string> passed = new List<string>(), failed = new List<string>(), notes = new List<string>();
    private readonly List<string> seen = new List<string>();
    private BasementChapter chapter;
    private string output, stage = "start";

    public static void Arm() => UnityEditor.SessionState.SetBool(ArmKey, true);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (!UnityEditor.SessionState.GetBool(ArmKey, false)) return;
        UnityEditor.SessionState.EraseBool(ArmKey);
        var sequence = FindFirstObjectByType<IntroSequence>();
        if (sequence == null) return;
        sequence.useDebugSkip = true;
        sequence.startStage = DebugStoryStage.StartBasementTunnels;
        new GameObject("Hale meeting verification").AddComponent<HaleMeetingPlayProbe>();
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/hale-meeting");
        Directory.CreateDirectory(output);
        try { yield return Run(); }
        finally { Write(); }
    }

    private IEnumerator Run()
    {
        stage = "wait-explore";
        yield return Until(() => (chapter = FindFirstObjectByType<BasementChapter>(FindObjectsInactive.Include)) != null &&
            chapter.CurrentStage == BasementChapter.Stage.Explore, 40f);
        Check(chapter != null && chapter.CurrentStage == BasementChapter.Stage.Explore, "Ночь: игрок за вентиляцией, этап Explore");
        if (chapter == null || chapter.CurrentStage != BasementChapter.Stage.Explore) yield break;

        stage = "panel";
        var panel = FindFirstObjectByType<NarrativeReadingPanel>(FindObjectsInactive.Include);
        Check(panel != null, "Панель реплик найдена");
        if (panel == null) yield break;
        typeof(NarrativeReadingPanel).GetField("autoAdvance", Hidden).SetValue(panel, true);
        var speakerText = (TextMeshProUGUI)typeof(NarrativeReadingPanel).GetField("speakerText", Hidden).GetValue(panel);
        var body = (TextMeshProUGUI)typeof(NarrativeReadingPanel).GetField("body", Hidden).GetValue(panel);

        stage = "finale";
        var finale = (IEnumerator)typeof(BasementChapter).GetMethod("FinaleRoutine", Hidden).Invoke(chapter, null);
        chapter.StartCoroutine(finale);
        string last = null;
        for (float t = 0f; t < 150f && chapter.CurrentStage != BasementChapter.Stage.Done; t += Time.unscaledDeltaTime)
        {
            if (speakerText != null && body != null && body.gameObject.activeInHierarchy)
            {
                string current = speakerText.text + "|" + body.text;
                if (current != last && !string.IsNullOrEmpty(body.text))
                {
                    last = current;
                    seen.Add(current);
                    yield return new WaitForSeconds(0.9f);
                    yield return new WaitForEndOfFrame();
                    if (seen.Count == 1 || speakerText.text == "Я") Shot(seen.Count == 1 ? "hale-line" : "hero-line");
                }
            }
            yield return null;
        }

        stage = "check";
        Check(seen.Count >= Expected.Length, $"Показано реплик: {seen.Count}");
        for (int i = 0; i < Expected.Length; i++)
        {
            string want = Expected[i][0] + "|" + Expected[i][1];
            Check(seen.Contains(want), $"Реплика {i + 1}: {want}");
        }
        foreach (string line in seen) Note(line);
        Check(chapter.CurrentStage == BasementChapter.Stage.Done, $"Сцена дошла до конца, этап {chapter.CurrentStage}");
        stage = "complete";
    }

    private void Shot(string name)
    {
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
        Destroy(shot);
    }

    private IEnumerator Until(Func<bool> condition, float timeout)
    {
        for (float t = 0f; t < timeout && !condition(); t += Time.unscaledDeltaTime) yield return null;
    }

    private void Check(bool ok, string text) => (ok ? passed : failed).Add(text);

    private void Note(string text) => notes.Add(text);

    private void Write()
    {
        var lines = new List<string> { "stage: " + stage, "PASSED " + passed.Count, "FAILED " + failed.Count };
        foreach (string f in failed) lines.Add("  FAIL " + f);
        foreach (string p in passed) lines.Add("  ok   " + p);
        foreach (string n in notes) lines.Add("  note " + n);
        File.WriteAllLines(Path.Combine(output, "report.txt"), lines);
    }
}
#endif
