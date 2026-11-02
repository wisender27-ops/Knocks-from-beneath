using UnityEngine;

namespace KnocksFromBeneath
{
    // S-02: стук Хейла идёт не из-под ног, а по его трубе (BasementLayout.PipeRoute): из ближайшей к слушателю точки.
    // Чистая геометрия, без объектов сцены.
    public static class HalePipe
    {
        public static Vector3 NearestPoint(Vector3 world)
        {
            var from = new Vector2(world.x, world.z);
            Vector2[] route = BasementLayout.PipeRoute;
            Vector2 best = route[0];
            float bestDistance = float.PositiveInfinity;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], segment = route[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(from - a, segment) / segment.sqrMagnitude);
                Vector2 point = a + segment * t;
                float distance = (from - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = point;
            }
            return new Vector3(best.x, BasementLayout.PipeY, best.y);
        }
    }
}
