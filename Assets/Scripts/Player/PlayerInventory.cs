using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class InventoryEntry
    {
        public readonly InventoryItemDefinition Definition;
        public int Quantity;
        public InventoryEntry(InventoryItemDefinition definition, int quantity) { Definition = definition; Quantity = quantity; }
    }

    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private InventoryDatabase database;
        [SerializeField] private float switchCooldown = 0.25f;

        [Header("Наличие предметов (Логика)")]
        public bool hasCrowbar;
        public bool hasFlashlight;
        public bool hasHammer;

        [Header("Объекты в руках (Визуал)")]
        public GameObject crowbarInHand;
        public GameObject flashlightInHand;
        public GameObject hammerInHand;

        [Header("Настройки фонарика")]
        public Light flashlightLightSource;
        public AudioSource flashlightAudioSource;
        public AudioClip soundOn;
        public AudioClip soundOff;

        [Header("Настройки удара ломом")]
        public float hitDistance = 2.5f;
        public LayerMask interactableLayer;
        public float damageDelay = 0.2f;

        private readonly List<InventoryEntry> entries = new List<InventoryEntry>(8);
        private float lastSwitchTime = -1f;
        private int currentItemIndex;
        private string equippedItemId = string.Empty;
        private Animator crowbarAnim;

        public event Action Changed;
        public IReadOnlyList<InventoryEntry> Entries => entries;
        public string EquippedItemId => equippedItemId;

        private void Start()
        {
            if (crowbarInHand != null) crowbarAnim = crowbarInHand.GetComponent<Animator>();
            SetHeldObjectsInactive();
            ImportLegacyState();
        }

        private void OnDisable() { CancelInvoke(nameof(CheckHit)); }

        private void Update()
        {
            if (RetroInventoryScreen.IsAnyOpen) return;

            if (Time.time - lastSwitchTime >= switchCooldown)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(2);
                else if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToSlot(3);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f && Time.time - lastSwitchTime >= switchCooldown && entries.Count > 0)
                SwitchToSlot((currentItemIndex + (scroll > 0f ? 1 : -1) + entries.Count) % entries.Count);

            if (!Input.GetMouseButtonDown(0)) return;
            if (crowbarInHand != null && crowbarInHand.activeSelf) PerformCrowbarAttack();
            else if (flashlightInHand != null && flashlightInHand.activeSelf) ToggleFlashlight();
        }

        public bool AddItem(string id, int quantity = 1)
        {
            InventoryItemDefinition definition = database != null ? database.Find(id) : null;
            if (definition == null || quantity <= 0) return false;

            InventoryEntry existing = FindEntry(id);
            if (existing != null)
            {
                if (existing.Quantity >= definition.MaxStack) return false;
                existing.Quantity = Mathf.Min(existing.Quantity + quantity, definition.MaxStack);
            }
            else entries.Add(new InventoryEntry(definition, Mathf.Min(quantity, definition.MaxStack)));

            SetLegacyFlag(id, true);
            Changed?.Invoke();
            return true;
        }

        public bool UseItemAt(int index)
        {
            if (index < 0 || index >= entries.Count) return false;
            InventoryEntry entry = entries[index];

            if (entry.Definition.IsEquippable)
            {
                if (equippedItemId == entry.Definition.Id) Unequip();
                else Equip(entry.Definition.Id);
                return true;
            }

            if (entry.Definition.Category != InventoryItemCategory.Consumable || !TryApplyConsumable(entry.Definition)) return false;
            entry.Quantity--;
            if (entry.Quantity <= 0)
            {
                SetLegacyFlag(entry.Definition.Id, false);
                entries.RemoveAt(index);
            }
            Changed?.Invoke();
            return true;
        }

        public void Equip(string id)
        {
            InventoryEntry entry = FindEntry(id);
            if (entry == null || !entry.Definition.IsEquippable) return;
            equippedItemId = id;
            ActivateItem(id);
            Changed?.Invoke();
        }

        public void Unequip()
        {
            equippedItemId = string.Empty;
            ActivateItem(string.Empty);
            Changed?.Invoke();
        }

        public void ActivateItem(string itemName)
        {
            SetHeldObjectsInactive();
            if (itemName == "Crowbar" && hasCrowbar && crowbarInHand != null) crowbarInHand.SetActive(true);
            else if (itemName == "Flashlight" && hasFlashlight && flashlightInHand != null) flashlightInHand.SetActive(true);
            else if (itemName == "Hammer" && hasHammer && hammerInHand != null) hammerInHand.SetActive(true);
            if (InventoryUI.Instance != null) InventoryUI.Instance.SetActiveSlot(itemName);
        }

        private void SwitchToSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= entries.Count) return;
            currentItemIndex = slotIndex;
            Equip(entries[slotIndex].Definition.Id);
            lastSwitchTime = Time.time;
        }

        private void ImportLegacyState()
        {
            if (hasCrowbar) AddItem("Crowbar");
            if (hasFlashlight) AddItem("Flashlight");
            if (hasHammer) AddItem("Hammer");
            ActivateItem(string.Empty);
        }

        private InventoryEntry FindEntry(string id)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].Definition.Id == id) return entries[i];
            return null;
        }

        private void SetLegacyFlag(string id, bool value)
        {
            if (id == "Crowbar") hasCrowbar = value;
            else if (id == "Flashlight") hasFlashlight = value;
            else if (id == "Hammer") hasHammer = value;
        }

        private bool TryApplyConsumable(InventoryItemDefinition definition)
        {
            IInventoryConsumableTarget[] consumers = GetComponents<IInventoryConsumableTarget>();
            for (int i = 0; i < consumers.Length; i++) if (consumers[i].TryConsume(definition)) return true;
            return false;
        }

        private void SetHeldObjectsInactive()
        {
            if (flashlightLightSource != null) flashlightLightSource.enabled = false;
            if (crowbarInHand != null) crowbarInHand.SetActive(false);
            if (flashlightInHand != null) flashlightInHand.SetActive(false);
            if (hammerInHand != null) hammerInHand.SetActive(false);
        }

        private void ToggleFlashlight()
        {
            if (flashlightLightSource == null) return;
            flashlightLightSource.enabled = !flashlightLightSource.enabled;
            if (flashlightAudioSource == null) return;
            AudioClip clip = flashlightLightSource.enabled ? soundOn : soundOff;
            if (clip != null) flashlightAudioSource.PlayOneShot(clip);
        }

        private void PerformCrowbarAttack()
        {
            if (crowbarAnim == null) return;
            crowbarAnim.SetTrigger("Attack");
            Invoke(nameof(CheckHit), Mathf.Max(0f, damageDelay));
        }

        public void CheckHit()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, hitDistance, interactableLayer)) return;
            BreakableObject breakable = hit.collider.GetComponent<BreakableObject>();
            if (breakable != null) breakable.Break();
            FloorLogic floor = hit.collider.GetComponent<FloorLogic>();
            if (floor != null) floor.Break();
        }
    }

    public interface IInventoryConsumableTarget { bool TryConsume(InventoryItemDefinition definition); }
}
