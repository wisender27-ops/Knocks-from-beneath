#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка S-09 (ловушка). BasementTrapPlayProbe.Arm(сценарий) из Editor-команды до входа в Play Mode: сцена не
// меняется, этап прыжка (StartBasementTrap) выставляется в рантайме. Сценарии: success, late, wedge-late, caught, mic, epilogue.
// Концовка перехватывается, EndingScreen не запускается и в реестр просмотренных ничего не пишется.
// Отчёт и кадры — .codex/verification/basement-trap/<сценарий>.
[DefaultExecutionOrder(-100)]
public sealed class BasementTrapPlayProbe : MonoBehaviour
{
    private const string ArmKey = "kfb.basementtrap.probe";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<string> passed = new List<string>(), failed = new List<string>(), notes = new List<string>();
    private IntroSequence intro;
    private BasementChapter chapter;
    private BasementLevel level;
    private PlayerController player;
    private PlayerInteraction interaction;
    private Camera view;
    private EndingId? ending;
    private NarrativeReadingPanel panel;
    private string scenario, output, stage = "start";

    public static void Arm(string scenario) => UnityEditor.SessionState.SetString(ArmKey, scenario);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        string scenario = UnityEditor.SessionState.GetString(ArmKey, "");
        if (scenario.Length == 0) return;
        UnityEditor.SessionState.EraseString(ArmKey);
        var sequence = FindFirstObjectByType<IntroSequence>();
        if (sequence == null) return;
        sequence.useDebugSkip = true;
        sequence.startStage = DebugStoryStage.StartBasementTrap;
        var probe = new GameObject("Basement trap verification").AddComponent<BasementTrapPlayProbe>();
        probe.scenario = scenario;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/basement-trap/" + scenario);
        Directory.CreateDirectory(output);
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        interaction = player.GetComponent<PlayerInteraction>();
        view = player.GetComponentInChildren<Camera>();
        level = FindFirstObjectByType<BasementLevel>(FindObjectsInactive.Include);
        try { yield return Run(); }
        finally { Write(); UnityEditor.EditorApplication.isPlaying = false; }
    }

    private IEnumerator Run()
    {
        stage = "wait-trap";
        yield return Until(() => (chapter = FindFirstObjectByType<BasementChapter>(FindObjectsInactive.Include)) != null &&
            chapter.CurrentStage == BasementChapter.Stage.Trap, 40f);
        Check(chapter != null && chapter.CurrentStage == BasementChapter.Stage.Trap, "Прыжок: этап Trap");
        if (chapter == null || chapter.CurrentStage != BasementChapter.Stage.Trap) yield break;
        Action<EndingId> capture = id => ending = id;
        typeof(BasementChapter).GetField("endGame", Hidden).SetValue(chapter, capture);
        // S-11: реплики концовок ждут подтверждения; автопрогон только в памяти, PlayerPrefs не меняется.
        panel = (NarrativeReadingPanel)typeof(ThoughtManager).GetField("readingPanel", Hidden).GetValue(ThoughtManager.Instance);
        typeof(NarrativeReadingPanel).GetField("autoAdvance", Hidden).SetValue(panel, true);
        if (scenario == "epilogue") { yield return Epilogue(); yield break; }
        Check(!level.lockVisual.activeSelf, "Замок сбит");
        Check(!level.cellDoorCollider.activeSelf, "Решётка открыта");
        Check(intro.playerInventory.HasItem("Crowbar"), "Лом в инвентаре");

        // Проба молчит, но за столом шумно: только сценарий mic слушает настоящий микрофон.
        var listener = (BasementHideListener)typeof(BasementChapter).GetField("hideListener", Hidden).GetValue(chapter);
        if (scenario != "mic") listener.End();
        else Note($"Микрофон: {(listener.UsesMicrophone ? "есть" : "нет")}");
        if (scenario == "caught") { yield return Caught(); yield break; }
        if (scenario == "lease") { yield return Lease(); yield break; }
        if (scenario == "padlock") { yield return Padlock(); yield break; }

        stage = "hide";
        Look(BasementLayout.HideCrateCenter);
        Place(new Vector3(BasementLayout.HideExit.x, BasementLayout.FloorY, BasementLayout.HideExit.z));
        yield return Wait(0.3f);
        Look(BasementLayout.HideCrateCenter);
        yield return Wait(0.1f);
        Check(Press("Спрятаться за коробкой"), "Прицел на коробке, подпись «Спрятаться за коробкой»");
        yield return Wait(0.2f);
        Shot("hidden");

        stage = "wait-monster";
        float started = Time.time;
        yield return Until(() => chapter.CurrentTrapPhase != BasementChapter.TrapPhase.Lure, 25f);
        Note($"Монстр дошёл за {Time.time - started:0.0} с");
        if (scenario == "mic") { Note($"Микрофон: фаза {chapter.CurrentTrapPhase}"); yield break; }
        Check(chapter.CurrentTrapPhase == BasementChapter.TrapPhase.Window, $"Монстр в камере, фаза {chapter.CurrentTrapPhase}");
        if (chapter.CurrentTrapPhase != BasementChapter.TrapPhase.Window) yield break;
        Transform monster = FindFirstObjectByType<BasementStalker>().transform;
        Check(monster.position.z < BasementLayout.CellArea.yMax - 0.3f, $"Монстр за линией решётки, z={monster.position.z:0.00}");
        yield return Wait(0.6f);
        Shot("monster-in-cell");

        if (scenario == "late")
        {
            stage = "late";
            yield return LastLine(30f);
            Check(ending == EndingId.Hidden, $"Не успел захлопнуть: концовка {ending}");
            yield break;
        }

        stage = "leave-hide";
        typeof(BasementChapter).GetMethod("ExitHideSpot", Hidden).Invoke(chapter, null);
        yield return null;
        Vector3 door = level.cellDoor.TransformPoint(new Vector3(-1.15f, 1.1f, 0f));
        Look(door);
        yield return Wait(0.2f);
        Look(door);
        yield return null;
        Check(Press("Захлопнуть решётку"), "Прицел на решётке, подпись «Захлопнуть решётку»");
        yield return Wait(0.5f);
        Check(chapter.CurrentTrapPhase == BasementChapter.TrapPhase.Slammed, $"Решётка захлопнута, фаза {chapter.CurrentTrapPhase}");
        Check(Quaternion.Angle(level.cellDoor.localRotation, Quaternion.identity) < 1f, "Решётка стоит в закрытом положении");
        Check(level.cellDoorCollider.activeSelf, "Коллайдер решётки включён");
        Shot("slammed");

        if (scenario == "wedge-late")
        {
            stage = "wedge-late";
            yield return LastLine(30f);
            Check(ending == EndingId.Hidden, $"Не вставил лом: концовка {ending}");
            yield break;
        }

        stage = "wedge";
        Place(new Vector3(-44.9f, BasementLayout.FloorY, 109.5f));
        yield return Wait(0.15f);
        Look(BasementLayout.CellLock);
        yield return Wait(0.1f);
        Look(BasementLayout.CellLock);
        yield return null;
        Check(Press("Вставить лом в проушины"), "Прицел на проушинах, подпись «Вставить лом в проушины»");
        yield return Wait(0.5f);
        Check(chapter.CurrentTrapPhase == BasementChapter.TrapPhase.Wedged, $"Лом вставлен, фаза {chapter.CurrentTrapPhase}");
        Check(!intro.playerInventory.HasItem("Crowbar"), "Лом исчез из инвентаря");
        Check(!intro.playerInventory.hasCrowbar, "Старый флаг лома снят");
        Check(GameObject.Find("Crowbar in eyelets") != null, "Лом стоит в проушинах");
        yield return Wait(1f);
        Shot("wedged");
        float rotation = Quaternion.Angle(level.cellDoor.localRotation, Quaternion.identity);
        yield return FinalTape();
        yield return Until(() => ending.HasValue, 90f);
        Check(ending == EndingId.Answer, $"Концовка {ending}");
        Check(chapter.CurrentStage == BasementChapter.Stage.Done, $"Глава закончена, этап {chapter.CurrentStage}");
        Note($"Решётка до плёнки: {rotation:0.0}°, после: {Quaternion.Angle(level.cellDoor.localRotation, Quaternion.identity):0.0}°");
    }

    // Договор открывается в окне «Осмотра» лицом к камере.
    private IEnumerator Lease()
    {
        stage = "lease";
        foreach (InspectableClue clue in FindObjectsByType<InspectableClue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (clue.ClueId == "clue-lease") Check(ClueInspectWindow.Show(clue), "Окно осмотра открылось");
        yield return Wait(0.8f);
        Shot("lease-preview");
        yield return Wait(0.5f);
    }

    // Замок в кадре: подошли к закрытой решётке снаружи (окно прыжка держит её открытой, закрываем для снимка).
    private IEnumerator Padlock()
    {
        stage = "padlock";
        level.cellDoor.localRotation = Quaternion.identity;
        level.lockVisual.SetActive(true);
        if (intro.playerInventory.flashlightLightSource != null) intro.playerInventory.flashlightLightSource.enabled = true;
        Place(new Vector3(-45.3f, BasementLayout.FloorY, 108.95f));
        yield return Wait(0.2f);
        Transform body = level.lockVisual.transform.Find("Barn padlock");
        Check(body != null, "Модель амбарного замка на месте");
        if (body == null) yield break;
        Look(body.position + Vector3.down * 0.08f);
        yield return Wait(0.3f);
        Shot("padlock");
        Note($"Замок: {body.position:F2}, размер {body.GetComponent<MeshFilter>().sharedMesh.bounds.size:F3}");
        yield return Wait(0.5f);
    }

    // Игрок стоит на пути монстра и не прячется.
    private IEnumerator Caught()
    {
        stage = "caught";
        Place(new Vector3(BasementLayout.TrapMouth.x, BasementLayout.FloorY, BasementLayout.TrapMouth.y + 0.6f));
        yield return LastLine(45f);
        Check(ending == EndingId.Hidden, $"Поймал на пути: концовка {ending}");
    }

    // S-10: после запирания идут утренние стуки на чёрном экране и четыре реплики героя.
    private IEnumerator FinalTape()
    {
        stage = "final-knocks";
        int seen = 0;
        var lines = BasementChapter.KnockLines;
        for (float t = 0f; t < 120f && !ending.HasValue; t += Time.unscaledDeltaTime)
        {
            if (seen < lines.Length && PanelText() == lines[seen]) { if (seen == 0) Shot("final-line"); seen++; }
            yield return null;
        }
        Check(seen == lines.Length, $"Реплики после запирания показаны: {seen}/{lines.Length}");
    }

    // S-11: реплика E3 видна на чёрной камере, и после неё концовка всё равно наступает.
    private IEnumerator LastLine(float timeout)
    {
        yield return Until(() => PanelText() == NightTwoExits.CaughtThought, timeout);
        Check(PanelText() == NightTwoExits.CaughtThought, "Реплика E3 на экране: " + PanelText());
        Check(view.cullingMask == 0, "Камера чёрная");
        yield return Wait(0.5f);
        Shot("e3-line");
        yield return Until(() => ending.HasValue, 15f);
    }

    // S-11: эпилог E4 напрямую, без погони.
    private IEnumerator Epilogue()
    {
        stage = "epilogue";
        var exits = intro.GetComponent<NightTwoExits>();
        Check(exits != null, "NightTwoExits есть");
        bool done = false;
        StartCoroutine(RunEpilogue(exits, () => done = true));
        yield return Until(() => PanelText().StartsWith("Новая комната"), 10f);
        Check(PanelText().StartsWith("Новая комната"), "Первая реплика E4: " + PanelText());
        yield return Wait(0.5f);
        Shot("e4-first");
        yield return Until(() => PanelText().StartsWith("Под полом"), 15f);
        Check(PanelText().StartsWith("Под полом"), "Вторая реплика E4: " + PanelText());
        yield return Wait(0.5f);
        Shot("e4-second");
        yield return Until(() => done, 15f);
        Check(done, "Эпилог E4 завершился");
    }

    private static IEnumerator RunEpilogue(NightTwoExits exits, Action finished)
    {
        yield return (IEnumerator)typeof(NightTwoExits).GetMethod("EscapeEpilogue", Hidden).Invoke(exits, null);
        finished();
    }

    private string PanelText()
    {
        var body = (TMPro.TMP_Text)typeof(NarrativeReadingPanel).GetField("body", Hidden).GetValue(panel);
        return body != null ? body.text ?? "" : "";
    }

    private bool Press(string label)
    {
        Physics.SyncTransforms();
        if (!interaction.TryGetInteractionHit(out RaycastHit hit)) { Note("Луч ни во что не попал"); return false; }
        var target = hit.collider.GetComponentInParent<BasementCellLock>();
        Note($"Луч: {hit.collider.name} на {hit.distance:0.00} м, подпись «{(target != null ? target.Label : "—")}»");
        if (target == null || !target.CanInteract || target.Label != label) return false;
        target.Interact();
        return true;
    }

    private void Place(Vector3 feet)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = feet + Vector3.up * 0.02f;
        body.enabled = true;
        Physics.SyncTransforms();
    }

    // Голова повёрнута прямо через камеру: поворот игрока по Y и наклон головы, как при обычном управлении.
    private void Look(Vector3 point)
    {
        Vector3 flat = point - view.transform.position;
        flat.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(flat);
        view.transform.rotation = Quaternion.LookRotation(point - view.transform.position);
        // Контроллер каждый кадр ставит наклон из своего поля.
        typeof(PlayerController).GetField("yRotation", Hidden).SetValue(player, Mathf.DeltaAngle(0f, view.transform.localEulerAngles.x));
        Physics.SyncTransforms();
    }

    private void Shot(string name)
    {
        string path = Path.Combine(output, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
    }

    private IEnumerator Until(Func<bool> condition, float timeout)
    {
        for (float t = 0f; t < timeout && !condition(); t += Time.unscaledDeltaTime) yield return null;
    }

    private static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

    private void Check(bool ok, string text) => (ok ? passed : failed).Add(text);

    private void Note(string text) => notes.Add(text);

    private void Write()
    {
        var lines = new List<string> { "scenario: " + scenario, "stage: " + stage, "PASSED " + passed.Count, "FAILED " + failed.Count };
        foreach (string f in failed) lines.Add("  FAIL " + f);
        foreach (string p in passed) lines.Add("  ok   " + p);
        foreach (string n in notes) lines.Add("  note " + n);
        File.WriteAllLines(Path.Combine(output, "report.txt"), lines);
    }
}
#endif
