using UnityEngine;

namespace KnocksFromBeneath
{
    // План вентиляции: то, чего игрок не видит в подвале. Комната под лестницей заканчивается забетонированным проёмом,
    // а под потолком вдоль северной стены висит короб вентиляции. Он уходит сквозь стену и выходит под потолком зала B,
    // в отрезанную половину подвала (BasementLayout, блоки A–D), где сидит хозяин. Чистые данные, объекты лежат в сцене (BasementDepths).
    public static class DepthsLayout
    {
        public const float DuctSize = 1.15f;     // высота короба изнутри: присевший герой (1 м) проходит
        public const float DuctWidth = 0.92f;    // ширина изнутри: сквозь стену короб идёт рядом со стеной прохода A
        public const float DuctFloorY = BasementLayout.FloorY + 1.26f; // короб висит под балками (низ балок −1.86)
        public const int CameraSlotNumber = 3; // номер места в StoryFlags.CameraPlacementSlot

        // Торец короба с решёткой, смотрит на запад, в комнату. Точка — центр решётки.
        public static readonly Vector3 Vent = new Vector3(-40.61f, DuctFloorY + DuctSize * 0.5f, 117.72f);
        // Штатив в подвале: смотрит наискось снизу на решётку. Точка обзора плёнки — на высоте видоискателя.
        public static readonly Vector3 CameraSlot = new Vector3(-43.0f, BasementLayout.FloorY, 115.2f);
        public const float CameraSlotYaw = 42f;
        public static readonly Vector3 TapeCameraPosition = new Vector3(-43.0f, BasementLayout.FloorY + 1.3f, 115.2f);
        public static readonly Vector3 TapeCameraLook = new Vector3(-40.8f, BasementLayout.FloorY + 1.45f, 117.6f);

        // Забетонированный проём в восточной стене — бывший вход в проход A.
        public static readonly Vector3 SealedDoor = new Vector3(BasementLayout.ExistingWallX, BasementLayout.FloorY + BasementLayout.DoorHeight * 0.5f,
            0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo));
        public static readonly Vector3 SealedDoorSize = new Vector3(0.25f, BasementLayout.DoorHeight, BasementLayout.EntranceTo - BasementLayout.EntranceFrom);

        // Короб: ось по его полу, прямо на восток. Торцы отстоят от концов оси на половину ширины, устье — на грани стены зала B.
        public static readonly Vector2[] DuctPath = { new Vector2(-40.15f, 117.72f), new Vector2(-37.36f, 117.72f) };
        public static readonly Vector3 DuctStart = new Vector3(DuctPath[0].x, DuctFloorY, DuctPath[0].y);
        public static readonly Vector3 DuctEnd = new Vector3(DuctPath[1].x, DuctFloorY, DuctPath[1].y);
        // Куда герой забирается, залезая в короб, и куда он смотрит.
        public static readonly Vector3 DuctClimbSpot = new Vector3(DuctPath[0].x - 0.05f, DuctFloorY, DuctPath[0].y);
        // Где герой оказывается, вылезая из короба в зал B: спрыгивает с устья на пол.
        public static readonly Vector3 DuctOutlet = new Vector3(-36.2f, BasementLayout.FloorY + 0.1f, DuctPath[1].y);

        // Под коробом можно стоять: внутри герой только на его высоте.
        public static bool AtDuctHeight(float y) => y > DuctFloorY - 0.3f && y < DuctFloorY + DuctSize;

        // Герой внутри короба: проверка по его внутреннему сечению, в плане.
        public static bool InDuct(Vector2 point)
        {
            float half = DuctWidth * 0.5f;
            for (int i = 1; i < DuctPath.Length; i++)
            {
                Rect leg = Rect.MinMaxRect(Mathf.Min(DuctPath[i - 1].x, DuctPath[i].x) - half, Mathf.Min(DuctPath[i - 1].y, DuctPath[i].y) - half,
                    Mathf.Max(DuctPath[i - 1].x, DuctPath[i].x) + half, Mathf.Max(DuctPath[i - 1].y, DuctPath[i].y) + half);
                if (leg.Contains(point)) return true;
            }
            return false;
        }

        // Зал B, у обратной стороны забетонированного проёма (сам проход A залит бетоном): сюда возвращает после поимки.
        public static readonly Vector3 WingStart = new Vector3(-36.3f, BasementLayout.FloorY + 0.1f, 116.2f);
        public static readonly Vector3 WingStartLook = new Vector3(-30f, 0f, 116.2f);
    }
}
