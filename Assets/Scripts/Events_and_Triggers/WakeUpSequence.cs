using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Подъём с кровати от первого лица: глаза открываются на потолок, герой садится, спускает ноги и встаёт
    // там, куда его уже поставил TeleportPlayerToBed. Экран в начале чёрный, после последовательности — прозрачный.
    [DisallowMultipleComponent]
    public sealed class WakeUpSequence : MonoBehaviour
    {
        // Изголовье bed_03 — со стороны x = -40.6, край кровати со стороны окна — z = 117.84.
        private static readonly Vector3 LyingEye = new Vector3(-41.05f, 5.2f, 117f);
        private static readonly Vector3 SittingEye = new Vector3(-41.6f, 5.62f, 117.1f);
        private static readonly Vector3 BedEdgeEye = new Vector3(-42.75f, 5.5f, 117.75f);
        // Ключи моргания: доля времени и непрозрачность век.
        private static readonly float[] BlinkTime = { 0f, .35f, .5f, .72f, .82f, 1f };
        private static readonly float[] BlinkAlpha = { 1f, .55f, .92f, .25f, .6f, 0f };
        private const float StandingEyeDrop = .2f;

        private PlayerController player;
        private Camera view;
        private AudioSource foley;

        public bool IsPlaying { get; private set; }

        public void Initialize(PlayerController playerController)
        {
            player = playerController;
            view = player != null ? player.GetComponentInChildren<Camera>(true) : null;
            foley = gameObject.AddComponent<AudioSource>();
            AudioRouting.Assign(foley, AudioBus.World);
            foley.playOnAwake = false;
            foley.spatialBlend = 0f;
        }

        // startled — разбудил грохот: без моргания, рывком.
        public IEnumerator Play(CanvasGroup fade, bool startled = false)
        {
            if (player == null || view == null || IsPlaying)
            {
                if (fade != null) fade.alpha = 0f;
                yield break;
            }
            IsPlaying = true;
            Transform eye = view.transform;
            Vector3 localPosition = eye.localPosition;
            bool wasLocked = player.isCameraLocked;
            player.StopMotion();
            player.isCameraLocked = true;
            HeadBobbing bob = player.GetComponentInChildren<HeadBobbing>();
            bool bobWasEnabled = bob != null && bob.enabled;
            if (bob != null) bob.enabled = false;
            var hidden = new List<Renderer>();
            ProtagonistBody body = player.GetComponentInChildren<ProtagonistBody>(true);
            if (body != null)
                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
            try
            {
                Quaternion lying = Quaternion.LookRotation(new Vector3(-.12f, 1f, 0f), Vector3.right);
                Quaternion sitting = Quaternion.LookRotation(new Vector3(-1f, -.18f, .1f), Vector3.up);
                Quaternion onEdge = Quaternion.LookRotation(new Vector3(-.45f, -.3f, 1f), Vector3.up);
                Vector3 standing = player.transform.position + Vector3.up * (player.standingHeight - StandingEyeDrop);
                Quaternion upright = player.transform.rotation;
                eye.SetPositionAndRotation(LyingEye, lying);

                if (startled)
                {
                    SetFade(fade, 0f);
                    PlayCloth(1f);
                    yield return Move(eye, LyingEye, SittingEye, lying, sitting, .55f);
                    yield return Move(eye, SittingEye, BedEdgeEye, sitting, onEdge, .4f);
                    PlayCloth(.7f);
                    yield return Move(eye, BedEdgeEye, standing, onEdge, upright, .6f);
                }
                else
                {
                    yield return OpenEyes(fade, eye, lying, 2.6f);
                    PlayCloth(.8f);
                    yield return Move(eye, LyingEye, SittingEye, lying, sitting, 1.5f);
                    yield return Wait(.35f);
                    PlayCloth(.6f);
                    yield return Move(eye, SittingEye, BedEdgeEye, sitting, onEdge, 1.1f);
                    yield return Wait(.25f);
                    PlayCloth(.5f);
                    yield return Move(eye, BedEdgeEye, standing, onEdge, upright, 1.25f);
                }
            }
            finally
            {
                SetFade(fade, 0f);
                foreach (Renderer renderer in hidden) if (renderer != null) renderer.enabled = true;
                if (bob != null) bob.enabled = bobWasEnabled;
                eye.localPosition = localPosition;
                player.ResetLook();
                player.isCameraLocked = wasLocked;
                IsPlaying = false;
            }
        }

        private static IEnumerator OpenEyes(CanvasGroup fade, Transform eye, Quaternion lying, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                SetFade(fade, BlinkOpacity(k));
                eye.rotation = lying * Quaternion.Euler(Mathf.Sin(t * 1.1f) * .8f, Mathf.Sin(t * .7f) * 1.2f, 0f);
                yield return null;
            }
            SetFade(fade, 0f);
            eye.rotation = lying;
        }

        // Непрозрачность век в момент k (0..1): полузакрытые глаза дважды смыкаются и открываются до конца.
        public static float BlinkOpacity(float k)
        {
            k = Mathf.Clamp01(k);
            for (int i = 1; i < BlinkTime.Length; i++)
                if (k <= BlinkTime[i])
                    return Mathf.Lerp(BlinkAlpha[i - 1], BlinkAlpha[i], Mathf.InverseLerp(BlinkTime[i - 1], BlinkTime[i], k));
            return 0f;
        }

        private static IEnumerator Move(Transform eye, Vector3 from, Vector3 to, Quaternion fromRotation, Quaternion toRotation, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                eye.SetPositionAndRotation(Vector3.Lerp(from, to, k), Quaternion.Slerp(fromRotation, toRotation, k));
                yield return null;
            }
            eye.SetPositionAndRotation(to, toRotation);
        }

        private static IEnumerator Wait(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime) yield return null;
        }

        private static void SetFade(CanvasGroup fade, float alpha)
        {
            if (fade != null) fade.alpha = alpha;
        }

        private void PlayCloth(float volume)
        {
            AudioClip clip = GameSounds.Library != null ? GameSounds.Pick(GameSounds.Library.cloth) : null;
            if (foley != null && clip != null) foley.PlayOneShot(clip, volume);
        }
    }
}
