using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10030)]
    public sealed class FollowingFootsteps : MonoBehaviour
    {
        private readonly Vector3[] trail = new Vector3[80];
        private PlayerController player;
        private Camera camera;
        private AudioSource source;
        private AudioClip[] clips;
        private string stage;
        private int head, count, bursts, beat;
        private float travelled, nextDistance, delay, nextBeat, fade;
        private Vector3 previous, facing, soundPoint;
        private bool paired, paused, initialized;

        public void Initialize(PlayerInventory inventory)
        {
            player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            camera = inventory != null ? inventory.GetComponentInChildren<Camera>() : null;
            if (camera == null) camera = Camera.main;
            source = HorrorMomentDirector.CreateSource(transform, "Steps following player", 13f);
            source.minDistance = 2.2f;
            clips = new[] { Resources.Load<AudioClip>("Audio/Horror/FollowingStep1"),
                Resources.Load<AudioClip>("Audio/Horror/FollowingStep2") };
            initialized = true;
            OnQuestChanged(QuestManager.Instance != null ? QuestManager.Instance.GetActiveQuestTag() : "");
        }

        private void OnEnable() => QuestManager.OnActiveQuestTagChanged += OnQuestChanged;
        private void OnDisable()
        {
            QuestManager.OnActiveQuestTagChanged -= OnQuestChanged;
            Cancel();
        }

        private void OnQuestChanged(string tag)
        {
            if (!initialized || tag == stage) return;
            Cancel();
            stage = tag;
            head = count = bursts = beat = 0;
            travelled = delay = 0f;
            nextDistance = tag == "board-basement" ? 1.1f : 1.6f;
            if (player != null) previous = player.transform.position;
        }

        private void LateUpdate()
        {
            if (!initialized || player == null || camera == null ||
                (stage != "hammer-find" && stage != "board-basement")) return;
            var director = HorrorMomentDirector.Instance;
            if (director == null) { Cancel(); return; }
            if (!director.GameplayAvailable)
            {
                HorrorMomentDirector.SetAudioPaused(source, true, ref paused);
                previous = player.transform.position;
                return;
            }
            HorrorMomentDirector.SetAudioPaused(source, false, ref paused);
            Vector3 position = player.transform.position;
            float distance = Vector3.Distance(previous, position);
            if (distance > 1.5f) { count = 0; Cancel(); distance = 0f; }
            travelled += distance;
            if (count == 0 || Vector3.Distance(trail[head], position) >= 0.18f)
            {
                head = (head + 1) % trail.Length;
                trail[head] = position;
                count = Mathf.Min(count + 1, trail.Length);
            }
            previous = position;
            bool moving = distance > 0.0005f && player.PlanarVelocity.sqrMagnitude > 0.025f;
            if (paired)
            {
                if (!director.IsOwner(this)) { Cancel(); return; }
                // При остановке или проверке спины шаги быстро затихают, не преследуя камеру.
                if (!moving || Vector3.Dot(facing, camera.transform.forward) < 0.3f)
                {
                    fade += Time.deltaTime;
                    source.volume = Mathf.Lerp(0.72f, 0f, fade / 0.12f);
                    if (fade >= 0.12f) FinishBurst();
                    return;
                }
                if (!director.CanPresent) { FinishBurst(); return; }
                delay += Time.deltaTime;
                if (beat < (stage == "board-basement" ? 1 : 2) && delay >= nextBeat)
                {
                    source.transform.position = soundPoint;
                    source.volume = stage == "board-basement" ? 0.62f : 0.72f;
                    source.pitch = beat == 0 ? 0.82f : 0.76f;
                    source.clip = clips[(bursts + beat) % clips.Length];
                    if (source.clip != null) source.Play();
                    beat++;
                    nextBeat += 0.48f;
                }
                if (delay >= (stage == "board-basement" ? 0.9f : 1.45f)) FinishBurst();
                return;
            }
            int limit = stage == "board-basement" ? 1 : 2;
            if (!moving || bursts >= limit || travelled < nextDistance || count < 6 || !director.CanPresent) return;
            if (!TryFindPoint(position, out soundPoint) || !director.TryBegin(this)) return;
            paired = true;
            facing = camera.transform.forward;
            delay = fade = 0f;
            nextBeat = 0.26f;
            beat = 0;
        }

        private bool TryFindPoint(Vector3 position, out Vector3 point)
        {
            point = Vector3.zero;
            for (int i = 1; i < count; i++)
            {
                Vector3 candidate = trail[(head - i + trail.Length) % trail.Length];
                if (Vector3.Distance(position, candidate) < 1.6f) continue;
                Vector3 direction = candidate - position;
                if (Vector3.Dot(direction.normalized, camera.transform.forward) > -0.2f) return false;
                if (!Physics.Raycast(candidate + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor,
                    2.5f, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore)) return false;
                point = floor.point + Vector3.up * 0.12f;
                return !Physics.Linecast(camera.transform.position, point + Vector3.up * 0.35f,
                    PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            }
            return false;
        }

        private void FinishBurst()
        {
            if (beat > 0) bursts++;
            nextDistance = travelled + 3.2f;
            HorrorMomentDirector.Instance?.End(this, stage == "board-basement" ? 4f : 2f);
            paired = false;
            if (source != null) source.Stop();
        }

        private void Cancel()
        {
            if (source != null) source.Stop();
            HorrorMomentDirector.Instance?.End(this, 0f);
            paired = false;
            paused = false;
        }
    }
}
