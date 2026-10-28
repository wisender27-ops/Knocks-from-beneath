using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // P-03: план продолжения подвала в мировых координатах. Существующая комната подвала (Basement_Reference)
    // занимает x −47.3…−37.7, z 112.4…118.4, пол −4.35. За её восточной стеной под землёй пусто: сюда уходит проход
    // (коридор A) в зал B, дальше коридор C и камера хозяина в блоке D. Чистые данные и геометрия, без объектов сцены.
    public enum BasementSide { West, East, South, North }

    public readonly struct BasementOpening
    {
        public readonly float From, To;
        public BasementOpening(float from, float to) { From = from; To = to; }
    }

    public sealed class BasementRoom
    {
        public readonly string Name;
        public readonly float XMin, XMax, ZMin, ZMax;
        private readonly Dictionary<BasementSide, List<BasementOpening>> openings = new Dictionary<BasementSide, List<BasementOpening>>();

        public BasementRoom(string name, float xMin, float xMax, float zMin, float zMax)
        {
            Name = name; XMin = xMin; XMax = xMax; ZMin = zMin; ZMax = zMax;
        }

        // Полностью открытая сторона — проём во всю ширину (концы коридоров).
        public BasementRoom OpenSide(BasementSide side)
        {
            bool alongX = side == BasementSide.South || side == BasementSide.North;
            return Open(side, alongX ? XMin : ZMin, alongX ? XMax : ZMax);
        }

        public BasementRoom Open(BasementSide side, float from, float to)
        {
            if (!openings.TryGetValue(side, out var list)) openings[side] = list = new List<BasementOpening>();
            list.Add(new BasementOpening(Mathf.Min(from, to), Mathf.Max(from, to)));
            return this;
        }

        public IReadOnlyList<BasementOpening> Openings(BasementSide side) =>
            openings.TryGetValue(side, out var list) ? (IReadOnlyList<BasementOpening>)list : System.Array.Empty<BasementOpening>();

        // Сторона открыта на всю длину: стены нет, соседние стены не нужно удлинять на толщину.
        public bool IsFullyOpen(BasementSide side)
        {
            bool alongX = side == BasementSide.South || side == BasementSide.North;
            float from = alongX ? XMin : ZMin, to = alongX ? XMax : ZMax;
            foreach (BasementOpening gap in Openings(side))
                if (gap.From <= from + 0.001f && gap.To >= to - 0.001f) return true;
            return false;
        }

        public bool Contains(Vector2 point, float margin = 0f) =>
            point.x >= XMin + margin && point.x <= XMax - margin && point.y >= ZMin + margin && point.y <= ZMax - margin;
    }

    public readonly struct BasementBox
    {
        public readonly Vector3 Center, Size;
        public BasementBox(Vector3 center, Vector3 size) { Center = center; Size = size; }
    }

    public static class BasementLayout
    {
        public const float FloorY = -4.35f;
        public const float CeilingY = -1.5f;
        public const float WallThickness = 0.3f;
        public const float DoorHeight = 2.3f;

        // Проём в восточной стене существующей комнаты (LeftWall, x −37.7) ведёт в коридор A.
        public const float ExistingWallX = -37.7f;
        public const float ExistingRoomZMin = 112.4f, ExistingRoomZMax = 118.4f;
        public const float EntranceFrom = 115.45f, EntranceTo = 116.95f;
        public const float PassageFrom = -28.9f, PassageTo = -27.4f;

        public static readonly BasementRoom CorridorA = new BasementRoom("A", -37.7f, -32f, EntranceFrom, EntranceTo)
            .OpenSide(BasementSide.West).OpenSide(BasementSide.East);
        public static readonly BasementRoom HallB = new BasementRoom("B", -32f, -24f, 110f, 122f)
            .Open(BasementSide.West, EntranceFrom, EntranceTo).Open(BasementSide.South, PassageFrom, PassageTo);
        public static readonly BasementRoom CorridorC = new BasementRoom("C", PassageFrom, PassageTo, 103f, 110f)
            .OpenSide(BasementSide.North).OpenSide(BasementSide.South);
        public static readonly BasementRoom CellBlockD = new BasementRoom("D", -32f, -23f, 96f, 103f)
            .Open(BasementSide.North, PassageFrom, PassageTo);

        public static readonly BasementRoom[] Rooms = { CorridorA, HallB, CorridorC, CellBlockD };

        // Камера хозяина: северная сторона — решётка с замком, остальные — стены.
        public static readonly Rect CellArea = Rect.MinMaxRect(-32f, 96f, -29.4f, 99f);
        public static readonly Vector3 CellLock = new Vector3(-29.75f, FloorY + 1.0f, 99f);
        public static readonly Vector3 OwnerPosition = new Vector3(-31.0f, FloorY, 97.6f);
        public static readonly Vector3 CellApproach = new Vector3(-30.4f, FloorY, 100.4f);

        // Игрок появляется у подножия лестницы существующей комнаты лицом к проходу.
        public static readonly Vector3 PlayerStart = new Vector3(-44.8f, FloorY + 0.1f, 115.4f);
        public static readonly Vector3 PlayerStartLook = new Vector3(-37.7f, 0f, 116.2f);

        // Монстр обходит зал B по периметру. Остальные комнаты — его «слепая зона»: он идёт в них только на звук.
        public static readonly Vector2[] Patrol =
        {
            new Vector2(-30.3f, 112.6f), new Vector2(-25.7f, 112.6f), new Vector2(-25.7f, 120.2f), new Vector2(-30.3f, 120.2f)
        };

        public struct Prop
        {
            public Vector3 Center, Size;
            public bool Cylinder;
        }

        // Колонны и ящики зала B: укрытия и препятствия.
        public static readonly Prop[] HallProps =
        {
            new Prop { Center = new Vector3(-28.2f, FloorY + 1.425f, 114.4f), Size = new Vector3(0.6f, 2.85f, 0.6f) },
            new Prop { Center = new Vector3(-27.8f, FloorY + 1.425f, 118.4f), Size = new Vector3(0.6f, 2.85f, 0.6f) },
            new Prop { Center = new Vector3(-24.9f, FloorY + 0.5f, 110.9f), Size = new Vector3(1.2f, 1.0f, 1.2f) },
            new Prop { Center = new Vector3(-31.1f, FloorY + 0.4f, 121.4f), Size = new Vector3(1.4f, 0.8f, 1.0f) },
            new Prop { Center = new Vector3(-24.6f, FloorY + 1.4f, 121.3f), Size = new Vector3(1.1f, 1.8f, 1.1f), Cylinder = true }
        };

        // Стены по сторонам комнаты с вырезами под проёмы + перемычки над ними.
        public static List<BasementBox> Walls(BasementRoom room)
        {
            var result = new List<BasementBox>();
            float height = CeilingY - FloorY;
            float t = WallThickness;
            foreach (BasementSide side in new[] { BasementSide.West, BasementSide.East, BasementSide.South, BasementSide.North })
            {
                if (room.IsFullyOpen(side)) continue;   // конец коридора: стены нет, перемычку даёт соседняя комната
                bool alongX = side == BasementSide.South || side == BasementSide.North;
                float from = alongX ? room.XMin - (room.IsFullyOpen(BasementSide.West) ? 0f : t) : room.ZMin;
                float to = alongX ? room.XMax + (room.IsFullyOpen(BasementSide.East) ? 0f : t) : room.ZMax;
                float fixedAxis = side switch
                {
                    BasementSide.West => room.XMin - t * 0.5f,
                    BasementSide.East => room.XMax + t * 0.5f,
                    BasementSide.South => room.ZMin - t * 0.5f,
                    _ => room.ZMax + t * 0.5f
                };
                var gaps = new List<BasementOpening>(room.Openings(side));
                gaps.Sort((a, b) => a.From.CompareTo(b.From));
                float cursor = from;
                foreach (BasementOpening gap in gaps)
                {
                    float gapFrom = Mathf.Max(gap.From, from), gapTo = Mathf.Min(gap.To, to);
                    if (gapTo <= gapFrom) continue;
                    AddSegment(result, alongX, fixedAxis, cursor, gapFrom, FloorY, height);
                    // Перемычка над проёмом.
                    AddSegment(result, alongX, fixedAxis, gapFrom, gapTo, FloorY + DoorHeight, height - DoorHeight);
                    cursor = gapTo;
                }
                AddSegment(result, alongX, fixedAxis, cursor, to, FloorY, height);
            }
            return result;
        }

        private static void AddSegment(List<BasementBox> list, bool alongX, float fixedAxis, float from, float to, float baseY, float height)
        {
            float length = to - from;
            if (length < 0.02f || height < 0.02f) return;
            float mid = (from + to) * 0.5f;
            float y = baseY + height * 0.5f;
            Vector3 center = alongX ? new Vector3(mid, y, fixedAxis) : new Vector3(fixedAxis, y, mid);
            Vector3 size = alongX ? new Vector3(length, height, WallThickness) : new Vector3(WallThickness, height, length);
            list.Add(new BasementBox(center, size));
        }

        public static bool InsideAnyRoom(Vector2 point, float margin = 0f)
        {
            foreach (BasementRoom room in Rooms)
                if (room.Contains(point, margin)) return true;
            return false;
        }

        public static bool BlockedByProp(Vector2 point, float radius)
        {
            foreach (Prop prop in HallProps)
            {
                float halfX = prop.Size.x * 0.5f + radius, halfZ = prop.Size.z * 0.5f + radius;
                if (Mathf.Abs(point.x - prop.Center.x) <= halfX && Mathf.Abs(point.y - prop.Center.z) <= halfZ) return true;
            }
            return false;
        }
    }
}
