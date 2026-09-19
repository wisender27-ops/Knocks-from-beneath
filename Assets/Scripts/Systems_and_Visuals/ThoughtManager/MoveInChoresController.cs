using System;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Мини-сюжет "быт после переезда": собрать мусор -> вынести мешок ->
// занести коробки в гараж. Выделен из IntroSequence (T-08). Не MonoBehaviour —
// получает нужные ссылки и колбэки от IntroSequence при создании.
public sealed class MoveInChoresController
{
    private readonly GameObject _trashZone;
    private readonly GameObject _garageZone;
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onChoresFinished;

    private bool _trashDeliveryQuestStarted;

    public MoveInChoresController(
        GameObject trashZone,
        GameObject garageZone,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onChoresFinished)
    {
        _trashZone = trashZone;
        _garageZone = garageZone;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onChoresFinished = onChoresFinished;
    }

    public void SetupTrashQuest()
    {
        if (TrashManager.Instance != null)
            TrashManager.Instance.Initialize();
        _createQuest("Собрать мусор по дому", 3, OnTrashCollected, "trash-collect");
    }

    void OnTrashCollected()
    {
        // Delivery quest is started after TrashManager finishes its thought sequence,
        // to prevent sequence breaks from completing it mid-dialogue.
        _trashDeliveryQuestStarted = false;
    }

    // Публикуется через GameEvents.OnTrashDeliveryReady, когда TrashManager
    // закончил свою последовательность мыслей и мешок готов к переносу.
    public void StartTrashDeliveryQuest()
    {
        if (_trashDeliveryQuestStarted) return;
        _trashDeliveryQuestStarted = true;

        if (_trashZone != null) _trashZone.SetActive(true);
        _createQuest("Вынести мусорный мешок", 1, OnTrashFinished, "trash-delivery");
    }

    public void OnTrashFinished()
    {
        if (_trashZone != null) _trashZone.SetActive(false);
        _showThoughts(new string[] {
            "Коробки всё ещё у входа...",
            "Как будто на каторгу приехал."
        }, SetupBoxQuest);
    }

    public void SetupBoxQuest()
    {
        if (_garageZone != null) _garageZone.SetActive(true);
        _createQuest("Отнести коробки в гараж", 1, OnBoxFinished, "box-delivery");
    }

    public void OnBoxFinished()
    {
        if (_garageZone != null) _garageZone.SetActive(false);
        _showThoughts(new string[] {
            "Спина отваливается.",
            "Надо хоть что-то поесть перед сном.",
            "Достану пирог из холодильника."
        }, _onChoresFinished);
    }
}
}
