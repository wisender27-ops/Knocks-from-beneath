using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // P-03 «Подвал как глава» + B-05 финал E5 «Ответ». Спуск через люк: за восточной стеной существующей комнаты
    // подвала строится продолжение (BasementLayout), по залу ходит монстр и слушает шаги, из камеры в дальнем блоке
    // стучит хозяин — по трубе, которая идёт наверх, в дом. Игрок идёт на стук, не выдавая себя, бьёт ломом по замку,
    // прячется в нише от монстра, который пришёл на грохот (микрофон, как под кроватью), и сбивает замок — выбор
    // «не заколачивать, а ответить». Геометрия создаётся в рантайме: сцена не меняется.
    public sealed class BasementChapter : MonoBehaviour
    {
        public enum Stage { Idle, Descending, Explore, Striking, Caught, Finale, Done }

        private const string FindQuest = "basement-find", ReachQuest = "basement-reach", StrikeQuest = "basement-unlock",
            HideQuest = "basement-hide", BreakQuest = "basement-break";
        private const int InteractableLayer = 6;
        private static readonly Vector3 HideRespawn = new Vector3(-28.15f, BasementLayout.FloorY, 101.3f);
        private static readonly Vector3 HideRespawnLook = new Vector3(-28.15f, 0f, 97f);

        private Transform player;
        private PlayerInventory inventory;
        private PlayerController controller;
        private Action<string, int, UnityAction, string> createQuest;
        private Action<string[], Action> say, sayQuick;
        private Action<EndingId> endGame;
        private Action silenceAbove;

        private GameObject extension;
        private GameObject monster;
        private BasementStalker stalker;
        private BasementHideListener hideListener;
        private BasementCellLock cellLock;
        private Transform cellDoor;
        private GameObject cellDoorCollider, lockVisual;
        private AudioSource knockSource, effects;
        private Coroutine knocking;
        private CanvasGroup fade;
        private Material wallMaterial, floorMaterial, ceilingMaterial, metalMaterial, woodMaterial, rugMaterial, pipeMaterial,
            skinMaterial, shadowMaterial;
        private Transform reference;
        private int catches;
        private bool reachedCell, hallSeen, struck, hideActive, hideFailed, breakReady;

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

        // Спуск: затемнение, подвал достраивается, игрок у подножия лестницы.
        public void Begin(BasementHatch hatch, bool debugJump = false)
        {
            if (CurrentStage != Stage.Idle || player == null) return;
            CurrentStage = Stage.Descending;
            StartCoroutine(BeginRoutine(hatch, debugJump));
        }

        private IEnumerator BeginRoutine(BasementHatch hatch, bool debugJump)
        {
            LockCamera(true);
            EnsureFade();
            yield return Fade(1f, debugJump ? 0.05f : 0.9f);
            silenceAbove?.Invoke();
            hatch?.OpenForCinematic();
            EnsureBuilt();
            SpawnMonster();
            PlaceAt(BasementLayout.PlayerStart, BasementLayout.PlayerStartLook);
            if (debugJump && inventory != null) inventory.hasCrowbar = true;
            yield return new WaitForSeconds(debugJump ? 0.05f : 0.7f);
            yield return Fade(0f, 1.4f);
            LockCamera(false);
            CurrentStage = Stage.Explore;
            catches = 0;
            reachedCell = hallSeen = struck = hideActive = hideFailed = breakReady = false;
            sayQuick?.Invoke(new[] { "Темно. И тихо. Только где-то глубже стучат.", "Он ходит здесь. Присесть и идти медленно — тогда он не услышит." }, null);
            createQuest?.Invoke("Найти того, кто стучал", 1, () => createQuest("Добраться до камеры", 1, OnReached, ReachQuest), FindQuest);
            stalker.Begin();
            knocking = StartCoroutine(KnockRoutine());
        }

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
            if (player == null || (CurrentStage != Stage.Explore && CurrentStage != Stage.Striking)) return;
            Vector3 p = player.position;
            if (CurrentStage != Stage.Explore) return;
            if (!hallSeen && p.x > -31.4f && p.y < -2f)
            {
                hallSeen = true;
                sayQuick?.Invoke(new[] { "Над головой труба. Тянется в сторону дома.", "Если по ней стучать, звук уйдёт во все комнаты." }, null);
            }
            if (QuestActive(FindQuest) && p.x > -31.4f && p.y < -2f) QuestManager.Instance.AddProgress(1);
            else if (QuestActive(ReachQuest) &&
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(BasementLayout.CellApproach.x, BasementLayout.CellApproach.z)) < 3.2f)
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
            hideActive && controller != null && controller.IsCrouching && BasementLayout.IsInNook(new Vector2(player.position.x, player.position.z));

        private void TickHide()
        {
            if (!hideActive || stalker == null || player == null) return;
            Vector2 at = new Vector2(player.position.x, player.position.z);
            float distance = Vector2.Distance(stalker.Brain.Position, at);
            bool moving = controller != null && !controller.IsCrouching && PlayerMoved();
            hideListener.Tick(Time.deltaTime, distance, moving);
            if (hideListener.Found && !hideFailed)
            {
                // Голос или шумное дыхание услышали: монстр идёт прямо на игрока.
                hideFailed = true;
                stalker.Alert(player.position);
            }
            if (CurrentStage == Stage.Explore && stalker.Brain.SearchCompleted)
            {
                hideActive = false;
                hideListener.End();
                sayQuick?.Invoke(new[] { "Ушёл. Теперь замок — пока не вернулся." }, null);
                if (QuestActive(HideQuest)) QuestManager.Instance.AddProgress(1);
                if (knocking == null) knocking = StartCoroutine(KnockRoutine());
            }
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
                PlaceAt(HideRespawn, HideRespawnLook);
                stalker.ResetPatrol(2);
            }
            else
            {
                PlaceAt(BasementLayout.PlayerStart, BasementLayout.PlayerStartLook);
                stalker.ResetPatrol(catches % 2 == 0 ? 0 : 2);
            }
            yield return new WaitForSeconds(0.9f);
            if (!wasHiding && catches == 2)
                sayQuick?.Invoke(new[] { "Он слышит шаги. Ctrl — присесть. Красться, а не бежать." }, null);
            yield return Fade(0f, 1f);
            LockCamera(false);
            CurrentStage = Stage.Explore;
            stalker.Begin();
            if (wasHiding)
            {
                sayQuick?.Invoke(new[] { "Он вернётся к замку. В нишу, присесть и молчать." }, null);
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
            sayQuick?.Invoke(new[] { "Грохнуло на весь подвал!", "Он идёт. Две ниши у стены. Присесть, не шуметь, молчать." }, null);
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
            if (QuestActive(BreakQuest)) QuestManager.Instance.AddProgress(1);

            bool spoken = false;
            say?.Invoke(new[]
            {
                "— …Ты ответил.",
                "— Я слышал. Каждую ночь. По трубе.",
                "— Я уже не верил. Двадцать лет только стук и тот, кто ходит наверху.",
                "— Он идёт. Слышишь? На звук.",
                "— Не бегай. Стучи, как стучал. Он идёт на ответ."
            }, () => spoken = true);
            while (!spoken) yield return null;

            for (int i = 0; i < 7; i++)
            {
                PlayEffect("Audio/Horror/FollowingStep1", 1f, 0.7f - i * 0.02f);
                yield return new WaitForSeconds(0.8f - i * 0.08f);
            }
            PlayEffect("Audio/CornerJumpscare", 1f, 0.6f);
            yield return Fade(1f, 1.2f);
            for (int i = 0; i < 3; i++)
            {
                PlayEffect("Audio/Horror/impactWood_heavy_000", 0.8f, i == 2 ? 1.1f : 1.25f);
                yield return new WaitForSeconds(i == 1 ? 1.4f : 0.45f);
            }
            yield return new WaitForSeconds(2.2f);
            CurrentStage = Stage.Done;
            endGame?.Invoke(EndingId.Answer);
        }

        private IEnumerator SwingDoor()
        {
            if (cellDoor == null) yield break;
            Quaternion from = cellDoor.localRotation, to = from * Quaternion.Euler(0f, 100f, 0f);
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
            var clip = Resources.Load<AudioClip>("Audio/Horror/impactWood_heavy_000");
            var wait = new WaitForSeconds(0.36f);
            yield return new WaitForSeconds(2f);
            while (CurrentStage != Stage.Finale && CurrentStage != Stage.Done)
            {
                if (CurrentStage == Stage.Explore && stalker != null &&
                    stalker.Brain.CurrentMode != BasementStalkerBrain.Mode.Investigate && Time.timeScale > 0f)
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
            if (body != null) body.enabled = false;
            float pivotToFeet = body != null ? body.center.y - body.height * 0.5f : 0f;
            player.position = new Vector3(feet.x, feet.y - pivotToFeet + 0.02f, feet.z);
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
            if (stalker != null) stalker.Caught -= OnCaught;
        }

        // -------------------------------------------------------------------------------------------------
        // Геометрия
        // -------------------------------------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (extension != null) return;
            extension = new GameObject("Basement Extension (P-03)");
            extension.transform.SetParent(transform, false);
            CollectMaterials();

            effects = AudioRouting.Create(extension.transform, "Basement effects", AudioBus.Horror, 0f, 30f);
            // Хозяин стучит по трубе у решётки: звук идёт оттуда, где труба поднимается от камеры.
            knockSource = AudioRouting.Create(extension.transform, "Walter knocks (pipe)", AudioBus.Horror, 1f, 45f);
            knockSource.transform.position = new Vector3(BasementLayout.PipeRoute[0].x, BasementLayout.FloorY + 1.3f, BasementLayout.PipeRoute[0].y);
            knockSource.minDistance = 3f;
            knockSource.volume = 1f;
            hideListener = extension.AddComponent<BasementHideListener>();

            OpenExistingWall();
            foreach (BasementRoom room in BasementLayout.AllRooms) BuildRoom(room);
            BuildProps();
            BuildJunk();
            BuildClonedProps();
            BuildPipe();
            BuildCell();
            BuildLights();
        }

        private void CollectMaterials()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Basement_Reference", StringComparison.Ordinal)) { reference = t; break; }
            wallMaterial = MaterialOf(reference, "LeftWall");
            floorMaterial = MaterialOf(reference, "Floor") ?? wallMaterial;
            ceilingMaterial = MaterialOf(reference, "CeilingBoards") ?? wallMaterial;
            metalMaterial = FindMaterial("DarkMetal") ?? wallMaterial;
            woodMaterial = FindMaterial("OldTimber") ?? ceilingMaterial;
            rugMaterial = FindMaterial("OldRug") ?? wallMaterial;
            pipeMaterial = FindMaterial("AgedPipe") ?? metalMaterial;
            skinMaterial = Tinted(wallMaterial, new Color(0.62f, 0.55f, 0.5f));
            shadowMaterial = Tinted(wallMaterial, new Color(0.03f, 0.03f, 0.035f));
        }

        private static Material MaterialOf(Transform root, string childName)
        {
            if (root == null) return null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name == childName && r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        private static Material FindMaterial(string name)
        {
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                if (m != null && m.name == name) return m;
            return null;
        }

        private static Material Tinted(Material source, Color color)
        {
            if (source == null) return null;
            var copy = new Material(source) { name = source.name + " (P-03)" };
            copy.color = color;
            return copy;
        }

        private GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent != null ? parent : extension.transform, false);
            box.transform.position = center;
            box.transform.localScale = size;
            var renderer = box.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (!collider) Destroy(box.GetComponent<Collider>());
            return box;
        }

        private GameObject Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = center;
            obj.transform.localScale = size;   // у цилиндра Unity высота — 2 × scale.y
            if (material != null) obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        // Восточную стену существующей комнаты заменяем на стену с проёмом в коридор A.
        private void OpenExistingWall()
        {
            if (reference == null) { Debug.LogWarning("[P-03] Basement_Reference не найден: проём в стене не сделан."); return; }
            foreach (Transform t in reference.GetComponentsInChildren<Transform>(true))
                if (t.name == "LeftWall" || t.name == "LeftWall_Masonry") t.gameObject.SetActive(false);

            float h = BasementLayout.CeilingY - BasementLayout.FloorY - 0.05f;
            float x = BasementLayout.ExistingWallX, thick = 0.25f;
            float zMin = BasementLayout.ExistingRoomZMin, zMax = BasementLayout.ExistingRoomZMax;
            float gapFrom = BasementLayout.EntranceFrom, gapTo = BasementLayout.EntranceTo;
            float baseY = BasementLayout.FloorY;
            Box("Wall south of entrance", extension.transform, new Vector3(x, baseY + h * 0.5f, (zMin + gapFrom) * 0.5f),
                new Vector3(thick, h, gapFrom - zMin), wallMaterial);
            Box("Wall north of entrance", extension.transform, new Vector3(x, baseY + h * 0.5f, (gapTo + zMax) * 0.5f),
                new Vector3(thick, h, zMax - gapTo), wallMaterial);
            float lintel = h - BasementLayout.DoorHeight;
            Box("Entrance lintel", extension.transform, new Vector3(x, baseY + BasementLayout.DoorHeight + lintel * 0.5f, (gapFrom + gapTo) * 0.5f),
                new Vector3(thick, lintel, gapTo - gapFrom), wallMaterial);
        }

        private void BuildRoom(BasementRoom room)
        {
            float t = BasementLayout.WallThickness;
            float x0 = room.XMin - (room.IsFullyOpen(BasementSide.West) ? 0f : t), x1 = room.XMax + (room.IsFullyOpen(BasementSide.East) ? 0f : t);
            float z0 = room.ZMin - (room.IsFullyOpen(BasementSide.South) ? 0f : t), z1 = room.ZMax + (room.IsFullyOpen(BasementSide.North) ? 0f : t);
            var parent = new GameObject("Room " + room.Name).transform;
            parent.SetParent(extension.transform, false);
            Vector3 center = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            Box("Floor", parent, new Vector3(center.x, BasementLayout.FloorY - 0.15f, center.z), new Vector3(x1 - x0, 0.3f, z1 - z0), floorMaterial);
            Box("Ceiling", parent, new Vector3(center.x, BasementLayout.CeilingY + 0.15f, center.z), new Vector3(x1 - x0, 0.3f, z1 - z0), ceilingMaterial);
            foreach (BasementBox wall in BasementLayout.Walls(room))
                Box("Wall", parent, wall.Center, wall.Size, wallMaterial);
        }

        private void BuildProps()
        {
            var parent = new GameObject("Hall props").transform;
            parent.SetParent(extension.transform, false);
            foreach (BasementLayout.Prop prop in BasementLayout.HallProps)
            {
                GameObject obj = prop.Cylinder
                    ? Cylinder("Boiler", parent, prop.Center, new Vector3(prop.Size.x, prop.Size.y * 0.5f, prop.Size.z), metalMaterial, true)
                    : Box("Pillar or crate", parent, prop.Center, prop.Size, wallMaterial);
                if (!prop.Cylinder && prop.Size.y < 2f) obj.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Хлам: ящики, бочки, доски, щебень, матрас, стеллажи, вёдра
        // -------------------------------------------------------------------------------------------------

        private void BuildJunk()
        {
            var parent = new GameObject("Junk").transform;
            parent.SetParent(extension.transform, false);
            int seed = 0;
            foreach (BasementLayout.Clutter item in BasementLayout.Junk)
            {
                var root = new GameObject("Junk " + item.Kind).transform;
                root.SetParent(parent, false);
                root.position = new Vector3(item.Position.x, BasementLayout.FloorY, item.Position.y);
                root.rotation = Quaternion.Euler(0f, item.Yaw, 0f);
                var random = new System.Random(seed++ * 7919 + 13);
                switch (item.Kind)
                {
                    case BasementLayout.ClutterKind.Crate: BuildCrate(root, item.Size); break;
                    case BasementLayout.ClutterKind.Barrel: BuildBarrel(root, item.Size); break;
                    case BasementLayout.ClutterKind.Planks: BuildPlanks(root, item.Size, random); break;
                    case BasementLayout.ClutterKind.Rubble: BuildRubble(root, item.Size, random); break;
                    case BasementLayout.ClutterKind.Mattress:
                    case BasementLayout.ClutterKind.Blanket: BuildMat(root, item.Size, item.Kind == BasementLayout.ClutterKind.Blanket); break;
                    case BasementLayout.ClutterKind.Shelf: BuildShelf(root, item.Size, random); break;
                    default: BuildBucket(root, item.Size); break;
                }
                if (item.Blocks)
                {
                    var blocker = root.gameObject.AddComponent<BoxCollider>();
                    blocker.center = new Vector3(0f, item.Size.y * 0.5f, 0f);
                    blocker.size = item.Size;
                }
            }
        }

        private void BuildCrate(Transform root, Vector3 size)
        {
            var box = Box("Crate", root, root.position + Vector3.up * size.y * 0.5f, size, woodMaterial, false);
            box.transform.rotation = root.rotation;
            // Рейки по рёбрам — чтобы не был просто кубом.
            var strap = Box("Strap", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x * 1.03f, size.y * 0.12f, size.z * 1.03f), metalMaterial, false);
            strap.transform.rotation = root.rotation;
        }

        private void BuildBarrel(Transform root, Vector3 size)
        {
            Cylinder("Barrel", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x, size.y * 0.5f, size.z), metalMaterial, false);
            foreach (float k in new[] { 0.18f, 0.82f })
                Cylinder("Hoop", root, root.position + Vector3.up * size.y * k, new Vector3(size.x * 1.06f, size.y * 0.025f, size.z * 1.06f), pipeMaterial, false);
        }

        private void BuildPlanks(Transform root, Vector3 size, System.Random random)
        {
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                float yaw = (float)(random.NextDouble() * 24 - 12);
                var plank = Box("Plank", root, root.position + Vector3.up * (0.05f + i * (size.y / count)), new Vector3(size.x, size.y / count * 0.9f, size.z * 0.55f), woodMaterial, false);
                plank.transform.rotation = root.rotation * Quaternion.Euler(0f, yaw, 0f);
                plank.transform.position += root.right * (float)(random.NextDouble() * 0.12 - 0.06);
            }
        }

        private void BuildRubble(Transform root, Vector3 size, System.Random random)
        {
            for (int i = 0; i < 6; i++)
            {
                float s = 0.18f + (float)random.NextDouble() * 0.25f;
                Vector3 offset = new Vector3((float)(random.NextDouble() - 0.5) * size.x * 0.8f, s * 0.35f * (i % 3 + 1) * 0.5f, (float)(random.NextDouble() - 0.5) * size.z * 0.8f);
                var chunk = Box("Chunk", root, root.position + root.rotation * offset, new Vector3(s, s * 0.7f, s * 0.9f), wallMaterial, false);
                chunk.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 40f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 40f);
            }
        }

        private void BuildMat(Transform root, Vector3 size, bool blanket)
        {
            var mat = Box(blanket ? "Blanket" : "Mattress", root, root.position + Vector3.up * size.y * 0.5f, size, rugMaterial, false);
            mat.transform.rotation = root.rotation;
        }

        private void BuildBucket(Transform root, Vector3 size)
        {
            Cylinder("Bucket", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x, size.y * 0.5f, size.z), metalMaterial, false);
        }

        private void BuildShelf(Transform root, Vector3 size, System.Random random)
        {
            // Длинная сторона — вдоль стены; доски и коробки на них.
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z, depth = alongX ? size.z : size.x;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
            Vector3 baseCenter = root.position;
            foreach (float k in new[] { -0.5f, 0.5f })
                foreach (float d in new[] { -0.5f, 0.5f })
                    Box("Post", root, baseCenter + along * (length * 0.5f - 0.05f) * k * 2f + across * (depth * 0.5f - 0.04f) * d * 2f + Vector3.up * size.y * 0.5f,
                        new Vector3(0.07f, size.y, 0.07f), woodMaterial, false);
            for (int level = 0; level < 3; level++)
            {
                float y = 0.08f + level * (size.y - 0.15f) / 2f;
                Box("Board", root, baseCenter + Vector3.up * y, alongX ? new Vector3(length, 0.05f, depth) : new Vector3(depth, 0.05f, length), woodMaterial, false);
                int items = 3 + random.Next(0, 3);
                for (int i = 0; i < items; i++)
                {
                    float u = ((float)i / items - 0.5f) * length * 0.85f + (float)random.NextDouble() * 0.1f;
                    float h = 0.12f + (float)random.NextDouble() * 0.2f;
                    Material material = random.Next(0, 3) == 0 ? metalMaterial : woodMaterial;
                    Box("Item", root, baseCenter + along * u + Vector3.up * (y + 0.025f + h * 0.5f), new Vector3(0.18f, h, 0.2f), material, false);
                }
            }
        }

        // Клоны мебели существующей комнаты подвала: коробки и складной стул. Ставятся на пол по нижней границе.
        private void BuildClonedProps()
        {
            if (reference == null) return;
            var parent = new GameObject("Cloned props").transform;
            parent.SetParent(extension.transform, false);
            PlaceClone(parent, "StoredBox_0", -26.4f, 121.5f, 25f);
            PlaceClone(parent, "StoredBox_2", -23.8f, 112.0f, -15f);
            PlaceClone(parent, "FoldingChair", -26.0f, 116.6f, 200f);
        }

        private void PlaceClone(Transform parent, string templateName, float x, float z, float yaw)
        {
            Transform template = null;
            foreach (Transform t in reference.GetComponentsInChildren<Transform>(true))
                if (t.name == templateName) { template = t; break; }
            if (template == null) return;
            GameObject copy = Instantiate(template.gameObject, parent);
            copy.name = templateName + " (clone)";
            copy.SetActive(true);
            copy.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            copy.transform.position = new Vector3(x, template.position.y, z);
            var renderers = copy.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            copy.transform.position += Vector3.up * (BasementLayout.FloorY - bounds.min.y);
        }

        // Труба хозяина: от решётки камеры под потолком через блок, коридор C, зал и коридор A — вверх, в дом.
        private void BuildPipe()
        {
            var parent = new GameObject("Walter's pipe").transform;
            parent.SetParent(extension.transform, false);
            float y = BasementLayout.PipeY;
            Vector2[] route = BasementLayout.PipeRoute;
            Vector3 start = new Vector3(route[0].x, BasementLayout.FloorY, route[0].y);
            Vector3 top = new Vector3(route[0].x, y, route[0].y);
            PipeSegment(parent, start, top);
            Vector3 previous = top;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 next = new Vector3(route[i].x, y, route[i].y);
                PipeSegment(parent, previous, next);
                var elbow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                elbow.name = "Elbow";
                elbow.transform.SetParent(parent, false);
                elbow.transform.position = previous;
                elbow.transform.localScale = Vector3.one * 0.19f;
                elbow.GetComponent<Renderer>().sharedMaterial = pipeMaterial;
                Destroy(elbow.GetComponent<Collider>());
                previous = next;
            }
            // Конец уходит вверх через перекрытие — к стоякам первого этажа.
            PipeSegment(parent, previous, new Vector3(previous.x, BasementLayout.CeilingY + 0.5f, previous.z));
        }

        private void PipeSegment(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f) return;
            var pipe = Cylinder("Pipe", parent, (a + b) * 0.5f, new Vector3(0.14f, length * 0.5f, 0.14f), pipeMaterial, false);
            pipe.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        // Камера: боковая и задняя стены уже даёт блок D; здесь — восточная стена ниши, решётка, замок, хозяин за ней.
        private void BuildCell()
        {
            float floor = BasementLayout.FloorY, height = BasementLayout.CeilingY - floor;
            Rect cell = BasementLayout.CellArea;
            Box("Cell east wall", extension.transform, new Vector3(cell.xMax + 0.15f, floor + height * 0.5f, (cell.yMin + cell.yMax) * 0.5f),
                new Vector3(0.3f, height, cell.height), wallMaterial);

            var door = new GameObject("Cell door");
            door.transform.SetParent(extension.transform, false);
            door.transform.position = new Vector3(cell.xMax, floor, cell.yMax);
            cellDoor = door.transform;
            float width = cell.width;
            for (int i = 0; i < 17; i++)
            {
                float x = -0.08f - i * ((width - 0.16f) / 16f);
                var bar = Box("Bar", door.transform, Vector3.zero, new Vector3(0.05f, 2.3f, 0.05f), metalMaterial, false);
                bar.transform.localPosition = new Vector3(x, 1.15f, 0f);
            }
            foreach (float y in new[] { 0.15f, 1.15f, 2.25f })
            {
                var rail = Box("Rail", door.transform, Vector3.zero, new Vector3(width, 0.06f, 0.06f), metalMaterial, false);
                rail.transform.localPosition = new Vector3(-width * 0.5f, y, 0f);
            }
            cellDoorCollider = Box("Cell door collider", door.transform, Vector3.zero, new Vector3(width, 2.4f, 0.12f), null);
            cellDoorCollider.transform.localPosition = new Vector3(-width * 0.5f, 1.2f, 0f);
            Destroy(cellDoorCollider.GetComponent<Renderer>());
            Destroy(cellDoorCollider.GetComponent<MeshFilter>());

            lockVisual = Box("Cell lock", door.transform, Vector3.zero, new Vector3(0.16f, 0.22f, 0.1f), metalMaterial);
            lockVisual.transform.position = BasementLayout.CellLock;
            Destroy(lockVisual.GetComponent<Collider>());
            var lockTrigger = new GameObject("Cell lock interaction", typeof(BoxCollider), typeof(BasementCellLock));
            lockTrigger.transform.SetParent(lockVisual.transform, false);
            lockTrigger.transform.position = BasementLayout.CellLock + new Vector3(0f, 0f, 0.1f);
            lockTrigger.layer = InteractableLayer;
            lockTrigger.GetComponent<BoxCollider>().size = new Vector3(0.9f, 0.9f, 0.5f);
            cellLock = lockTrigger.GetComponent<BasementCellLock>();
            cellLock.Initialize(CanUseLock, UseLock, LockLabel);

            BuildOwner();
        }

        // Хозяин виден не целиком: рука на прутьях и тёмная фигура у стены. Модели нет — это решение автора.
        private void BuildOwner()
        {
            var owner = new GameObject("Walter Hale (cell)").transform;
            owner.SetParent(extension.transform, false);
            Vector3 hand = new Vector3(-30.55f, BasementLayout.FloorY + 1.2f, 98.78f);
            var forearm = Box("Forearm", owner, hand + new Vector3(0f, -0.04f, -0.42f), new Vector3(0.075f, 0.07f, 0.85f), skinMaterial, false);
            forearm.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Box("Palm", owner, hand, new Vector3(0.11f, 0.035f, 0.12f), skinMaterial, false);
            for (int i = 0; i < 4; i++)
                Box("Finger", owner, hand + new Vector3(-0.04f + i * 0.027f, -0.02f, 0.1f), new Vector3(0.021f, 0.021f, 0.1f), skinMaterial, false);
            var thumb = Box("Thumb", owner, hand + new Vector3(0.07f, -0.01f, 0.03f), new Vector3(0.025f, 0.025f, 0.08f), skinMaterial, false);
            thumb.transform.rotation = Quaternion.Euler(0f, -35f, 0f);

            Vector3 at = BasementLayout.OwnerPosition;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Silhouette body";
            body.transform.SetParent(owner, false);
            body.transform.position = at + new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            body.transform.rotation = Quaternion.Euler(0f, 0f, 78f);
            body.GetComponent<Renderer>().sharedMaterial = shadowMaterial;
            Destroy(body.GetComponent<Collider>());
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Silhouette head";
            head.transform.SetParent(owner, false);
            head.transform.position = at + new Vector3(0.45f, 0.75f, 0.35f);
            head.transform.localScale = new Vector3(0.22f, 0.25f, 0.22f);
            head.GetComponent<Renderer>().sharedMaterial = shadowMaterial;
            Destroy(head.GetComponent<Collider>());
        }

        private void BuildLights()
        {
            // Единственный свет — тусклая красная лампа у камеры: ориентир в конце пути. Остальное — фонарь игрока.
            var lamp = new GameObject("Cell lamp").AddComponent<Light>();
            lamp.transform.SetParent(extension.transform, false);
            lamp.transform.position = new Vector3(-27.5f, BasementLayout.CeilingY - 0.4f, 100.5f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.25f, 0.15f);
            lamp.intensity = 0.8f;
            lamp.range = 5.5f;
            lamp.shadows = LightShadows.None;
        }

        private void SpawnMonster()
        {
            if (monster != null) return;
            var prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) { Debug.LogError("[P-03] Нет префаба Scares/CornerPeekMonster."); return; }
            Vector2 start = BasementLayout.Patrol[0];
            monster = Instantiate(prefab, new Vector3(start.x, BasementLayout.FloorY, start.y), Quaternion.identity, extension.transform);
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
