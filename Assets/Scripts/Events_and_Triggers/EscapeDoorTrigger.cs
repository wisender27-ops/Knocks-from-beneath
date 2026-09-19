using UnityEngine;

namespace KnocksFromBeneath
{

// Концовка 2 (побег через входную дверь), ночь 2 — T-19. По форме тот же одноразовый
// OnTriggerEnter, что KitchenNoiseTrigger/BedSleepInteractable уже используют.
// Активность (SetActive) контролирует BranchEndingController.Activate() — выключен, если
// дверь заперта на день 2 (GameState.frontDoorLocked).
public class EscapeDoorTrigger : MonoBehaviour
{
    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered || !other.CompareTag("Player")) return;
        _triggered = true;

        var intro = FindFirstObjectByType<IntroSequence>();
        intro?.OnEscapedThroughDoor();
    }
}
}
