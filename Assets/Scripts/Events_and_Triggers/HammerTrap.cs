using UnityEngine;

public class HammerTrap : MonoBehaviour
{
    [Header("Настройки сюжета")]
    public Door roomDoor;
    public GameObject monster;
    public Transform monsterSpot;
    public GameObject finalLogic;

    [Header("Настройки мигания света")]
    [SerializeField] private string lampID = "5";      // ID лампы
    [SerializeField] private float flickerDuration = 3.0f; // Сколько секунд мигает
    [SerializeField] private float flickerInterval = 0.1f; // Скорость (интервал) мигания

    [Header("Звук события")]
    public AudioSource targetSource;
    public AudioClip slamClip;

    public void TriggerEvent(PlayerInventory inv)
    {
        inv.hasHammer = true;
        inv.ActivateItem("Hammer");

        // ДОБАВЬ ЭТУ СТРОЧКУ
        QuestManager.Instance.AddProgress(1);

        if (roomDoor != null) roomDoor.CloseDoor();
        if (targetSource != null && slamClip != null)
            targetSource.PlayOneShot(slamClip);
        if (monster != null && monsterSpot != null)
        {
            monster.transform.position = monsterSpot.position;
            monster.transform.rotation = monsterSpot.rotation;
            monster.SetActive(true);
        }
        if (finalLogic != null) finalLogic.SetActive(true);
        if (LightingManager.Instance != null)
        {
            LightingManager.Instance.Flicker(lampID, flickerDuration, flickerInterval);
        }
        gameObject.SetActive(false);
    }
}