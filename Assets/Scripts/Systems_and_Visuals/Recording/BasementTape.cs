using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // C-04: вторая запись — плёнка из старой камеры хозяина на кронштейне в блоке D, идёт в финале E5 после затемнения.
    // Стук по трубе зовёт монстра в открытую камеру, решётка захлопывается цепью из-за кадра, он оборачивается в объектив;
    // под утро монстр всё ещё за решёткой, а сверху, из дома, отвечают стуком «2·1». Героя и хозяина на плёнке нет.
    public static class BasementTape
    {
        public const float Duration = 37f;
        private const float Floor = BasementLayout.FloorY;
        private const float MorningCut = 30f;
        private const float DoorSlamStart = 24f, DoorSlamEnd = 24.35f;
        private const float BarsZ = 107.75f;

        private static readonly Vector3 PipeInCell = new Vector3(BasementLayout.PipeRoute[0].x, Floor + 1.3f, BasementLayout.PipeRoute[0].y);
        private static readonly Vector3 Bars = new Vector3(-44.7f, Floor + 1.1f, BasementLayout.CellArea.yMax);
        private static readonly Vector3 HouseAbove = new Vector3(-44f, 0.2f, 110f);

        public const float LookIn = 27.2f, LookFull = 28.2f, Slammed = DoorSlamEnd;

        public static readonly TapeTimeline Timeline = new TapeTimeline(Duration,
            new[]
            {
                new TapeShot(0f, 3 * 3600 + 7 * 60 + 12),
                new TapeShot(MorningCut, 6 * 3600 + 58 * 60 + 40)
            },
            new[]
            {
                // Из перехода C вдоль северной стены, мимо объектива и в открытую камеру; решётка закрылась — разворот к ней.
                new[]
                {
                    new TapeKey(12f, BasementLayout.CellBlockD.XMax, Floor, 111.2f),
                    new TapeKey(15f, -41.2f, Floor, 110.9f),
                    new TapeKey(19f, -44.6f, Floor, 110.7f),
                    new TapeKey(22f, -44.6f, Floor, BasementLayout.CellArea.yMax),
                    new TapeKey(23.5f, -44.7f, Floor, 106.9f),
                    new TapeKey(25.5f, -44.7f, Floor, 106.9f),
                    new TapeKey(27f, -44.7f, Floor, BarsZ),
                    new TapeKey(MorningCut, -44.7f, Floor, BarsZ)
                },
                new[]
                {
                    new TapeKey(MorningCut, -44.7f, Floor, BarsZ),
                    new TapeKey(Duration, -44.7f, Floor, BarsZ + 0.001f)
                }
            },
            new[] { MorningCut },
            new List<TapeEvent>
            {
                new TapeEvent(3.4f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(3.76f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(4.76f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(8.4f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(8.76f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(9.76f, TapeSound.Knock, PipeInCell, 1f),
                new TapeEvent(DoorSlamEnd - 0.05f, TapeSound.Slam, Bars, 1f),
                new TapeEvent(DoorSlamEnd + 0.1f, TapeSound.DoorClose, Bars, 1f),
                new TapeEvent(27.5f, TapeSound.Bars, Bars, 1f),
                new TapeEvent(28.4f, TapeSound.Bars, Bars, 1f),
                new TapeEvent(29.1f, TapeSound.Bars, Bars, 1f),
                // Утро: ответ сверху, из дома.
                new TapeEvent(32.2f, TapeSound.Knock, HouseAbove, 0.9f),
                new TapeEvent(32.56f, TapeSound.Knock, HouseAbove, 0.9f),
                new TapeEvent(33.56f, TapeSound.Knock, HouseAbove, 0.9f)
            },
            LookIn, LookFull, Duration, Duration + 1f);

        // Решётка камеры: 1 — открыта, как её оставил игрок, 0 — захлопнута.
        public static float DoorOpen(float time) =>
            time < DoorSlamStart ? 1f : 1f - TapeTimeline.Smooth(Mathf.InverseLerp(DoorSlamStart, DoorSlamEnd, time));

        // Свет на плёнке — та же красная лампа у камеры и едва заметная подсветка у объектива.
        public static Light[] BuildLights(Transform parent)
        {
            Light lamp = NightRecording.NewLight(parent, "Cell Lamp", LightType.Point);
            lamp.transform.position = BasementLayout.CellLampPosition;
            lamp.color = new Color(1f, 0.25f, 0.15f);
            lamp.intensity = 1.6f;
            lamp.range = 7f;
            lamp.shadows = LightShadows.Soft;
            Light fill = NightRecording.NewLight(parent, "Lens Fill", LightType.Point);
            fill.transform.position = BasementLayout.TapeCameraPosition + Vector3.down * 0.5f;
            fill.color = new Color(0.6f, 0.62f, 0.7f);
            fill.intensity = 0.5f;
            fill.range = 6f;
            fill.shadows = LightShadows.None;
            // Лампа за углом решётки внутрь камеры не достаёт: без этого отсвета фигуру за прутьями не видно.
            Light cell = NightRecording.NewLight(parent, "Cell Glow", LightType.Point);
            cell.transform.position = new Vector3(-44.4f, BasementLayout.CeilingY - 0.5f, 107.4f);
            cell.color = new Color(1f, 0.32f, 0.2f);
            cell.intensity = 1.8f;
            cell.range = 4.5f;
            cell.shadows = LightShadows.None;
            return new[] { lamp, fill, cell };
        }
    }
}
