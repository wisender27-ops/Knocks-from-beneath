using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Звуки трубы вентиляции (собрана в сцене, Tools/Basement/Build Duct In Scene): капель и гул играют, пока герой внутри;
    // скрип металла — редкий, со случайной точки.
    public sealed class BasementDuctScene : MonoBehaviour
    {
        [SerializeField] private AudioSource[] loops = new AudioSource[0];
        [SerializeField] private AudioSource creak;
        [SerializeField] private AudioClip[] creakClips = new AudioClip[0];
        [SerializeField] private Vector3[] creakPoints = new Vector3[0];

        public void SetAudible(bool active)
        {
            StopAllCoroutines();
            foreach (AudioSource loop in loops)
            {
                if (loop == null) continue;
                if (active) { loop.time = Random.value * Mathf.Max(0.1f, loop.clip.length - 0.1f); loop.Play(); }
                else loop.Stop();
            }
            if (active) StartCoroutine(CreakRoutine());
        }

        private IEnumerator CreakRoutine()
        {
            if (creak == null || creakClips.Length == 0 || creakPoints.Length == 0) yield break;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(7f, 16f));
                creak.transform.position = creakPoints[Random.Range(0, creakPoints.Length)];
                creak.pitch = Random.Range(0.55f, 0.85f);
                creak.PlayOneShot(creakClips[Random.Range(0, creakClips.Length)], Random.Range(0.35f, 0.6f));
            }
        }
    }
}
