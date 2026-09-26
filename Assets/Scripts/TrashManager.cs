using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class TrashManager : MonoBehaviour
{
    public static TrashManager Instance;

    [Header("Мешок мусора")]
    public GameObject trashBagPrefab;

    [Header("Звук стука из-под пола")]
    public AudioSource floorKnockSource;
    public AudioClip floorKnockClip;

    [Header("Кучи мусора")]
    public GameObject[] trashPiles;

    [Header("Появление мешка")]
    [Tooltip("Длительность 'поп'-анимации появления мешка, сек. Та же кривая, что у мебели из коробок (EaseOutBack).")]
    [SerializeField] private float bagPopDuration = 0.35f;

    [Tooltip("Высота подскока мешка при появлении, в мирах. 0 — без подскока.")]
    [SerializeField] private float bagPopHop = 0.25f;

    [Tooltip("Звук 'поп' при появлении мешка. Необязателен — без клипа просто тихо.")]
    [SerializeField] private AudioClip bagPopSound;

    [Range(0f, 1f)]
    [SerializeField] private float bagPopVolume = 0.5f;

    [Tooltip("Зазор между нижней гранью мешка и полом в момент появления, м. Мешок опускается на него под тяжестью. 0.03 — почти бесшумно, 0.10 — заметный мягкий 'дроп'.")]
    [SerializeField] private float bagSpawnGap = 0.03f;

    private int _totalPiles;
    private int _collectedCount;
    private bool _bagSpawnStarted;

    // Раньше требуемое число мусора в квесте (3) было захардкожено отдельно в
    // MoveInChoresController.SetupTrashQuest — совпадало с trashPiles.Length только
    // случайно. Теперь квест берёт число прямо отсюда (не считая пустых слотов массива).
    public int PileCount
    {
        get
        {
            if (trashPiles == null) return 0;
            int count = 0;
            for (int i = 0; i < trashPiles.Length; i++)
                if (trashPiles[i] != null) count++;
            return count;
        }
    }

    void Awake()
    {
        Instance = this;
        // Кучи мусора видны с самого начала (захламлённый дом от прежних хозяев) — не
        // прячем их. Недоступность подбора до старта квеста обеспечивает
        // TrashPile.Collect() самостоятельно, проверяя activeQuest.questTag.
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Вызывается из IntroSequence в начале квеста сбора мусора
    public void Initialize()
    {
        _collectedCount = 0;
        _totalPiles = 0;
        _bagSpawnStarted = false;

        if (trashPiles == null) return;

        foreach (var pile in trashPiles)
        {
            if (pile == null) continue;
            _totalPiles++;
        }
    }

    // Вызывается из TrashPile.Collect()
    public void OnPileCollected()
    {
        if (_bagSpawnStarted || _totalPiles <= 0) return;
        _collectedCount++;

        if (_collectedCount >= _totalPiles)
        {
            _bagSpawnStarted = true;
            StartCoroutine(SpawnBagRoutine());
        }
    }

    IEnumerator SpawnBagRoutine()
    {
        // Спавним мешок у ног игрока с 'поп'-анимацией.
        //
        // Раньше мешок падал СВЕРХУ и относительно КАМЕРЫ: бралась camera.transform
        // (y камеры ~2.5), к ней прибавлялось +1.5, и мешок появлялся заметно выше
        // головы — выглядело как "из ниоткуда". Теперь точка ищется от позиции
        // самого игрока и только на полу.
        //
        // Раньше отсутствие Camera.main (camera == null) обрывало ВСЮ корутину через
        // yield break — GameEvents.OnTrashDeliveryReady ниже никогда не вызывался, и
        // квест "Вынести мусорный мешок" не мог начаться (софтлок). Теперь неудачный
        // спавн мешка не мешает уведомлению о готовности квеста.
        if (trashBagPrefab != null)
        {
            Transform player = null;
            Camera camera = Camera.main;
            if (camera != null)
            {
                // Ищем от реального тела игрока, а не от камеры: камера поднята над
                // землёй (y ~2.5), и от неё мешок уезжал вверх.
                //
                // GetComponentInParent тут НЕ подходит: камера висит не на CharacterController,
                // а на промежуточном ноде "CameraOffset", поэтому пришлось искать игрока
                // по тегу. Именно из-за этого старый код брал camera.transform и мешок
                // появлялся выше головы.
                player = FindPlayerBody(camera);
            }

            if (player == null)
            {
                Debug.LogWarning("[TrashManager] Не найден игрок — мешок не заспавнен, но квест продолжится.", this);
            }
            else
            {
                SpawnBagAtFeet(player);
            }
        }

        // Ждём 3 секунды перед мыслями игрока
        yield return new WaitForSeconds(3f);

        if (ThoughtManager.Instance == null)
        {
            PlayFloorKnock();
            GameEvents.OnTrashDeliveryReady?.Invoke();
            yield break;
        }

        ThoughtManager.Instance.ShowThoughts(new[] {
            "...Что это было?",
            "Странный звук. Нужно проверить.",
            "Может, это с кухни."
        }, () => GameEvents.OnTrashDeliveryReady?.Invoke(), onStart: PlayFloorKnock);
    }

    // Ищет тело игрока. Камера в этом проекте подвешена не к CharacterController,
    // а к промежуточному "CameraOffset", поэтому подниматься по parent'ам от камеры
    // бесполезно — берём объект с тегом Player.
    private static Transform FindPlayerBody(Camera camera)
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) return playerObject.transform;

        // Фолбэк, если тег вдруг не назначен: ищем ближайший CharacterController.
        CharacterController controller = FindAnyObjectByType<CharacterController>();
        return controller != null ? controller.transform : camera.transform;
    }

    private void SpawnBagAtFeet(Transform player)
    {
        // Радиус = половина ширины мешка, чтобы при поиске места проверялось
        // именно то пространство, которое он реально займёт.
        Collider bagCollider = trashBagPrefab.GetComponentInChildren<Collider>();
        float footprintRadius = bagCollider != null
            ? Mathf.Max(0.15f, Mathf.Max(bagCollider.bounds.extents.x, bagCollider.bounds.extents.z))
            : 0.25f;

        if (!GroundSpawnPoint.TryFindFreeSpot(player.position, player.forward, footprintRadius, out Vector3 spot))
        {
            // Свободного места не нашлось (тесно, завалено) — ставим перед игроком,
            // как раньше, но на уровне пола, чтобы не пропасть над головой.
            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            spot = player.position + forward.normalized * 0.9f;
            Debug.LogWarning("[TrashManager] Свободное место не найдено — мешок поставлен перед игроком.", this);
        }

        GameObject bag = Instantiate(trashBagPrefab, spot, Quaternion.identity);

        CollectableItem item = bag.GetComponent<CollectableItem>();
        if (item == null) item = bag.AddComponent<CollectableItem>();
        item.currentItemType = CollectableItem.ItemType.Trash;

        Rigidbody rb = bag.GetComponent<Rigidbody>();
        if (rb == null) rb = bag.AddComponent<Rigidbody>();
        rb.linearDamping = 2f;

        // Спавним чуть выше поверхности (мешок опускается и ложится на пол
        // своей гравитацией) — плюс чиним сломанный коллайдер префаба.
        float groundY = spot.y;
        EnsureUsableCollider(bag);
        Vector3 spawnPos = new Vector3(spot.x, groundY + SpawnLift(bag), spot.z);
        bag.transform.position = spawnPos;

        // 'Поп'-появление: вырастает из нуля с перелётом и лёгким подскоком.
        // На время анимации физика выключена, потом включается обратно — мешок
        // остаётся стоять на месте, а не проваливается и не улетает.
        PopScaleAnimator.Play(
            bag.transform,
            bag.transform.localScale,
            bagPopDuration,
            bagPopHop,
            freezePhysics: true);

        PopScaleAnimator.PlayPopSound(spawnPos, bagPopSound, bagPopVolume);
    }

    /// <summary>
    /// Гарантирует, что у мешка есть рабочий коллайдер.
    ///
    /// У префаба Garbage стоял MeshCollider НУЛЕВОГО размера: у импортированного
    /// из FBX меша Garbage_bags.001 mesh.bounds почти пустой (0.01 x 0.02 x 0.02
    /// при настоящем AABB вершин 0.013 x 0.016 x 0.042), а MeshCollider
    /// строится именно по mesh.bounds. Физика такой коллайдер не видит вообще,
    /// поэтому мешок проваливался сквозь пол. Поднять точку спавна выше не
    /// помогало бы — без физики он всё равно падал бы, просто чуть позже.
    ///
    /// Ставим BoxCollider по настоящим вершинам. Дополнительно подстраховываемся
    /// в рантайме на случай, если у другого префаба найдётся такой же битый
    /// коллайдер (свойство bagPopSound тоже может быть не назначено).
    /// </summary>
    private void EnsureUsableCollider(GameObject bag)
    {
        Collider existing = bag.GetComponentInChildren<Collider>();
        if (existing != null && HasRealGeometry(existing))
            return;

        if (existing != null)
        {
            Debug.LogWarning(
                $"[TrashManager] У мешка '{bag.name}' коллайдер {existing.GetType().Name} имеет нулевой размер " +
                "(сломанный mesh.bounds у FBX-меша) — заменяю на BoxCollider по вершинам.", bag);
        }

        bool fixedOnRoot = ColliderRepair.FitBoxColliderToMesh(bag);
        if (!fixedOnRoot)
        {
            // Префаб сложнее простого: меш лежит на дочернем объекте.
            var filter = bag.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.gameObject != bag)
                fixedOnRoot = ColliderRepair.FitBoxColliderToMesh(filter.gameObject);
        }

        if (!fixedOnRoot)
            Debug.LogError($"[TrashManager] Не удалось починить коллайдер мешка '{bag.name}'.", bag);
    }

    /// <summary>Есть ли у коллайдера настоящая геометрия (а не вырожденная в точку).</summary>
    private static bool HasRealGeometry(Collider collider)
    {
        if (collider == null) return false;
        Vector3 size = collider.bounds.size;
        return size.x > 0.001f && size.y > 0.001f && size.z > 0.001f;
    }

    /// <summary>
    /// На сколько поднять мешок над найденной поверхностью, чтобы он НЕ ТОНУЛ в полу.
    ///
    /// Раньше поднимали на 10% высоты + contact offset — этого мало. Высота мешка
    /// 0.86 м, значит половина (0.43 м) изначально оказывалась ВНУТРИ перекрытия.
    /// На 1-м этаже это списывали на "мешок тяжёлый, продавил плиту", а на 2-м
    /// (перекрытие всего 0.2 м толщиной) физика выдавливала мешок вниз сквозь
    /// плиту, и он проваливался.
    ///
    /// Теперь поднимаем на ПОЛОВИНУ высоты: нижняя грань мешка оказывается ровно
    /// на поверхности, мешок не пересекается с полом даже на миллиметр. Плюс
    /// небольшой зазор (3 см), чтобы он аккуратно осел под тяжестью, а не
    /// висел/дребезжал. Падать ему с этой высоты не нужно — спокойно
    /// опустится своим весом.
    /// </summary>
    private float SpawnLift(GameObject bag)
    {
        float height = 0.35f;
        var renderer = bag != null ? bag.GetComponentInChildren<Renderer>() : null;
        if (renderer != null)
        {
            float size = renderer.bounds.size.y;
            if (size > 0.01f) height = size;
        }

        return height * 0.5f + bagSpawnGap;
    }

    private void PlayFloorKnock()
    {
        if (floorKnockSource == null || !floorKnockSource.isActiveAndEnabled || floorKnockClip == null)
        {
            Debug.LogWarning("[TrashManager] Источник или клип стука недоступен.", this);
            return;
        }

        floorKnockSource.PlayOneShot(floorKnockClip);
    }
}
}
