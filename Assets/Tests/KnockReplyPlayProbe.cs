#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KnocksFromBeneath;
using UnityEngine;

// Play Mode проверка K-02 (ответный стук): KnockReplyPlayProbe.Begin() из Editor-команды.
// Ночь 2 (StartKitchenNoise): подсказка Q, стук только сидя и над подвалом (не гараж, не 2-й этаж),
// кивок камеры, повтор числа ударов, молчание на беспорядочный стук, пауза.
// Ночь 1 (StartNightOne): BeginNightOne() — игрок в кровати на 2-м этаже, стучать нельзя.
// Отчёт — .codex/verification/knock-reply.
[DefaultExecutionOrder(-100)]
public sealed class KnockReplyPlayProbe : MonoBehaviour
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
    private static readonly Vector3 Garage = new Vector3(-36f, 0.9f, 110f);
    private IntroSequence intro;
    private PlayerController player;
    private PlayerFloorKnock floorKnock;
    private KnockDirector knock;
    private KnockResponder responder;
    private RandomKnock randomKnock;
    private Transform cam;
    private string output;
    private bool nightOne;

    public static void Begin() => Create(false);
    public static void BeginNightOne() => Create(true);

    private static void Create(bool nightOne)
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Knock reply verification").AddComponent<KnockReplyPlayProbe>().nightOne = nightOne;
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/knock-reply");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        cam = player.GetComponentInChildren<Camera>().transform;
        floorKnock = intro.GetComponent<PlayerFloorKnock>();
        knock = intro.knockController.GetComponent<KnockDirector>();
        responder = intro.GetComponent<KnockResponder>();
        randomKnock = intro.knockController.GetComponent<RandomKnock>();
        Check(floorKnock != null, "PlayerFloorKnock добавлен IntroSequence");
        yield return nightOne ? NightOne() : NightTwo();
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
        intro.knockController.SetActive(false);
        yield return null;
        intro.knockController.SetActive(true);

        Stage("hint");
        int before = randomKnock.PlayedKnockCount;
        yield return Until(() => randomKnock.PlayedKnockCount > before, 15f);
        yield return null;
        yield return null;
        Check(floorKnock.HintVisible, "После стука снизу (игрок над подвалом) появилась подсказка «Q — постучать в пол»");

        Stage("rules");
        player.StandUp();
        yield return Wait(0.3f);
        Check(!floorKnock.TryKnock(), "Стоя постучать нельзя");
        Vector3 bedroom = new Vector3(-44f, 4.2f, 115f); // второй этаж над прямоугольником подвала — отсекает высота пола
        Teleport(bedroom);
        player.CrouchDown();
        yield return Wait(0.4f);
        Check(!floorKnock.IsOverBasement && !floorKnock.TryKnock(), $"Второй этаж — нельзя (y={bedroom.y:0.0})");
        Check(!floorKnock.HintVisible, "На втором этаже подсказки нет");
        Teleport(Garage);
        yield return Wait(0.4f);
        Check(!floorKnock.IsOverBasement && !floorKnock.TryKnock(), "Гараж — нельзя");
        Teleport(Center);
        yield return Wait(0.4f);
        Check(player.IsCrouching && floorKnock.IsOverBasement, "Первый этаж дома, сидя — над подвалом");

        Stage("dip");
        yield return new WaitForEndOfFrame();
        float pitchBefore = Pitch();
        Check(floorKnock.TryKnock(), "Удар в пол проходит в обычном геймплее");
        float maxDip = 0f;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            yield return new WaitForEndOfFrame();
            maxDip = Mathf.Max(maxDip, Mathf.DeltaAngle(pitchBefore, Pitch()));
        }
        yield return new WaitForEndOfFrame();
        Check(maxDip > 3f && maxDip < 6f, $"Кивок камеры вниз ({maxDip:0.0}°)");
        Check(Mathf.Abs(Mathf.DeltaAngle(pitchBefore, Pitch())) < 0.05f, "Камера вернулась в исходный наклон");
        Check(!floorKnock.HintVisible, "После своего стука подсказка исчезла");
        Check(knock.HasPlayer && knock.Attention > 0.8f, $"Стук игрока выдаёт его стучащему (внимание {knock.Attention:0.00} через 0.5 с)");

        Stage("repeat");
        yield return Until(() => responder.LastReply != KnockReplyKind.None && !responder.IsReplying, 5f);
        Check(responder.LastReply == KnockReplyKind.Repeat && responder.LastReplyCount == 1, $"Один удар — один в ответ ({responder.LastReply} {responder.LastReplyCount})");
        yield return Wait(1f);
        int played = responder.ReplyKnocksPlayed;
        int randomBefore = randomKnock.PlayedKnockCount;
        for (int i = 0; i < 3; i++) { floorKnock.TryKnock(); yield return Wait(0.3f); }
        yield return Until(() => responder.IsReplying, 3f);
        yield return Until(() => !responder.IsReplying, 5f);
        Check(responder.LastReply == KnockReplyKind.Repeat && responder.LastReplyCount == 3 && responder.ReplyKnocksPlayed - played == 3,
            $"Три удара — три в ответ (сыграно {responder.ReplyKnocksPlayed - played})");
        Check(randomKnock.PlayedKnockCount == randomBefore, "Пока игрок стучит и ждёт ответа, случайный стук молчит");

        Stage("silence");
        yield return Wait(1f);
        played = responder.ReplyKnocksPlayed;
        for (int i = 0; i < 7; i++) { floorKnock.TryKnock(); yield return Wait(0.22f); }
        yield return Wait(2f);
        randomBefore = randomKnock.PlayedKnockCount;
        Check(responder.LastReply == KnockReplyKind.Silence && responder.ReplyKnocksPlayed == played, "Беспорядочный стук — в ответ тишина");
        yield return Wait(6f);
        Check(randomKnock.PlayedKnockCount == randomBefore, "После беспорядочного стука стучащий затих");

        Stage("controller-off");
        // Квест кухни выключает Knock Controller — хозяин всё равно отвечает (из-под пола под игроком).
        yield return Wait(1f);
        intro.knockController.SetActive(false);
        played = responder.ReplyKnocksPlayed;
        for (int i = 0; i < 2; i++) { floorKnock.TryKnock(); yield return Wait(0.3f); }
        yield return Until(() => responder.IsReplying, 3f);
        yield return Until(() => !responder.IsReplying, 5f);
        Check(responder.LastReply == KnockReplyKind.Repeat && responder.ReplyKnocksPlayed - played == 2,
            $"Knock Controller выключен (после кухни) — ответ всё равно приходит (сыграно {responder.ReplyKnocksPlayed - played})");
        intro.knockController.SetActive(true);

        Stage("pause");
        Time.timeScale = 0f;
        int count = floorKnock.KnockCount;
        Check(!floorKnock.TryKnock() && floorKnock.KnockCount == count, "На паузе Q не работает");
        Time.timeScale = 1f;
        player.isCameraLocked = true;
        Check(!floorKnock.TryKnock(), "При заблокированной камере (катсцена) Q не работает");
        player.isCameraLocked = false;
        report.notes.Add($"knocks={floorKnock.KnockCount} replyKnocks={responder.ReplyKnocksPlayed} occluders={knock.Occluders}");
    }

    private IEnumerator NightOne()
    {
        var scare = intro.GetComponent<NightOneScare>();
        Stage("wait-scare");
        yield return Until(() => scare.IsPlaying, 25f);
        Check(scare.IsPlaying, "Сцена стука ночи 1 началась");
        // Два удара снизу (3.2 + 0.64 с), затем ожидание ответа игрока.
        yield return Wait(4.6f);
        player.CrouchDown();
        yield return Wait(0.4f);
        Check(!floorKnock.HintVisible, "Ночь 1: игрок в кровати на 2-м этаже — подсказки Q нет");
        Check(!floorKnock.TryKnock(), "Ночь 1: на 2-м этаже постучать нельзя");
        player.StandUp();
        yield return Until(() => !scare.IsPlaying, 15f);
        Check(scare.AnsweredKnocks == 1, $"Ночь 1: без ответа игрока сцена идёт как раньше — один удар ({scare.AnsweredKnocks})");
    }

    private float Pitch() => cam.localEulerAngles.x;

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
