using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // План «низа»: то, чего игрок не видит в подвале. Комната под лестницей заканчивается замурованным проёмом,
    // в стене — квадратная вентиляционная труба, по ней ползут в технические тоннели далеко за пределами дома. Те выводят наверх уже за стену,
    // в отрезанную половину подвала (BasementLayout, блоки A–D), где сидит хозяин. Чистые данные, без объектов сцены.
    public static class DepthsLayout
    {
        // Комнаты тоннелей строятся теми же BasementRoom и стенами, что подвал, только на Drop ниже.
        public const float Drop = -7f;
        public const float FloorY = BasementLayout.FloorY + Drop;
        public const float CeilingY = BasementLayout.CeilingY + Drop;
        public const float DuctSize = 1.15f;     // ширина и высота трубы изнутри: присевший герой (1 м) проходит
        public const float DuctFloorY = -7.7f;   // между полом подвала и потолком тоннелей
        public const int CameraSlotNumber = 3; // номер места в StoryFlags.CameraPlacementSlot

        // Решётка у пола в восточной стене, рядом с замурованным проёмом. Точка — на стене, у пола, по центру решётки.
        public static readonly Vector3 Vent = new Vector3(-37.83f, BasementLayout.FloorY, 117.6f);
        // Штатив в подвале: смотрит наискось на решётку. Точка обзора плёнки — на высоте видоискателя.
        public static readonly Vector3 CameraSlot = new Vector3(-41.6f, BasementLayout.FloorY, 116.0f);
        public const float CameraSlotYaw = 68f;
        public static readonly Vector3 TapeCameraPosition = new Vector3(-41.6f, BasementLayout.FloorY + 1.3f, 116.0f);
        public static readonly Vector3 TapeCameraLook = new Vector3(-38.3f, BasementLayout.FloorY + 0.75f, 117.4f);

        // Замурованный проём в восточной стене — бывший вход в проход A.
        public static readonly Vector3 SealedDoor = new Vector3(BasementLayout.ExistingWallX, BasementLayout.FloorY + BasementLayout.DoorHeight * 0.5f,
            0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo));
        public static readonly Vector3 SealedDoorSize = new Vector3(0.25f, BasementLayout.DoorHeight, BasementLayout.EntranceTo - BasementLayout.EntranceFrom);

        public static readonly BasementRoom Shaft = new BasementRoom("Shaft", -41.7f, -38.7f, 113.7f, 121f)
            .Open(BasementSide.East, 118f, 121f);
        public static readonly BasementRoom Collector = new BasementRoom("Collector", -38.7f, -9f, 118f, 121f)
            .OpenSide(BasementSide.West).OpenSide(BasementSide.East).Open(BasementSide.North, -22f, -20.4f);
        public static readonly BasementRoom Store = new BasementRoom("Store", -24f, -18f, 121f, 126f)
            .Open(BasementSide.South, -22f, -20.4f);
        public static readonly BasementRoom PumpHall = new BasementRoom("PumpHall", -9f, 1f, 104f, 122f)
            .Open(BasementSide.West, 118f, 121f).Open(BasementSide.West, 104f, 106.5f);
        public static readonly BasementRoom Gallery = new BasementRoom("Gallery", -31f, -9f, 104f, 106.5f)
            .OpenSide(BasementSide.West).OpenSide(BasementSide.East);
        public static readonly BasementRoom ValveRoom = new BasementRoom("ValveRoom", -38f, -31f, 100f, 108f)
            .Open(BasementSide.East, 104f, 106.5f).Open(BasementSide.North, -38f, -36.5f);
        public static readonly BasementRoom Riser = new BasementRoom("Riser", -38f, -36.5f, 108f, 116.6f)
            .OpenSide(BasementSide.South);

        public static readonly BasementRoom[] Rooms = { Shaft, Collector, Store, PumpHall, Gallery, ValveRoom, Riser };

        // Труба: ось по полу, от решётки до выхода над шахтой. Входной конец за стеной подвала — под затемнением.
        public static readonly Vector2[] DuctPath =
        {
            new Vector2(-30f, 117.6f), new Vector2(-30f, 123f), new Vector2(-40.2f, 123f), new Vector2(-40.2f, 120.2f)
        };
        public static readonly Vector3 DuctStart = new Vector3(DuctPath[0].x, DuctFloorY, DuctPath[0].y);
        public static readonly Vector3 DuctEnd = new Vector3(DuctPath[3].x, DuctFloorY, DuctPath[3].y);

        // Точка под трубой, путь по тоннелям и низ лестницы наверх, за замурованную стену.
        public static readonly Vector3 DropPoint = Floor(-40.2f, 117.6f);
        public static readonly Vector2[] Route =
        {
            new Vector2(-40.2f, 119.5f), new Vector2(-21.2f, 119.5f), new Vector2(-4f, 119.5f),
            new Vector2(-4f, 105.2f), new Vector2(-20f, 105.2f), new Vector2(-34f, 105.2f),
            new Vector2(-37.25f, 105.2f), new Vector2(-37.25f, 115.6f)
        };
        public static readonly Vector3 LadderUp = Floor(-37.25f, 116.2f);

        // Куда выходит лестница: конец прохода A, у обратной стороны замурованного проёма.
        public static readonly Vector3 WingStart = new Vector3(-37.3f, BasementLayout.FloorY + 0.1f, 116.2f);
        public static readonly Vector3 WingStartLook = new Vector3(-30f, 0f, 116.2f);

        public static Vector3 Floor(float x, float z) => new Vector3(x, FloorY, z);

        public static bool IsInside(Vector2 point, float margin = 0f)
        {
            foreach (BasementRoom room in Rooms)
                if (room.Contains(point, margin)) return true;
            return false;
        }

        public static IEnumerable<BasementLayout.Prop> Props()
        {
            // Насосный зал: баки и станины вдоль стен, середина свободна.
            foreach (float z in new[] { 108f, 112f, 116f })
            {
                yield return Tank(-7.4f, z, 1.5f, 2.5f);
                yield return Block(-0.6f, z, 1.4f, 1.1f, 2.2f);
            }
            // Вентильная: распределительные шкафы у восточной и южной стен, проход к стояку вдоль западной.
            yield return Block(-31.8f, 101.8f, 1f, 2.1f, 2.6f);
            yield return Block(-33.8f, 100.6f, 2.2f, 1.6f, 0.8f);
            // Кладовая обходчиков.
            yield return Block(-23.2f, 124.2f, 1.1f, 1.7f, 2.6f);
            yield return Block(-18.8f, 124.6f, 1.2f, 1f, 1.8f);
        }

        private static BasementLayout.Prop Tank(float x, float z, float width, float height) => new BasementLayout.Prop
        {
            Center = new Vector3(x, FloorY + height * 0.5f, z), Size = new Vector3(width, height, width), Cylinder = true
        };

        private static BasementLayout.Prop Block(float x, float z, float width, float height, float depth) => new BasementLayout.Prop
        {
            Center = new Vector3(x, FloorY + height * 0.5f, z), Size = new Vector3(width, height, depth)
        };

        public static bool BlockedByProp(Vector2 point, float radius)
        {
            foreach (BasementLayout.Prop prop in Props())
            {
                float halfX = prop.Size.x * 0.5f + radius, halfZ = prop.Size.z * 0.5f + radius;
                if (Mathf.Abs(point.x - prop.Center.x) <= halfX && Mathf.Abs(point.y - prop.Center.z) <= halfZ) return true;
            }
            return false;
        }
    }
}
