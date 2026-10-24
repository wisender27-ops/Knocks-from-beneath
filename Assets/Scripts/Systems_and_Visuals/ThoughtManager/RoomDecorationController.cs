using System;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// День 2: расстановка мелких декоративных вещей по дому (T-14). По структуре — двойник
// коробочной части MoveInChoresController, но отдельный самостоятельный под-контроллер:
// свой квест, свой набор зон, никакого общего состояния с днём 1. Переиспользует те же
// PlacementZone/RoomRevealZone (T-12) с новым набором зон/предметов в сцене.
public sealed class RoomDecorationController
{
    private readonly GameObject[] _decorationZones;
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onDecorationFinished;

    public RoomDecorationController(
        GameObject[] decorationZones,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onDecorationFinished)
    {
        _decorationZones = decorationZones;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onDecorationFinished = onDecorationFinished;
    }

    public void SetupDecorationQuest()
    {
        SetZonesActive(true);
        int amount = _decorationZones != null ? Mathf.Max(1, _decorationZones.Length) : 1;
        _createQuest("Убрать разбросанные вещи на кухне", amount, OnDecorationFinished, "room-decoration");
        if (_decorationZones == null) return;
        foreach (GameObject zoneObject in _decorationZones)
        {
            PlacementZone zone = zoneObject != null ? zoneObject.GetComponent<PlacementZone>() : null;
            if (zone == null || zone.slots == null) continue;
            foreach (Transform slot in zone.slots)
            {
                FurnitureSlot marker = slot != null ? slot.GetComponent<FurnitureSlot>() : null;
                if (marker != null && marker.ItemForSlot != null)
                    marker.ItemForSlot.GetComponent<PickableItem>()?.PrepareForPlacementQuest();
            }
        }
    }

    void OnDecorationFinished()
    {
        SetZonesActive(false);
        _showThoughts(new string[] {
            "Вчера всё стояло нормально. Само оно на пол не упало.",
            "Ладно. Сначала поем, потом разберусь."
        }, _onDecorationFinished);
    }

    void SetZonesActive(bool active)
    {
        if (_decorationZones == null) return;
        for (int i = 0; i < _decorationZones.Length; i++)
        {
            if (_decorationZones[i] == null) continue;
            _decorationZones[i].SetActive(active);
            PlacementZone zone = _decorationZones[i].GetComponent<PlacementZone>();
            if (zone != null) zone.ConfigureQuestPlacement(active ? "room-decoration" : null);
        }
    }
}
}
