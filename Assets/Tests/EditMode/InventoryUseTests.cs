using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public sealed class InventoryTestConsumer : MonoBehaviour, IInventoryConsumableTarget
{
    public bool Accept;
    public int Calls;
    public bool TryConsume(InventoryItemDefinition definition) { Calls++; return Accept; }
}

public class InventoryUseTests
{
    private GameObject player;
    private PlayerInventory inventory;
    private InventoryDatabase database;
    private InventoryItemDefinition item;

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("InventoryUseTest");
        inventory = player.AddComponent<PlayerInventory>();
        database = ScriptableObject.CreateInstance<InventoryDatabase>();
        item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        Set(item, "id", "Flashlight");
        Set(item, "category", InventoryItemCategory.Tool);
        Set(item, "maxStack", 3);
        Set(database, "items", new[] { item });
        Set(inventory, "database", database);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(database);
        Object.DestroyImmediate(item);
    }

    [Test]
    public void Use_EquipsAndUnequipsWithoutConsuming()
    {
        var held = new GameObject("HeldFlashlight");
        held.transform.SetParent(player.transform);
        inventory.flashlightInHand = held;
        inventory.AddItem("Flashlight");
        int changes = 0;
        inventory.Changed += () => changes++;
        Assert.That(inventory.UseItemAt(0), Is.True);
        Assert.That(inventory.EquippedItemId, Is.EqualTo("Flashlight"));
        Assert.That(held.activeSelf, Is.True);
        Assert.That(inventory.UseItemAt(0), Is.True);
        Assert.That(inventory.EquippedItemId, Is.Empty);
        Assert.That(held.activeSelf, Is.False);
        Assert.That(inventory.Entries[0].Quantity, Is.EqualTo(1));
        Assert.That(inventory.HasItem("Flashlight"), Is.True);
        Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void Use_MissingHeldObjectDoesNotReportSuccessOrChangeEquipment()
    {
        inventory.AddItem("Flashlight");
        Assert.That(inventory.UseItemAt(0), Is.False);
        Assert.That(inventory.EquippedItemId, Is.Empty);
        Assert.That(inventory.Entries[0].Quantity, Is.EqualTo(1));
    }

    [Test]
    public void Use_PhysicalCarryBlocksEquipment()
    {
        var held = new GameObject("HeldFlashlight");
        held.transform.SetParent(player.transform);
        inventory.flashlightInHand = held;
        var pickup = player.AddComponent<PickupController>();
        Set(pickup, "_heldObj", held);
        inventory.AddItem("Flashlight");
        Assert.That(inventory.UseItemAt(0), Is.False);
        Assert.That(inventory.EquippedItemId, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Use_ConsumableConsumesOnlyWhenAccepted(bool accept)
    {
        Set(item, "category", InventoryItemCategory.Consumable);
        var consumer = player.AddComponent<InventoryTestConsumer>();
        consumer.Accept = accept;
        inventory.AddItem("Flashlight", 2);
        int changes = 0;
        inventory.Changed += () => changes++;
        Assert.That(inventory.UseItemAt(0), Is.EqualTo(accept));
        Assert.That(consumer.Calls, Is.EqualTo(1));
        Assert.That(inventory.Entries[0].Quantity, Is.EqualTo(accept ? 1 : 2));
        Assert.That(changes, Is.EqualTo(accept ? 1 : 0));
    }

    [Test]
    public void Use_LastConsumableRemovesEntryAndLegacyFlag()
    {
        Set(item, "category", InventoryItemCategory.Consumable);
        player.AddComponent<InventoryTestConsumer>().Accept = true;
        inventory.AddItem("Flashlight");
        Assert.That(inventory.UseItemAt(0), Is.True);
        Assert.That(inventory.Entries, Is.Empty);
        Assert.That(inventory.HasItem("Flashlight"), Is.False);
        Assert.That(inventory.UseItemAt(0), Is.False);
    }

    [Test]
    public void Use_OrdinaryItemAndInvalidIndicesDoNothing()
    {
        Set(item, "category", InventoryItemCategory.Item);
        inventory.AddItem("Flashlight");
        Assert.That(inventory.UseItemAt(-1), Is.False);
        Assert.That(inventory.UseItemAt(1), Is.False);
        Assert.That(inventory.UseItemAt(0), Is.False);
        Assert.That(inventory.Entries[0].Quantity, Is.EqualTo(1));
    }
}
