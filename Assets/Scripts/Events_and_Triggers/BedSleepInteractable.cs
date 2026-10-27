using UnityEngine;

namespace KnocksFromBeneath
{

public class BedSleepInteractable : MonoBehaviour
{
    public bool CanInteract
    {
        get
        {
            QuestManager quests = QuestManager.Instance;
            return quests != null &&
                   (quests.IsQuestActive("go-to-bed") || quests.IsQuestActive("evening-walk") || quests.IsQuestActive("neighbor-return-bed") ||
                    quests.IsQuestActive(GlassOfWaterScene.BedQuestTag));
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        GameEvents.OnBedTriggerReached?.Invoke();
    }
}
}
