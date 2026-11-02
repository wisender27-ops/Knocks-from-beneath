using UnityEngine;

namespace KnocksFromBeneath
{
    // Страховка для физических предметов, которые игрок берёт в руки: сильный бросок или
    // неудачная укладка может протолкнуть предмет сквозь пол/стену. Помним последнюю
    // устойчивую позу и возвращаем предмет в неё, если он упал под карту или улетел далеко.
    // Вешается PickupController при первом захвате; без захвата игроком ничего не делает.
    [DisallowMultipleComponent]
    public sealed class PhysicsItemGuard : MonoBehaviour
    {
        private const float CheckInterval = 0.25f;
        private const float RestSpeed = 0.15f;
        private const float RestSeconds = 0.5f;
        private const float FallBelowSafe = 12f;
        private const float MaxDistanceFromSafe = 45f;
        private const float RespawnLift = 0.15f;

        private Rigidbody body;
        private Vector3 safePosition;
        private Quaternion safeRotation;
        private float nextCheck, restTime;

        public static void Attach(GameObject item)
        {
            if (item == null || item.GetComponent<PhysicsItemGuard>() != null) return;
            item.AddComponent<PhysicsItemGuard>();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            safePosition = transform.position;
            safeRotation = transform.rotation;
        }

        private void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;
            if (body == null || body.isKinematic || transform.parent != null)
            {
                restTime = 0f;
                return;
            }

            Vector3 position = transform.position;
            bool broken = float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                position.y < safePosition.y - FallBelowSafe ||
                (position - safePosition).sqrMagnitude > MaxDistanceFromSafe * MaxDistanceFromSafe;
            if (broken)
            {
                Respawn();
                return;
            }

            if (body.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed)
            {
                restTime += CheckInterval;
                if (restTime >= RestSeconds)
                {
                    safePosition = position;
                    safeRotation = transform.rotation;
                }
            }
            else restTime = 0f;
        }

        // Предмет, которого касается игрок, не должен "возвращаться" из рук: проверка выше
        // пропускает кинематические тела, а в руках предмет всегда кинематический.
        private void Respawn()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = safePosition + Vector3.up * RespawnLift;
            body.rotation = safeRotation;
            transform.SetPositionAndRotation(safePosition + Vector3.up * RespawnLift, safeRotation);
            restTime = 0f;
        }
    }
}
