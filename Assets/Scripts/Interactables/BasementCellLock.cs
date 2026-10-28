using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // P-03 / B-05: замок на решётке камеры хозяина. Логику и условия задаёт BasementChapter.
    public sealed class BasementCellLock : MonoBehaviour
    {
        private Func<bool> canInteract;
        private Action interact;
        private Func<string> label;

        public void Initialize(Func<bool> can, Action action, Func<string> prompt = null)
        {
            canInteract = can;
            interact = action;
            label = prompt;
        }

        public bool CanInteract => canInteract != null && canInteract();
        public string Label => label != null ? label() : "Вскрыть замок";

        public void Interact()
        {
            if (CanInteract) interact?.Invoke();
        }
    }
}
