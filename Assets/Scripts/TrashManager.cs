using UnityEngine;
using System.Collections;

public class TrashManager : MonoBehaviour
{
    public static TrashManager Instance;

    [Header("�������� �����")]
    public GameObject trashBagPrefab;

    [Header("���� �� ��� ����")]
    public AudioSource floorKnockSource;
    public AudioClip floorKnockClip;

    [Header("����� ������")]
    public GameObject[] trashPiles;

    private int _totalPiles;
    private int _collectedCount;

    void Awake()
    {
        Instance = this;
        HideAll(); // ��������� �� ��� ������
    }

    public void HideAll()
    {
        foreach (var pile in trashPiles)
            if (pile != null) pile.SetActive(false);
    }

    // ���������� �� IntroSequence ����� ����� ����������
    public void Initialize()
    {
        _collectedCount = 0;
        _totalPiles = trashPiles.Length;

        foreach (var pile in trashPiles)
            if (pile != null) pile.SetActive(true);
    }

    // ���������� �� TrashPile.Collect()
    public void OnPileCollected()
    {
        _collectedCount++;

        if (_collectedCount >= _totalPiles)
            StartCoroutine(SpawnBagRoutine());
    }

    IEnumerator SpawnBagRoutine()
    {
        // ���� �����
        if (floorKnockSource != null && floorKnockClip != null)
            floorKnockSource.PlayOneShot(floorKnockClip);

        // ������� ����� ����� �������
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

        // ��� 3 ������� ����� �������
        yield return new WaitForSeconds(3f);

        ThoughtManager.Instance.ShowThoughts(new string[] {
            "...Что это было?",
            "Странный звук. Нужно проверить.",
            "Может, это с кухни."
        }, () =>
        {
            IntroSequence intro = FindObjectOfType<IntroSequence>();
            if (intro != null)
                intro.StartTrashDeliveryQuest();
        });
    }
}