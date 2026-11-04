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
        public enum Stage { Idle, Descending, Explore, Striking, Caught, Finale, Done, Room, Tunnels, Duct, Trap }

        // S-09: монстр идёт на стук → вошёл в камеру → решётка захлопнута → лом в проушинах.
        public enum TrapPhase { None, Lure, Window, Slammed, Wedged, Failed }

        private const string FindQuest = "basement-find", ReachQuest = "basement-reach", StrikeQuest = "basement-unlock",
            HideQuest = "basement-hide", BreakQuest = "basement-break",
            VentQuest = "basement-vent", DuctQuest = "basement-duct",
            TrapHideQuest = "basement-trap-hide", TrapSlamQuest = "basement-trap-slam", TrapWedgeQuest = "basement-trap-wedge";
        private const float CellDoorSwing = 100f;
        public static readonly string[] KnockLines =
        {
            Loc.Key("Утром я постучал в пол. Два и один."),
            Loc.Key("Снизу ответили тем же. Без пауз."),
            Loc.Key("Хейл держал его стуком. Теперь это делать мне. Каждую ночь."),
            Loc.Key("Если я сбегу, оно пойдёт следом. Как пришло сюда.")
        };
        public const float TrapWindowSeconds = 7f, TrapWedgeSeconds = 5f;
        private const float RushSpeed = 6f, RushReach = 1.2f, RushLimit = 3.5f;
        // Монстр реагирует на удар не сразу: у игрока есть время найти засаду (было 1.5 с — не хватало).
        private const float HideReactionDelay = 4.5f, TrapLureDelay = 3f;

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
        private AudioSource knockSource, effects, screamSource;
        private UiKeyHints exitHint;
        private GameObject exitHintCanvas;
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
        private BasementCellLock doorGrip;
        private BoxCollider doorGripCollider;
        private Vector3 doorGripHasp;
        private GameObject wedgedCrowbar;
        private Coroutine trapKnocks;
        private float trapLeft;

        public Stage CurrentStage { get; private set; }
        public TrapPhase CurrentTrapPhase { get; private set; }
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
        public void Begin(BasementHatch hatch, bool debugJump = false, bool debugWing = false, bool debugTrap = false)
        {
            if (CurrentStage != Stage.Idle || player == null) return;
            CurrentStage = Stage.Descending;
            StartCoroutine(BeginRoutine(hatch, debugJump, debugWing, debugTrap));
        }

        private IEnumerator BeginRoutine(BasementHatch hatch, bool debugJump, bool debugWing, bool debugTrap)
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
            if (debugTrap)
            {
                PlaceAt(BasementLayout.CellApproach, BasementLayout.CellLock);
                SpawnMonster();
                reachedCell = struck = true;
                lockVisual.SetActive(false);
                cellDoorCollider.SetActive(false);
                cellDoor.localRotation = tapeCellDoor.Closed * Quaternion.Euler(0f, CellDoorSwing, 0f);
                owner.RevealFace();
                yield return Fade(0f, 0.6f);
                BeginTrap();
                yield break;
            }
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
            sayQuick?.Invoke(new[] { Loc.T("Штатив опрокинут, камера валяется на полу."), Loc.T("Посмотрю, что она успела снять.") }, null);
            createQuest?.Invoke(Loc.T("Залезть в вентиляцию"), 1, null, VentQuest);
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
            sayQuick?.Invoke(new[] { Loc.T("Оно вылезло отсюда. Увидело объектив — и ударило."), Loc.T("Оно знало, что его снимают. И знает, кто поставил камеру.") }, null);
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
            else FindFirstObjectByType<ScrewdriverHunt>()?.NeedScrewdriver();
        }

        private string VentLabel() => depths.hatch.IsUnbolted ? Loc.T("Залезть в короб")
            : inventory != null && inventory.HasItem("Screwdriver") ? Loc.T("Открутить болты") : Loc.T("Нужна отвёртка");

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
            // Присед сразу, пока экран чёрный: иначе камера на высоте роста полсекунды видит подвал сквозь стены короба.
            controller?.SnapCrouch();
            // Монстр появляется тоже за чёрным экраном: стоит в зале до выхода из трубы, а не возникает на глазах.
            SpawnMonster();
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
                sayQuick?.Invoke(new[] { Loc.T("Ну и вонь. Гниль и сырость — будто здесь что-то сдохло."), Loc.T("Жесть проржавела и гнётся под коленями. Стук — где-то впереди.") }, null);
                createQuest?.Invoke(Loc.T("Ползти по коробу"), 1, null, DuctQuest);
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
            sayQuick?.Invoke(new[] { Loc.T("Вот она, вторая половина подвала. Та, что за свежей кладкой."), Loc.T("Оно бродит где-то здесь. Если пригнуться и идти медленно, оно не услышит.") }, null);
            createQuest?.Invoke(Loc.T("Найти того, кто стучал"), 1, () => createQuest(Loc.T("Добраться до стальной камеры"), 1, OnReached, ReachQuest), FindQuest);
            stalker.Begin();
        }

        private static Vector3 PipeInCell() =>
            new Vector3(BasementLayout.PipeRoute[0].x, BasementLayout.FloorY + 1.3f, BasementLayout.PipeRoute[0].y);

        // Цепочка заданий: дойти → ударить по замку → спрятаться → сбить замок.
        private void OnReached()
        {
            createQuest?.Invoke(Loc.T("Ударить по замку ломом"), 1, OnStruckQuestDone, StrikeQuest);
        }

        private void OnStruckQuestDone()
        {
            createQuest?.Invoke(Loc.T("Спрятаться от монстра"), 1, OnHideQuestDone, HideQuest);
        }

        private void OnHideQuestDone()
        {
            breakReady = true;
            createQuest?.Invoke(Loc.T("Сбить замок"), 1, null, BreakQuest);
        }

        private void Update()
        {
            if (player == null) return;
            if (CurrentStage == Stage.Trap) { TickTrap(); return; }
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
                sayQuick?.Invoke(new[] { Loc.T("Над головой труба. Уходит наверх, в дом."), Loc.T("Стучи по такой — и услышат во всех комнатах.") }, null);
            }
            if (QuestActive(FindQuest) && inHall) QuestManager.Instance.AddProgress(1);
            else if (QuestActive(ReachQuest) && BasementLayout.CellBlockD.Contains(at) &&
                Vector2.Distance(at, new Vector2(BasementLayout.CellApproach.x, BasementLayout.CellApproach.z)) < 3.2f)
            {
                reachedCell = true;
                QuestManager.Instance.AddProgress(1);
                sayQuick?.Invoke(new[] { Loc.T("Вот по этой трубе он и стучит. Поэтому стук слышен по всему дому."), Loc.T("На решётке замок. Хорошо, что лом при мне.") }, null);
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
                else SetHideHint(true);
            }
            Vector2 at = new Vector2(player.position.x, player.position.z);
            hideListener.Tick(Time.deltaTime, Vector2.Distance(stalker.Brain.Position, at));
            if (hideListener.Found && !hideFailed)
            {
                hideFailed = true;
                sayQuick?.Invoke(new[] { Loc.T("Услышало…") }, null);
                if (CurrentStage == Stage.Trap) { FailTrap(); return; }
                stalker.Alert(player.position);
            }
            if (CurrentStage == Stage.Trap) return;
            if (!hideFailed && !distracted && inHideSpot && stalker.Brain.AtSearchPoint(1))
            {
                inspectionTime += Time.deltaTime;
                if (inspectionTime >= 3f)
                {
                    distracted = true;
                    if (distractionSource != null)
                    {
                        // Монстр уходит на этот звук: игрок должен слышать его из укрытия, через стены.
                        foreach (string path in new[] { "Audio/Horror/impactPunch_heavy_000", "Audio/Horror/impactMetal_heavy_001", "Audio/Horror/impactWood_heavy_000" })
                        {
                            AudioClip clip = Resources.Load<AudioClip>(path);
                            if (clip != null) distractionSource.PlayOneShot(clip, 1.5f);
                        }
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
                sayQuick?.Invoke(new[] { Loc.T("Ушло. Что-то упало вдалеке. Пока оно не вернулось — к замку.") }, null);
                if (QuestActive(HideQuest)) QuestManager.Instance.AddProgress(1);
                if (knocking == null) knocking = StartCoroutine(KnockRoutine());
            }
        }

        private bool CanEnterHideSpot() => hideActive && !inHideSpot && !hideFailed &&
            (CurrentStage == Stage.Explore || CurrentTrapPhase == TrapPhase.Lure);

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
            sayQuick?.Invoke(new[] { Loc.T("Оно рядом. Только не…") }, null);
            if (QuestActive(TrapHideQuest)) QuestManager.Instance.AddProgress(1);
        }

        // Подсказка о выходе из засады: без неё не очевидно, что ждать можно не вечно и что выйти можно по E.
        private void SetHideHint(bool visible)
        {
            if (exitHint == null)
            {
                if (!visible) return;
                Canvas canvas = UiStyle.OverlayCanvas(transform, "Basement Hide Hint", 140, new Vector2(1280f, 720f), false);
                exitHintCanvas = canvas.gameObject;
                exitHint = UiKeyHints.Create(canvas.transform, "Hide exit", UiStyle.Font(), 1.2f).Backed();
                exitHint.RectTransform.anchorMin = exitHint.RectTransform.anchorMax = exitHint.RectTransform.pivot = new Vector2(0.5f, 0f);
                // Выше индикатора микрофона, он занимает низ по центру.
                exitHint.RectTransform.anchoredPosition = new Vector2(0f, 120f);
            }
            if (exitHintCanvas.activeSelf != visible) exitHintCanvas.SetActive(visible);
            if (visible) exitHint.Set("E", CurrentTrapPhase == TrapPhase.Window ? Loc.T("ВЫЙТИ И ЗАХЛОПНУТЬ РЕШЁТКУ") : Loc.T("ВЫЙТИ ИЗ УКРЫТИЯ"));
        }

        private void ExitHideSpot()
        {
            SetHideHint(false);
            if (!inHideSpot) return;
            inHideSpot = false;
            if (hideActive && !hideFailed && stalker != null && CurrentStage != Stage.Trap)
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

        private void BeginHide(float alertDelay = 0f)
        {
            hideFailed = false;
            distracted = false;
            inspectionTime = 0f;
            hideActive = true;
            hideListener.Begin();
            stalker.PlayerHidden = PlayerHidden;
            StartCoroutine(AlertAfter(alertDelay));
        }

        // Пауза после удара: монстр реагирует не сразу, игрок успевает оглядеться и найти коробку.
        private IEnumerator AlertAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            if (hideActive && !hideFailed && CurrentStage == Stage.Explore) stalker.AlertAndSearch(player.position);
        }

        // -------------------------------------------------------------------------------------------------
        // Поимка: бросок монстра, VHS-кадр смерти (как в других концовках), возврат
        // -------------------------------------------------------------------------------------------------

        private void OnCaught()
        {
            if (CurrentStage == Stage.Trap) { FailTrap(); return; }
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
                sayQuick?.Invoke(new[] { Loc.T("Оно идёт на звук шагов. Пригнуться — Ctrl. Не бежать, красться.") }, null);
            yield return Fade(0f, 1f);
            LockCamera(false);
            CurrentStage = Stage.Explore;
            stalker.Begin();
            if (wasHiding)
            {
                sayQuick?.Invoke(new[] { Loc.T("Оно вернётся к замку. За ящики у стены — и ни звука.") }, null);
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
            PlayScream(1f, 0.95f);
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

        private string LockLabel() => !struck ? Loc.T("Ударить по замку") : Loc.T("Сбить замок");

        private void UseLock()
        {
            if (inventory == null || !inventory.hasCrowbar)
            {
                sayQuick?.Invoke(new[] { Loc.T("Голыми руками этот замок не сбить. Нужен лом.") }, null);
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
            sayQuick?.Invoke(new[] { Loc.T("Грохот на весь подвал!"), Loc.T("Оно идёт сюда. За ящики у стены, быстро.") }, null);
            BeginHide(HideReactionDelay);
        }

        private IEnumerator FinaleRoutine()
        {
            CurrentStage = Stage.Finale;
            LockCamera(true);
            stalker.Stop();
            if (knocking != null) { StopCoroutine(knocking); knocking = null; }
            yield return TurnToward(BasementLayout.CellLock, 0.6f);
            // Разговор и ловушка идут без фона: капель и гула с потолка достаточно было до финала.
            if (level != null && level.atmosphere != null) level.atmosphere.Silence(1.2f);
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
            yield return FaceHale();

            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Кто ты? Четыре месяца я стучу по трубе и не знаю, кто меня слышит."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Погоди… Дэниел Росс?"));
            yield return SayLine(Loc.Key("Я"), Loc.T("Откуда вы меня знаете?"));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Ты снимал у меня дом. В договоре была копия твоих прав. С фотографией."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Он никогда не шёл за мной. Он шёл за тобой."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Зачем ты сбил замок?"));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Сын запер меня здесь по моей же просьбе. Этот замок меня не держал. Он меня защищал."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Я стучал по трубе, чтобы он оставался внизу. А теперь он услышал грохот. Он идёт сюда."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Ходить я не могу. Останусь и буду стучать. Он пойдёт на стук."));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Спрячься за коробкой. Когда он войдёт ко мне, захлопни решётку и вставь лом в проушины."));
            yield return SayLine(Loc.Key("Я"), Loc.T("А вы?"));
            yield return SayLine(Loc.Key("ХЕЙЛ"), Loc.T("Иди."));
            BeginTrap();
        }

        // -------------------------------------------------------------------------------------------------
        // Ловушка (S-09): Хейл стучит, монстр идёт в камеру, игрок запирает его ломом
        // -------------------------------------------------------------------------------------------------

        private void BeginTrap()
        {
            CurrentStage = Stage.Trap;
            CurrentTrapPhase = TrapPhase.Lure;
            doorGrip.gameObject.SetActive(true);
            LockCamera(false);
            createQuest?.Invoke(Loc.T("Спрятаться за коробкой"), 1, null, TrapHideQuest);
            hideFailed = false;
            hideActive = true;
            hideListener.Begin();
            stalker.PlayerHidden = PlayerHidden;
            stalker.Hold(BasementLayout.TrapStart);
            trapKnocks = StartCoroutine(TrapKnockRoutine());
            StartCoroutine(TrapLureRoutine());
        }

        // Хейл уже стучит, монстр выходит не сразу: игрок успевает дойти до коробки.
        private IEnumerator TrapLureRoutine()
        {
            float waited = 0f;
            while (waited < TrapLureDelay)
            {
                if (!NarrativeReadingPanel.WorldHalted && !PauseMenuController.InputBlocked) waited += Time.deltaTime;
                yield return null;
            }
            if (CurrentTrapPhase == TrapPhase.Lure) stalker.Lure(BasementLayout.TrapRoute);
        }

        private IEnumerator TrapKnockRoutine()
        {
            var clip = Resources.Load<AudioClip>("Audio/Horror/impactMetal_heavy_001");
            var wait = new WaitForSeconds(0.36f);
            var rest = new WaitForSeconds(1f);
            var pause = new WaitForSeconds(2.2f);
            while (CurrentTrapPhase == TrapPhase.Lure)
            {
                hideListener.IgnoreSoundFor(2.2f);
                knockSource.PlayOneShot(clip, 0.9f);
                yield return wait;
                knockSource.PlayOneShot(clip, 0.9f);
                yield return rest;
                knockSource.PlayOneShot(clip, 0.9f);
                yield return pause;
            }
        }

        private void TickTrap()
        {
            if (NarrativeReadingPanel.WorldHalted || PauseMenuController.InputBlocked) return;
            if (CurrentTrapPhase == TrapPhase.Lure)
            {
                if (stalker.Brain.LureFinished) OpenTrapWindow();
            }
            else if (CurrentTrapPhase == TrapPhase.Window || CurrentTrapPhase == TrapPhase.Slammed)
            {
                trapLeft -= Time.deltaTime;
                if (trapLeft <= 0f) FailTrap();
            }
        }

        // Монстр у Хейла и занят им: теперь его не слышно и он не слышит, но времени мало.
        private void OpenTrapWindow()
        {
            CurrentTrapPhase = TrapPhase.Window;
            trapLeft = TrapWindowSeconds;
            if (trapKnocks != null) { StopCoroutine(trapKnocks); trapKnocks = null; }
            hideListener.End();
            stalker.Stop();
            FaceMonster(BasementLayout.OwnerPosition);
            stalker.Rig?.Lunge(0.25f, 0.6f);
            PlayScream(0.7f, 0.6f);
            if (QuestActive(TrapHideQuest)) QuestManager.Instance.AddProgress(1);
            sayQuick?.Invoke(new[] { Loc.T("Стук оборвался. Оно в камере. Решётку, сейчас!") }, null);
            createQuest?.Invoke(Loc.T("Захлопнуть решётку"), 1, null, TrapSlamQuest);
        }

        private void FaceMonster(Vector3 point)
        {
            Vector3 flat = point - monster.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) monster.transform.rotation = Quaternion.LookRotation(flat);
        }

        private bool OutsideCell() => player.position.z > BasementLayout.CellArea.yMax + 0.1f;

        private bool CanUseDoorGrip() => CurrentStage == Stage.Trap &&
            ((CurrentTrapPhase == TrapPhase.Window && OutsideCell()) || CurrentTrapPhase == TrapPhase.Slammed);

        private string DoorGripLabel() => CurrentTrapPhase == TrapPhase.Window ? Loc.T("Захлопнуть решётку") : Loc.T("Вставить лом в проушины");

        private void UseDoorGrip()
        {
            if (CurrentTrapPhase == TrapPhase.Window) SlamDoor();
            else if (CurrentTrapPhase == TrapPhase.Slammed) StartCoroutine(WedgeRoutine());
        }

        private void SlamDoor()
        {
            CurrentTrapPhase = TrapPhase.Slammed;
            trapLeft = TrapWedgeSeconds;
            hideActive = false;
            doorGripCollider.center = doorGripHasp;
            doorGripCollider.size = new Vector3(0.9f, 0.9f, 0.5f);
            if (QuestActive(TrapSlamQuest)) QuestManager.Instance.AddProgress(1);
            createQuest?.Invoke(Loc.T("Вставить лом в проушины"), 1, null, TrapWedgeQuest);
            StartCoroutine(SlamRoutine());
            StartCoroutine(RattleRoutine());
        }

        private IEnumerator SlamRoutine()
        {
            Quaternion from = cellDoor.localRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                cellDoor.localRotation = Quaternion.Slerp(from, tapeCellDoor.Closed, t * t);
                yield return null;
            }
            cellDoor.localRotation = tapeCellDoor.Closed;
            if (cellDoorCollider != null) cellDoorCollider.SetActive(true);
            PlayEffect("Audio/Horror/impactMetal_heavy_001", 1f, 0.8f);
            PlayEffect("Audio/Horror/impactPunch_heavy_000", 1f, 1.1f);
            sayQuick?.Invoke(new[] { Loc.T("Оно оборачивается. Лом, быстрее!") }, null);
            stalker.Rig?.Stand();
            FaceMonster(new Vector3(monster.transform.position.x, 0f, BasementLayout.CellArea.yMax));
            Vector3 start = monster.transform.position;
            var bars = new Vector3(start.x, start.y, BasementLayout.CellArea.yMax - 0.45f);
            for (float t = 0f; t < 1f && CurrentTrapPhase != TrapPhase.Failed; t += Time.deltaTime / 0.5f)
            {
                monster.transform.position = Vector3.Lerp(start, bars, t);
                yield return null;
            }
        }

        // Монстр трясёт решётку, пока игрок не уйдёт с этой сцены.
        private IEnumerator RattleRoutine()
        {
            yield return new WaitForSeconds(0.7f);
            // Лом встал в проушины — решётка держит, и грохот прекращается: дальше только тишина и реплики.
            while (CurrentTrapPhase == TrapPhase.Slammed)
            {
                PlayEffect("Audio/Horror/impactMetal_heavy_001", 0.75f, UnityEngine.Random.Range(0.75f, 1.05f));
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.45f, 0.85f));
            }
        }

        private IEnumerator WedgeRoutine()
        {
            CurrentTrapPhase = TrapPhase.Wedged;
            doorGrip.gameObject.SetActive(false);
            controller?.StopMotion();
            LockCamera(true);
            MountCrowbar();
            inventory?.RemoveItem("Crowbar");
            if (QuestActive(TrapWedgeQuest)) QuestManager.Instance.AddProgress(1);
            PlayEffect("Audio/Horror/impactPlank_medium_000", 1f, 1.5f);
            yield return TurnToward(BasementLayout.CellLock, 0.35f);
            sayQuick?.Invoke(new[] { Loc.T("Держит.") }, null);
            yield return new WaitForSeconds(2f);
            sayQuick?.Invoke(new[] { Loc.T("Хейл больше не стучит.") }, null);
            yield return new WaitForSeconds(2.5f);
            CurrentTrapPhase = TrapPhase.None;
            CurrentStage = Stage.Finale;
            yield return Fade(1f, 1.2f);
            yield return MorningKnocks();
            yield return LastKnockLines();
            CurrentStage = Stage.Done;
            endGame?.Invoke(EndingId.Answer);
        }

        // Лом из рук остаётся стоять в проушинах: длинная ось модели вертикально, через все три.
        private void MountCrowbar()
        {
            GameObject held = inventory != null ? inventory.crowbarInHand : null;
            if (held == null) return;
            wedgedCrowbar = Instantiate(held);
            wedgedCrowbar.name = "Crowbar in eyelets";
            foreach (Animator animator in wedgedCrowbar.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Collider part in wedgedCrowbar.GetComponentsInChildren<Collider>(true)) Destroy(part);
            wedgedCrowbar.transform.localScale = held.transform.lossyScale;
            wedgedCrowbar.transform.rotation = Quaternion.identity;
            wedgedCrowbar.SetActive(true);
            var model = wedgedCrowbar.GetComponentInChildren<Renderer>(true);
            if (model != null)
            {
                Vector3 size = model.localBounds.size;
                Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
                wedgedCrowbar.transform.rotation = Quaternion.FromToRotation(model.transform.TransformDirection(axis), Vector3.up);
                wedgedCrowbar.transform.position += HaspCenter() - model.bounds.center;
            }
            wedgedCrowbar.transform.SetParent(level.transform, true);
        }

        private static Vector3 HaspCenter() => BasementLayout.CellLock + new Vector3(-0.05f, 0f, 0.07f);

        private void FailTrap()
        {
            if (CurrentTrapPhase == TrapPhase.Failed || CurrentTrapPhase == TrapPhase.Wedged) return;
            bool burst = CurrentTrapPhase == TrapPhase.Slammed;
            CurrentTrapPhase = TrapPhase.Failed;
            if (trapKnocks != null) { StopCoroutine(trapKnocks); trapKnocks = null; }
            StartCoroutine(TrapDeathRoutine(burst));
        }

        // E3: монстр вырвался или услышал. Конец всегда один, бежать уже поздно.
        private IEnumerator TrapDeathRoutine(bool burst)
        {
            hideActive = false;
            hideListener.End();
            doorGrip.gameObject.SetActive(false);
            stalker.Stop();
            if (burst)
            {
                PlayEffect("Audio/Horror/impactPunch_heavy_000", 1f, 0.9f);
                if (cellDoorCollider != null) cellDoorCollider.SetActive(false);
                yield return SwingDoor();
            }
            yield return RushAtPlayer();
            ExitHideSpot();
            LockCamera(true);
            controller?.StopMotion();
            yield return Jumpscare();
            yield return DeathTapeEffect.Play();
            NightTwoExits.PrepareEndingLines(player.GetComponentInChildren<Camera>(), fade);
            yield return NightTwoExits.ShowLines(new[] { NightTwoExits.CaughtThought });
            yield return Fade(1f, 0.01f);
            CurrentStage = Stage.Done;
            endGame?.Invoke(EndingId.Hidden);
        }

        private IEnumerator RushAtPlayer()
        {
            stalker.Rig?.Run(1.4f);
            for (float t = 0f; t < RushLimit; t += Time.deltaTime)
            {
                Vector3 position = monster.transform.position;
                var at = new Vector2(position.x, position.z);
                var goal = new Vector2(player.position.x, player.position.z);
                if (Vector2.Distance(at, goal) <= RushReach) yield break;
                Vector2 next = at.y < BasementLayout.CellArea.yMax && goal.y > BasementLayout.CellArea.yMax
                    ? BasementLayout.TrapMouth : BasementStalkerBrain.NextPoint(at, goal);
                Vector2 step = Vector2.MoveTowards(at, next, RushSpeed * Time.deltaTime);
                monster.transform.position = new Vector3(step.x, BasementLayout.FloorY, step.y);
                FaceMonster(new Vector3(next.x, 0f, next.y));
                yield return null;
            }
        }

        // Герой подходит к проёму и смотрит на лицо Хейла, камера за стеной его бы не показала.
        private IEnumerator FaceHale()
        {
            Camera eye = player.GetComponentInChildren<Camera>();
            if (eye == null || owner == null) yield break;
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            Vector3 fromPosition = player.position;
            Vector3 toPosition = new Vector3(BasementLayout.HaleTalkSpot.x, fromPosition.y, BasementLayout.HaleTalkSpot.z);
            Quaternion fromBody = player.rotation, fromEye = eye.transform.localRotation;
            Vector3 face = owner.FacePoint;
            float eyeHeight = eye.transform.position.y - fromPosition.y;
            Vector3 aim = face - new Vector3(toPosition.x, toPosition.y + eyeHeight, toPosition.z);
            Vector3 flat = new Vector3(aim.x, 0f, aim.z);
            Quaternion toBody = Quaternion.LookRotation(flat);
            float pitch = -Mathf.Atan2(aim.y, flat.magnitude) * Mathf.Rad2Deg;
            Quaternion toEye = Quaternion.Euler(pitch, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                player.position = Vector3.Lerp(fromPosition, toPosition, smooth);
                player.rotation = Quaternion.Slerp(fromBody, toBody, smooth);
                eye.transform.localRotation = Quaternion.Slerp(fromEye, toEye, smooth);
                yield return null;
            }
            player.position = toPosition;
            player.rotation = toBody;
            eye.transform.localRotation = toEye;
            if (body != null) body.enabled = true;
            Physics.SyncTransforms();
            controller?.SetLookPitch(pitch);
        }

        private IEnumerator SayLine(string speaker, string line)
        {
            if (ThoughtManager.Instance == null) yield break;
            bool finished = false;
            ThoughtManager.Instance.ShowDialogue(speaker, new[] { line }, () => finished = true);
            while (!finished) yield return null;
        }

        // S-10: чёрный экран, утро. Сверху стучат «2·1», снизу отвечают тем же стуком, но без пауз.
        private IEnumerator MorningKnocks()
        {
            yield return new WaitForSeconds(1.5f);
            const string clip = "Audio/Horror/AnswerKnock";
            foreach (float pause in new[] { 0.36f, 1.0f })
            {
                PlayEffect(clip, 0.9f, 1f);
                yield return new WaitForSeconds(pause);
            }
            PlayEffect(clip, 0.9f, 1f);
            yield return new WaitForSeconds(1.8f);
            for (int i = 0; i < 6; i++)
            {
                PlayEffect(clip, 1f, 0.7f);
                yield return new WaitForSeconds(0.36f);
            }
            yield return new WaitForSeconds(1.5f);
        }

        // S-10: герой понимает, что стучать теперь ему; это же объясняет E4: кто убежит, тот приведёт его с собой.
        private IEnumerator LastKnockLines()
        {
            NightTwoExits.PrepareEndingLines(player != null ? player.GetComponentInChildren<Camera>() : null, fade);
            yield return NightTwoExits.ShowLines(KnockLines);
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

        // Крик монстра идёт на своём источнике: у effects высота тона общая для всех звуков, и грохот решётки
        // (каждые 0.5 с, другой pitch) менял тон крика на ходу — он плыл, рвался и «прыгал» по громкости.
        private void PlayScream(float volume, float pitch)
        {
            var clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (clip == null || screamSource == null) return;
            screamSource.Stop();
            screamSource.pitch = pitch;
            screamSource.PlayOneShot(clip, volume);
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
            tapeCellDoor = new TapeDoor(cellDoor, CellDoorSwing, time => 0f); // закрытое положение — до взлома
            cellDoorCollider = level.cellDoorCollider;
            lockVisual = level.lockVisual;
            cellLock = level.cellLock;
            owner = level.owner;
            BuildPadlock();
            BuildDoorGrip();
            BuildHasp();
            cellLock.Initialize(CanUseLock, UseLock, LockLabel);
            level.hideSpot.Initialize(CanEnterHideSpot, EnterHideSpot, () => Loc.T("Спрятаться за коробкой"));
            // Метка над укрытием: после удара по замку не очевидно, куда бежать. Видна, пока в укрытие можно залезть.
            QuestItemMarker hideMarker = QuestItemMarker.Ensure(level.hideSpot.gameObject);
            // Над самим ящиком, а не за ним: точка за ящиком закрыта им же, и метка не показывалась бы.
            hideMarker.ConfigureAnchor(new Bounds(level.hideSpot.transform.position + Vector3.up * 0.6f, Vector3.one * 0.2f));
            hideMarker.ConfigureCondition(CanEnterHideSpot);
            hideMarker.VisibleDistance = 14f;
            // Света в подвале нет: ночью 2 дом обесточен, идти приходится с фонарём. Ни лампа камеры, ни плафон не горят.
            if (level.cellLamp != null) level.cellLamp.enabled = false;
            Transform reference = level.transform.parent;
            Transform bulb = reference != null ? reference.Find("WarmBareBulb") : null;
            Light bulbLight = bulb != null ? bulb.GetComponent<Light>() : null;
            if (bulbLight != null) bulbLight.enabled = false;
            if (level.atmosphere != null) level.atmosphere.Activate();

            Transform root = level.transform;
            effects = AudioRouting.Create(root, "Basement effects", AudioBus.Horror, 0f, 30f);
            screamSource = AudioRouting.Create(root, "Basement scream", AudioBus.Horror, 0f, 30f);
            // Хозяин стучит по трубе у решётки: звук идёт оттуда, где труба поднимается от камеры.
            knockSource = AudioRouting.Create(root, "Walter knocks (pipe)", AudioBus.Horror, 1f, 45f);
            knockSource.transform.position = PipeInCell();
            knockSource.minDistance = 3f;
            knockSource.volume = 1f;
            hideListener = level.gameObject.AddComponent<BasementHideListener>();
            distractionSource = AudioRouting.Create(root, "Basement distant distraction", AudioBus.Horror, 0.6f, 45f);
            distractionSource.transform.position = BasementLayout.DistractionPoint;
            distractionSource.minDistance = 12f;

            depths = BasementDepths.Get();
            OpenVent();
            // Камера лежит на полу, сбитая вместе со штативом, и сама служит «пультом» записи; если снаряжения нет (отладка) — точка.
            CameraPlacementController gear = CameraPlacementController.Instance;
            if (gear == null || !gear.KnockOverAsPlayer(DepthsLayout.Vent, BasementLayout.FloorY, CanWatchTape, WatchTape, () => Loc.T("Посмотреть запись")))
            {
                depths.tapeCamera.gameObject.SetActive(true);
                depths.tapeCamera.Initialize(CanWatchTape, WatchTape, () => Loc.T("Посмотреть запись"));
            }
        }

        // Куб-заглушка из сцены остаётся опорой: на нём точка взаимодействия и анимация удара, а виден амбарный замок.
        private void BuildPadlock()
        {
            Transform anchor = lockVisual.transform;
            Transform trigger = cellLock.transform;
            Vector3 triggerAt = trigger.position - anchor.position + BasementLayout.CellLock;
            var box = cellLock.GetComponent<BoxCollider>();
            anchor.position = BasementLayout.CellLock;
            anchor.localScale = Vector3.one;
            trigger.position = triggerAt;
            trigger.localScale = Vector3.one;
            box.size = new Vector3(0.3f, 0.35f, 0.15f);

            var cube = lockVisual.GetComponent<Renderer>();
            Material metal = cube.sharedMaterial;
            cube.enabled = false;
            // Старое железо светлее решётки: замок должен читаться в луче фонаря.
            var iron = new Material(metal) { name = "Padlock iron" };
            iron.SetColor("_BaseColor", new Color(0.72f, 0.55f, 0.4f));
            var dark = new Material(metal) { name = "Padlock keyhole" };
            dark.SetColor("_BaseColor", Color.black);
            var model = new GameObject("Barn padlock", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(anchor, false);
            model.transform.position = HaspCenter() + new Vector3(0f, 0.03f, 0.025f);
            model.GetComponent<MeshFilter>().sharedMesh = BarnPadlock.BuildMesh();
            model.GetComponent<MeshRenderer>().sharedMaterials = new[] { iron, dark };
            model.transform.localScale = Vector3.one * 1.5f;
        }

        // Луч взаимодействия не видит триггеры, поэтому коллайдер твёрдый. Включается только на ловушке.
        private void BuildDoorGrip()
        {
            float width = BasementLayout.CellArea.width;
            var grip = new GameObject("Cell door grip", typeof(BoxCollider), typeof(BasementCellLock));
            grip.layer = level.cellLock.gameObject.layer;
            grip.transform.SetParent(cellDoor, false);
            doorGripCollider = grip.GetComponent<BoxCollider>();
            doorGripCollider.center = new Vector3(-width * 0.5f, 1.15f, 0f);
            doorGripCollider.size = new Vector3(width, 2.3f, 0.12f);
            doorGripHasp = cellDoor.InverseTransformPoint(BasementLayout.CellLock); // решётка ещё закрыта
            doorGrip = grip.GetComponent<BasementCellLock>();
            doorGrip.Initialize(CanUseDoorGrip, UseDoorGrip, DoorGripLabel);
            grip.SetActive(false);
        }

        // Проушины: две на решётке и одна на стене, замок висит на них, лом встаёт сквозь все три.
        private void BuildHasp()
        {
            Material metal = lockVisual.GetComponent<Renderer>().sharedMaterial;
            Vector3 center = HaspCenter();
            HaspPlate(metal, cellDoor, center + Vector3.up * 0.16f);
            HaspPlate(metal, cellDoor, center + Vector3.down * 0.16f);
            HaspPlate(metal, level.transform, center);
        }

        private static void HaspPlate(Material metal, Transform parent, Vector3 position)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Cell hasp";
            Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(parent, false);
            plate.transform.position = position;
            plate.transform.localScale = new Vector3(0.16f, 0.03f, 0.1f);
            plate.GetComponent<Renderer>().sharedMaterial = metal;
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
