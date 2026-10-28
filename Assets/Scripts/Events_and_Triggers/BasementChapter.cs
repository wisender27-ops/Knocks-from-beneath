using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // P-03 «Подвал как глава» + B-05 финал E5 «Ответ». Спуск через люк: за восточной стеной существующей комнаты
    // подвала строится продолжение (BasementLayout), по залу ходит монстр и слушает шаги, из камеры в дальнем блоке
    // стучит хозяин. Игрок идёт на стук, не выдавая себя, вскрывает замок ломом — и выбирает не заколачивать, а ответить.
    // Геометрия создаётся в рантайме: сцена не меняется.
    public sealed class BasementChapter : MonoBehaviour
    {
        public enum Stage { Idle, Descending, Explore, Caught, Finale, Done }

        private const string FindQuest = "basement-find", ReachQuest = "basement-reach", UnlockQuest = "basement-unlock";
        private const int InteractableLayer = 6;

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
        private BasementCellLock cellLock;
        private Transform cellDoor;
        private GameObject cellDoorCollider, lockVisual;
        private AudioSource knockSource, effects;
        private Coroutine knocking;
        private CanvasGroup fade;
        private Material wallMaterial, floorMaterial, ceilingMaterial, metalMaterial, skinMaterial, shadowMaterial;
        private int catches;
        private bool reachedCell;

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
            reachedCell = false;
            sayQuick?.Invoke(new[] { "Темно. И тихо. Только где-то глубже стучат.", "Он ходит здесь. Присесть и идти медленно — тогда он не услышит." }, null);
            createQuest?.Invoke("Найти того, кто стучал", 1, () => createQuest("Добраться до камеры", 1, OnReachedStage, ReachQuest), FindQuest);
            stalker.Begin();
            knocking = StartCoroutine(KnockRoutine());
        }

        private void OnReachedStage()
        {
            createQuest?.Invoke("Вскрыть замок камеры", 1, null, UnlockQuest);
        }

        private void Update()
        {
            if (CurrentStage != Stage.Explore || player == null) return;
            Vector3 p = player.position;
            if (QuestActive(FindQuest) && p.x > -31.4f && p.y < -2f) QuestManager.Instance.AddProgress(1);
            else if (QuestActive(ReachQuest) &&
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(BasementLayout.CellApproach.x, BasementLayout.CellApproach.z)) < 3.2f)
            {
                reachedCell = true;
                QuestManager.Instance.AddProgress(1);
            }
        }

        private static bool QuestActive(string tag) => QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(tag);

        // -------------------------------------------------------------------------------------------------
        // Поимка
        // -------------------------------------------------------------------------------------------------

        private void OnCaught()
        {
            if (CurrentStage != Stage.Explore) return;
            CurrentStage = Stage.Caught;
            StartCoroutine(CaughtRoutine());
        }

        private IEnumerator CaughtRoutine()
        {
            LockCamera(true);
            stalker.Stop();
            PlayEffect("Audio/CornerJumpscare", 1f, 0.95f);
            yield return Fade(1f, 0.35f);
            catches++;
            PlaceAt(BasementLayout.PlayerStart, BasementLayout.PlayerStartLook);
            stalker.ResetPatrol(catches % 2 == 0 ? 0 : 2);
            yield return new WaitForSeconds(0.9f);
            if (catches == 2)
                sayQuick?.Invoke(new[] { "Он слышит шаги. Ctrl — присесть. Красться, а не бежать." }, null);
            yield return Fade(0f, 1f);
            LockCamera(false);
            CurrentStage = Stage.Explore;
            stalker.Begin();
        }

        // -------------------------------------------------------------------------------------------------
        // Замок и финал
        // -------------------------------------------------------------------------------------------------

        private bool CanUseLock() => CurrentStage == Stage.Explore && reachedCell && QuestActive(UnlockQuest);

        private void UseLock()
        {
            if (inventory == null || !inventory.hasCrowbar)
            {
                sayQuick?.Invoke(new[] { "Замок. Голыми руками не сбить. Нужен лом." }, null);
                return;
            }
            StartCoroutine(FinaleRoutine());
        }

        private IEnumerator FinaleRoutine()
        {
            CurrentStage = Stage.Finale;
            LockCamera(true);
            stalker.Stop();
            if (knocking != null) { StopCoroutine(knocking); knocking = null; }
            Vector3 lockPoint = BasementLayout.CellLock;
            yield return TurnToward(lockPoint, 0.6f);
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
            if (QuestActive(UnlockQuest)) QuestManager.Instance.AddProgress(1);

            bool spoken = false;
            say?.Invoke(new[]
            {
                "— …Ты ответил.",
                "— Я слышал. Каждую ночь.",
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
        // Стук хозяина
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
            knockSource = AudioRouting.Create(extension.transform, "Walter knocks", AudioBus.Horror, 1f, 45f);
            knockSource.transform.position = new Vector3(-30.7f, BasementLayout.FloorY + 1f, 97.6f);
            knockSource.minDistance = 3f;
            knockSource.volume = 1f;

            OpenExistingWall();
            foreach (BasementRoom room in BasementLayout.Rooms) BuildRoom(room);
            BuildProps();
            BuildCell();
            BuildLights();
        }

        private void CollectMaterials()
        {
            Transform reference = null;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Basement_Reference", StringComparison.Ordinal)) { reference = t; break; }
            wallMaterial = MaterialOf(reference, "LeftWall");
            floorMaterial = MaterialOf(reference, "Floor") ?? wallMaterial;
            ceilingMaterial = MaterialOf(reference, "CeilingBoards") ?? wallMaterial;
            metalMaterial = FindMaterial("DarkMetal") ?? wallMaterial;
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

        // Восточную стену существующей комнаты заменяем на стену с проёмом в коридор A.
        private void OpenExistingWall()
        {
            Transform reference = null;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Basement_Reference", StringComparison.Ordinal)) { reference = t; break; }
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
                    ? GameObject.CreatePrimitive(PrimitiveType.Cylinder)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = prop.Cylinder ? "Boiler" : "Pillar or crate";
                obj.transform.SetParent(parent, false);
                obj.transform.position = prop.Center;
                obj.transform.localScale = prop.Cylinder ? new Vector3(prop.Size.x, prop.Size.y * 0.5f, prop.Size.z) : prop.Size;
                obj.GetComponent<Renderer>().sharedMaterial = prop.Cylinder ? metalMaterial : wallMaterial;
            }
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
            cellLock.Initialize(CanUseLock, UseLock);

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
