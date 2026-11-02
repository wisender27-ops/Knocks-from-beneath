using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Обстановка залов за вентиляцией (собрана в сцене, Tools/Basement/Dress Rooms): лампы и фоновые звуки живут только во время главы.
    // Одна из ламп редко подмигивает: контакт старый, и слышно, как щёлкает нить.
    public sealed class BasementRoomAtmosphere : MonoBehaviour
    {
        [SerializeField] private Light[] lamps = new Light[0];
        [SerializeField] private AudioSource[] loops = new AudioSource[0];
        [SerializeField] private Light flickerLamp;
        [SerializeField] private AudioSource flickerSound;

        public void Activate()
        {
            foreach (Light lamp in lamps)
                if (lamp != null) lamp.enabled = true;
            foreach (AudioSource loop in loops)
            {
                if (loop == null) continue;
                loop.time = Random.value * Mathf.Max(0.1f, loop.clip.length - 0.1f);
                loop.Play();
            }
            StopAllCoroutines();
            if (flickerLamp != null) StartCoroutine(FlickerRoutine());
        }

        private IEnumerator FlickerRoutine()
        {
            float bright = flickerLamp.intensity;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(9f, 24f));
                if (flickerSound != null) flickerSound.Play();
                int blinks = Random.Range(2, 5);
                for (int i = 0; i < blinks; i++)
                {
                    flickerLamp.intensity = bright * Random.Range(0.05f, 0.3f);
                    yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                    flickerLamp.intensity = bright;
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
                }
            }
        }
    }
}
