using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    /// <summary>
    /// Общая механика "приятного появления": предмет вырастает из нуля с
    /// перелётом (EaseOutBack) и, по желанию, с коротким подскоком.
    ///
    /// Раньше это было приватной механикой PlacementZone (появление мебели из
    /// коробок) — вынесено отдельно, чтобы тем же эффектом пользовались другие
    /// спавны, в частности мусорный мешок в TrashManager.
    ///
    /// ВАЖНО: анимация всегда крутится на ПОСТОЯННОМ раннере, а не на самом
    /// объекте. Иначе, если вызывающий код выключит GameObject сразу после
    /// запуска (например, зона коробок синхронно гасит себя в AddProgress),
    /// StartCoroutine на выключенном объекте молча ничего не запустит, и предмет
    /// навсегда останется с нулевым масштабом.
    /// </summary>
    public static class PopScaleAnimator
    {
        private static MonoBehaviour _runner;

        /// <summary>Постоянный раннер, который никогда не выключается вместе с предметом.</summary>
        public static MonoBehaviour Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("~PopScaleRunner");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    if (Application.isPlaying)
                        Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<PopScaleRunner>();
                }
                return _runner;
            }
        }

        /// <summary>
        /// Плавно "выращивает" объект до targetLocalScale. Масштаб сразу обнуляется,
        /// поэтому вызывающий может заранее сделать объект активным — как в
        /// PlacementZone, где активность проверяется квестами сразу после вызова.
        /// </summary>
        /// <param name="t">Что анимируем.</param>
        /// <param name="targetLocalScale">Масштаб, в который нужно прийти.</param>
        /// <param name="duration">Длительность «попа», сек.</param>
        /// <param name="hopHeight">Высота подскока в мирах (0 — без подскока).</param>
        /// <param name="freezePhysics">На время анимации сделать Rigidbody кинематическим
        /// и вернуть его в исходное состояние по завершении (для предметов, которые
        /// после появления должны упасть/откатиться).</param>
        public static void Play(Transform t, Vector3 targetLocalScale, float duration, float hopHeight = 0f, bool freezePhysics = false)
        {
            if (t == null) return;

            t.localScale = Vector3.zero;
            Runner.StartCoroutine(Routine(t, targetLocalScale, duration, hopHeight, freezePhysics));
        }

        private static IEnumerator Routine(Transform t, Vector3 targetLocalScale, float duration, float hopHeight, bool freezePhysics)
        {
            Rigidbody rb = null;
            bool rbWasKinematic = false;
            bool rbUsedGravity = true;
            if (freezePhysics)
            {
                rb = t.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rbWasKinematic = rb.isKinematic;
                    rbUsedGravity = rb.useGravity;
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }

            Vector3 startPosition = t.position;

            if (duration <= 0f)
            {
                if (t != null) t.localScale = targetLocalScale;
                ReleasePhysics(rb, rbWasKinematic, rbUsedGravity);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null)
                {
                    ReleasePhysics(rb, rbWasKinematic, rbUsedGravity);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                t.localScale = targetLocalScale * EaseOutBack(p);

                if (hopHeight > 0f)
                {
                    // Параболическая дуга: 0 на обоих концах, максимум в середине.
                    float arc = 4f * p * (1f - p);
                    t.position = startPosition + Vector3.up * (hopHeight * arc);
                }

                yield return null;
            }

            if (t != null)
            {
                t.localScale = targetLocalScale;
                if (hopHeight > 0f) t.position = startPosition;
            }
            ReleasePhysics(rb, rbWasKinematic, rbUsedGravity);
        }

        private static void ReleasePhysics(Rigidbody rb, bool wasKinematic, bool usedGravity)
        {
            if (rb == null) return;
            rb.useGravity = usedGravity;
            rb.isKinematic = wasKinematic;
        }

        /// <summary>Перелёт с небольшим превышением — «пружинящее» появление.</summary>
        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        /// <summary>
        /// Короткий «поп» на месте появления. Без клипа — просто тихо.
        /// Питум слегка рандомизируется, чтобы серия одинаковых звуков не сливалась.
        /// </summary>
        public static void PlayPopSound(Vector3 position, AudioClip clip, float volume = 0.5f)
        {
            if (clip == null) return;

            var sfxGo = new GameObject("RevealPopSFX");
            sfxGo.transform.position = position;
            var src = sfxGo.AddComponent<AudioSource>();
            src.clip = clip;
            src.pitch = Random.Range(0.92f, 1.08f);
            src.volume = volume;
            src.spatialBlend = 1f;
            src.Play();
            Object.Destroy(sfxGo, clip.length / src.pitch + 0.1f);
        }
    }

    /// <summary>Пустой MonoBehaviour-хост исключительно для StartCoroutine.</summary>
    internal class PopScaleRunner : MonoBehaviour { }
}
