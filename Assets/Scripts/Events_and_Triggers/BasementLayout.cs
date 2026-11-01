using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // P-03: план подвала в мировых координатах, в пятне дома (x −46.75…−33.44, z 104.88…118.92; см. HouseFootprint).
    // Существующая комната (Basement_Reference) занимает x −47.3…−37.7, z 112.4…118.4, пол −4.35. Остальное огибает её
    // буквой U: короткий проход A, зал B под гаражом, переход C и блок D с камерой и нишами под кухней. Блок D стоит
    // внутри выемки в грунте: грань меша grass_soil идёт по x −46.3/−38.3 при z ≤ 109.9 и по z 105.4, поэтому C идёт севернее.
    // Чистые данные и геометрия, без объектов сцены.
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

        // Пятно дома (перекрытие первого этажа и гараж) и допуск «подвал чуть больше дома».
        public static readonly Rect HouseFootprint = Rect.MinMaxRect(-46.75f, 104.88f, -33.44f, 118.92f);
        public const float FootprintMargin = 1.6f;

        // Проём в восточной стене существующей комнаты (LeftWall, x −37.7) ведёт в проход A.
        public const float ExistingWallX = -37.7f;
        public const float ExistingRoomZMin = 112.4f, ExistingRoomZMax = 118.4f;
        public const float EntranceFrom = 115.45f, EntranceTo = 116.95f;
        public const float PassageFrom = 110.5f, PassageTo = 111.9f;
        public const float SouthRoomsZMin = 105.9f;

        // Существующая комната с лестницей: в цепочке комнат считается концом прохода A.
        public static readonly Rect ExistingRoom = Rect.MinMaxRect(-47.3f, ExistingRoomZMin, ExistingWallX, 122f);

        public static readonly BasementRoom CorridorA = new BasementRoom("A", ExistingWallX, -36.9f, EntranceFrom, EntranceTo)
            .OpenSide(BasementSide.West).OpenSide(BasementSide.East);
        public static readonly BasementRoom HallB = new BasementRoom("B", -36.9f, -32.2f, 106.5f, 118.4f)
            .Open(BasementSide.West, EntranceFrom, EntranceTo).Open(BasementSide.West, PassageFrom, PassageTo);
        public static readonly BasementRoom CorridorC = new BasementRoom("C", -38.8f, -36.9f, PassageFrom, PassageTo)
            .OpenSide(BasementSide.East).OpenSide(BasementSide.West);
        // Камера хозяина: северная сторона — решётка с замком, остальные — стены.
        public static readonly Rect CellArea = Rect.MinMaxRect(-45.7f, SouthRoomsZMin, -43.4f, 108.2f);
        // Ниши у южной стены блока D — укрытия (B-05): присел внутри и молчи, монстр тебя не увидит.
        public static readonly Rect NookARect = Rect.MinMaxRect(-42.6f, SouthRoomsZMin, -41.0f, 107.8f);
        public static readonly Rect NookBRect = Rect.MinMaxRect(-40.4f, SouthRoomsZMin, -38.8f, 107.8f);

        public static readonly BasementRoom CellBlockD = new BasementRoom("D", -45.7f, -38.8f, 107.6f, PassageTo)
            .Open(BasementSide.East, PassageFrom, PassageTo)
            .Open(BasementSide.South, CellArea.xMin, CellArea.xMax)
            .Open(BasementSide.South, NookARect.xMin, NookARect.xMax)
            .Open(BasementSide.South, NookBRect.xMin, NookBRect.xMax);
        public static readonly BasementRoom CellRoom = new BasementRoom("G", CellArea.xMin, CellArea.xMax, SouthRoomsZMin, 107.6f)
            .OpenSide(BasementSide.North);
        public static readonly BasementRoom NookA = new BasementRoom("E", NookARect.xMin, NookARect.xMax, SouthRoomsZMin, 107.6f)
            .OpenSide(BasementSide.North);
        public static readonly BasementRoom NookB = new BasementRoom("F", NookBRect.xMin, NookBRect.xMax, SouthRoomsZMin, 107.6f)
            .OpenSide(BasementSide.North);

        // Цепочка комнат A–B–C–D: по ней строится маршрут монстра.
        public static readonly BasementRoom[] Rooms = { CorridorA, HallB, CorridorC, CellBlockD };
        // Всё, что нужно построить: цепочка, камера и ниши.
        public static readonly BasementRoom[] AllRooms = { CorridorA, HallB, CorridorC, CellBlockD, CellRoom, NookA, NookB };

        // Игрок в нише: при присиде монстр не видит его (и всё равно слышит голос и шаги).
        public static bool IsInNook(Vector2 point) => NookARect.Contains(point) || NookBRect.Contains(point);

        // Обход блока D после удара по замку: у замка, у каждой ниши (лицом к ней), по центру и к выходу.
        public static readonly Vector2[] SearchRoute =
        {
            new Vector2(-43.9f, 109.6f), new Vector2(-41.8f, 109.5f), new Vector2(-40.2f, 109.4f),
            new Vector2(-41.3f, 110.7f), new Vector2(-39.4f, 111.2f)
        };
        public static readonly Vector2[] SearchFacing =
        {
            new Vector2(-0.4f, -1f), Vector2.down, Vector2.down, Vector2.zero, Vector2.zero
        };

        // Труба, по которой стучит хозяин: из камеры под потолком в дом. Точки (x, z), высота — под потолком.
        public const float PipeY = CeilingY - 0.3f;
        public static readonly Vector2[] PipeRoute =
        {
            new Vector2(-44.6f, 107.4f), new Vector2(-44.6f, 111.2f), new Vector2(-36.2f, 111.2f),
            new Vector2(-36.2f, 116.2f), new Vector2(-37.4f, 116.2f)
        };

        public static readonly Vector3 CellLock = new Vector3(-43.75f, FloorY + 1.0f, CellArea.yMax);
        public static readonly Vector3 OwnerPosition = new Vector3(-44.7f, FloorY, 107.0f);
        public static readonly Vector3 CellApproach = new Vector3(-44.4f, FloorY, 109.7f);
        public static readonly Vector3 CellLampPosition = new Vector3(-43.4f, CeilingY - 0.4f, 110.0f);
        // Старая камера хозяина на кронштейне у северной стены (C-04): смотрит на решётку.
        public static readonly Vector3 TapeCameraPosition = new Vector3(-42.0f, FloorY + 2.05f, 111.6f);
        public static readonly Vector3 TapeCameraLook = new Vector3(-44.6f, FloorY + 0.9f, 107.4f);

        // Укрытие за ящиками в нише A: ящики у устья, игрок внутри, выход — перед нишей.
        public static readonly Vector3 HideCrateCenter = new Vector3(NookARect.center.x, FloorY + 0.85f, 107.85f);
        public static readonly Vector3 HideSpot = new Vector3(NookARect.center.x, FloorY, 106.95f);
        public static readonly Vector3 HideExit = new Vector3(NookARect.center.x, FloorY, 108.9f);
        public static readonly Vector3 HideRespawn = new Vector3(-40.6f, FloorY, 110.6f);
        public static readonly Vector3 HideRespawnLook = new Vector3(-45f, 0f, 109f);
        public static readonly Vector3 DistractionPoint = new Vector3(-37.85f, FloorY, 111.2f);

        // Игрок появляется у подножия лестницы существующей комнаты лицом к проходу.
        public static readonly Vector3 PlayerStart = new Vector3(-44.8f, FloorY + 0.1f, 115.4f);
        public static readonly Vector3 PlayerStartLook = new Vector3(ExistingWallX, 0f, 116.2f);

        // Монстр обходит зал B вокруг колонн, у западной стены остаётся полоса, чтобы прокрасться к переходу.
        // Остальные комнаты — его «слепая зона»: он идёт в них только на звук.
        public static readonly Vector2[] Patrol =
        {
            new Vector2(-34.9f, 108.6f), new Vector2(-33.1f, 108.6f), new Vector2(-33.1f, 116.4f), new Vector2(-34.9f, 116.4f)
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
            C(ClutterKind.Shelf, -32.45f, 112.4f, 0.5f, 2.0f, 2.3f),
            C(ClutterKind.Barrel, -36.45f, 107.0f, 0.6f, 0.9f, 0.6f), C(ClutterKind.Barrel, -35.8f, 106.95f, 0.6f, 0.9f, 0.6f),
            C(ClutterKind.Barrel, -36.4f, 107.7f, 0.6f, 0.9f, 0.6f, 20f),
            C(ClutterKind.Planks, -32.75f, 109.9f, 0.8f, 0.4f, 1.2f, 12f),
            C(ClutterKind.Rubble, -36.4f, 113.4f, 0.7f, 0.5f, 0.9f, 30f),
            // Переход C
            C(ClutterKind.Bucket, -37.9f, 110.75f, 0.35f, 0.35f, 0.35f),
            // Блок D
            C(ClutterKind.Shelf, -43.6f, 111.6f, 2.4f, 1.9f, 0.5f),
            C(ClutterKind.Barrel, -45.25f, 111.4f, 0.6f, 0.9f, 0.6f), C(ClutterKind.Barrel, -45.2f, 110.65f, 0.6f, 0.9f, 0.6f, 40f),
            C(ClutterKind.Crate, -39.3f, 109.85f, 0.9f, 0.9f, 0.9f, 8f),
            C(ClutterKind.Mattress, -40.8f, 111.5f, 1.8f, 0.25f, 0.8f, -8f),
            // Ящики у ниш прикрывают их с одной стороны
            C(ClutterKind.Crate, -43.0f, 108.0f, 0.6f, 1.0f, 0.6f, 6f), C(ClutterKind.Crate, -40.7f, 107.95f, 0.6f, 1.1f, 0.5f, -4f),
            // В нишах и в камере
            C(ClutterKind.Crate, -42.25f, 106.35f, 0.4f, 0.5f, 0.4f, 0f, false), C(ClutterKind.Barrel, -39.1f, 106.25f, 0.5f, 0.8f, 0.5f, 0f, false),
            C(ClutterKind.Blanket, -45.1f, 106.8f, 1.2f, 0.07f, 0.8f, 14f, false), C(ClutterKind.Bucket, -43.8f, 106.3f, 0.35f, 0.35f, 0.35f, 0f, false)
        };

        // Точки, которые хлам не должен перекрывать: обход монстра, проёмы, подход к замку, устья ниш.
        public static IEnumerable<Vector2> KeepClear()
        {
            foreach (Vector2 point in Patrol) yield return point;
            foreach (Vector2 point in SearchRoute) yield return point;
            yield return new Vector2(HallB.XMin, 0.5f * (EntranceFrom + EntranceTo));
            yield return new Vector2(HallB.XMin, 0.5f * (PassageFrom + PassageTo));
            yield return new Vector2(CellBlockD.XMax, 0.5f * (PassageFrom + PassageTo));
            yield return new Vector2(CellApproach.x, CellApproach.z);
            yield return new Vector2(NookARect.center.x, HideSpot.z);
            yield return new Vector2(NookBRect.center.x, HideSpot.z);
            yield return new Vector2(NookARect.center.x, HideExit.z);
            yield return new Vector2(NookBRect.center.x, HideExit.z);
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
            new Prop { Center = new Vector3(-34.0f, FloorY + 1.425f, 110.6f), Size = new Vector3(0.6f, 2.85f, 0.6f) },
            new Prop { Center = new Vector3(-34.0f, FloorY + 1.425f, 114.2f), Size = new Vector3(0.6f, 2.85f, 0.6f) },
            new Prop { Center = new Vector3(-33.0f, FloorY + 0.5f, 107.4f), Size = new Vector3(1.2f, 1.0f, 1.2f) },
            new Prop { Center = new Vector3(-33.0f, FloorY + 1.4f, 117.6f), Size = new Vector3(1.1f, 1.8f, 1.1f), Cylinder = true }
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
