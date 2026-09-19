using UnityEngine;

namespace KnocksFromBeneath
{

// Звонок/записка от соседа, день 2 (T-16). Самостоятельный интерактив уровня
// BedSleepInteractable/LightSwitch — НЕ под-контроллер IntroSequence, не часть основной
// квест-цепочки. Срабатывает один раз, публикует событие на случай если кому-то в будущем
// понадобится знать, что игрок это видел (сейчас никто не подписан).
public class NeighborNoteInteractable : MonoBehaviour
{
    [Tooltip("Текст записки/звонка — идёт через ThoughtManager, как весь остальной текст.")]
    [SerializeField] private string[] noteLines;

    private bool _used;

    public void Interact()
    {
        if (_used) return;
        _used = true;

        if (ThoughtManager.Instance != null)
            ThoughtManager.Instance.ShowThoughts(noteLines, null);

        GameEvents.OnNeighborNoteRead?.Invoke();
    }
}
}
