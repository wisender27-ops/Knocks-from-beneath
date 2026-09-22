using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    [CreateAssetMenu(menuName = "Knocks From Beneath/Inventory/Database", fileName = "InventoryDatabase")]
    public sealed class InventoryDatabase : ScriptableObject
    {
        [SerializeField] private InventoryItemDefinition[] items;
        public IReadOnlyList<InventoryItemDefinition> Items => items;

        public InventoryItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id) || items == null) return null;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].Id == id) return items[i];
            return null;
        }
    }
}
