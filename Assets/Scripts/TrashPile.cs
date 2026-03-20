using UnityEngine;

public class TrashPile : MonoBehaviour
{
    public void Collect()
    {
        QuestManager.Instance.AddProgress(1);
        TrashManager.Instance.OnPileCollected();
        Destroy(gameObject);
    }
}