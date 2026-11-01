using UnityEngine;

namespace KnocksFromBeneath
{
    // План вентиляции: то, чего игрок не видит в подвале. Комната под лестницей заканчивается забетонированным проёмом,
    // в стене рядом — квадратная вентиляционная труба. Короткая, в два колена: огибает угол зала B за стеной и выходит в сам зал,
    // в отрезанную половину подвала (BasementLayout, блоки A–D), где сидит хозяин. Чистые данные, объекты лежат в сцене (BasementDepths).
    public static class DepthsLayout
    {
        public const float DuctSize = 1.15f;     // ширина и высота трубы изнутри: присевший герой (1 м) проходит
        public const float DuctFloorY = BasementLayout.FloorY + 0.08f; // на уровне порога решётки
        public const int CameraSlotNumber = 3; // номер места в StoryFlags.CameraPlacementSlot

        // Решётка у пола в восточной стене, рядом с замурованным проёмом. Точка — на стене, у пола, по центру решётки.
        public static readonly Vector3 Vent = new Vector3(-37.83f, BasementLayout.FloorY, 117.6f);
        // Штатив в подвале: смотрит наискось на решётку. Точка обзора плёнки — на высоте видоискателя.
        public static readonly Vector3 CameraSlot = new Vector3(-41.6f, BasementLayout.FloorY, 116.0f);
        public const float CameraSlotYaw = 68f;
        public static readonly Vector3 TapeCameraPosition = new Vector3(-41.6f, BasementLayout.FloorY + 1.3f, 116.0f);
        public static readonly Vector3 TapeCameraLook = new Vector3(-38.3f, BasementLayout.FloorY + 0.75f, 117.4f);

        // Забетонированный проём в восточной стене — бывший вход в проход A.
        public static readonly Vector3 SealedDoor = new Vector3(BasementLayout.ExistingWallX, BasementLayout.FloorY + BasementLayout.DoorHeight * 0.5f,
            0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo));
        public static readonly Vector3 SealedDoorSize = new Vector3(0.25f, BasementLayout.DoorHeight, BasementLayout.EntranceTo - BasementLayout.EntranceFrom);

        // Труба: ось по полу, от решётки вокруг угла зала B. Последняя точка — за северной стеной зала, устье на её внутренней грани.
        public static readonly Vector2[] DuctPath =
        {
            new Vector2(-37f, 117.85f), new Vector2(-37f, 119.3f), new Vector2(-35.75f, 119.3f), new Vector2(-35.75f, 118.975f)
        };
        public static readonly Vector3 DuctStart = new Vector3(DuctPath[0].x, DuctFloorY, DuctPath[0].y);
        public static readonly Vector3 DuctEnd = new Vector3(DuctPath[3].x, DuctFloorY, DuctPath[3].y);
        // Где герой оказывается, вылезая из трубы в зал B.
        public static readonly Vector3 DuctOutlet = new Vector3(DuctPath[3].x, BasementLayout.FloorY + 0.1f, 117.8f);

        // Герой внутри трубы: проверка по её внутреннему сечению.
        public static bool InDuct(Vector2 point)
        {
            float half = DuctSize * 0.5f;
            for (int i = 1; i < DuctPath.Length; i++)
            {
                Rect leg = Rect.MinMaxRect(Mathf.Min(DuctPath[i - 1].x, DuctPath[i].x) - half, Mathf.Min(DuctPath[i - 1].y, DuctPath[i].y) - half,
                    Mathf.Max(DuctPath[i - 1].x, DuctPath[i].x) + half, Mathf.Max(DuctPath[i - 1].y, DuctPath[i].y) + half);
                if (leg.Contains(point)) return true;
            }
            return false;
        }

        // Конец прохода A, у обратной стороны забетонированного проёма: сюда возвращает после поимки.
        public static readonly Vector3 WingStart = new Vector3(-37.3f, BasementLayout.FloorY + 0.1f, 116.2f);
        public static readonly Vector3 WingStartLook = new Vector3(-30f, 0f, 116.2f);
    }
}
