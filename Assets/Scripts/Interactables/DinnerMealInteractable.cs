using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class DinnerMealInteractable : MonoBehaviour
    {
        [SerializeField] private StoveInteractable stove;

        public bool CanInteract => stove != null && stove.IsReady &&
            QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("dinner-eat");

        public void Interact()
        {
            if (CanInteract) stove.TryEatDinner();
        }
    }
}
