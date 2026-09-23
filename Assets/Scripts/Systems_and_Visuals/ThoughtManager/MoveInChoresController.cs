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
    private readonly GameObject[] _roomZones;
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onChoresFinished;

    private bool _trashDeliveryQuestStarted;

    public MoveInChoresController(
        GameObject trashZone,
        GameObject[] roomZones,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onChoresFinished)
    {
        _trashZone = trashZone;
        _roomZones = roomZones;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onChoresFinished = onChoresFinished;
    }

    public void SetupTrashQuest()
    {
        int amount = 3; // запасное значение, если TrashManager почему-то недоступен
        if (TrashManager.Instance != null)
        {
            TrashManager.Instance.Initialize();
            amount = Mathf.Max(1, TrashManager.Instance.PileCount);
        }
        _createQuest("Собрать мусор по дому", amount, OnTrashCollected, "trash-collect");
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
        SetRoomZonesActive(true);
        int amount = _roomZones != null ? Mathf.Max(1, _roomZones.Length) : 1;
        _createQuest("Отнести коробки по комнатам", amount, OnBoxFinished, "box-delivery");
    }

    public void OnBoxFinished()
    {
        SetRoomZonesActive(false);
        if (_roomZones != null)
        {
            foreach (GameObject roomZone in _roomZones)
            {
                if (roomZone == null) continue;
                PlacementZone zone = roomZone.GetComponent<PlacementZone>();
                if (zone != null) zone.RemovePlacedQuestBoxesAfter(5f);
            }
        }
        _showThoughts(new string[] {
            "Спина отваливается.",
            "Надо хоть что-то поесть перед сном.",
            "Достану пирог из холодильника."
        }, _onChoresFinished);
    }

    void SetRoomZonesActive(bool active)
    {
        if (_roomZones == null) return;
        for (int i = 0; i < _roomZones.Length; i++)
        {
            if (_roomZones[i] != null)
            {
                PlacementZone zone = _roomZones[i].GetComponent<PlacementZone>();
                if (zone != null) zone.ConfigureBoxQuestPlacement(active);
                _roomZones[i].SetActive(active);
            }
        }
    }
}
}
