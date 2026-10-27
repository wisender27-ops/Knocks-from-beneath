using UnityEngine;

namespace KnocksFromBeneath
{
    // Выбитая входная дверь ночи 2 (засов был задвинут): подошёл и посмотрел на неё —
    // герой один раз комментирует. Когда угодно, лишь бы не шла пугалка/катсцена.
    [DisallowMultipleComponent]
    public sealed class BrokenDoorRemark : MonoBehaviour
    {
        public const float NoticeDistance = 3.5f;
        public const float NoticeDot = 0.8f; // ~37° от центра взгляда
        private const float CheckInterval = 0.2f;
        private const string Key = "broken-front-door";

        private static readonly string[] Lines =
        {
            "Дверь выбита… вместе с петлями.",
            "Засов выдержал. Петли — нет."
        };

        private Collider _leaf;
        private Transform _eye;
        private float _nextCheck;

        public static bool Notices(Vector3 eye, Vector3 forward, Vector3 target)
        {
            Vector3 toTarget = target - eye;
            float distance = toTarget.magnitude;
            if (distance > NoticeDistance) return false;
            return distance < 0.01f || Vector3.Dot(forward.normalized, toTarget / distance) >= NoticeDot;
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            if (_eye == null)
            {
                Camera camera = Camera.main;
                if (camera == null) return;
                _eye = camera.transform;
            }
            // Коллайдер створки включается, когда она долетела до пола; старые коллайдеры двери выключены.
            if (_leaf == null || !_leaf.enabled)
            {
                _leaf = null;
                foreach (Collider candidate in GetComponents<Collider>())
                    if (candidate.enabled && !candidate.isTrigger) { _leaf = candidate; break; }
                if (_leaf == null) return;
            }

            HorrorMomentDirector director = HorrorMomentDirector.Instance;
            if (director != null && !director.CanPresent) return;

            Vector3 target = _leaf.bounds.center;
            if (!Notices(_eye.position, _eye.forward, target)) return;
            if (Physics.Linecast(_eye.position, target, out RaycastHit hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.collider != _leaf) return;

            ThoughtManager thoughts = ThoughtManager.Instance;
            if (thoughts != null && thoughts.TryShowInteractionThoughts(Key, Lines, float.MaxValue))
                Destroy(this);
        }
    }
}
