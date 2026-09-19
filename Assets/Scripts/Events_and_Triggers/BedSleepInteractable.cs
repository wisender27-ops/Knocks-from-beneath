using UnityEngine;

namespace KnocksFromBeneath
{

public class BedSleepInteractable : MonoBehaviour
{
    public void Interact()
    {
        GameEvents.OnBedTriggerReached?.Invoke();
    }
}
}
