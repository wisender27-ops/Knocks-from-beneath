using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class TapePlaybackInteractable : MonoBehaviour
    {
        private IntroSequence intro;
        public bool CanInteract => intro != null && intro.CanPlayNightTape;
        public void Initialize(IntroSequence owner) => intro = owner;
        public void Interact() { if (CanInteract) intro.PlayNightRecording(); }
    }
}
