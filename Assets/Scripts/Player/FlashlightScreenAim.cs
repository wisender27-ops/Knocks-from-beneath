using UnityEngine;

namespace KnocksFromBeneath
{
    [DefaultExecutionOrder(11000)]
    public sealed class FlashlightScreenAim : MonoBehaviour
    {
        private Light _beam;
        private Camera _camera;
        private BasementScareSequence _basementScare;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public void Initialize(Light beam, Camera camera)
        {
            _beam = beam;
            _camera = camera;
            _basementScare = FindFirstObjectByType<BasementScareSequence>(FindObjectsInactive.Include);
        }

        private void LateUpdate()
        {
            if (_beam == null || _camera == null || !_beam.enabled || !_beam.gameObject.activeInHierarchy) return;
            if (_basementScare != null && _basementScare.Phase != BasementScareSequence.ScarePhase.Ready &&
                _basementScare.Phase != BasementScareSequence.ScarePhase.Finished) return;
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = 15f;
            int count = Physics.RaycastNonAlloc(ray, _hits, distance, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform.root)) continue;
                distance = Mathf.Min(distance, _hits[i].distance);
            }
            Vector3 direction = ray.GetPoint(Mathf.Max(0.5f, distance)) - _beam.transform.position;
            if (direction.sqrMagnitude > 0.001f) _beam.transform.rotation = Quaternion.LookRotation(direction, _camera.transform.up);
        }
    }
}
