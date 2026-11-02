using UnityEngine;

namespace KnocksFromBeneath
{
    // Инструмент, лежащий в мире: взять можно в любой момент, без квеста.
    public sealed class ToolPickup : MonoBehaviour
    {
        [SerializeField] private string itemId = "Screwdriver";
        [SerializeField] private string prompt = "Взять отвёртку";

        public string Prompt => prompt;

        public void Take(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.AddItem(itemId)) return;
            gameObject.SetActive(false);
        }
    }
}
