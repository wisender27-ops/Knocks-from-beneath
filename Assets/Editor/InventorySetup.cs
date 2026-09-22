#if UNITY_EDITOR
using KnocksFromBeneath;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InventorySetup
{
    private const string Root = "Assets/Inventory";

    public static void Install()
    {
        EnsureFolder("Assets", "Inventory");
        EnsureFolder(Root, "Items");
        EnsureFolder(Root, "Prefabs");

        InventoryItemDefinition crowbar = CreateItem(
            "Crowbar", "ЛОМ", "Тяжёлый старый лом. Пригодится, чтобы вскрывать и ломать непрочные преграды.",
            InventoryItemCategory.Weapon, "Assets/Textures/crowbar.png", "Assets/Prefabs/Models/Crowbar/CrowBar_Low-Poly.fbx");
        InventoryItemDefinition flashlight = CreateItem(
            "Flashlight", "ФОНАРИК", "Потёртый фонарик. Луч слабый, но без него в тёмных комнатах почти ничего не видно.",
            InventoryItemCategory.Tool, "Assets/Textures/flashlight.png", "Assets/Prefabs/Models/Flashlight/flashlight.fbx");
        InventoryItemDefinition hammer = CreateItem(
            "Hammer", "МОЛОТОК", "Прочный рабочий молоток. Нужен для ремонта и некоторых механизмов в доме.",
            InventoryItemCategory.Tool, "Assets/Textures/hammer.png", "Assets/Prefabs/Hammer/Hammer.fbx", "Assets/Prefabs/Hammer/Hammer_BaseColor.png");

        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(Root + "/InventoryDatabase.asset");
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<InventoryDatabase>();
            AssetDatabase.CreateAsset(database, Root + "/InventoryDatabase.asset");
        }
        SerializedObject databaseSerialized = new SerializedObject(database);
        SerializedProperty items = databaseSerialized.FindProperty("items");
        items.arraySize = 3;
        items.GetArrayElementAtIndex(0).objectReferenceValue = crowbar;
        items.GetArrayElementAtIndex(1).objectReferenceValue = flashlight;
        items.GetArrayElementAtIndex(2).objectReferenceValue = hammer;
        databaseSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabRoot = new GameObject("RetroInventoryScreen");
        RetroInventoryScreen screen = prefabRoot.AddComponent<RetroInventoryScreen>();
        SerializedObject screenSerialized = new SerializedObject(screen);
        screenSerialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/VCR OSD Mono Cyr SDF.asset");
        screenSerialized.ApplyModifiedPropertiesWithoutUndo();
        string prefabPath = Root + "/Prefabs/RetroInventoryScreen.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        Object.DestroyImmediate(prefabRoot);

        PlayerInventory playerInventory = Object.FindAnyObjectByType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[InventorySetup] PlayerInventory not found in active scene.");
            return;
        }

        SerializedObject inventorySerialized = new SerializedObject(playerInventory);
        inventorySerialized.FindProperty("database").objectReferenceValue = database;
        inventorySerialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(playerInventory);

        RetroInventoryScreen existing = Object.FindAnyObjectByType<RetroInventoryScreen>();
        if (existing == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Retro Inventory Screen";
            existing = instance.GetComponent<RetroInventoryScreen>();
        }

        SerializedObject existingSerialized = new SerializedObject(existing);
        existingSerialized.FindProperty("inventory").objectReferenceValue = playerInventory;
        existingSerialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(existing);

        EditorUtility.SetDirty(playerInventory);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[InventorySetup] Inventory assets, prefab and scene instance installed.");
    }

    private static InventoryItemDefinition CreateItem(string id, string displayName, string description, InventoryItemCategory category, string iconPath, string prefabPath, string previewTexturePath = null)
    {
        string path = Root + "/Items/" + id + ".asset";
        InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("maxStack").intValue = 1;
        serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        serialized.FindProperty("previewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        serialized.FindProperty("previewTextureOverride").objectReferenceValue = string.IsNullOrEmpty(previewTexturePath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(previewTexturePath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
