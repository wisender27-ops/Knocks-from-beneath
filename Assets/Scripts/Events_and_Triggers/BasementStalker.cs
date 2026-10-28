using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // P-03: логика монстра подвала без сцены. Он обходит зал B и ловит игрока на слух (KnockTracker из K-01):
    // присед и медленный шаг не слышны, ходьба — через пару секунд, бег — почти сразу. Заметил — идёт к месту,
    // где слышал игрока (через проёмы, не сквозь стены), ничего не нашёл — ищет и возвращается на обход.
    public sealed class BasementStalkerBrain
    {
        public enum Mode { Patrol, Investigate, Search, RoomSearch }

        public const float PatrolSpeed = 1.1f;
        public const float HuntSpeed = 2.3f;
        public const float CatchDistance = 1.35f;
        public const float CloseSightDistance = 2.2f;
        public const float WalkHearRange = 9f;
        public const float RunHearRange = 15f;
        public const float SearchSeconds = 4f;
        public const float CornerPause = 1.2f;
        private const float Arrive = 0.45f;

        private static readonly Vector2 DoorAB = new Vector2(-32f, 0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo));
        private static readonly Vector2 DoorBC = new Vector2(0.5f * (BasementLayout.PassageFrom + BasementLayout.PassageTo), 110f);
        private static readonly Vector2 DoorCD = new Vector2(DoorBC.x, 103f);

        private readonly KnockTracker tracker = new KnockTracker
        {
            WalkHearSeconds = 1.4f, RunHearSeconds = 0.4f, ForgetSeconds = 3f
        };
        public const float RoomSearchPause = 2.2f;

        private int waypoint, routeIndex;
        private float pause, searchLeft;
        private Vector2[] route, routeFacing;
        private bool routeArrived;

        // Обход комнаты после шума (удар по замку) закончен: монстр прошёл все точки и ушёл на свой обход.
        public bool SearchCompleted { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Heading { get; private set; } = Vector2.up;
        public Mode CurrentMode { get; private set; }
        public Vector2 Target { get; private set; }
        public bool Moving { get; private set; }
        public bool CaughtPlayer { get; private set; }
        public float Attention => tracker.Attention;

        public void Reset(Vector2 position)
        {
            Position = position;
            tracker.Reset(position);
            CurrentMode = Mode.Patrol;
            waypoint = NearestWaypoint(position);
            pause = searchLeft = 0f;
            route = routeFacing = null;
            routeArrived = false;
            SearchCompleted = false;
            CaughtPlayer = false;
            Moving = false;
        }

        // Шум не от шагов (лом по замку): монстр узнаёт, где игрок.
        public void Alert(Vector2 point)
        {
            tracker.Hear(point);
            CurrentMode = Mode.Investigate;
            Target = point;
        }

        // Шум в комнате: монстр идёт на звук, а дойдя, обходит точки маршрута (facing — куда смотреть на точке)
        // и уходит. Игрок, который прячется и молчит, переживает обход.
        public void AlertAndSearch(Vector2 point, Vector2[] searchRoute, Vector2[] searchFacing)
        {
            Alert(point);
            route = searchRoute;
            routeFacing = searchFacing;
            routeIndex = 0;
            routeArrived = false;
            SearchCompleted = false;
        }

        public void Step(Vector2 player, KnockNoise noise, bool playerInSight, float dt)
        {
            if (dt <= 0f || CaughtPlayer) return;
            float distance = Vector2.Distance(Position, player);
            if (noise == KnockNoise.Walk && distance > WalkHearRange) noise = KnockNoise.Quiet;
            if (noise == KnockNoise.Run && distance > RunHearRange) noise = KnockNoise.Quiet;
            if (distance <= CloseSightDistance && playerInSight) tracker.Hear(player);
            tracker.Step(player, noise, dt);

            if (tracker.HasPlayer)
            {
                CurrentMode = Mode.Investigate;
                Target = tracker.KnownTarget;
                searchLeft = SearchSeconds;
            }
            if (distance <= CatchDistance && (CurrentMode != Mode.Patrol || distance <= CatchDistance * 0.6f))
            {
                CaughtPlayer = true;
                Moving = false;
                return;
            }

            switch (CurrentMode)
            {
                case Mode.Investigate:
                    if (Vector2.Distance(Position, Target) <= Arrive)
                    {
                        CurrentMode = route != null ? Mode.RoomSearch : Mode.Search;
                        searchLeft = SearchSeconds;
                        pause = 0f;
                        routeArrived = false;
                        Moving = false;
                    }
                    else MoveToward(Target, HuntSpeed, dt);
                    break;
                case Mode.RoomSearch:
                    RoomSearch(dt);
                    break;
                case Mode.Search:
                    Moving = false;
                    searchLeft -= dt;
                    if (searchLeft <= 0f)
                    {
                        CurrentMode = Mode.Patrol;
                        waypoint = NearestWaypoint(Position);
                    }
                    break;
                default:
                    Patrol(dt);
                    break;
            }
        }

        private void RoomSearch(float dt)
        {
            if (route == null || routeIndex >= route.Length) { FinishRoomSearch(); return; }
            if (pause > 0f)
            {
                pause -= dt;
                Moving = false;
                if (pause <= 0f && routeArrived)
                {
                    routeArrived = false;
                    routeIndex++;
                    if (routeIndex >= route.Length) FinishRoomSearch();
                }
                return;
            }
            Vector2 goal = route[routeIndex];
            if (Vector2.Distance(Position, goal) <= Arrive)
            {
                routeArrived = true;
                pause = RoomSearchPause;
                Moving = false;
                if (routeFacing != null && routeIndex < routeFacing.Length && routeFacing[routeIndex].sqrMagnitude > 0.01f)
                    Heading = routeFacing[routeIndex].normalized;
                return;
            }
            MoveToward(goal, PatrolSpeed, dt);
        }

        private void FinishRoomSearch()
        {
            route = routeFacing = null;
            routeArrived = false;
            SearchCompleted = true;
            CurrentMode = Mode.Patrol;
            waypoint = NearestWaypoint(Position);
            Moving = false;
        }

        private void Patrol(float dt)
        {
            if (pause > 0f)
            {
                pause -= dt;
                Moving = false;
                return;
            }
            Vector2 goal = BasementLayout.Patrol[waypoint];
            if (Vector2.Distance(Position, goal) <= Arrive)
            {
                waypoint = (waypoint + 1) % BasementLayout.Patrol.Length;
                pause = CornerPause;
                Moving = false;
                return;
            }
            MoveToward(goal, PatrolSpeed, dt);
        }

        private void MoveToward(Vector2 goal, float speed, float dt)
        {
            Vector2 next = NextPoint(Position, goal);
            Vector2 delta = next - Position;
            float length = delta.magnitude;
            if (length < 0.001f) { Moving = false; return; }
            Heading = delta / length;
            Position += Heading * Mathf.Min(length, speed * dt);
            Moving = true;
        }

        private int NearestWaypoint(Vector2 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < BasementLayout.Patrol.Length; i++)
            {
                float d = Vector2.Distance(position, BasementLayout.Patrol[i]);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // 0 = коридор A, 1 = зал B, 2 = коридор C, 3 = блок D. Вне комнат — ближайшая по центру.
        public static int RoomIndex(Vector2 point)
        {
            for (int i = 0; i < BasementLayout.Rooms.Length; i++)
                if (BasementLayout.Rooms[i].Contains(point)) return i;
            int best = 1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < BasementLayout.Rooms.Length; i++)
            {
                BasementRoom room = BasementLayout.Rooms[i];
                var center = new Vector2((room.XMin + room.XMax) * 0.5f, (room.ZMin + room.ZMax) * 0.5f);
                float d = Vector2.Distance(point, center);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // Первая точка маршрута от from к to: цепочка комнат A–B–C–D, между ними — проёмы.
        public static Vector2 NextPoint(Vector2 from, Vector2 to)
        {
            int a = RoomIndex(from), b = RoomIndex(to);
            if (a == b) return to;
            int step = b > a ? 1 : -1;
            int edge = step > 0 ? a : a - 1;           // проём между комнатами edge и edge + 1
            Vector2 door = edge == 0 ? DoorAB : edge == 1 ? DoorBC : DoorCD;
            return Vector2.Distance(from, door) > 0.4f ? door : (step > 0 ? DoorBeyond(edge) : DoorBefore(edge));
        }

        // Выход за проём в следующую комнату, чтобы маршрут не упирался в саму точку проёма.
        private static Vector2 DoorBeyond(int edge) => edge == 0 ? DoorAB + Vector2.right * 0.8f
            : edge == 1 ? DoorBC + Vector2.down * 0.8f : DoorCD + Vector2.down * 0.8f;

        private static Vector2 DoorBefore(int edge) => edge == 0 ? DoorAB + Vector2.left * 0.8f
            : edge == 1 ? DoorBC + Vector2.up * 0.8f : DoorCD + Vector2.up * 0.8f;
    }

    // Исполнитель в сцене: двигает модель монстра по мозгу, слушает шаги игрока, шагает звуком, сообщает о поимке.
    public sealed class BasementStalker : MonoBehaviour
    {
        private const float StillSpeed = 0.15f;
        private const float SlowWalkMargin = 0.15f;
        private const float RunSpeedFactor = 1.25f;
        private const float TeleportDistance = 3f;
        private const float StrideMetres = 0.95f;

        private readonly BasementStalkerBrain brain = new BasementStalkerBrain();
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private Transform player;
        private PlayerController movement;
        private CornerStalkerRig rig;
        private AudioSource steps;
        private AudioClip[] stepClips;
        private Vector3 lastPlayer;
        private float strideLeft;
        private bool running, walkingAnimation;

        public event Action Caught;
        public BasementStalkerBrain Brain => brain;
        // Игрок сидит в нише и прячется: вблизи монстр его не видит (слух работает как раньше).
        public Func<bool> PlayerHidden { get; set; }
        public CornerStalkerRig Rig => rig;
        public Transform HeadTransform => rig != null ? rig.Head : null;
        public bool Active => running;

        public void Initialize(Transform playerTransform, GameObject monster)
        {
            player = playerTransform;
            movement = player != null ? player.GetComponent<PlayerController>() : null;
            rig = monster.GetComponent<CornerStalkerRig>();
            steps = AudioRouting.Create(monster.transform, "Basement monster steps", AudioBus.Horror, 1f, 24f);
            steps.minDistance = 2.5f;
            steps.volume = 0.85f;
            steps.pitch = 0.78f;
            stepClips = new[] { Resources.Load<AudioClip>("Audio/Horror/FollowingStep1"), Resources.Load<AudioClip>("Audio/Horror/FollowingStep2") };
            transform.position = new Vector3(BasementLayout.Patrol[0].x, BasementLayout.FloorY, BasementLayout.Patrol[0].y);
            brain.Reset(BasementLayout.Patrol[0]);
        }

        public void Begin()
        {
            running = true;
            lastPlayer = player != null ? player.position : Vector3.zero;
        }

        public void Stop()
        {
            running = false;
            rig?.Stand();
            walkingAnimation = false;
        }

        // После поимки: монстр возвращается на обход с другого края зала, внимание сброшено.
        public void ResetPatrol(int waypoint)
        {
            Vector2 start = BasementLayout.Patrol[Mathf.Clamp(waypoint, 0, BasementLayout.Patrol.Length - 1)];
            brain.Reset(start);
            transform.position = new Vector3(start.x, BasementLayout.FloorY, start.y);
        }

        public void Alert(Vector3 worldPoint) => brain.Alert(new Vector2(worldPoint.x, worldPoint.z));

        // Шум у замка: идти на звук, затем обойти блок D с остановками у ниш.
        public void AlertAndSearch(Vector3 worldPoint) => brain.AlertAndSearch(new Vector2(worldPoint.x, worldPoint.z),
            BasementLayout.SearchRoute, BasementLayout.SearchFacing);

        private void Update()
        {
            if (!running || player == null || Time.timeScale <= 0f || PauseMenuController.InputBlocked) return;
            float dt = Time.deltaTime;
            Vector3 position = player.position;
            brain.Step(new Vector2(position.x, position.z), ClassifyNoise(position, dt), InSight(position), dt);

            Vector2 at = brain.Position;
            transform.position = new Vector3(at.x, BasementLayout.FloorY, at.y);
            Vector2 heading = brain.Heading;
            if (heading.sqrMagnitude > 0.001f)
            {
                Quaternion face = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y));
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-7f * dt));
            }
            UpdateAnimation();
            UpdateSteps(dt);
            if (brain.CaughtPlayer)
            {
                running = false;
                Caught?.Invoke();
            }
        }

        private void UpdateAnimation()
        {
            if (rig == null || !rig.CanWalk) return;
            if (brain.Moving)
            {
                float speed = brain.CurrentMode == BasementStalkerBrain.Mode.Investigate
                    ? BasementStalkerBrain.HuntSpeed : BasementStalkerBrain.PatrolSpeed;
                rig.Walk(speed);
                walkingAnimation = true;
            }
            else if (walkingAnimation)
            {
                rig.Stand();
                walkingAnimation = false;
            }
        }

        private void UpdateSteps(float dt)
        {
            if (!brain.Moving || steps == null) return;
            float speed = brain.CurrentMode == BasementStalkerBrain.Mode.Investigate
                ? BasementStalkerBrain.HuntSpeed : BasementStalkerBrain.PatrolSpeed;
            strideLeft -= speed * dt;
            if (strideLeft > 0f) return;
            strideLeft = StrideMetres;
            AudioClip clip = stepClips[UnityEngine.Random.Range(0, stepClips.Length)];
            if (clip != null) steps.PlayOneShot(clip, 0.9f);
        }

        // Шум — по фактическому смещению игрока, как у стучащего (K-01): присед и медленный шаг тихи.
        private KnockNoise ClassifyNoise(Vector3 position, float dt)
        {
            Vector3 moved = Vector3.ProjectOnPlane(position - lastPlayer, Vector3.up);
            lastPlayer = position;
            if (movement == null || moved.sqrMagnitude > TeleportDistance * TeleportDistance || movement.IsCrouching)
                return KnockNoise.Quiet;
            float speed = moved.magnitude / dt;
            if (speed < StillSpeed || speed <= movement.crouchSpeed + SlowWalkMargin) return KnockNoise.Quiet;
            return movement.IsRunning || speed > movement.walkSpeed * RunSpeedFactor ? KnockNoise.Run : KnockNoise.Walk;
        }

        // Вблизи монстр видит игрока, если между ними нет стены: только в пределах своего поля зрения вперёд.
        private bool InSight(Vector3 position)
        {
            if (PlayerHidden != null && PlayerHidden()) return false;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 to = position + Vector3.up * 1.2f - eye;
            float distance = to.magnitude;
            if (distance > BasementStalkerBrain.CloseSightDistance || distance < 0.01f) return false;
            if (Vector3.Dot(transform.forward, to / distance) < 0.2f) return false;
            int count = Physics.RaycastNonAlloc(eye, to / distance, hits, distance, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform owner = hits[i].collider.transform;
                if (owner.IsChildOf(player) || owner.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}
