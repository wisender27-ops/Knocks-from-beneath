using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class HorrorMomentDirector : MonoBehaviour
    {
        public static HorrorMomentDirector Instance { get; private set; }
        private PlayerController player;
        private Object owner;
        private float quietUntil;
        public UnityEngine.Audio.AudioMixerGroup OutputMixerGroup { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public bool GameplayAvailable => isActiveAndEnabled && Time.timeScale > 0f &&
            player != null && player.isActiveAndEnabled && !player.isCameraLocked &&
            !FadeTransitionPresentation.IsActive && !RetroInventoryScreen.IsAnyOpen &&
            !QuestJournalScreen.IsAnyOpen && !PlayerInteraction.IsLockSequencePlaying;

        public bool CanPresent => GameplayAvailable && NarrativeDisplayQueue.CanShowInteractionHint;

        public void Initialize(PlayerInventory inventory)
        {
            player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            Instance = this;
            var bob = player != null ? player.GetComponentInChildren<HeadBobbing>(true) : null;
            var reference = bob != null ? bob.footstepSource : null;
            if (reference == null && player != null) reference = player.GetComponent<AudioSource>();
            OutputMixerGroup = reference != null ? reference.outputAudioMixerGroup : null;
        }

        public bool TryBegin(Object requester, bool ignoreCooldown = false)
        {
            if (requester == null || owner != null || !CanPresent ||
                (!ignoreCooldown && Time.time < quietUntil)) return false;
            owner = requester;
            return true;
        }

        public bool IsOwner(Object requester) => requester != null && owner == requester;

        public void End(Object requester, float recoverySeconds = 15f)
        {
            if (!IsOwner(requester)) return;
            owner = null;
            quietUntil = Mathf.Max(quietUntil, Time.time + Mathf.Max(0f, recoverySeconds));
        }

        public void Interrupt(float recoverySeconds = 20f)
        {
            owner = null;
            quietUntil = Mathf.Max(quietUntil, Time.time + Mathf.Max(0f, recoverySeconds));
        }

        public static AudioSource CreateSource(Transform parent, string sourceName, float distance = 12f)
        {
            var sound = new GameObject(sourceName);
            sound.transform.SetParent(parent, false);
            var source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.8f;
            source.maxDistance = distance;
            source.priority = 100;
            if (Instance != null) source.outputAudioMixerGroup = Instance.OutputMixerGroup;
            return source;
        }

        public static void SetAudioPaused(AudioSource source, bool shouldPause, ref bool paused)
        {
            if (source == null || shouldPause == paused) return;
            if (shouldPause) source.Pause(); else source.UnPause();
            paused = shouldPause;
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            Interrupt(0f);
            if (Instance == this) Instance = null;
        }
    }
}
