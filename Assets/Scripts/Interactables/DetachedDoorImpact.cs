using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{
    public sealed class DetachedDoorImpact : MonoBehaviour
    {
        private AudioSource _source;
        private AudioClip _landingClip;

        public void Initialize(AudioClip breakClip, AudioClip landingClip, Vector3 landingCenter,
            Quaternion landingRotation, BoxCollider leafCollider, Rigidbody body)
        {
            _landingClip = landingClip;
            _source = gameObject.AddComponent<AudioSource>();
            AudioRouting.Assign(_source, AudioBus.Horror);
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 4f;
            _source.maxDistance = 30f;
            _source.volume = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            AudioClip slam = Resources.Load<AudioClip>("Audio/CornerDoorSlam");
            if (slam != null) _source.PlayOneShot(slam);
            if (breakClip != null) _source.PlayOneShot(breakClip);
            StartCoroutine(FlyToLanding(landingCenter, landingRotation, leafCollider, body));
        }

        private IEnumerator FlyToLanding(Vector3 center, Quaternion rotation, BoxCollider collider, Rigidbody body)
        {
            Vector3 start = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 end = center - rotation * Vector3.Scale(collider.center, transform.lossyScale);
            const float duration = 0.75f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.75f);
                transform.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, rotation, t));
                yield return null;
            }
            transform.SetPositionAndRotation(end, rotation);
            body.position = end;
            body.rotation = rotation;
            collider.enabled = true;
            if (_landingClip != null) _source.PlayOneShot(_landingClip);
        }
    }
}
