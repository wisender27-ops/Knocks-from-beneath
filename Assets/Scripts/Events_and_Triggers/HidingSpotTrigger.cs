using UnityEngine;

namespace KnocksFromBeneath
{

// Одна из 3 зон укрытия ночи 2 (T-20) — кровать/шкаф/корзина для белья. Сама зона не
// решает ничего сама, только сообщает вход/выход через IntroSequence в HideEndingController,
// который знает, какая зона к какой ambush-точке привязана.
public class HidingSpotTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var intro = FindFirstObjectByType<IntroSequence>();
        intro?.OnHidingSpotEntered(gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var intro = FindFirstObjectByType<IntroSequence>();
        intro?.OnHidingSpotExited(gameObject);
    }
}
}
