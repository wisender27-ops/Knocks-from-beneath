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
    private readonly DeliveryTruckSequence _boxDelivery;

    private bool _trashDeliveryQuestStarted;

    public MoveInChoresController(
        GameObject trashZone,
        GameObject[] roomZones,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onChoresFinished,
        DeliveryTruckSequence boxDelivery = null)
    {
        _trashZone = trashZone;
        _roomZones = roomZones;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onChoresFinished = onChoresFinished;
        _boxDelivery = boxDelivery;
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
        // Коробок во дворе ещё нет: их привозит фургон, и квест стартует после доставки.
        if (_boxDelivery == null)
        {
            ShowBoxThoughts();
            return;
        }
        _boxDelivery.Play(ShowBoxThoughts);
    }

    void ShowBoxThoughts()
    {
        _showThoughts(new string[] {
            "Наконец-то привезли мои коробки.",
            "Ещё их по комнатам растащить.",
            "Переехал, блин. Отдохнул."
        }, SetupBoxQuest);
    }

    public void SetupBoxQuest()
    {
        // Отладочный старт с этапа коробок идёт в обход доставки — ставим их сразу.
        if (_boxDelivery != null) _boxDelivery.EnsureDelivered();
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
            "Ну всё, спина сказала спасибо.",
            "Теперь поесть — и в кровать.",
            "В холодильнике ещё пирог остался."
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
