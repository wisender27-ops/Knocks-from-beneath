using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class BasementHatch : MonoBehaviour
    {
        [SerializeField] private Transform leftLeaf;
        [SerializeField] private Transform rightLeaf;
        [SerializeField] private float openAngle = 58f;
        [SerializeField] private float degreesPerSecond = 90f;

        [Tooltip("Замок на створках. Пока он не вскрыт ломом, створки не открываются — " +
                 "E даёт отказ. Не задан — люк открывается свободно (обратная совместимость).")]
        [SerializeField] private BasementHatchLock lock_;

        private float angle;
        private float jolt;
        private bool isOpen;
        private bool lockedResponseUsed;
        private Action lockedResponseComplete;
        private AudioSource responseAudio;
        private Quaternion responseLeftRotation, responseRightRotation;
        public bool IsLockedResponsePlaying { get; private set; }
        public bool IsBoarded { get; private set; }
        public bool IsOpen => isOpen && angle >= openAngle * 0.85f;
        public bool IsMoving => Mathf.Abs(angle - (isOpen ? openAngle : 0f)) > .01f;
        public float InteractionProgress => Mathf.Clamp01(isOpen ? angle / Mathf.Max(.01f, openAngle) : 1f - angle / Mathf.Max(.01f, openAngle));
        public bool IsLocked => lock_ != null && lock_.IsLocked;

        private void Awake()
        {
            ApplyRotation();
            enabled = false;
        }

        // Пробует открыть створки. Пока замок цел — только отказ (дребезг), без движения.
        public bool TryOpen()
        {
            if (IsLockedResponsePlaying) return false;
            if (IsBoarded) return false;
            if (isOpen)
            {
                Close();
                return true;
            }

            if (lock_ != null && !lock_.TryUnlockByHand())
                return false;

            Open();
            return true;
        }

        public void Interact() => TryOpen();

        public bool TryPlayLockedResponse(Action onComplete)
        {
            if (IsLockedResponsePlaying) return true;
            if (lockedResponseUsed || !IsLocked || IsBoarded || leftLeaf == null || rightLeaf == null)
                return false;
            lockedResponseUsed = true;
            IsLockedResponsePlaying = true;
            lockedResponseComplete = onComplete;
            responseLeftRotation = leftLeaf.localRotation;
            responseRightRotation = rightLeaf.localRotation;
            enabled = true;
            StartCoroutine(LockedResponseRoutine());
            return true;
        }

        private IEnumerator LockedResponseRoutine()
        {
            var clip = Resources.Load<AudioClip>("Audio/Horror/HatchAnswer");
            if (clip == null) clip = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
            if (clip == null || HorrorMomentDirector.Instance == null)
            {
                FinishLockedResponse();
                yield break;
            }

            float wait = 0f;
            var director = HorrorMomentDirector.Instance;
            while (director != null && (!director.CanPresent || !director.TryBegin(this, true)))
            {
                if (!IsLocked || IsBoarded || wait >= 8f)
                {
                    FinishLockedResponse();
                    yield break;
                }
                if (director.GameplayAvailable) wait += Time.unscaledDeltaTime;
                yield return null;
            }
            if (director == null || !IsLocked || IsBoarded)
            {
                FinishLockedResponse();
                yield break;
            }

            var soundObject = new GameObject("Hatch interior response");
            soundObject.transform.SetParent(transform, false);
            soundObject.transform.position = lock_.StrikePoint - Vector3.up * 0.15f;
            responseAudio = soundObject.AddComponent<AudioSource>();
            responseAudio.outputAudioMixerGroup = director.OutputMixerGroup;
            responseAudio.playOnAwake = false;
            responseAudio.spatialBlend = 1f;
            responseAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            responseAudio.minDistance = 1.2f;
            responseAudio.maxDistance = 14f;
            responseAudio.volume = 0.8f;
            responseAudio.clip = clip;

            // Два удара изнутри после дребезга; замок и логическое открытие не меняются.
            float elapsed = 0f;
            int strike = 0;
            bool paused = false;
            while (elapsed < 2.8f)
            {
                if (director == null || !director.IsOwner(this) || !IsLocked || IsBoarded
                    || leftLeaf == null || rightLeaf == null || responseAudio == null)
                    break;
                if (!director.GameplayAvailable)
                {
                    if (!paused) { responseAudio.Pause(); paused = true; }
                    yield return null;
                    continue;
                }
                if (paused) { responseAudio.UnPause(); paused = false; }
                elapsed += Time.deltaTime;
                float pulse = 0f;
                if (elapsed >= 0.9f && strike == 0)
                {
                    responseAudio.pitch = 0.9f;
                    responseAudio.Play();
                    strike = 1;
                }
                if (elapsed >= 1.52f && strike == 1)
                {
                    responseAudio.pitch = 0.84f;
                    responseAudio.Play();
                    strike = 2;
                }
                float sinceStrike = elapsed - (strike == 2 ? 1.52f : 0.9f);
                if (strike > 0 && sinceStrike < 0.42f)
                    pulse = Mathf.Sin(Mathf.Clamp01(sinceStrike / 0.42f) * Mathf.PI)
                            * Mathf.Exp(-sinceStrike * 7f) * 1.4f;
                leftLeaf.localRotation = responseLeftRotation * Quaternion.Euler(0f, 0f, pulse);
                rightLeaf.localRotation = responseRightRotation * Quaternion.Euler(0f, 0f, -pulse * 0.65f);
                yield return null;
            }
            FinishLockedResponse();
        }

        private void FinishLockedResponse()
        {
            if (!IsLockedResponsePlaying) return;
            IsLockedResponsePlaying = false;
            if (leftLeaf != null) leftLeaf.localRotation = responseLeftRotation;
            if (rightLeaf != null) rightLeaf.localRotation = responseRightRotation;
            if (responseAudio != null)
            {
                responseAudio.Stop();
                Destroy(responseAudio.gameObject);
                responseAudio = null;
            }
            var director = HorrorMomentDirector.Instance;
            if (director != null && director.IsOwner(this)) director.End(this, 18f);
            var complete = lockedResponseComplete;
            lockedResponseComplete = null;
            complete?.Invoke();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            FinishLockedResponse();
        }

        public void Seal()
        {
            IsBoarded = true;
            Close();
        }

        public void OpenForCinematic()
        {
            if (!IsBoarded) Open();
        }

        // «Гвозди»: створки закрываются под доски, но люк ещё не заколочен.
        public void CloseForNailing() => Close();

        // Удар изнутри: створки подпрыгивают на strength градусов и падают обратно.
        public void Jolt(float strength)
        {
            if (leftLeaf == null || rightLeaf == null || IsLockedResponsePlaying) return;
            jolt = Mathf.Max(jolt, strength);
            enabled = true;
        }

        private void Open()
        {
            if (leftLeaf == null || rightLeaf == null) return;
            isOpen = true;
            enabled = true;
        }

        private void Close()
        {
            if (leftLeaf == null || rightLeaf == null) return;
            isOpen = false;
            enabled = true;
        }

        // Отладочный пропуск: снять замок и открыть створки, минуя удар ломом.
        // Используется стадиями StartHammerQuest / StartFinale / StartBranchEndings.
        public void DebugUnlock()
        {
            if (lock_ != null) lock_.DebugForceBroken();
            ApplyRotation();
            Open();
        }

        private void Update()
        {
            if (IsLockedResponsePlaying) return;
            float target = isOpen ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, degreesPerSecond * Time.deltaTime);
            jolt = Mathf.MoveTowards(jolt, 0f, (jolt * 9f + 2f) * Time.deltaTime);
            ApplyRotation();
            if (Mathf.Approximately(angle, target) && jolt <= 0f) enabled = false;
        }

        private void ApplyRotation()
        {
            if (leftLeaf != null) leftLeaf.localRotation = Quaternion.Euler(0f, 0f, angle + jolt);
            if (rightLeaf != null) rightLeaf.localRotation = Quaternion.Euler(0f, 0f, -angle - jolt * 0.65f);
        }
    }
}
