using System;
using System.Collections;
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
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Transform _player;

    private const string KitchenQuestTag = "go-kitchen";
    private const float KitchenReach = 3.8f;
    private const float FirstFloorMaxY = 3f;
    private const float MessSightRange = 14f, MessSightAngle = 45f;

    public RoomDecorationController(
        GameObject[] decorationZones,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onDecorationFinished,
        Func<IEnumerator, Coroutine> startCoroutine = null,
        Transform player = null)
    {
        _decorationZones = decorationZones;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onDecorationFinished = onDecorationFinished;
        _startCoroutine = startCoroutine;
        _player = player;
    }

    // Утро: сперва герой спускается на кухню и видит погром, и только тогда появляется уборка.
    public void SetupKitchenQuest()
    {
        // U-02: погром должен уже быть на кухне, когда герой туда спускается. Раньше
        // вещи переезжали на свои места разброса только в SetupDecorationQuest — то
        // есть в момент, когда игрок уже стоял на кухне, и он видел, как всё «появилось».
        // Сейчас герой ещё в спальне на втором этаже — кухни не видно.
        ScatterItems();
        if (!TryGetKitchenPoint(out Vector3 kitchen) || _player == null || _startCoroutine == null)
        {
            SetupDecorationQuest();
            return;
        }
        _createQuest(Loc.T("Пойти на кухню"), 1, OnKitchenReached, KitchenQuestTag);
        _startCoroutine(WaitForKitchen(kitchen));
    }

    IEnumerator WaitForKitchen(Vector3 kitchen)
    {
        var interval = new WaitForSeconds(.2f);
        while (_player != null && !IsAtKitchen(_player.position, kitchen) && !SeesMess()) yield return interval;
        QuestManager quests = QuestManager.Instance;
        if (quests != null && quests.IsQuestActive(KitchenQuestTag)) quests.AddProgress(1);
    }

    public static bool IsAtKitchen(Vector3 player, Vector3 kitchen) =>
        player.y < FirstFloorMaxY && Vector3.ProjectOnPlane(player - kitchen, Vector3.up).sqrMagnitude <= KitchenReach * KitchenReach;

    // Погром замечен, как только разбросанная вещь попала в поле зрения без преград (например, с лестницы).
    bool SeesMess()
    {
        Camera view = Camera.main;
        if (view == null || _decorationZones == null) return false;
        Vector3 eye = view.transform.position;
        foreach (GameObject zoneObject in _decorationZones)
        {
            PlacementZone zone = zoneObject != null ? zoneObject.GetComponent<PlacementZone>() : null;
            if (zone == null || zone.slots == null) continue;
            foreach (Transform slot in zone.slots)
            {
                FurnitureSlot marker = slot != null ? slot.GetComponent<FurnitureSlot>() : null;
                GameObject item = marker != null ? marker.ItemForSlot : null;
                if (item == null || !item.activeInHierarchy) continue;
                Vector3 to = item.transform.position + Vector3.up * .1f - eye;
                if (to.sqrMagnitude > MessSightRange * MessSightRange ||
                    Vector3.Angle(view.transform.forward, to) > MessSightAngle) continue;
                if (!Physics.Raycast(eye, to, out RaycastHit hit, to.magnitude, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore) ||
                    hit.transform.IsChildOf(item.transform)) return true;
            }
        }
        return false;
    }

    bool TryGetKitchenPoint(out Vector3 kitchen)
    {
        kitchen = Vector3.zero;
        int count = 0;
        if (_decorationZones != null)
            foreach (GameObject zone in _decorationZones)
                if (zone != null) { kitchen += zone.transform.position; count++; }
        if (count > 0) kitchen /= count;
        return count > 0;
    }

    void OnKitchenReached() => _showThoughts(new[] { Loc.T("Что здесь случилось?") }, SetupDecorationQuest);

    public void SetupDecorationQuest()
    {
        SetZonesActive(true);
        int amount = _decorationZones != null ? Mathf.Max(1, _decorationZones.Length) : 1;
        _createQuest(Loc.T("Убрать разбросанные вещи на кухне"), amount, OnDecorationFinished, "room-decoration");
        // Повторный вызов — no-op (PrepareForPlacementQuest срабатывает один раз);
        // нужен для дебаг-стартов, которые идут сюда в обход SetupKitchenQuest.
        ScatterItems();
    }

    public void ScatterItems()
    {
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
            Loc.T("Вчера всё стояло на местах. Само оно на пол не попадало.")
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
