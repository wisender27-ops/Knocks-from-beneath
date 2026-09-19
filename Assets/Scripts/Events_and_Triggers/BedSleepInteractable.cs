using UnityEngine;

public class BedSleepInteractable : MonoBehaviour
{
    public void Interact()
    {
        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro != null)
            intro.OnBedTriggerReached();
    }
}