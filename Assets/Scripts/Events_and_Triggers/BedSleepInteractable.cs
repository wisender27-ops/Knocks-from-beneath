using UnityEngine;

public class BedSleepInteractable : MonoBehaviour
{
    public void Interact()
    {
        var intro = FindObjectOfType<IntroSequence>();
        if (intro != null)
            intro.OnBedTriggerReached();
    }
}