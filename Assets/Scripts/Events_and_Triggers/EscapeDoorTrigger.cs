using UnityEngine;

namespace KnocksFromBeneath
{

// Старый триггер побега у входной двери (T-19). С B-02 всегда выключен (IntroSequence.ResetTriggers): побег (E4)
// засчитывает NightTwoExits — по дороге, мимо люка. Оставлен, чтобы ссылка в сцене не стала Missing Script.
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
