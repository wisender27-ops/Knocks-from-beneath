using UnityEngine;
using System.Collections;

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

    void Awake()
    {
        Instance = this;
        HideAll(); // Прячем все кучи заранее
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void HideAll()
    {
        if (trashPiles == null) return;

        foreach (var pile in trashPiles)
            if (pile != null) pile.SetActive(false);
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
            pile.SetActive(true);
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
        // Звук стука
        if (floorKnockSource != null && floorKnockClip != null)
            floorKnockSource.PlayOneShot(floorKnockClip);

        // Спавним мешок мусора, он падает сверху
        if (trashBagPrefab != null)
        {
            Camera camera = Camera.main;
            if (camera == null)
                yield break;

            Transform player = camera.transform;
            Vector3 spawnPos = player.position + player.forward * 1.2f;
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

        // Ждём 3 секунды перед мыслями игрока
        yield return new WaitForSeconds(3f);

        if (ThoughtManager.Instance == null)
        {
            GameEvents.OnTrashDeliveryReady?.Invoke();
            yield break;
        }

        ThoughtManager.Instance.ShowThoughts(new string[] {
            "...Что это было?",
            "Странный звук. Нужно проверить.",
            "Может, это с кухни."
        }, () =>
        {
            GameEvents.OnTrashDeliveryReady?.Invoke();
        });
    }
}
