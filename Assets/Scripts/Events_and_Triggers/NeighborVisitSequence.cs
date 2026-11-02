using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class NeighborVisitSequence : MonoBehaviour
    {
        public GameObject neighborPrefab;
        public Transform visitPoint;
        public Transform[] departurePath;
        public Door frontDoor;
        public LightSwitch porchLightSwitch;
        public Transform player;
        public AudioClip knockClip;
        [Min(0.5f)] public float conversationDistance = 3f;
        [Min(0.1f)] public float walkingSpeed = 1.1f;
        // Квест «Вернуться в кровать» появляется, пока сосед ещё идёт: столько секунд до конца его пути.
        private const float BedQuestLeadSeconds = 5f;
        // true — сосед попрощался, из дома не выйти; false — герой лёг или сцена прервана.
        [System.NonSerialized] public System.Action<bool> stayIndoors;
        public string walkingBoolParameter = "IsWalking";

        // N-01: пока на крыльце темно, за окном гостиной стоит чёрный силуэт «как у монстра» —
        // выше ростом, длиннорукий, сгорбленный — и следит головой за игроком. Включённый свет
        // в тот же кадр показывает обычного соседа; открытая дверь — всегда человек на крыльце.
        [Header("Силуэт в окне (N-01)")]
        [Tooltip("Где силуэт стоит в темноте — за окном, лицом в дом. Пусто — сосед сразу у двери.")]
        public Transform windowPeekPoint;
        [Tooltip("Путь от окна к двери, по которому сосед уходит, когда включили свет.")]
        public Transform[] windowReturnPath;
        [Range(0f, 0.3f)] public float silhouetteExtraHeight = 0.12f;
        [Range(1f, 1.6f)] public float silhouetteArmLength = 1.3f;

        // Дверь открывается наружу, поэтому стучит сосед сбоку, со стороны ручки, вне дуги
        // полотна. В момент, когда дверь трогается, он уже стоит в visitPoint напротив проёма:
        // через закрытую дверь перенос не виден.
        [Header("Стук в дверь")]
        [Tooltip("Где сосед ждёт и стучит. Пусто — стук без жеста, сосед сразу в visitPoint.")]
        public Transform knockPoint;
        [Tooltip("Точка на полотне двери, куда бьёт кулак.")]
        public Transform knockContact;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        private Quaternion closedDoorRotation;
        private bool porchStateCaptured, porchWasOn;
        private GameObject neighbor;
        private AudioSource knockSource;
        private bool played, running, lockCaptured, doorWasLocked, waitingForBed, bedReached, audioPaused;
        private bool silhouetteActive, steppingBack, viewWasLocked, atDoor = true, atVisit = true, knockSoon;
        private PlayerController viewController;
        private NeighborKnockIK knockIK;
        private int returnIndex;
        private Renderer[] silhouetteRenderers;
        private Transform spine, chest, neck, head, leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm;
        private Vector3 neighborScale, leftUpperScale, rightUpperScale, leftLowerScale, rightLowerScale;
        public bool IsPlaying => running;
        public bool IsConfigured => neighborPrefab != null && visitPoint != null && frontDoor != null && player != null;
        public bool HasVisited { get; private set; }
        public bool IsSilhouetteVisible => silhouetteActive;
        public bool IsAtDoor => atDoor;

        public IEnumerator Play()
        {
            if (played || !isActiveAndEnabled || !IsConfigured)
                yield break;
            played = true;
            running = true;
            closedDoorRotation = frontDoor.transform.rotation;
            doorWasLocked = frontDoor.IsInteractionLocked;
            lockCaptured = true;
            frontDoor.SetInteractionLocked(false);
            try
            {
                if (porchLightSwitch != null)
                {
                    porchWasOn = porchLightSwitch.isOn;
                    porchStateCaptured = true;
                    porchLightSwitch.ForceTurnOff();
                }
                bool knocksByHand = knockPoint != null && knockContact != null;
                Transform waitPoint = knocksByHand ? knockPoint : visitPoint;
                neighbor = Instantiate(neighborPrefab, waitPoint.position, waitPoint.rotation);
                neighbor.SetActive(true);
                Animator neighborAnimator = neighbor.GetComponentInChildren<Animator>();
                if (knocksByHand && neighborAnimator != null && neighborAnimator.isHuman)
                    knockIK = neighborAnimator.gameObject.AddComponent<NeighborKnockIK>();
                else neighbor.transform.SetPositionAndRotation(visitPoint.position, visitPoint.rotation);
                atVisit = knockIK == null;
                knockSource = HorrorMomentDirector.CreateSource(frontDoor.transform, "Neighbor door knocks", 24f);
                knockSource.minDistance = 3f;
                // A-01: сосед стучит костяшками по двери — не тем же звуком, что хозяин снизу.
                if (knockClip == null) knockClip = GameSounds.Library?.doorKnock;
                if (knockClip == null) knockClip = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
                if (porchLightSwitch != null && windowPeekPoint != null) BeginSilhouette();
                yield return Knock();
                if (!running) yield break;
                QuestManager.Instance?.CreateQuest("Проверить, кто стучит в дверь", 1, questTag: "neighbor-visit");
                float reminder = 0f;
                int cornerSide = 0;
                bool frontDoorTouched = false;
                while (running && !CanTalk())
                {
                    if (Playable())
                    {
                        // Игрок обошёл дом с заднего двора, не трогая главную дверь: сосед выходит ему навстречу.
                        frontDoorTouched |= frontDoor.isOpen;
                        if (!frontDoorTouched && (cornerSide = CornerSideOf(player.position)) != 0)
                        {
                            yield return CornerEmerge(cornerSide);
                            break;
                        }
                        if (!silhouetteActive && !atDoor) StepBackToDoor();
                        // Дойдя от окна до двери, сосед вскоре стучит снова — игрок видит его жест.
                        if (knockSoon) { knockSoon = false; reminder = Mathf.Max(reminder, 12.8f); }
                        reminder += Time.deltaTime;
                        if (reminder >= 14f)
                        {
                            reminder = 0f;
                            yield return Knock();
                        }
                    }
                    yield return null;
                }
                if (!running) yield break;
                QuestManager.Instance?.RecordQuestCompletion("neighbor-visit");
                if (cornerSide != 0)
                {
                    yield return Say("СОСЕД", "Ох... Ты меня напугал. Я уже думал, что-то случилось.");
                    yield return Say("СОСЕД", "Я из дома напротив. Видел, как ты ходишь по первому этажу, а потом пропал. Стучу, стучу — тишина.");
                    yield return Say("Я", "Я... вышел подышать. С другой стороны дома.");
                    yield return Say("СОСЕД", "В такую темень? Ну... главное, что всё в порядке.");
                }
                else
                {
                    yield return MeetAtDoor();
                    if (!running) yield break;
                    yield return Say("СОСЕД", "Привет. Я из дома напротив. Извини, что так поздно.");
                    yield return Say("СОСЕД", "Увидел, как ты ходишь на первом этаже. Решил зайти познакомиться.");
                    yield return Say("Я", "На первом? Я спал наверху. Только сейчас проснулся.");
                    yield return Say("СОСЕД", "Да? Ну... наверное, показалось. В окне мелькнуло что-то.");
                }
                // K-05: игрок стучал в пол, пока шёл к двери, и снизу ответили — сосед это слышал.
                if (StoryFlags.KnockAnswered)
                {
                    yield return Say("СОСЕД", "И ещё. Пока я стоял у двери, это ты по полу стучал?");
                    yield return Say("СОСЕД", "Тук-тук... и снизу будто кто-то отвечает.");
                    yield return Say("Я", "...Трубы, наверное. Старый дом.");
                }
                // S-05: пока нет звонка M-06, эту часть истории рассказывает сосед на пороге.
                yield return Say("СОСЕД", "Ты ведь у сына Хейла купил? Самого Уолтера весной не стало видно. Сын сказал, уехал к сестре.");
                yield return Say("СОСЕД", "А потом неделю мешки с цементом таскал в подвал. Бетонировал что-то. Один, по ночам.");
                yield return Say("Я", "Мне про подвал ничего не говорили.");
                yield return Say("СОСЕД", "Ладно, не буду мешать. Спокойной ночи.");
                if (!running) yield break;
                ReleasePlayerView();
                stayIndoors?.Invoke(true);
                yield return Leave(cornerSide != 0 ? CornerDepartureStart : 0);
                if (!running) yield break;
                HasVisited = true;
                AskToReturnToBed();
                while (running && !bedReached) yield return null;
                if (!running) yield break;
                QuestManager.Instance?.RecordQuestCompletion("neighbor-return-bed");
            }
            finally
            {
                Cleanup();
            }
        }

        private bool Playable() => !NarrativeReadingPanel.WorldHalted && !RetroInventoryScreen.IsAnyOpen && !QuestJournalScreen.IsAnyOpen;

        private bool CanTalk()
        {
            if (!Playable() || !atVisit || !frontDoor.isOpen || !NarrativeDisplayQueue.CanShowInteractionHint) return false;
            if (Quaternion.Angle(closedDoorRotation, frontDoor.transform.rotation) < 65f) return false;
            Vector3 delta = player.position - visitPoint.position;
            if (Mathf.Abs(delta.y) >= 2.2f || Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > conversationDistance * conversationDistance) return false;
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : player.position + Vector3.up * 1.6f;
            Vector3 target = visitPoint.position + Vector3.up * 1.5f;
            if (Physics.Linecast(eye, target, out RaycastHit hit, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore))
                return neighbor != null && hit.transform.IsChildOf(neighbor.transform);
            return true;
        }

        private void BeginSilhouette()
        {
            silhouetteRenderers = neighbor.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.black);
            block.SetColor(ColorId, Color.black);
            block.SetFloat(SmoothnessId, 0f);
            block.SetFloat(MetallicId, 0f);
            foreach (Renderer rend in silhouetteRenderers)
                if (rend != null) rend.SetPropertyBlock(block);

            Animator animator = neighbor.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            neighbor.transform.SetPositionAndRotation(windowPeekPoint.position, windowPeekPoint.rotation);
            atDoor = false;
            returnIndex = 0;
            neighborScale = neighbor.transform.localScale;
            leftUpperScale = leftUpperArm != null ? leftUpperArm.localScale : Vector3.one;
            rightUpperScale = rightUpperArm != null ? rightUpperArm.localScale : Vector3.one;
            leftLowerScale = leftLowerArm != null ? leftLowerArm.localScale : Vector3.one;
            rightLowerScale = rightLowerArm != null ? rightLowerArm.localScale : Vector3.one;
            neighbor.transform.localScale = neighborScale * (1f + silhouetteExtraHeight);
            silhouetteActive = true;
        }

        private void EndSilhouette()
        {
            silhouetteActive = false;
            if (silhouetteRenderers != null)
                foreach (Renderer rend in silhouetteRenderers)
                    if (rend != null) rend.SetPropertyBlock(null);
            silhouetteRenderers = null;
            if (neighbor != null) neighbor.transform.localScale = neighborScale;
            if (leftUpperArm != null) leftUpperArm.localScale = leftUpperScale;
            if (rightUpperArm != null) rightUpperArm.localScale = rightUpperScale;
            if (leftLowerArm != null) leftLowerArm.localScale = leftLowerScale;
            if (rightLowerArm != null) rightLowerArm.localScale = rightLowerScale;
        }

        private void LateUpdate()
        {
            if (neighbor == null || atVisit) return;
            // Дверь трогается — напротив проёма всегда человек. Через закрытую дверь перенос
            // не виден. Проверка до рендера, в том же кадре.
            if (frontDoor.isOpen || Quaternion.Angle(closedDoorRotation, frontDoor.transform.rotation) > 0.5f)
            {
                if (silhouetteActive) EndSilhouette();
                ArriveAtVisit();
                return;
            }
            if (!silhouetteActive) return;
            // Свет включён в Update игрока — в этом же кадре за окном уже обычный сосед.
            if (porchLightSwitch == null || porchLightSwitch.isOn) { EndSilhouette(); return; }
            ApplySilhouettePose();
        }

        private void ApplySilhouettePose()
        {
            Transform root = neighbor.transform;
            float sway = Mathf.Sin(Time.time * 0.7f) * 2.5f;
            Rotate(spine, root.right, 12f);
            Rotate(spine, root.forward, sway);
            Rotate(chest, root.right, 10f);
            Rotate(neck, root.right, 16f);
            Rotate(head, root.forward, 24f - sway);
            float segment = Mathf.Sqrt(silhouetteArmLength);
            if (leftUpperArm != null) leftUpperArm.localScale = leftUpperScale * segment;
            if (rightUpperArm != null) rightUpperArm.localScale = rightUpperScale * segment;
            if (leftLowerArm != null) leftLowerArm.localScale = leftLowerScale * segment;
            if (rightLowerArm != null) rightLowerArm.localScale = rightLowerScale * segment;

            // Голова следит за игроком — дальше, чем позволяет человеческая шея.
            Camera camera = Camera.main;
            if (camera == null || head == null) return;
            Vector3 toCamera = Vector3.ProjectOnPlane(camera.transform.position - head.position, Vector3.up);
            if (toCamera.sqrMagnitude < 0.01f) return;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(root.forward, Vector3.up), toCamera, Vector3.up), -110f, 110f);
            Rotate(neck, Vector3.up, yaw * 0.4f);
            Rotate(head, Vector3.up, yaw * 0.6f);
        }

        private static void Rotate(Transform bone, Vector3 axis, float angle)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }

        // При свете сосед обычной походкой уходит от окна к двери.
        private void StepBackToDoor()
        {
            if (neighbor == null) { atDoor = atVisit = true; return; }
            Transform body = neighbor.transform;
            Transform waitPoint = WaitPoint;
            Vector3 target = windowReturnPath != null && returnIndex < windowReturnPath.Length && windowReturnPath[returnIndex] != null
                ? windowReturnPath[returnIndex].position : waitPoint.position;
            Vector3 offset = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
            if (offset.sqrMagnitude > 0.0004f)
            {
                if (!steppingBack) SetWalking(true);
                steppingBack = true;
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(offset), 360f * Time.deltaTime);
                body.position = Vector3.MoveTowards(body.position, target, walkingSpeed * Time.deltaTime);
                return;
            }
            body.position = target;
            if (windowReturnPath != null && returnIndex < windowReturnPath.Length) { returnIndex++; return; }
            if (steppingBack) SetWalking(false);
            steppingBack = false;
            body.rotation = Quaternion.RotateTowards(body.rotation, waitPoint.rotation, 240f * Time.deltaTime);
            if (Quaternion.Angle(body.rotation, waitPoint.rotation) >= 1f) return;
            atDoor = true;
            atVisit = waitPoint == visitPoint;
            knockSoon = !atVisit;
        }

        private Transform WaitPoint => knockIK != null ? knockPoint : visitPoint;

        private void ArriveAtVisit()
        {
            if (steppingBack) SetWalking(false);
            steppingBack = false;
            if (knockIK != null) knockIK.StopNow();
            neighbor.transform.SetPositionAndRotation(visitPoint.position, visitPoint.rotation);
            atDoor = atVisit = true;
        }

        // Диалог модальный и останавливает время: дверь замерла бы на полпути, а её зацикленный
        // скрип при нулевом времени не затихает и звучит весь разговор. Поэтому до первой реплики
        // дверь доезжает до конца и замолкает, а игрок и сосед поворачиваются друг к другу.
        private IEnumerator MeetAtDoor()
        {
            PlayerDoorOpen doorInput = player.GetComponentInChildren<PlayerDoorOpen>();
            if (doorInput != null) doorInput.EndDoorHold();
            frontDoor.OpenDoor();
            viewController = player.GetComponent<PlayerController>();
            if (viewController != null)
            {
                viewWasLocked = viewController.isCameraLocked;
                viewController.StopMotion();
                viewController.isCameraLocked = true;
            }
            Quaternion bodyFrom = player.rotation;
            Quaternion previousDoor = frontDoor.transform.rotation;
            float turn = 0f, settled = 0f, elapsed = 0f;
            while (running && (turn < 1f || settled < 0.45f) && elapsed < 3f)
            {
                if (Playable())
                {
                    elapsed += Time.deltaTime;
                    turn = Mathf.Min(1f, turn + Time.deltaTime / 0.8f);
                    Vector3 toNeighbor = neighbor != null ? Vector3.ProjectOnPlane(neighbor.transform.position - player.position, Vector3.up) : Vector3.zero;
                    if (toNeighbor.sqrMagnitude > 0.01f)
                    {
                        player.rotation = Quaternion.Slerp(bodyFrom, Quaternion.LookRotation(toNeighbor), Mathf.SmoothStep(0f, 1f, turn));
                        neighbor.transform.rotation = Quaternion.RotateTowards(neighbor.transform.rotation, Quaternion.LookRotation(-toNeighbor), 240f * Time.deltaTime);
                    }
                    // Скрип затихает сам, только пока время идёт: ждём, пока створка постоит.
                    bool moving = Quaternion.Angle(previousDoor, frontDoor.transform.rotation) > 0.05f;
                    settled = moving ? 0f : settled + Time.deltaTime;
                    previousDoor = frontDoor.transform.rotation;
                }
                yield return null;
            }
            if (frontDoor.sfxSource != null) frontDoor.sfxSource.Pause();
            // Пока диалог держит паузу, сосед не застывает статуей, а дышит в idle.
            SetNeighborUnscaled(true);
        }

        private void ReleasePlayerView()
        {
            SetNeighborUnscaled(false);
            if (viewController != null) viewController.isCameraLocked = viewWasLocked;
            viewController = null;
        }

        private void SetNeighborUnscaled(bool unscaled)
        {
            Animator animator = neighbor != null ? neighbor.GetComponentInChildren<Animator>() : null;
            if (animator != null) animator.updateMode = unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
        }

        // У двери сосед стучит рукой, и звук звучит в момент касания кулака. Из окна или
        // от visitPoint — только звук: стук без видимого стучащего.
        private IEnumerator Knock()
        {
            bool byHand = knockIK != null && atDoor && !atVisit && !silhouetteActive;
            if (byHand)
            {
                knockIK.Begin(knockContact.position, knockContact.forward);
                yield return new WaitForSeconds(NeighborKnockIK.RaiseTime);
            }
            for (int i = 0; i < 3; i++)
            {
                while (running && !Playable()) yield return null;
                if (!running || (byHand && atVisit)) yield break;
                if (byHand)
                {
                    knockIK.Tap();
                    yield return new WaitForSeconds(NeighborKnockIK.TapContactDelay);
                    if (!running || atVisit) yield break;
                }
                if (knockSource != null && knockClip != null) knockSource.PlayOneShot(knockClip, 0.7f);
                yield return new WaitForSeconds(byHand ? 0.42f - NeighborKnockIK.TapContactDelay : 0.42f);
            }
            if (byHand && !atVisit) knockIK.End();
        }

        // Засада у угла (микропасхалка): игрок вышел через заднюю дверь и идёт вдоль дома к крыльцу.
        // Стоит ему подойти вплотную к переднему углу, как сосед, потерявший его из виду, мгновенно
        // выскакивает из-за угла, а камера следит за ним. Координаты — по полу участка: проходы шириной
        // ~2 м между домом (x −47…−33) и забором; западное крыло дома кончается на z ≈ 119.5, восточное на ≈ 117.
        private const float WalkwayWestMinX = -50f, WalkwayWestMaxX = -46.8f, WestLaneX = -48f, WestCornerZ = 119.5f;
        private const float WalkwayEastMinX = -33.2f, WalkwayEastMaxX = -30f, EastLaneX = -32f, EastCornerZ = 117f;
        private const float HouseFrontSouthZ = 104.7f;
        private const float CornerTriggerBefore = 1.6f;
        private const float CornerEmergeSeconds = 0.3f;
        private const float CornerHideSide = 3f, CornerHideMinZ = 2.5f;
        // Сосед встаёт в метре от героя, а камера ещё и подаётся к нему: лицом к лицу.
        private const float CornerStandDistance = 1f, CornerLungeDistance = 0.35f, CornerFovKick = 16f;
        // С этой точки departurePath ведёт по двору перед домом, минуя крыльцо.
        private const int CornerDepartureStart = 2;

        // −1 запад, 1 восток, 0 — игрок не у угла.
        private static int CornerSideOf(Vector3 position)
        {
            if (position.y >= 3f || position.z <= HouseFrontSouthZ) return 0;
            if (position.x > WalkwayWestMinX && position.x < WalkwayWestMaxX && position.z >= WestCornerZ - CornerTriggerBefore) return -1;
            if (position.x > WalkwayEastMinX && position.x < WalkwayEastMaxX && position.z >= EastCornerZ - CornerTriggerBefore) return 1;
            return 0;
        }

        private static Vector3 OnGround(Vector3 point)
        {
            if (Physics.Raycast(new Vector3(point.x, 4f, point.z), Vector3.down, out RaycastHit hit, 8f,
                    PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore))
                point.y = hit.point.y;
            return point;
        }

        // Сосед вылетает из-за угла за 0.3 с со страшным звуком; игрок теряет управление, камера
        // успевает за ним, затем он замирает лицом к герою — дальше идёт модальный диалог.
        private IEnumerator CornerEmerge(int side)
        {
            if (neighbor == null) yield break;
            if (silhouetteActive) EndSilhouette();
            if (knockIK != null) knockIK.StopNow();
            steppingBack = false;
            atDoor = atVisit = true;

            float cornerZ = side < 0 ? WestCornerZ : EastCornerZ;
            Vector3 stand = OnGround(new Vector3(player.position.x, 0f, player.position.z + CornerStandDistance));
            Vector3 hidden = OnGround(new Vector3(player.position.x - side * CornerHideSide, 0f, Mathf.Max(stand.z + 1.5f, cornerZ + CornerHideMinZ)));
            neighbor.transform.SetPositionAndRotation(hidden, Quaternion.LookRotation(Vector3.ProjectOnPlane(stand - hidden, Vector3.up)));

            viewController = player.GetComponent<PlayerController>();
            if (viewController != null)
            {
                viewWasLocked = viewController.isCameraLocked;
                viewController.StopMotion();
                viewController.isCameraLocked = true;
            }
            Camera view = viewController != null ? viewController.GetComponentInChildren<Camera>() : Camera.main;
            CharacterController body3d = player.GetComponent<CharacterController>();
            float baseFov = view != null ? view.fieldOfView : 0f, lunged = 0f;
            SetWalking(true);
            AudioSource sting = AudioRouting.Create(neighbor.transform, "Neighbor corner scare", AudioBus.Horror, 0f);
            sting.clip = Resources.Load<AudioClip>("Audio/NeighborCornerHit");
            if (sting.clip != null) sting.Play();

            // Камера следит за ним всё появление и ещё немного после: сосед поворачивается к герою.
            float elapsed = 0f;
            while (running && neighbor != null && elapsed < CornerEmergeSeconds + 0.5f)
            {
                if (Playable())
                {
                    elapsed += Time.deltaTime;
                    Transform body = neighbor.transform;
                    if (elapsed <= CornerEmergeSeconds)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / CornerEmergeSeconds);
                        body.position = Vector3.Lerp(hidden, stand, t);
                        if (view != null) view.fieldOfView = baseFov + CornerFovKick * Mathf.Sin(Mathf.PI * elapsed / CornerEmergeSeconds * 0.5f);
                        if (body3d != null && body3d.enabled)
                        {
                            float step = CornerLungeDistance * t - lunged;
                            lunged += step;
                            body3d.Move(Vector3.ProjectOnPlane(body.position - player.position, Vector3.up).normalized * step);
                        }
                    }
                    else
                    {
                        if (elapsed - Time.deltaTime <= CornerEmergeSeconds) { body.position = stand; SetWalking(false); }
                        Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - body.position, Vector3.up);
                        if (toPlayer.sqrMagnitude > 0.01f)
                            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(toPlayer), 540f * Time.deltaTime);
                    }
                    TrackNeighbor(view, 1f - Mathf.Exp(-18f * Time.deltaTime));
                }
                yield return null;
            }
            if (view != null) view.fieldOfView = baseFov;
            if (!running || neighbor == null) yield break;
            neighbor.transform.position = stand;
            SetWalking(false);
            SetNeighborUnscaled(true);
        }

        // Корпус героя поворачивается к голове соседа по курсу, камера — по наклону.
        private void TrackNeighbor(Camera view, float k)
        {
            if (view == null || neighbor == null) return;
            Vector3 aim = neighbor.transform.position + Vector3.up * 1.6f - view.transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) return;
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(flat), k);
            if (viewController == null) return;
            float pitch = -Mathf.Asin(Mathf.Clamp(aim.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float current = view.transform.localEulerAngles.x;
            if (current > 180f) current -= 360f;
            viewController.SetLookPitch(Mathf.Lerp(current, pitch, k));
        }

        private IEnumerator Say(string speaker, string line)
        {
            if (!running || ThoughtManager.Instance == null) yield break;
            bool finished = false;
            ThoughtManager.Instance.ShowDialogue(speaker, new[] { line }, () => finished = true);
            while (running && !finished) yield return null;
        }

        private IEnumerator Leave(int firstWaypoint = 0)
        {
            if (departurePath == null) yield break;
            float pathLeft = 0f;
            Vector3 pathPoint = neighbor != null ? neighbor.transform.position : visitPoint.position;
            for (int i = firstWaypoint; i < departurePath.Length; i++)
            {
                if (departurePath[i] == null) continue;
                pathLeft += Vector3.Distance(pathPoint, departurePath[i].position);
                pathPoint = departurePath[i].position;
            }
            SetWalking(true);
            for (int i = firstWaypoint; i < departurePath.Length; i++)
            {
                Transform waypoint = departurePath[i];
                if (waypoint == null) continue;
                while (running && neighbor != null && (neighbor.transform.position - waypoint.position).sqrMagnitude > 0.01f)
                {
                    if (Playable())
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(waypoint.position - neighbor.transform.position, Vector3.up);
                        if (direction.sqrMagnitude > 0.001f)
                            neighbor.transform.rotation = Quaternion.RotateTowards(neighbor.transform.rotation, Quaternion.LookRotation(direction), 180f * Time.deltaTime);
                        neighbor.transform.position = Vector3.MoveTowards(neighbor.transform.position, waypoint.position, walkingSpeed * Time.deltaTime);
                        pathLeft -= walkingSpeed * Time.deltaTime;
                        if (pathLeft <= BedQuestLeadSeconds * walkingSpeed) AskToReturnToBed();
                    }
                    yield return null;
                }
            }
            if (neighbor != null) Destroy(neighbor);
            neighbor = null;
        }

        private void SetWalking(bool walking)
        {
            Animator animator = neighbor != null ? neighbor.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(walkingBoolParameter)) return;
            int hash = Animator.StringToHash(walkingBoolParameter);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(hash, walking);
                    return;
                }
        }

        private void AskToReturnToBed()
        {
            if (waitingForBed) return;
            waitingForBed = true;
            bedReached = false;
            GameEvents.OnBedTriggerReached += OnBedReached;
            QuestManager.Instance?.CreateQuest("Вернуться в кровать", 1, questTag: "neighbor-return-bed");
        }

        private void OnBedReached()
        {
            if (waitingForBed && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("neighbor-return-bed"))
                bedReached = true;
        }

        private void RestoreDoor()
        {
            if (!lockCaptured) return;
            if (frontDoor != null) frontDoor.SetInteractionLocked(doorWasLocked);
            lockCaptured = false;
        }

        private void Cleanup()
        {
            GameEvents.OnBedTriggerReached -= OnBedReached;
            waitingForBed = false;
            running = false;
            ReleasePlayerView();
            if (silhouetteActive) EndSilhouette();
            atDoor = atVisit = true;
            knockSoon = false;
            knockIK = null;
            steppingBack = false;
            stayIndoors?.Invoke(false);
            RestoreDoor();
            if (porchStateCaptured && porchLightSwitch != null)
            {
                if (porchWasOn) porchLightSwitch.ForceTurnOn();
                else porchLightSwitch.ForceTurnOff();
            }
            porchStateCaptured = false;
            if (neighbor != null) Destroy(neighbor);
            neighbor = null;
            if (knockSource != null) Destroy(knockSource.gameObject);
            knockSource = null;
        }

        private void Update()
        {
            HorrorMomentDirector.SetAudioPaused(knockSource, !Playable(), ref audioPaused);
        }

        private void OnDisable() => Cleanup();
    }
}
