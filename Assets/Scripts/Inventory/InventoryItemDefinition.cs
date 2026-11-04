using UnityEngine;

namespace KnocksFromBeneath
{
    public enum InventoryItemCategory { Item, Consumable, Tool, Weapon }

    [CreateAssetMenu(menuName = "Knocks From Beneath/Inventory/Item", fileName = "InventoryItem")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(3, 7)] [SerializeField] private string description;
        [SerializeField] private InventoryItemCategory category;
        [Min(1)] [SerializeField] private int maxStack = 1;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject previewPrefab;
        [SerializeField] private Texture2D previewTextureOverride;

        public string Id => id;
        // Поля хранят русский текст — ключ Loc; наружу отдаётся перевод на текущий язык.
        public string DisplayName => Loc.T(displayName);
        public string Description => Loc.T(description);
        public InventoryItemCategory Category => category;
        public int MaxStack => maxStack;
        public Sprite Icon => icon;
        public GameObject PreviewPrefab => previewPrefab;
        public Texture2D PreviewTextureOverride => previewTextureOverride;
        public bool IsEquippable => category == InventoryItemCategory.Tool || category == InventoryItemCategory.Weapon;
    }
}
