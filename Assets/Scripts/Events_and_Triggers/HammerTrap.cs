using UnityEngine;

namespace KnocksFromBeneath
{

// Момент подбора молотка (квест "hammer-find", SetupHammerQuest в IntroSequence). T-21:
// раньше это был дореформенный "молоток = мгновенный джампскейр" — TriggerEvent без всякого
// гейта захлопывал дверь, спавнил монстра на monsterSpot и включал finalLogic. finalLogic
// оказался дословно тем же объектом, что и IntroSequence.finaleTrigger, а monster — тем же
// Monster, которым управляет FinaleController.OnTriggerEnter у дыры (проверено сравнением
// ссылок в редакторе). То есть при подборе молотка сюжет задваивался и запускался раньше
// срока, минуя поход к дыре. Оставлена только настоящая механика подбора: инвентарь, квест,
// мигание лампы как атмосферный штрих. Момент "монстр выходит" — целиком у FinaleController.
public class HammerTrap : MonoBehaviour
{
    [Header("Настройки мигания света")]
    [SerializeField] private string lampID = "5";      // ID лампы
    [SerializeField] private float flickerDuration = 3.0f; // Сколько секунд мигает
    [SerializeField] private float flickerInterval = 0.1f; // Скорость (интервал) мигания

    public void TriggerEvent(PlayerInventory inv)
    {
        if (inv == null) return;
        if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestActive("hammer-find"))
            return;

        inv.AddItem("Hammer");
        inv.Equip("Hammer");

        QuestManager.Instance.AddProgress(1);

        if (LightingManager.Instance != null)
            LightingManager.Instance.Flicker(lampID, flickerDuration, flickerInterval);

        gameObject.SetActive(false);
    }
}
}
