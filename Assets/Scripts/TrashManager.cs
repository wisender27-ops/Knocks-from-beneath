using UnityEngine;
using System.Collections;

public class TrashManager : MonoBehaviour
{
    public static TrashManager Instance;

    [Header("Мусорный мешок")]
    public GameObject trashBagPrefab;

    [Header("Стук из под пола")]
    public AudioSource floorKnockSource;
    public AudioClip floorKnockClip;

    [Header("Кучки мусора")]
    public GameObject[] trashPiles;

    private int _totalPiles;
    private int _collectedCount;

    void Awake()
    {
        Instance = this;
        HideAll(); // Выключаем всё при старте
    }

    public void HideAll()
    {
        foreach (var pile in trashPiles)
            if (pile != null) pile.SetActive(false);
    }

    // Вызывается из IntroSequence когда квест начинается
    public void Initialize()
    {
        _collectedCount = 0;
        _totalPiles = trashPiles.Length;

        foreach (var pile in trashPiles)
            if (pile != null) pile.SetActive(true);
    }

    // Вызывается из TrashPile.Collect()
    public void OnPileCollected()
    {
        _collectedCount++;

        if (_collectedCount >= _totalPiles)
            StartCoroutine(SpawnBagRoutine());
    }

    IEnumerator SpawnBagRoutine()
    {
        // Звук стука
        if (floorKnockSource != null && floorKnockClip != null)
            floorKnockSource.PlayOneShot(floorKnockClip);

        // Спавним мешок перед игроком
        if (trashBagPrefab != null)
        {
            Transform player = Camera.main.transform;
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

        // Ждём 3 секунды перед мыслями
        yield return new WaitForSeconds(3f);

        ThoughtManager.Instance.ShowThoughts(new string[] {
            "...Что это было?",
            "Наверное трубы. Старый дом.",
            "Надо вынести этот мешок."
        }, null);
    }
}