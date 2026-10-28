using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // P-03 / B-05: замок на решётке камеры хозяина. Логику и условия задаёт BasementChapter.
    public sealed class BasementCellLock : MonoBehaviour
    {
        private Func<bool> canInteract;
        private Action interact;

        public void Initialize(Func<bool> can, Action action)
        {
            canInteract = can;
            interact = action;
        }

        public bool CanInteract => canInteract != null && canInteract();

        public void Interact()
        {
            if (CanInteract) interact?.Invoke();
        }
    }
}
