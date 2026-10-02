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
        private bool isOpen;
        public bool IsBoarded { get; private set; }
        public bool IsOpen => isOpen && angle >= openAngle * 0.85f;
        public bool IsLocked => lock_ != null && lock_.IsLocked;

        private void Awake()
        {
            ApplyRotation();
            enabled = false;
        }

        // Пробует открыть створки. Пока замок цел — только отказ (дребезг), без движения.
        public bool TryOpen()
        {
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

        public void Seal()
        {
            IsBoarded = true;
            Close();
        }

        public void OpenForCinematic()
        {
            if (!IsBoarded) Open();
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
            float target = isOpen ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, degreesPerSecond * Time.deltaTime);
            ApplyRotation();
            if (Mathf.Approximately(angle, target)) enabled = false;
        }

        private void ApplyRotation()
        {
            if (leftLeaf != null) leftLeaf.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (rightLeaf != null) rightLeaf.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
