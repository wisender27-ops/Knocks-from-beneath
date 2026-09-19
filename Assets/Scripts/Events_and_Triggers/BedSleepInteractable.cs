using UnityEngine;

public class BedSleepInteractable : MonoBehaviour
{
    public void Interact()
    {
        GameEvents.OnBedTriggerReached?.Invoke();
    }
}