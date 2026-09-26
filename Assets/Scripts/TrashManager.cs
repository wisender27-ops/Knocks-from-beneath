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
        // Спавним мешок мусора, он падает сверху. Раньше отсутствие Camera.main (camera == null)
        // обрывало ВСЮ корутину через yield break — GameEvents.OnTrashDeliveryReady ниже
        // никогда не вызывался, и квест "Вынести мусорный мешок" не мог начаться (софтлок).
        // Теперь неудачный спавн мешка не мешает уведомлению о готовности квеста.
        if (trashBagPrefab != null)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[TrashManager] Camera.main не найдена — мешок не заспавнен, но квест продолжится.");
            }
            else
            {
                Transform player = camera.transform;
                Vector3 forwardDir = player.forward;
                forwardDir.y = 0f;
                if (forwardDir.sqrMagnitude < 0.0001f) forwardDir = Vector3.forward;
                forwardDir.Normalize();

                // Раньше мешок ставился на фиксированные 1.2 м вперёд без проверки
                // препятствий: если игрок смотрел в стену/окно, мешок оказывался за ней,
                // и квест доставки было не выполнить. Раскастом укорачиваем дистанцию
                // до ближайшей преграды.
                float forwardDist = 1.2f;
                if (Physics.Raycast(player.position, forwardDir, out RaycastHit wallHit, forwardDist, PhysicsMasks.RaycastAll, QueryTriggerInteraction.Ignore))
                    forwardDist = Mathf.Max(0.3f, wallHit.distance - 0.3f);

                Vector3 spawnPos = player.position + forwardDir * forwardDist;
                spawnPos.y = player.position.y - 1f;
                Vector3 spawnPosHigh = spawnPos + Vector3.up * 1.5f;

                GameObject bag = Instantiate(trashBagPrefab, spawnPosHigh, Quaternion.identity);

                CollectableItem item = bag.GetComponent<CollectableItem>();
                if (item == null) item = bag.AddComponent<CollectableItem>();
                item.currentItemType = CollectableItem.ItemType.Trash;

                Rigidbody rb = bag.GetComponent<Rigidbody>();
                if (rb == null) rb = bag.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.linearDamping = 2f;
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
