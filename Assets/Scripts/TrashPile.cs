using UnityEngine;
using System.Collections;

public class TrashPile : MonoBehaviour
{
    [Header("Стук из под пола")]
    public AudioSource floorKnockSource;
    public AudioClip floorKnockClip;

    [Header("Мусорный мешок")]
    public GameObject trashBagPrefab;

    // Эти поля должны быть заполнены только на ОДНОМ объекте —
    // остальные возьмут ссылки от него через static
    private static AudioSource _staticKnockSource;
    private static AudioClip _staticKnockClip;
    private static GameObject _staticTrashBagPrefab;

    private static int _totalPiles = 0;
    private static int _collectedCount = 0;

    void OnEnable()
    {
        _collectedCount = 0;
        _totalPiles = 0;
    }

    void Start()
    {
        _totalPiles++;

        // Берём ссылки от того объекта у которого они заполнены
        if (floorKnockSource != null) _staticKnockSource = floorKnockSource;
        if (floorKnockClip != null) _staticKnockClip = floorKnockClip;
        if (trashBagPrefab != null) _staticTrashBagPrefab = trashBagPrefab;
    }

    public void Collect()
    {
        _collectedCount++;
        QuestManager.Instance.AddProgress(1);

        // Если это была последняя кучка — запускаем событие
        if (_collectedCount >= _totalPiles)
            TriggerFloorKnock();

        Destroy(gameObject);
    }

    void TriggerFloorKnock()
    {
        if (_staticKnockSource != null && _staticKnockClip != null)
            _staticKnockSource.PlayOneShot(_staticKnockClip);

        if (_staticTrashBagPrefab != null)
        {
            Transform player = Camera.main.transform;

            Vector3 spawnPos = player.position + player.forward * 1.2f;
            spawnPos.y = player.position.y - 1f;
            Vector3 spawnPosHigh = spawnPos + Vector3.up * 1.5f;

            GameObject bag = Instantiate(_staticTrashBagPrefab, spawnPosHigh, Quaternion.identity);

            CollectableItem item = bag.GetComponent<CollectableItem>();
            if (item == null) item = bag.AddComponent<CollectableItem>();
            item.currentItemType = CollectableItem.ItemType.Trash;

            Rigidbody rb = bag.GetComponent<Rigidbody>();
            if (rb == null) rb = bag.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.linearDamping = 2f;
        }

        // Ждём 3 секунды перед мыслями
        StartCoroutine(DelayedThoughts());
    }

    IEnumerator DelayedThoughts()
    {
        yield return new WaitForSeconds(3f);

        ThoughtManager.Instance.ShowThoughts(new string[] {
        "...Что это было?",
        "Наверное трубы. Старый дом.",
        "Надо вынести этот мешок."
    }, null);
    }
}