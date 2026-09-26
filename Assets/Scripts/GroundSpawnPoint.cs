using UnityEngine;

namespace KnocksFromBeneath
{
    /// <summary>
    /// Подбор места для появления предмета "под ногами" игрока: ищем ближайшую
    /// свободную точку перед ним, проверяя, что там не стоит другой предмет и нет
    /// препятствия.
    ///
    /// Почему нельзя просто поставить в player.position + forward * 1.2:
    /// игрок часто стоит спиной к стене или в углу — мешок оказывался внутри
    /// стены или в шкафу, и квест доставки становился невыполнимым (софтлок).
    /// Поэтому перебираем позиции по расширяющейся спирали и берём первую,
    /// где предмет действительно помещается.
    ///
    /// Слой-блокер DoorNoRaycast исключён из проверок через PhysicsMasks — такой
    /// коллайдер не должен считаться препятствием (он держит физику, но не виден
    /// лучам), иначе калитка перед ногами считалась бы "занятым местом".
    /// </summary>
    public static class GroundSpawnPoint
    {
        /// <summary>
        /// Ищет свободное место на полу рядом с игроком.
        /// </summary>
        /// <param name="playerPosition">Позиция ног игрока.</param>
        /// <param name="playerForward">Направление взгляда (горизонтальная составляющая).</param>
        /// <param name="footprintRadius">Радиус, который должен помещаться на полу (мешок).</param>
        /// <param name="outPoint">Найденная точка (включая высоту пола).</param>
        /// <returns>true, если место нашлось; иначе outPoint = точка перед игроком как запасной вариант.</returns>
        public static bool TryFindFreeSpot(
            Vector3 playerPosition,
            Vector3 playerForward,
            float footprintRadius,
            out Vector3 outPoint)
        {
            Vector3 forward = playerForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            // Точка перед игроком — запасной вариант, если вокруг вообще негде.
            outPoint = playerPosition + forward * 0.9f;

            // Проверяем площадку: там не должно быть ничего, кроме пола.
            for (float distance = 0.9f; distance <= 3.0f; distance += 0.35f)
            {
                // Несколько углов вокруг направления взгляда — чтобы не упираться в
                // одну стену прямо по курсу, а обойти её сбоку.
                for (int step = 0; step < 5; step++)
                {
                    // (step - 2) даёт -2..2 — отклонение в градусах в обе стороны.
                    float angle = (step - 2) * 30f;
                    Vector3 offsetDir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    Vector3 candidate = playerPosition + offsetDir * distance;

                    if (IsSpotFree(candidate, footprintRadius, out float groundY))
                    {
                        outPoint = new Vector3(candidate.x, groundY, candidate.z);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Свободно ли место: под точкой должен быть пол, а выше пола — пустота
        /// в пределах радиуса (чтобы мешок не оказался в столе или в стене).
        /// </summary>
        private static bool IsSpotFree(Vector3 candidate, float radius, out float groundY)
        {
            // 1) Ищем пол под точкой. Слой-блокер исключён: калитка под ногами —
            //    не пол и не препятствие, сквозь неё всё проходит.
            if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down,
                    out RaycastHit groundHit, 3.0f, PhysicsMasks.RaycastDefault))
            {
                groundY = candidate.y;
                return false;
            }
            groundY = groundHit.point.y;

            // 2) Проверяем, что над полом в пределах радиуса ничего не висит.
            //
            // ПРОБА НАЧИНАЕТСЯ СТРОГО НАД ПОЛОМ, иначе бокс своим нижним краем
            // накрывает сам пол (у него в сцене двойное покрытие: Plane И
            // MeshCollider "colliders_floor_xx") — и проверка всегда находила бы
            // "занятое" место, а мешок падал бы в пол по запасному пути.
            //
            // Три уровня — низ мешка, корпус, верх мешка. Пол на этом уровне уже
            // строго ниже бокса, а сам предмет ещё не создан, поэтому ложных
            // срабатываний на собственном коллайдере не будет.
            Vector3 half = new Vector3(radius * 0.85f, 0.1f, radius * 0.85f);
            float step = Mathf.Max(0.12f, radius * 0.5f);

            for (int level = 1; level <= 3; level++)
            {
                Vector3 probeCenter = new Vector3(candidate.x, groundY + step * level, candidate.z);
                if (Physics.CheckBox(probeCenter, half, Quaternion.identity,
                        PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore))
                    return false;
            }

            return true;
        }
    }
}
