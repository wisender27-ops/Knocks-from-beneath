using UnityEditor;
using UnityEngine;

namespace KnocksFromBeneath.EditorTools
{

// Сборка замка на уличном люке в подвал. Отдельный editor-скрипт, а не ручная расстановка:
// геометрию нужно пересобирать итеративно (правка формы, размеров, посадки на створку),
// и вручную это означало бы каждый раз кликать в инспекторе.
//
// Запуск: Window > Hermes > Rebuild Basement Hatch Lock
//
// Идемпотентен: повторный запуск пересоздаёт только своё поддерево BasementHatchLock.
// При наличии FBX из Blender использует его, иначе собирает замок из примитивов.
public static class BasementHatchLockBuilder
{
    private const string RootName = "BasementHatchLock";

    // Геометрия из Blender. Её локальное начало — точка, где накладка ложится
    // на створку; нужные смещения детей (корпус под накладкой, дуга в корпусе)
    // уже зашиты в FBX.
    private const string ModelPath = "Assets/Basement/Models/HatchLock.fbx";

    // Габариты замка в метрах — используются и для коллайдеров, и для проверки,
    // что ассет импортировался в нормальном масштабе.
    private static readonly Vector3 HaspSize = new Vector3(0.30f, 0.05f, 0.10f);
    private static readonly Vector3 PadlockSize = new Vector3(0.13f, 0.25f, 0.07f);

    // Параметры запасного варианта на примитивах.
    private const float HaspLength = 0.30f;
    private const float HaspWidth = 0.10f;
    private const float HaspThickness = 0.018f;
    private const float BodyWidth = 0.13f;
    private const float BodyHeight = 0.16f;
    private const float BodyDepth = 0.055f;
    private const float ShackleThickness = 0.022f;

    [MenuItem("Window/Hermes/Rebuild Basement Hatch Lock")]
    public static void Rebuild()
    {
        var hatch = Object.FindFirstObjectByType<BasementHatch>();
        if (hatch == null)
        {
            Debug.LogError("[BasementHatchLockBuilder] В сцене нет BasementHatch — не на что вешать замок.");
            return;
        }

        foreach (var stale in Object.FindObjectsByType<BasementHatchLock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (stale.gameObject.name == RootName)
                Object.DestroyImmediate(stale.gameObject);
        }

        // Замок живёт на ПРАВОЙ створке: левая откроется первой и не должна утаскивать
        // накладку с собой — иначе замок «уедет» вместе с открытой дверью.
        var leaf = FindChildByName(hatch.transform, "RightLeaf");
        if (leaf == null)
        {
            Debug.LogError("[BasementHatchLockBuilder] У люка нет створки RightLeaf.");
            return;
        }

        Vector3 anchor = FindHaspAnchor(leaf);

        // Чиним масштаб ассета до инстанцирования, иначе на сцене снова окажется
        // накладка размером 30 м вместо 0.30.
        BakeFbxScale(ModelPath);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model != null)
        {
            BuildFromModel(hatch, leaf, anchor, model);
            return;
        }

        BuildFromPrimitives(hatch, leaf, anchor);
    }

    // ------------------------------------------------------------------
    // Вариант 1: настоящая модель из Blender
    // ------------------------------------------------------------------
    private static void BuildFromModel(BasementHatch hatch, Transform leaf, Vector3 anchor, GameObject model)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = RootName;
        instance.transform.SetParent(leaf, false);
        instance.transform.localPosition = anchor;
        instance.transform.localRotation = Quaternion.identity;

        var hasp = FindChildByName(instance.transform, "HASP");
        var padlock = FindChildByName(instance.transform, "Padlock");
        // В FBX дуга называется Shackle — префикс Padlock остался от сборки на примитивах.
        var shackle = padlock != null ? FindChildByName(padlock, "Shackle") : null;

        if (hasp == null || padlock == null)
        {
            Debug.LogError($"[BasementHatchLockBuilder] В префабе нет HASP/Padlock: {ModelPath}");
            return;
        }

        // Материалы LockSteel/LockSteelDark из FBX используют другой шейдер и
        // выбивались бы из сцены — подменяем на набор подвала.
        Paint(hasp, LoadMaterial("Assets/Basement/Materials/DarkMetal.mat"));
        Paint(padlock, LoadMaterial("Assets/Basement/Materials/IvoryMetal.mat"));

        // У FBX-мешей нет коллайдеров, а без них не работают ни подсказка, ни удар ломом.
        EnsureBoxCollider(hasp.gameObject, HaspSize);
        EnsureBoxCollider(padlock.gameObject, PadlockSize);

        var lockComponent = instance.GetComponent<BasementHatchLock>() ?? instance.AddComponent<BasementHatchLock>();
        Assign(lockComponent, "padlock", padlock.gameObject);
        Assign(lockComponent, "haspPlate", hasp);
        Assign(lockComponent, "shackle", shackle);

        WireToHatch(hatch, lockComponent);
        EditorUtility.SetDirty(hatch);
        EditorUtility.SetDirty(instance);
        Debug.Log("[BasementHatchLockBuilder] Замок собран из модели Blender.");
    }

    // ------------------------------------------------------------------
    // Вариант 2 (фолбэк): примитивы, если FBX ещё не импортирован
    // ------------------------------------------------------------------
    private static void BuildFromPrimitives(BasementHatch hatch, Transform leaf, Vector3 anchor)
    {
        var root = new GameObject(RootName);
        root.transform.SetParent(leaf, false);
        root.transform.localPosition = anchor;
        root.transform.localRotation = Quaternion.identity;

        var hasp = BuildHasp(root.transform);
        var padlock = BuildPadlock(root.transform);

        Paint(hasp, LoadMaterial("Assets/Basement/Materials/DarkMetal.mat"));
        Paint(padlock, LoadMaterial("Assets/Basement/Materials/IvoryMetal.mat"));

        EnsureBoxCollider(hasp.gameObject, HaspSize);
        EnsureBoxCollider(padlock.gameObject, PadlockSize);

        var lockComponent = root.AddComponent<BasementHatchLock>();
        Assign(lockComponent, "padlock", padlock.gameObject);
        Assign(lockComponent, "haspPlate", hasp);
        Assign(lockComponent, "shackle", FindChildByName(padlock, "PadlockShackle"));

        WireToHatch(hatch, lockComponent);
        EditorUtility.SetDirty(hatch);
        EditorUtility.SetDirty(root);
        Debug.Log("[BasementHatchLockBuilder] Замок собран из примитивов (FBX не найден).");
    }

    // ------------------------------------------------------------------
    // Масштаб FBX
    // ------------------------------------------------------------------
    // Сбрасывает постоянный множитель масштаба в импортированном FBX-ассете.
    //
    // Проблема: FBX из Blender приходит с множителем 100 в root.transform АССЕТА, из-за
    // чего накладка 0.30 м в сцене становится 30 м и перекрывает всю створку.
    //
    // Ключевой факт: МЕШ-БАНДСЫ при этом уже верные (0.300 м) — конвертация
    // сантиметров в метры Unity делает в вершинах. Лишний множитель живёт только
    // в трансформе корня. Поэтому НЕЛЬЗЯ трогать вершины: деление геометрии на 100
    // даёт 0.003 м вместо 0.300 (проверено). Лечится исключительно сбросом
    // трансформов корня.
    //
    // Идемпотентно: если корневой масштаб уже ~1, метод выходит сразу.
    private static void BakeFbxScale(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return;

        float scale = asset.transform.localScale.x;
        if (Mathf.Approximately(scale, 1f) || Mathf.Approximately(scale, 0f)) return;

        foreach (var t in asset.GetComponentsInChildren<Transform>(true))
            t.localScale = Vector3.one;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BasementHatchLockBuilder] Масштаб FBX сброшен: x{scale} -> x1 (геометрия не тронута).");
    }

    // ------------------------------------------------------------------
    // Общее
    // ------------------------------------------------------------------
    private static void WireToHatch(BasementHatch hatch, BasementHatchLock lockComponent)
    {
        // Замок — часть люка, а не отдельная дверь: без этой ссылки BasementHatch
        // не увидит замок и створки откроются руками без лома.
        var so = new SerializedObject(hatch);
        var prop = so.FindProperty("lock_");
        if (prop != null) prop.objectReferenceValue = lockComponent;
        else Debug.LogWarning("[BasementHatchLockBuilder] У BasementHatch нет поля lock_ — проверьте имя.");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Точка крепления — шов между створками на верхней грани панели.
    //
    // Считаем в МИРОВЫХ координатах и только потом переводим в локальные створки.
    // Обратный порядок неверен: створка повёрнута на 17° по X, мировой AABB панели после
    // поворота выше самой панели, и перебор углов в локальных давал якорь на ~0.36 м
    // выше поверхности — замок висел в воздухе.
    private static Vector3 FindHaspAnchor(Transform leaf)
    {
        Collider panel = null;
        float largest = -1f;

        foreach (var col in leaf.GetComponentsInChildren<Collider>())
        {
            float volume = col.bounds.size.x * col.bounds.size.y * col.bounds.size.z;
            if (volume > largest)
            {
                largest = volume;
                panel = col;
            }
        }

        if (panel == null) return new Vector3(-1.02f, 0.42f, 0f);

        Bounds b = panel.bounds;
        // Шов = левая кромка панели правой створки; верх = bounds.max.y.
        return leaf.InverseTransformPoint(new Vector3(b.min.x, b.max.y, b.center.z));
    }

    private static Transform BuildHasp(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "HaspPlate";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, HaspThickness * 0.5f, 0f);
        go.transform.localScale = new Vector3(HaspLength, HaspThickness, HaspWidth);
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        return go.transform;
    }

    private static Transform BuildPadlock(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Padlock";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -BodyHeight * 0.5f - 0.055f, 0.045f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(BodyWidth, BodyDepth, BodyHeight);
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());

        var body = go.transform;

        // Дуга из примитивов — масштабированный цилиндр на торце: в лоу-фай читается
        // как дуга и стоит один объект вместо отдельного тора.
        var shackle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shackle.name = "PadlockShackle";
        shackle.transform.SetParent(body, false);
        shackle.transform.localPosition = new Vector3(0f, 0f, -BodyHeight * 0.5f - 0.012f);
        shackle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        shackle.transform.localScale = new Vector3(ShackleThickness, HaspWidth * 0.42f, ShackleThickness);
        Object.DestroyImmediate(shackle.GetComponent<MeshCollider>());

        return body;
    }

    private static void Paint(Transform target, Material mat)
    {
        if (target == null || mat == null) return;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = mat;
    }

    // BoxCollider.size живёт в ЛОКАЛЬНЫХ единицах и домножается на lossyScale при расчёте
    // bounds, поэтому size = мировые габариты дают коллайдер в десятки раз меньше
    // нужного. Делим на масштаб, чтобы в мире получить ровно запрошенное.
    private static void EnsureBoxCollider(GameObject go, Vector3 worldSize)
    {
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.isTrigger = false;

        Vector3 scale = go.transform.lossyScale;
        box.size = new Vector3(
            SafeDivide(worldSize.x, scale.x),
            SafeDivide(worldSize.y, scale.y),
            SafeDivide(worldSize.z, scale.z));
    }

    private static float SafeDivide(float value, float divisor)
        => Mathf.Approximately(divisor, 0f) ? value : value / Mathf.Abs(divisor);

    private static Material LoadMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) Debug.LogWarning($"[BasementHatchLockBuilder] Не найден материал {path}");
        return mat;
    }

    // Присваивание приватных [SerializeField] полей только через SerializedObject:
    // поля приватные, напрямую из editor-кода недоступны.
    private static void Assign(BasementHatchLock target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[BasementHatchLockBuilder] Нет поля {fieldName} у BasementHatchLock");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
}
