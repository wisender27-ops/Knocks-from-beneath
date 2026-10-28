using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class TapeTransport
    {
        public const string EvidenceId = "night-tape-face";
        public float Time { get; private set; }
        public float Speed { get; private set; }
        public bool Playing { get; private set; } = true;
        public bool IsStopped => !Playing && Mathf.Abs(Speed) < 0.001f;

        public void Synchronize(float time) => Time = NightTape.Clamp(time);

        public void TogglePlay()
        {
            bool wasMoving = Playing || Mathf.Abs(Speed) > 0.001f;
            Speed = 0f;
            Playing = !wasMoving;
            if (Playing && Time >= NightTape.Duration) Time = 0f;
        }

        public void Advance(float deltaTime, int shuttle)
        {
            if (deltaTime <= 0f) return;
            shuttle = Mathf.Clamp(shuttle, -1, 1);
            if (shuttle != 0) Playing = false;
            Speed = Mathf.MoveTowards(Speed, shuttle * 6f, deltaTime * 18f);
            Time = NightTape.Clamp(Time + (Playing ? 1f : Speed) * deltaTime);
            if (Time >= NightTape.Duration && (Playing || Speed > 0f)) { Playing = false; Speed = 0f; }
            if (Time <= 0f && Speed < 0f) Speed = 0f;
        }

        public bool CanCapture
        {
            get
            {
                TapeFigurePose pose = NightTape.Figure(Time);
                return IsStopped && pose.Visible && pose.Look >= 0.9f;
            }
        }

        public bool CaptureEvidence()
        {
            if (!CanCapture) return false;
            StoryFlags.MarkRecordingSeen();
            StoryFlags.RegisterClue(EvidenceId);
            return true;
        }
    }
}
