using UnityEngine;

public class QuestZone : MonoBehaviour
{
    public int boxesRequired = 3;
    private int currentBoxes = 0;
    private bool isQuestCompleted = false;

    private void OnTriggerEnter(Collider other)
    {
        // Просто меняем "Box" на "Pickable"
        if (other.CompareTag("Pickable"))
        {
            currentBoxes++;
            Debug.Log("Коробок в зоне: " + currentBoxes + " из " + boxesRequired);

            CheckQuestStatus();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pickable"))
        {
            currentBoxes--;
            Debug.Log("Коробку вынесли. В зоне осталось: " + currentBoxes);
            
            // Если вынесли коробку, квест снова можно завершить позже
            if (currentBoxes < boxesRequired) isQuestCompleted = false;
        }
    }

    void CheckQuestStatus()
    {
        if (currentBoxes >= boxesRequired && !isQuestCompleted)
        {
            isQuestCompleted = true;
            Debug.Log("--- КВЕСТ ВЫПОЛНЕН! Все коробки на месте! ---");
        }
    }
}