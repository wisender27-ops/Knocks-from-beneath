using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // P-03 «Подвал как глава» + B-05 финал E5 «Ответ». Ночь 2: игрок спускается в подвал, смотрит запись с камеры,
    // которую поставил днём (монстр выбирается из вентиляции), и ползёт по этой короткой квадратной трубе (BasementDepths, план в DepthsLayout).
    // Она выводит за забетонированную стену, в отрезанную половину подвала (BasementLevel, план в BasementLayout).
    // Там по залу ходит монстр и слушает шаги, из камеры в дальнем блоке стучит хозяин — по трубе, которая идёт наверх,
    // в дом. Игрок идёт на стук, не выдавая себя, бьёт ломом по замку, прячется от монстра, который пришёл на грохот
    // (микрофон, как под кроватью), и сбивает замок — выбор «не заколачивать, а ответить». Здесь только логика.
    public sealed class BasementChapter : MonoBehaviour
    {
        // Значения не менять: Tunnels больше не используется, но Duct идёт после него.
        public enum Stage { Idle, Descending, Explore, Striking, Caught, Finale, Done, Room, Tunnels, Duct }

        private const string FindQuest = "basement-find", ReachQuest = "basement-reach", StrikeQuest = "basement-unlock",
            HideQuest = "basement-hide", BreakQuest = "basement-break",
            VentQuest = "basement-vent", DuctQuest = "basement-duct";
        private const float CellDoorSwing = 100f;

        private Transform player;
        private PlayerInventory inventory;
        private PlayerController controller;
        private Action<string, int, UnityAction, string> createQuest;
        private Action<string[], Action> say, sayQuick;
        private Action<EndingId> endGame;
        private Action silenceAbove;

        private BasementLevel level;
        private BasementDepths depths;
        private GameObject monster;
        private BasementStalker stalker;
        private BasementHideListener hideListener;
        private BasementCellLock cellLock;
        private Transform cellDoor;
        private TapeDoor tapeCellDoor;
        private GameObject cellDoorCollider, lockVisual;
        private AudioSource knockSource, effects;
        private Coroutine knocking;
        private CanvasGroup fade;
        private int catches;
        private bool reachedCell, hallSeen, struck, hideActive, hideFailed, breakReady, tapeSeen;
        private bool inHideSpot, distracted, savedCameraLock, savedBodyEnabled, savedFlashlight;
        private int hideEnteredFrame;
        private float hideYaw, hidePitch, inspectionTime;
        private Camera hideCamera;
        private CharacterController hideBody;
        private Vector3 savedEyePosition;
        private Quaternion savedCameraRotation;
        private Behaviour[] hideInputs;
        private bool[] hideInputStates;
        private AudioSource distractionSource;
        private BasementOwner owner;

        public Stage CurrentStage { get; private set; }
        public bool IsActive => CurrentStage != Stage.Idle && CurrentStage != Stage.Done;

        public void Initialize(Transform playerTransform, PlayerInventory playerInventory,
            Action<string, int, UnityAction, string> createQuestCallback, Action<string[], Action> importantThoughts,
            Action<string[], Action> quickThoughts, Action<EndingId> endGameCallback, Action silenceAboveCallback)
        {
            player = playerTransform;
            inventory = playerInventory;
            controller = player != null ? player.GetComponent<PlayerController>() : null;
            createQuest = createQuestCallback;
            say = importantThoughts;
            sayQuick = quickThoughts;
            endGame = endGameCallback;
            silenceAbove = silenceAboveCallback;
        }

        // Спуск: затемнение, подвал оживает, игрок у подножия лестницы.
        public void Begin(BasementHatch hatch, bool debugJump = false, bool debugWing = false)
        {
            if (CurrentStage != Stage.Idle || player == null) return;
            CurrentStage = Stage.Descending;
            StartCoroutine(BeginRoutine(hatch, debugJump, debugWing));
        }

        private IEnumerator BeginRoutine(BasementHatch hatch, bool debugJump, bool debugWing)
        {
            LockCamera(true);
            EnsureFade();
            yield return Fade(1f, debugJump ? 0.05f : 0.9f);
            silenceAbove?.Invoke();
            hatch?.OpenForCinematic();
            EnsureBuilt();
            if (debugJump && inventory != null) inventory.hasCrowbar = true;
            catches = 0;
            reachedCell = hallSeen = struck = hideActive = hideFailed = breakReady = tapeSeen = false;
            if (debugWing)
            {
                PlaceAt(DepthsLayout.DuctOutlet, DepthsLayout.WingStart);
                yield return new WaitForSeconds(0.6f);
                yield return Fade(0f, 1.2f);
                LockCamera(false);
                EnterWing();
                yield break;
            }
            PlaceAt(BasementLayout.PlayerStart, BasementLayout.PlayerStartLook);
            yield return new WaitForSeconds(debugJump ? 0.05f : 0.7f);
            yield return Fade(0f, 1.4f);
            LockCamera(false);
            CurrentStage = Stage.Room;
            sayQuick?.Invoke(new[] { "Штатив сбит, камера на полу.", "Можно посмотреть, что она успела записать." }, null);
            createQuest?.Invoke("Залезть в вентиляцию", 1, null, VentQuest);
        }

        // Запись необязательна: решётка открыта, в трубу можно лезть сразу. Смотреть её можно, пока игрок в подвале.
        private bool CanWatchTape() => CurrentStage == Stage.Room;

        private void WatchTape()
        {
            if (CanWatchTape()) StartCoroutine(WatchTapeRoutine());
        }

        // Плёнка идёт прямо в подвале: монстр выбирается из той самой трубы, у которой лежит камера.
        private IEnumerator WatchTapeRoutine()
        {
            CurrentStage = Stage.Descending;
            LockCamera(true);
            yield return PlayVentTape();
            LockCamera(false);
            CurrentStage = Stage.Room;
            if (tapeSeen) yield break;
            tapeSeen = true;
            sayQuick?.Invoke(new[] { "Он вылез отсюда. Увидел объектив — и ударил.", "Он знал, что его снимают. И он идёт за тем, кто поставил камеру." }, null);
        }

        private IEnumerator PlayVentTape()
        {
            if (depths.tapeView == null) yield break;
            CameraPlacementController gear = CameraPlacementController.Instance;
            NightRecording recording = NightRecording.Create(depths.tapeView, new TapeSetup
            {
                Timeline = VentTape.Timeline,
                Doors = new TapeDoor[0],
                Lights = VentTape.BuildLights,
                ViewShift = VentTape.ViewShift,
                Hidden = gear != null ? gear.GearRenderers() : new Renderer[0]
            });
            yield return TapeReel.Play(recording, transform);
            Destroy(recording.gameObject);
        }

        private void OpenVent()
        {
            depths.vent.Initialize(() => CurrentStage == Stage.Room, UseVent, VentLabel);
        }

        private void UseVent()
        {
            if (depths.hatch.IsUnbolted) StartCoroutine(ClimbIntoDuct());
            else if (inventory != null && inventory.HasItem("Screwdriver")) depths.hatch.Begin();
        }

        private string VentLabel() => depths.hatch.IsUnbolted ? "Залезть в короб"
            : inventory != null && inventory.HasItem("Screwdriver") ? "Открутить болты" : "Нужна отвёртка";

        // Короб висит выше пояса: герой забирается в него с ящика, за затемнением.
        private IEnumerator ClimbIntoDuct()
        {
            CurrentStage = Stage.Descending;
            LockCamera(true);
            yield return Fade(1f, 0.35f);
            if (controller != null) controller.ForceCrouch = true;
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.height = controller != null ? controller.crouchingHeight : 1f;
            PlaceAt(DepthsLayout.DuctClimbSpot, DepthsLayout.DuctEnd);
            CurrentStage = Stage.Room;
            yield return Fade(0f, 0.35f);
            LockCamera(false);
        }

        // Короб: только на корточках. Из него можно вернуться в подвал, спрыгнув с торца.
        private void EnterDuct()
        {
            CurrentStage = Stage.Duct;
            depths.SetDuctActive(true);
            knockSource.transform.position = DepthsLayout.DuctEnd;
            if (inventory != null && inventory.hasFlashlight && inventory.flashlightLightSource != null)
                inventory.flashlightLightSource.enabled = true;
            if (QuestActive(VentQuest))
            {
                QuestManager.Instance.AddProgress(1);
                sayQuick?.Invoke(new[] { "Ну и вонь. Гниль, сырость, будто здесь что-то сдохло.", "Жесть сгнила, прогибается под коленями. Стук впереди." }, null);
                createQuest?.Invoke("Ползти по трубе", 1, null, DuctQuest);
            }
            if (knocking == null) knocking = StartCoroutine(KnockRoutine());
        }

        private void LeaveDuct()
        {
            CurrentStage = Stage.Room;
            depths.SetDuctActive(false);
            if (controller != null) controller.ForceCrouch = false;
        }

        // Труба кончилась: герой вылез за забетонированной стеной, в ту половину подвала, которой «не было».
        private void EnterWing()
        {
            depths.SetDuctActive(false);
            if (controller != null) controller.ForceCrouch = false;
            SpawnMonster();
            knockSource.transform.position = PipeInCell();
            if (QuestActive(DuctQuest)) QuestManager.Instance.AddProgress(1);
            CurrentStage = Stage.Explore;
            sayQuick?.Invoke(new[] { "За этой стеной подвал, из которого я ушёл. Вот вторая половина.", "Он ходит здесь. Присесть и идти медленно, тогда он не услышит." }, null);
            createQuest?.Invoke("Найти того, кто стучал", 1, () => createQuest("Добраться до камеры", 1, OnReached, ReachQuest), FindQuest);
            stalker.Begin();
        }

        private static Vector3 PipeInCell() =>
            new Vector3(BasementLayout.PipeRoute[0].x, BasementLayout.FloorY + 1.3f, BasementLayout.PipeRoute[0].y);

        // Цепочка заданий: дойти → ударить по замку → спрятаться → сбить замок.
        private void OnReached()
        {
            createQuest?.Invoke("Ударить по замку ломом", 1, OnStruckQuestDone, StrikeQuest);
        }

        private void OnStruckQuestDone()
        {
            createQuest?.Invoke("Спрятаться от монстра", 1, OnHideQuestDone, HideQuest);
        }

        private void OnHideQuestDone()
        {
            breakReady = true;
            createQuest?.Invoke("Сбить замок", 1, null, BreakQuest);
        }

        private void Update()
        {
            if (player == null) return;
            Vector3 p = player.position;
            var at = new Vector2(p.x, p.z);
            if (CurrentStage == Stage.Room && DepthsLayout.AtDuctHeight(p.y) && DepthsLayout.InDuct(at)) { EnterDuct(); return; }
            if (CurrentStage == Stage.Duct)
            {
                if (BasementLayout.HallB.Contains(at)) EnterWing();
                else if (!DepthsLayout.InDuct(at)) LeaveDuct();
                return;
            }
            if (CurrentStage != Stage.Explore) return;
            bool inHall = BasementLayout.HallB.Contains(at, 0.6f) && p.y < -2f;
            if (!hallSeen && inHall)
            {
                hallSeen = true;
                sayQuick?.Invoke(new[] { "Над головой труба. Уходит наверх, в дом.", "Если по ней стучать, звук уйдёт во все комнаты." }, null);
            }
            if (QuestActive(FindQuest) && inHall) QuestManager.Instance.AddProgress(1);
            else if (QuestActive(ReachQuest) && BasementLayout.CellBlockD.Contains(at) &&
                Vector2.Distance(at, new Vector2(BasementLayout.CellApproach.x, BasementLayout.CellApproach.z)) < 3.2f)
            {
                reachedCell = true;
                QuestManager.Instance.AddProgress(1);
                sayQuick?.Invoke(new[] { "Он стучит по этой трубе. Вот почему стук слышно по всему дому.", "Замок на решётке. Лом при мне." }, null);
            }
        }

        private void LateUpdate()
        {
            TickHide();
        }

        private static bool QuestActive(string tag) => QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(tag);

        // -------------------------------------------------------------------------------------------------
        // Прятки после удара по замку
        // -------------------------------------------------------------------------------------------------

        private bool PlayerHidden() =>
            hideActive && inHideSpot && !hideFailed;

        private void TickHide()
        {
            if (!hideActive || stalker == null || player == null || NarrativeReadingPanel.WorldHalted || PauseMenuController.InputBlocked) return;
            if (inHideSpot)
            {
                float sensitivity = SettingsStore.GetMouseSensitivity();
                hideYaw = Mathf.Clamp(hideYaw + Input.GetAxis("Mouse X") * sensitivity, -35f, 35f);
                hidePitch = Mathf.Clamp(hidePitch - Input.GetAxis("Mouse Y") * sensitivity * (SettingsStore.GetInvertY() ? -1f : 1f), -15f, 15f);
                player.rotation = Quaternion.Euler(0f, hideYaw, 0f);
                if (hideCamera != null) hideCamera.transform.localRotation = Quaternion.Euler(hidePitch, 0f, 0f);
                if (Time.frameCount != hideEnteredFrame && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl)))
                    ExitHideSpot();
            }
            Vector2 at = new Vector2(player.position.x, player.position.z);
            float distance = Vector2.Distance(stalker.Brain.Position, at);
            bool moving = !inHideSpot && controller != null && !controller.IsCrouching && PlayerMoved();
            hideListener.Tick(Time.deltaTime, distance, moving);
            if (hideListener.Found && !hideFailed)
            {
                hideFailed = true;
                sayQuick?.Invoke(new[] { "Услышал…" }, null);
                stalker.Alert(player.position);
            }
            if (!hideFailed && !distracted && inHideSpot && stalker.Brain.AtSearchPoint(1))
            {
                inspectionTime += Time.deltaTime;
                if (inspectionTime >= 3f)
                {
                    distracted = true;
                    if (distractionSource != null)
                    {
                        AudioClip clip = Resources.Load<AudioClip>("Audio/Horror/impactPunch_heavy_000");
                        if (clip != null) distractionSource.PlayOneShot(clip);
                    }
                    stalker.Distract(BasementLayout.DistractionPoint);
                }
            }
            if (CurrentStage == Stage.Explore && stalker.Brain.SearchCompleted)
            {
                if (!distracted || !inHideSpot || hideFailed)
                {
                    stalker.AlertAndSearch(player.position);
                    return;
                }
                hideActive = false;
                hideListener.End();
                ExitHideSpot();
                sayQuick?.Invoke(new[] { "Ушёл. Что-то упало далеко. Пока он не вернулся, к замку." }, null);
                if (QuestActive(HideQuest)) QuestManager.Instance.AddProgress(1);
                if (knocking == null) knocking = StartCoroutine(KnockRoutine());
            }
        }

        private bool CanEnterHideSpot() => hideActive && !inHideSpot && !hideFailed && CurrentStage == Stage.Explore;

        private void EnterHideSpot()
        {
            if (!CanEnterHideSpot() || controller == null) return;
            inHideSpot = true;
            hideEnteredFrame = Time.frameCount;
            hideCamera = player.GetComponentInChildren<Camera>();
            hideBody = player.GetComponent<CharacterController>();
            savedCameraLock = controller.isCameraLocked;
            savedBodyEnabled = hideBody != null && hideBody.enabled;
            if (hideCamera != null)
            {
                savedCameraRotation = hideCamera.transform.localRotation;
                if (hideCamera.transform.parent != null)
                {
                    savedEyePosition = hideCamera.transform.parent.localPosition;
                    Vector3 eye = savedEyePosition;
                    eye.y = BasementLayout.HideEyeHeight;
                    hideCamera.transform.parent.localPosition = eye;
                }
                hideCamera.transform.localRotation = Quaternion.identity;
            }
            hideInputs = new Behaviour[] { inventory, player.GetComponent<PlayerInteraction>(), player.GetComponentInChildren<PlayerDoorOpen>(),
                player.GetComponentInChildren<HeadBobbing>(), player.GetComponent<PlayerFloorKnock>() };
            hideInputStates = new bool[hideInputs.Length];
            for (int i = 0; i < hideInputs.Length; i++)
            {
                hideInputStates[i] = hideInputs[i] != null && hideInputs[i].enabled;
                if (hideInputs[i] != null) hideInputs[i].enabled = false;
            }
            savedFlashlight = inventory != null && inventory.flashlightLightSource != null && inventory.flashlightLightSource.enabled;
            if (savedFlashlight) inventory.flashlightLightSource.enabled = false;
            controller.StopMotion();
            controller.isCameraLocked = true;
            if (hideBody != null) hideBody.enabled = false;
            player.SetPositionAndRotation(BasementLayout.HideSpot, Quaternion.identity);
            hideYaw = hidePitch = 0f;
            Physics.SyncTransforms();
            sayQuick?.Invoke(new[] { "Он рядом. Только не…" }, null);
        }

        private void ExitHideSpot()
        {
            if (!inHideSpot) return;
            inHideSpot = false;
            if (hideActive && !hideFailed && stalker != null)
            {
                distracted = false;
                inspectionTime = 0f;
                stalker.AlertAndSearch(player.position);
            }
            if (hideCamera != null)
            {
                hideCamera.transform.localRotation = savedCameraRotation;
                if (hideCamera.transform.parent != null) hideCamera.transform.parent.localPosition = savedEyePosition;
            }
            if (hideInputs != null)
                for (int i = 0; i < hideInputs.Length; i++) if (hideInputs[i] != null) hideInputs[i].enabled = hideInputStates[i];
            hideInputs = null;
            player.position = BasementLayout.HideExit;
            if (hideBody != null) hideBody.enabled = savedBodyEnabled;
            if (controller != null) controller.isCameraLocked = savedCameraLock;
            if (savedFlashlight && inventory != null && inventory.flashlightLightSource != null) inventory.flashlightLightSource.enabled = true;
        }

        private Vector3 lastPlayerPosition;

        private bool PlayerMoved()
        {
            Vector3 position = player.position;
            bool moved = (position - lastPlayerPosition).sqrMagnitude > 0.0004f;
            lastPlayerPosition = position;
            return moved;
        }

        private void BeginHide()
        {
            hideFailed = false;
            distracted = false;
            inspectionTime = 0f;
            hideActive = true;
            hideListener.Begin();
            stalker.PlayerHidden = PlayerHidden;
            stalker.AlertAndSearch(player.position);
        }

        // -------------------------------------------------------------------------------------------------
        // Поимка: бросок монстра, VHS-кадр смерти (как в других концовках), возврат
        // -------------------------------------------------------------------------------------------------

        private void OnCaught()
        {
            if (CurrentStage != Stage.Explore) return;
            CurrentStage = Stage.Caught;
            StartCoroutine(CaughtRoutine());
        }

        private IEnumerator CaughtRoutine()
        {
            bool wasHiding = hideActive;
            hideActive = false;
            ExitHideSpot();
            hideListener?.End();
            LockCamera(true);
            controller?.StopMotion();
            stalker.Stop();
            yield return Jumpscare();
            yield return DeathTapeEffect.Play();
            catches++;
            yield return Fade(1f, 0.01f);
            if (wasHiding)
            {
                PlaceAt(BasementLayout.HideRespawn, BasementLayout.HideRespawnLook);
                stalker.ResetPatrol(2);
            }
            else
            {
                PlaceAt(DepthsLayout.WingStart, DepthsLayout.WingStartLook);
                stalker.ResetPatrol(catches % 2 == 0 ? 0 : 2);
            }
            yield return new WaitForSeconds(0.9f);
            if (!wasHiding && catches == 2)
                sayQuick?.Invoke(new[] { "Он слышит шаги. Присесть на Ctrl. Красться, а не бежать." }, null);
            yield return Fade(0f, 1f);
            LockCamera(false);
            CurrentStage = Stage.Explore;
            stalker.Begin();
            if (wasHiding)
            {
                sayQuick?.Invoke(new[] { "Он вернётся к замку. За ящики у стены. Молчать." }, null);
                yield return new WaitForSeconds(6f);
                if (CurrentStage == Stage.Explore && struck) BeginHide();
            }
        }

        // Монстр бросается на игрока (рука тянется к лицу), камера на его голову, затем VHS-кадр.
        private IEnumerator Jumpscare()
        {
            var camera = player.GetComponentInChildren<Camera>();
            var rig = stalker.Rig;
            if (rig != null) { rig.StopMoving(); rig.Lunge(0.25f, 0.6f); }
            PlayEffect("Audio/CornerJumpscare", 1f, 0.95f);
            Vector3 from = monster.transform.position;
            Vector3 direction = player.position - from;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : player.forward;
            monster.transform.rotation = Quaternion.LookRotation(direction);
            Vector3 target = player.position - direction * 0.65f;
            target.y = BasementLayout.FloorY;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                monster.transform.position = Vector3.Lerp(from, target, Mathf.Clamp01(t / 0.16f));
                if (camera != null)
                {
                    Transform head = stalker.HeadTransform;
                    Vector3 headPoint = head != null ? head.position : monster.transform.position + Vector3.up * 1.5f;
                    Vector3 look = headPoint - camera.transform.position;
                    if (look.sqrMagnitude > 0.001f) camera.transform.rotation = Quaternion.LookRotation(look);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.65f);
        }

        // -------------------------------------------------------------------------------------------------
        // Замок: удар ломом, прятки, взлом
        // -------------------------------------------------------------------------------------------------

        private bool CanUseLock() => CurrentStage == Stage.Explore && reachedCell && !hideActive &&
            ((!struck && QuestActive(StrikeQuest)) || (struck && breakReady && QuestActive(BreakQuest)));

        private string LockLabel() => !struck ? "Ударить по замку" : "Сбить замок";

        private void UseLock()
        {
            if (inventory == null || !inventory.hasCrowbar)
            {
                sayQuick?.Invoke(new[] { "Замок. Голыми руками не сбить. Нужен лом." }, null);
                return;
            }
            if (!struck) StartCoroutine(StrikeRoutine());
            else StartCoroutine(FinaleRoutine());
        }

        // Один тяжёлый удар: грохот на весь подвал. Монстр идёт на звук — надо спрятаться в нишу и молчать.
        private IEnumerator StrikeRoutine()
        {
            CurrentStage = Stage.Striking;
            LockCamera(true);
            yield return TurnToward(BasementLayout.CellLock, 0.5f);
            PlayEffect("Audio/Horror/impactPunch_heavy_000", 1f, 1.2f);
            PlayEffect("Audio/Horror/impactPlank_medium_000", 1f, 1.7f);
            PlayEffect("Audio/Horror/impactWood_heavy_000", 1f, 0.8f);
            if (lockVisual != null) lockVisual.transform.localRotation *= Quaternion.Euler(0f, 0f, 24f);
            yield return new WaitForSeconds(0.7f);
            struck = true;
            LockCamera(false);
            CurrentStage = Stage.Explore;
            if (knocking != null) { StopCoroutine(knocking); knocking = null; }
            if (QuestActive(StrikeQuest)) QuestManager.Instance.AddProgress(1);
            sayQuick?.Invoke(new[] { "Грохнуло на весь подвал!", "Он идёт. Спрятаться. За ящики у стены." }, null);
            BeginHide();
        }

        private IEnumerator FinaleRoutine()
        {
            CurrentStage = Stage.Finale;
            LockCamera(true);
            stalker.Stop();
            if (knocking != null) { StopCoroutine(knocking); knocking = null; }
            yield return TurnToward(BasementLayout.CellLock, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                PlayEffect("Audio/Horror/impactPunch_heavy_000", 1f, 1.35f);
                PlayEffect("Audio/Horror/impactPlank_medium_000", 0.9f, 1.8f);
                yield return new WaitForSeconds(0.85f);
            }
            if (lockVisual != null) lockVisual.SetActive(false);
            PlayEffect("Audio/Horror/impactWood_heavy_000", 1f, 1.6f);
            if (cellDoorCollider != null) cellDoorCollider.SetActive(false);
            yield return SwingDoor();
            if (owner != null) owner.RevealFace();
            if (QuestActive(BreakQuest)) QuestManager.Instance.AddProgress(1);

            bool spoken = false;
            say?.Invoke(new[]
            {
                "…Ты ответил.",
                "Я слышал. Каждую ночь. По трубе.",
                "Я уже не верил. Двадцать лет только стук и тот, кто ходит наверху.",
                "Он идёт. Слышишь? На звук.",
                "Не бегай. Стучи, как стучал. Он идёт на ответ.",
                "Камера на стене моя. Пусть пишет. Хоть раз он попадёт на плёнку там, где надо."
            }, () => spoken = true);
            while (!spoken) yield return null;

            for (int i = 0; i < 7; i++)
            {
                PlayEffect("Audio/Horror/FollowingStep1", 1f, 0.7f - i * 0.02f);
                yield return new WaitForSeconds(0.8f - i * 0.08f);
            }
            PlayEffect("Audio/CornerJumpscare", 1f, 0.6f);
            yield return Fade(1f, 1.2f);
            yield return PlaySecondTape();
            yield return new WaitForSeconds(1.2f);
            CurrentStage = Stage.Done;
            endGame?.Invoke(EndingId.Answer);
        }

        // C-04: плёнка камеры хозяина. Живой монстр и хозяин из сцены на ней не видны: там свой сценарий BasementTape.
        private IEnumerator PlaySecondTape()
        {
            if (level.tapeView == null) { Debug.LogError("[C-04] У BasementLevel нет камеры хозяина."); yield break; }
            var hidden = new System.Collections.Generic.List<Renderer>(level.tapeView.parent.GetComponentsInChildren<Renderer>(true));
            if (monster != null) hidden.AddRange(monster.GetComponentsInChildren<Renderer>(true));
            if (owner != null) hidden.AddRange(owner.GetComponentsInChildren<Renderer>(true));
            NightRecording recording = NightRecording.Create(level.tapeView, new TapeSetup
            {
                Timeline = BasementTape.Timeline,
                Doors = new[] { tapeCellDoor },
                Lights = BasementTape.BuildLights,
                Hidden = hidden.ToArray()
            });
            yield return TapeReel.Play(recording, transform);
            Destroy(recording.gameObject);
        }

        private IEnumerator SwingDoor()
        {
            if (cellDoor == null) yield break;
            Quaternion from = cellDoor.localRotation, to = from * Quaternion.Euler(0f, CellDoorSwing, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
            {
                cellDoor.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            cellDoor.localRotation = to;
        }

        private IEnumerator TurnToward(Vector3 point, float seconds)
        {
            Vector3 flat = point - player.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) yield break;
            Quaternion from = player.rotation, to = Quaternion.LookRotation(flat);
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                player.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Стук хозяина — по трубе
        // -------------------------------------------------------------------------------------------------

        // Ритм «2·1», как у перестука (K-03). Пока монстр охотится, хозяин молчит.
        private IEnumerator KnockRoutine()
        {
            var clip = Resources.Load<AudioClip>("Audio/Horror/impactMetal_heavy_001");
            var wait = new WaitForSeconds(0.36f);
            yield return new WaitForSeconds(2f);
            while (CurrentStage != Stage.Finale && CurrentStage != Stage.Done)
            {
                if ((CurrentStage == Stage.Duct || (CurrentStage == Stage.Explore && stalker != null &&
                    stalker.Brain.CurrentMode != BasementStalkerBrain.Mode.Investigate)) && !NarrativeReadingPanel.WorldHalted)
                {
                    knockSource.PlayOneShot(clip, 0.9f);
                    yield return wait;
                    knockSource.PlayOneShot(clip, 0.9f);
                    yield return new WaitForSeconds(1f);
                    knockSource.PlayOneShot(clip, 0.9f);
                }
                yield return new WaitForSeconds(UnityEngine.Random.Range(5.5f, 8.5f));
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Игрок, затемнение, звук
        // -------------------------------------------------------------------------------------------------

        private void LockCamera(bool locked)
        {
            if (controller != null) controller.isCameraLocked = locked;
        }

        private void PlaceAt(Vector3 feet, Vector3 look)
        {
            var body = player.GetComponent<CharacterController>();
            if (body != null)
            {
                body.enabled = false;
                // Как держит PlayerController: при дебаг-прыжке он ещё не обновил капсулу, и она стояла бы под полом.
                body.center = new Vector3(0f, body.height * 0.5f, 0f);
            }
            player.position = new Vector3(feet.x, feet.y + 0.02f, feet.z);
            Vector3 flat = look - feet;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(flat);
            if (body != null) body.enabled = true;
            controller?.StandUp();
            lastPlayerPosition = player.position;
            Physics.SyncTransforms();
        }

        private void EnsureFade()
        {
            if (fade != null) return;
            var canvasObject = new GameObject("Basement Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var image = UiStyle.Fill(canvasObject.transform, "Black", Color.black);
            image.raycastTarget = false;
            fade = canvasObject.GetComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            fade.interactable = false;
        }

        private IEnumerator Fade(float target, float seconds)
        {
            EnsureFade();
            float start = fade.alpha;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                fade.alpha = Mathf.Lerp(start, target, t);
                yield return null;
            }
            fade.alpha = target;
        }

        private void PlayEffect(string path, float volume, float pitch)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null || effects == null) return;
            effects.pitch = pitch;
            effects.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            if (controller != null) controller.ForceCrouch = false;
            ExitHideSpot();
            if (stalker != null) stalker.Caught -= OnCaught;
        }

        // -------------------------------------------------------------------------------------------------
        // Геометрия
        // -------------------------------------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (level != null) return;
            level = FindFirstObjectByType<BasementLevel>(FindObjectsInactive.Include);
            if (level == null) { Debug.LogError("[P-03] BasementLevel не найден в сцене."); return; }
            cellDoor = level.cellDoor;
            tapeCellDoor = new TapeDoor(cellDoor, CellDoorSwing, BasementTape.DoorOpen); // закрытое положение — до взлома
            cellDoorCollider = level.cellDoorCollider;
            lockVisual = level.lockVisual;
            cellLock = level.cellLock;
            owner = level.owner;
            cellLock.Initialize(CanUseLock, UseLock, LockLabel);
            level.hideSpot.Initialize(CanEnterHideSpot, EnterHideSpot, () => "Спрятаться за коробкой");
            // Лампа без теней просвечивала бы сквозь перекрытие в дом: горит только во время главы.
            level.cellLamp.enabled = true;
            if (level.atmosphere != null) level.atmosphere.Activate();

            Transform root = level.transform;
            effects = AudioRouting.Create(root, "Basement effects", AudioBus.Horror, 0f, 30f);
            // Хозяин стучит по трубе у решётки: звук идёт оттуда, где труба поднимается от камеры.
            knockSource = AudioRouting.Create(root, "Walter knocks (pipe)", AudioBus.Horror, 1f, 45f);
            knockSource.transform.position = PipeInCell();
            knockSource.minDistance = 3f;
            knockSource.volume = 1f;
            hideListener = level.gameObject.AddComponent<BasementHideListener>();
            distractionSource = AudioRouting.Create(root, "Basement distant distraction", AudioBus.Horror, 1f, 30f);
            distractionSource.transform.position = BasementLayout.DistractionPoint;

            depths = BasementDepths.Get();
            OpenVent();
            // Камера лежит на полу, сбитая вместе со штативом, и сама служит «пультом» записи; если снаряжения нет (отладка) — точка.
            CameraPlacementController gear = CameraPlacementController.Instance;
            if (gear == null || !gear.KnockOverAsPlayer(DepthsLayout.Vent, BasementLayout.FloorY, CanWatchTape, WatchTape, () => "Посмотреть запись"))
            {
                depths.tapeCamera.gameObject.SetActive(true);
                depths.tapeCamera.Initialize(CanWatchTape, WatchTape, () => "Посмотреть запись");
            }
        }

        private void SpawnMonster()
        {
            if (monster != null) return;
            var prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) { Debug.LogError("[P-03] Нет префаба Scares/CornerPeekMonster."); return; }
            Vector2 start = BasementLayout.Patrol[0];
            monster = Instantiate(prefab, new Vector3(start.x, BasementLayout.FloorY, start.y), Quaternion.identity, level.transform);
            monster.SetActive(true);
            foreach (Collider c in monster.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Rigidbody body in monster.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            stalker = monster.AddComponent<BasementStalker>();
            stalker.Initialize(player, monster);
            stalker.Caught += OnCaught;
            monster.GetComponent<CornerStalkerRig>()?.Stand();
        }
    }
}
