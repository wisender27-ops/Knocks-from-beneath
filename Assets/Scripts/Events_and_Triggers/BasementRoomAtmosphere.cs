using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Обстановка залов за вентиляцией (собрана в сцене, Tools/Basement/Dress Rooms): фоновые звуки живут только во время главы.
    // Ламп здесь нет: ночью 2 дом обесточен, подвал освещает только фонарь героя. Лампы, их гул и подмигивание из сцены
    // больше не включаются — поля оставлены, чтобы не терять ссылки в Main.unity.
    public sealed class BasementRoomAtmosphere : MonoBehaviour
    {
        [SerializeField] private Light[] lamps = new Light[0];
        [SerializeField] private AudioSource[] loops = new AudioSource[0];
        [SerializeField] private Light flickerLamp;
        [SerializeField] private AudioSource flickerSound;

        private Coroutine silencing;

        public void Activate()
        {
            foreach (Light lamp in lamps)
                if (lamp != null) lamp.enabled = false;
            if (flickerLamp != null) flickerLamp.enabled = false;
            foreach (AudioSource loop in loops)
            {
                if (loop == null || loop.clip == null || IsLampSound(loop)) continue;
                loop.time = Random.value * Mathf.Max(0.1f, loop.clip.length - 0.1f);
                loop.Play();
            }
        }

        // Гул ламп без ламп — звук из ниоткуда.
        private static bool IsLampSound(AudioSource source) =>
            source.clip.name.IndexOf("Lamp", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            source.clip.name.IndexOf("Bulb", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // Финал: фон стихает, остаются реплики и стук.
        public void Silence(float seconds)
        {
            if (silencing != null) StopCoroutine(silencing);
            silencing = StartCoroutine(SilenceRoutine(seconds));
        }

        private IEnumerator SilenceRoutine(float seconds)
        {
            var start = new float[loops.Length];
            for (int i = 0; i < loops.Length; i++) start[i] = loops[i] != null ? loops[i].volume : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                for (int i = 0; i < loops.Length; i++)
                    if (loops[i] != null) loops[i].volume = start[i] * (1f - t);
                yield return null;
            }
            for (int i = 0; i < loops.Length; i++)
            {
                if (loops[i] == null) continue;
                loops[i].Stop();
                loops[i].volume = start[i];
            }
        }
    }
}
