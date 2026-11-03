using UnityEngine;

namespace KnocksFromBeneath
{
    // Инструмент, лежащий в мире: взять можно в любой момент, без квеста.
    public sealed class ToolPickup : MonoBehaviour
    {
        [SerializeField] private string itemId = "Screwdriver";
        [SerializeField] private string prompt = Loc.Key("Взять отвёртку");

        // Сюжетные сцены (поиск отвёртки) слушают подбор по id предмета.
        public static event System.Action<string> Taken;

        public string Prompt => Loc.T(prompt);

        public void Take(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.AddItem(itemId)) return;
            gameObject.SetActive(false);
            Taken?.Invoke(itemId);
        }
    }
}
