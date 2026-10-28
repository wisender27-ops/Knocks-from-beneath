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
        // Ниши у южной стены блока D — укрытия (B-05): присел внутри и молчи, монстр тебя не увидит.
        public static readonly Rect NookARect = Rect.MinMaxRect(-27.8f, 94.4f, -26.2f, 96.2f);
        public static readonly Rect NookBRect = Rect.MinMaxRect(-24.6f, 94.4f, -23.0f, 96.2f);

        public static readonly BasementRoom CellBlockD = new BasementRoom("D", -32f, -23f, 96f, 103f)
            .Open(BasementSide.North, PassageFrom, PassageTo)
            .Open(BasementSide.South, NookARect.xMin, NookARect.xMax)
            .Open(BasementSide.South, NookBRect.xMin, NookBRect.xMax);
        public static readonly BasementRoom NookA = new BasementRoom("E", NookARect.xMin, NookARect.xMax, 94.4f, 96f)
            .OpenSide(BasementSide.North);
        public static readonly BasementRoom NookB = new BasementRoom("F", NookBRect.xMin, NookBRect.xMax, 94.4f, 96f)
            .OpenSide(BasementSide.North);

        // Цепочка комнат A–B–C–D: по ней строится маршрут монстра.
        public static readonly BasementRoom[] Rooms = { CorridorA, HallB, CorridorC, CellBlockD };
        // Всё, что нужно построить: цепочка и ниши.
        public static readonly BasementRoom[] AllRooms = { CorridorA, HallB, CorridorC, CellBlockD, NookA, NookB };

        // Игрок в нише: при присиде монстр не видит его (и всё равно слышит голос и шаги).
        public static bool IsInNook(Vector2 point) => NookARect.Contains(point) || NookBRect.Contains(point);

        // Обход блока D после удара по замку: у замка, у каждой ниши (лицом к ней), по центру и к выходу.
        public static readonly Vector2[] SearchRoute =
        {
            new Vector2(-29.4f, 100.8f), new Vector2(-27.0f, 97.9f), new Vector2(-23.9f, 97.9f),
            new Vector2(-25.8f, 100.9f), new Vector2(-28.15f, 102.4f)
        };
        public static readonly Vector2[] SearchFacing =
        {
            new Vector2(-1f, -0.4f), Vector2.down, Vector2.down, Vector2.zero, Vector2.zero
        };

        // Труба, по которой стучит хозяин: из камеры под потолком в дом. Точки (x, z), высота — под потолком.
        public const float PipeY = CeilingY - 0.3f;
        public static readonly Vector2[] PipeRoute =
        {
            new Vector2(-30.6f, 98.4f), new Vector2(-30.6f, 102.6f), new Vector2(-28.15f, 102.6f),
            new Vector2(-28.15f, 116.2f), new Vector2(-37.4f, 116.2f)
        };

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

        public enum ClutterKind { Crate, Barrel, Planks, Rubble, Mattress, Shelf, Bucket, Blanket }

        public struct Clutter
        {
            public ClutterKind Kind;
            public Vector2 Position;   // центр на полу
            public Vector3 Size;       // габариты по осям мира до поворота
            public float Yaw;
            public bool Blocks;        // мешает ходьбе (учитывается проверкой проходов)
        }

        private static Clutter C(ClutterKind kind, float x, float z, float sx, float sy, float sz, float yaw = 0f, bool blocks = true) =>
            new Clutter { Kind = kind, Position = new Vector2(x, z), Size = new Vector3(sx, sy, sz), Yaw = yaw, Blocks = blocks };

        // Подвальный хлам. Проходы, точки обхода монстра и подходы к замку/нишам свободны (см. тесты).
        public static readonly Clutter[] Junk =
        {
            // Зал B
            C(ClutterKind.Shelf, -24.35f, 116.65f, 0.5f, 2.0f, 2.3f),
            C(ClutterKind.Barrel, -31.5f, 110.8f, 0.6f, 0.9f, 0.6f), C(ClutterKind.Barrel, -30.85f, 110.55f, 0.6f, 0.9f, 0.6f),
            C(ClutterKind.Barrel, -31.6f, 111.55f, 0.6f, 0.9f, 0.6f, 20f),
            C(ClutterKind.Planks, -24.6f, 114.0f, 1.2f, 0.4f, 1.2f, 12f),
            C(ClutterKind.Rubble, -31.4f, 118.6f, 0.9f, 0.5f, 0.9f, 30f),
            C(ClutterKind.Mattress, -28.3f, 121.4f, 1.8f, 0.25f, 0.8f, -8f),
            // Коридор C
            C(ClutterKind.Bucket, -28.65f, 105.2f, 0.35f, 0.35f, 0.35f),
            // Блок D
            C(ClutterKind.Shelf, -30.6f, 102.7f, 2.4f, 1.9f, 0.5f),
            C(ClutterKind.Barrel, -23.6f, 102.3f, 0.6f, 0.9f, 0.6f), C(ClutterKind.Barrel, -23.55f, 101.55f, 0.6f, 0.9f, 0.6f, 40f),
            C(ClutterKind.Crate, -23.7f, 100.2f, 0.9f, 0.9f, 0.9f, 8f),
            // Ящики у ниш прикрывают их с одной стороны
            C(ClutterKind.Crate, -28.4f, 96.8f, 0.8f, 1.1f, 0.8f, 6f), C(ClutterKind.Crate, -25.4f, 96.7f, 0.9f, 1.2f, 0.8f, -4f),
            // В нишах и в камере
            C(ClutterKind.Crate, -27.55f, 94.9f, 0.4f, 0.5f, 0.4f, 0f, false), C(ClutterKind.Barrel, -23.3f, 94.8f, 0.5f, 0.8f, 0.5f, 0f, false),
            C(ClutterKind.Blanket, -31.2f, 97.0f, 1.2f, 0.07f, 0.8f, 14f, false), C(ClutterKind.Bucket, -29.9f, 96.5f, 0.35f, 0.35f, 0.35f, 0f, false)
        };

        // Точки, которые хлам не должен перекрывать: обход монстра, проёмы, подход к замку, устья ниш.
        public static IEnumerable<Vector2> KeepClear()
        {
            foreach (Vector2 point in Patrol) yield return point;
            foreach (Vector2 point in SearchRoute) yield return point;
            yield return new Vector2(-32f, 0.5f * (EntranceFrom + EntranceTo));
            yield return new Vector2(0.5f * (PassageFrom + PassageTo), 110f);
            yield return new Vector2(0.5f * (PassageFrom + PassageTo), 103f);
            yield return new Vector2(CellApproach.x, CellApproach.z);
            yield return new Vector2(NookARect.center.x, 95.4f);
            yield return new Vector2(NookBRect.center.x, 95.4f);
            yield return new Vector2(NookARect.center.x, 97.3f);
            yield return new Vector2(NookBRect.center.x, 97.3f);
        }

        public static bool BlockedByJunk(Vector2 point, float radius)
        {
            foreach (Clutter item in Junk)
            {
                if (!item.Blocks) continue;
                float halfX = item.Size.x * 0.5f + radius, halfZ = item.Size.z * 0.5f + radius;
                if (Mathf.Abs(point.x - item.Position.x) <= halfX && Mathf.Abs(point.y - item.Position.y) <= halfZ) return true;
            }
            return false;
        }

        // Колонны и ящики зала B: укрытия и препятствия.
        public static readonly Prop[] HallProps =
        {
            new Prop { Center = new Vector3(-27.0f, FloorY + 1.425f, 114.4f), Size = new Vector3(0.6f, 2.85f, 0.6f) },
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
