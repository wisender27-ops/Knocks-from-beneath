using UnityEngine;

namespace KnocksFromBeneath
{

// Замок на входной двери (T-17) — отдельное от самого открывания Interact()-действие.
// Реальная блокировка (Door.SetInteractionLocked) и флаг GameState.frontDoorLocked
// выставляются в EveningRoundController, сюда попадает только тонкий проброс через
// IntroSequence — тот же паттерн, что StoveInteractable/MicrowaveInteractable уже используют.
public class FrontDoorLockInteractable : MonoBehaviour
{
    public void Interact()
    {
        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null) return;
        intro.OnFrontDoorLocked();
    }
}
}
