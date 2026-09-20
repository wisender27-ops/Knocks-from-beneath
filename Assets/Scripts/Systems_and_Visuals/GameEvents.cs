using System;

namespace KnocksFromBeneath
{

// Шина уведомлений об игровых событиях — для случаев, когда отправителю
// не нужен ответ и не нужна ссылка на конкретный обработчик (IntroSequence
// и т.п.). Подписчик решает сам, важно ли ему это событие прямо сейчас —
// издатель не должен спрашивать состояние подписчика перед вызовом.
public static class GameEvents
{
    // Началась ночь
    public static Action OnNightStarted;

    // Ночь кончилась и снова наступил день (NightOneController). Парный к OnNightStarted:
    // всё, что включается на ночь, должно уметь выключиться обратно.
    public static Action OnDayStarted;

    // Игрок дошёл до кровати (BedSleepInteractable)
    public static Action OnBedTriggerReached;

    // Игрок нашёл источник шума на кухне (KitchenNoiseTrigger)
    public static Action OnKitchenNoiseHeard;

    // Весь мусор собран и мешок готов — можно начинать квест доставки (TrashManager)
    public static Action OnTrashDeliveryReady;

    // Игрок взял в руки пирог (PlayerInteraction) — подписчик сам решает,
    // релевантно ли это сейчас (например, активен ли квест "достать пирог")
    public static Action OnPieGrabbed;

    // Игрок доел пирог (PlayerInteraction)
    public static Action OnPieEaten;

    // Игрок прочитал записку/принял звонок соседа (NeighborNoteInteractable, T-16) —
    // побочная необязательная находка дня 2, не часть основной квест-цепочки.
    public static Action OnNeighborNoteRead;
}
}
