using UnityEngine;

public class HammerTrap : MonoBehaviour
{
    [Header("Настройки сюжета")]
    public Door roomDoor;           // Твоя дверь
    public AudioSource slamSound;   // Звук удара
    public GameObject monster;      // Монстр
    public Transform monsterSpot;   // Точка у дыры
    public GameObject finalLogic;   // Скрипт развилки (Часть 2)

    // Этот метод теперь PUBLIC, чтобы его вызвал инвентарь
    public void TriggerEvent(PlayerInventory inv)
    {
        // 1. Ставим ту самую галочку в инвентаре
        inv.hasHammer = true;
        inv.ActivateItem("Hammer"); // Сразу даем его в руки

        // 2. Закрываем дверь
        if (roomDoor != null) roomDoor.CloseDoor();
        if (slamSound != null) slamSound.Play();

        // 3. Переносим монстра к дыре
        if (monster != null && monsterSpot != null)
        {
            monster.transform.position = monsterSpot.position;
            monster.transform.rotation = monsterSpot.rotation;
            monster.SetActive(true);
        }

        // 4. Включаем логику финала
        if (finalLogic != null) finalLogic.SetActive(true);

        // 5. Удаляем молоток из мира (он теперь в инвентаре)
        gameObject.SetActive(false);
    }
}