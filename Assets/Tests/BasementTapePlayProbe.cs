#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка C-04 (камера на полу, запись с ударом, труба). BasementTapePlayProbe.Arm() из Editor-команды до входа
// в Play Mode: сцена не меняется, этап прыжка (StartBasement) выставляется в рантайме до IntroSequence.Start.
// Отчёт и кадры — .codex/verification/basement-tape.
[DefaultExecutionOrder(-100)]
public sealed class BasementTapePlayProbe : MonoBehaviour
{
    private const string ArmKey = "kfb.basementtape.probe";

    private readonly List<string> passed = new List<string>(), failed = new List<string>(), notes = new List<string>();
    private IntroSequence intro;
    private PlayerController player;
    private BasementChapter chapter;
    private BasementDepths depths;
    private Camera view;
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
        sequence.startStage = DebugStoryStage.StartBasement;
        new GameObject("Basement tape verification").AddComponent<BasementTapePlayProbe>();
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/basement-tape");
        Directory.CreateDirectory(output);
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        view = player.GetComponentInChildren<Camera>();
        try { yield return Run(); }
        finally { Write(); }
    }

    private IEnumerator Run()
    {
        stage = "wait-room";
        yield return Until(() => (chapter = FindFirstObjectByType<BasementChapter>(FindObjectsInactive.Include)) != null &&
            chapter.CurrentStage == BasementChapter.Stage.Room, 40f);
        Check(chapter != null && chapter.CurrentStage == BasementChapter.Stage.Room, "Ночь: игрок в подвале, этап Room");
        if (chapter == null || chapter.CurrentStage != BasementChapter.Stage.Room) yield break;
        depths = BasementDepths.Get();
        yield return Wait(0.5f);

        var phone = FindFirstObjectByType<PhoneFlashlight>(FindObjectsInactive.Include);
        Light beam = intro.playerInventory != null ? intro.playerInventory.flashlightLightSource : null;
        if (beam != null) { beam.enabled = true; Note($"Фонарь: {beam.type}, сила {beam.intensity}, дальность {beam.range}, угол {beam.spotAngle}"); }
        else Note("Фонаря героя нет (flashlightLightSource == null), телефон: " + (phone != null && phone.TryUse()));
        stage = "camera-on-floor";
        GameObject tripod = GameObject.Find("Camera Tripod (C-01)");
        Check(tripod != null, "Штатив на месте");
        if (tripod != null)
        {
            Check(tripod.transform.up.y < 0.4f, $"Штатив лежит (up.y={tripod.transform.up.y:0.00})");
            var renderers = tripod.GetComponentsInChildren<Renderer>();
            float low = float.MaxValue; foreach (var r in renderers) low = Mathf.Min(low, r.bounds.min.y);
            Check(Mathf.Abs(low - BasementLayout.FloorY) < 0.12f, $"Штатив на полу (низ {low:0.00}, пол {BasementLayout.FloorY:0.00})");
            Check(!tripod.CompareTag("Pickable"), "Штатив нельзя взять (тег снят)");
            var lockOnTripod = tripod.GetComponent<BasementCellLock>();
            Check(lockOnTripod != null && lockOnTripod.CanInteract, "Камера служит пультом записи");
            Vector3 center = renderers[0].bounds.center; foreach (var r in renderers) center = r.bounds.center;
            Place(center + new Vector3(1.4f, 0f, 0.3f), center);
            yield return Wait(0.3f);
            Ray ray = new Ray(view.transform.position, center - view.transform.position);
            bool hit = Physics.Raycast(ray, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore);
            var actor = player.GetComponent<PlayerInteraction>();
            InteractionFocus focus = default;
            bool resolved = hit && InteractionFocusResolver.TryResolve(actor, intro, h, out focus);
            Check(resolved && focus.Prompt == Loc.T("Посмотреть запись"), $"Подсказка у камеры: «{(resolved ? focus.Prompt : "—")}» (ожидалось «Посмотреть запись», попадание {(hit ? h.collider.name : "нет")})");
            Capture("camera-on-floor");
        }

        stage = "vent-look";
        Place(new Vector3(-39.2f, BasementLayout.FloorY, 117.6f), new Vector3(-37.83f, BasementLayout.FloorY + 0.5f, 117.6f));
        view.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
        yield return Wait(0.4f);
        Capture("vent-look-1m4");
        Place(new Vector3(-38.55f, BasementLayout.FloorY, 117.6f), new Vector3(-37.4f, BasementLayout.FloorY + 0.3f, 117.6f));
        view.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        yield return Wait(0.4f);
        Capture("vent-look-close");

        stage = "tape";
        var lockPoint = tripod != null ? tripod.GetComponent<BasementCellLock>() : null;
        if (lockPoint != null) lockPoint.Interact();
        NightRecording recording = null;
        yield return Until(() => (recording = FindFirstObjectByType<NightRecording>()) != null, 5f);
        Check(recording != null, "Запись пошла");
        if (recording != null)
        {
            foreach (float t in new[] { 3.8f, 10f, 12.4f, 13.6f, 14.6f, 16f, 17.8f, 19f, 20.5f, 24f, 27.8f, 29.6f, 30.35f, 30.65f, 30.8f, 31.05f, 31.6f, 32.3f })
            {
                recording.Seek(t);
                yield return null;
                recording.Seek(t);
                yield return new WaitForEndOfFrame();
                CaptureTape(recording, "tape-" + t.ToString("00.00"));
            }
            recording.Seek(VentTape.Duration);
            yield return Until(() => chapter.CurrentStage == BasementChapter.Stage.Room, 15f);
            Check(chapter.CurrentStage == BasementChapter.Stage.Room, "После записи герой снова в подвале");
        }

        stage = "duct";
        Place(new Vector3(-39.2f, BasementLayout.FloorY, 117.6f), new Vector3(-37.83f, BasementLayout.FloorY + 0.5f, 117.6f));
        yield return Wait(0.2f);
        // Игрок идёт сам: присед и CharacterController.Move через проём, трубу и устье, без действий.
        Place(new Vector3(-38.6f, BasementLayout.FloorY + 0.05f, 117.7f), new Vector3(-37f, BasementLayout.FloorY + 0.5f, 117.7f));
        player.CrouchDown();
        yield return Wait(1.2f);
        Check(chapter.CurrentStage == BasementChapter.Stage.Room, "У проёма герой ещё в подвале");
        var body = player.GetComponent<CharacterController>();
        Vector3[] walk =
        {
            new Vector3(-37f, 0f, 117.7f), new Vector3(-37f, 0f, 119.3f), new Vector3(-35.75f, 0f, 119.3f), new Vector3(-35.75f, 0f, 117.6f)
        };
        for (int i = 0; i < walk.Length; i++)
        {
            for (float t = 0f; t < 12f; t += Time.deltaTime)
            {
                Vector3 to = walk[i] - player.transform.position;
                to.y = 0f;
                if (to.magnitude < 0.08f) break;
                body.Move(to.normalized * 1.2f * Time.deltaTime + Vector3.down * 0.05f);
                yield return null;
            }
            Vector3 p = player.transform.position;
            Check(new Vector2(p.x - walk[i].x, p.z - walk[i].z).magnitude < 0.2f, $"Дошёл до точки {i} ({p.x:0.00}, {p.z:0.00})");
            if (i == 1) { Check(chapter.CurrentStage == BasementChapter.Stage.Duct, "В трубе этап Duct"); Check(player.IsCrouching, "Под крышей герой на корточках"); Capture("duct-walk"); }
        }
        yield return Wait(0.3f);
        Check(chapter.CurrentStage == BasementChapter.Stage.Explore, "Вылез в зал B сам, этап Explore");
        stage = "complete";
    }

    private void Place(Vector3 feet, Vector3 look)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = feet + Vector3.up * 0.02f;
        Vector3 flat = look - feet; flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(flat);
        body.enabled = true;
        view.transform.rotation = Quaternion.LookRotation(look - view.transform.position);
        Physics.SyncTransforms();
    }

    private void CaptureTape(NightRecording recording, string name)
    {
        var rt = recording.Texture;
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
        Destroy(shot);
    }

    private void Capture(string name)
    {
        var rt = new RenderTexture(960, 540, 24);
        var previous = view.targetTexture;
        view.targetTexture = rt;
        view.Render();
        view.targetTexture = previous;
        var old = RenderTexture.active;
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        RenderTexture.active = old;
        File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
        Destroy(shot);
        rt.Release();
        Destroy(rt);
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
    }

    private IEnumerator Until(Func<bool> condition, float timeout)
    {
        for (float t = 0f; t < timeout && !condition(); t += Time.deltaTime) yield return null;
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
