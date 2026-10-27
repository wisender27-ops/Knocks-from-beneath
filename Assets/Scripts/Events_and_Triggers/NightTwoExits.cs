using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Выходы развилки ночи 2 (B-02). До развилки задние двери на задний двор (гараж Door_009 и сдвижная дверь кухни)
    // ночью не открываются — герой не хочет выходить в темноту. После пряток (Open) они открыты, и игрок решает ногами:
    // задний двор → onBackyard (E3), дорога в RoadDistance от люка → onRoad (E4). Границы — по полу участка, без триггеров
    // в сцене: дом x −47…−33, z 104.7…120; задний двор — за порогом задних дверей; асфальт дороги — с z 128.6.
    public sealed class NightTwoExits : MonoBehaviour
    {
        private static readonly Vector3 GarageBackDoor = new Vector3(-38.04f, 1.55f, 104.71f);
        private static readonly Vector3 KitchenBackDoor = new Vector3(-43.39f, 2.10f, 104.71f);
        private const float DoorMatchDistance = 0.6f;
        private const float BackyardZ = 104f;          // задние двери на z 104.71: шаг за порог — уже двор
        private const float PlotSouthZ = 90.5f;
        private const float PlotWestX = -50.1f, PlotEastX = -29.6f;
        private const float GroundFloorMaxY = 3f;       // второй этаж (пол 4.1) — не двор
        private const float RoadZ = 128.5f;
        public const float RoadDistance = 10f;
        private const float ThoughtCooldown = 5f;

        private static readonly string[] DarkOutside = { "Я не хочу выходить из дома, пока там темно." };

        private Transform _player;
        private Vector3 _hatch;
        private Action _onBackyard, _onRoad;
        private Door[] _backDoors = Array.Empty<Door>();
        private bool _locked, _watching;

        public void Initialize(Transform player, Vector3 hatch, Action onBackyard, Action onRoad)
        {
            _player = player;
            _hatch = hatch;
            _onBackyard = onBackyard;
            _onRoad = onRoad;
            Door garage = null, kitchen = null;
            foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (garage == null && Vector3.Distance(door.transform.position, GarageBackDoor) < DoorMatchDistance) garage = door;
                else if (kitchen == null && Vector3.Distance(door.transform.position, KitchenBackDoor) < DoorMatchDistance) kitchen = door;
            }
            if (garage == null || kitchen == null)
                Debug.LogWarning("[NightTwoExits] Не найдена задняя дверь: " + (garage == null ? "гараж " : "") + (kitchen == null ? "кухня" : ""));
            _backDoors = garage != null && kitchen != null ? new[] { garage, kitchen }
                : garage != null ? new[] { garage } : kitchen != null ? new[] { kitchen } : Array.Empty<Door>();
            foreach (Door door in _backDoors) door.LockedAttempted += OnLockedAttempt;
        }

        private void OnEnable()
        {
            GameEvents.OnNightTwoStarted += LockBackDoors;
            GameEvents.OnDayStarted += Release;
        }

        private void OnDisable()
        {
            GameEvents.OnNightTwoStarted -= LockBackDoors;
            GameEvents.OnDayStarted -= Release;
        }

        private void OnDestroy()
        {
            foreach (Door door in _backDoors)
                if (door != null) door.LockedAttempted -= OnLockedAttempt;
        }

        // Развилка: задние двери открываются, следим, куда ушёл игрок. false — концовка уже выбрана.
        public void SetOpen(bool open)
        {
            _watching = open;
            if (open) SetBackDoorsLocked(false);
        }

        private void LockBackDoors()
        {
            _watching = false;
            SetBackDoorsLocked(true);
        }

        private void Release()
        {
            _watching = false;
            SetBackDoorsLocked(false);
        }

        private void SetBackDoorsLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;
            foreach (Door door in _backDoors)
                if (door != null) door.SetInteractionLocked(locked);
        }

        private void OnLockedAttempt()
        {
            if (_locked) ThoughtManager.Instance?.TryShowInteractionThoughts("back-door-dark", DarkOutside, ThoughtCooldown);
        }

        private void Update()
        {
            if (!_watching || _player == null) return;
            Vector3 position = _player.position;
            if (IsInBackyard(position))
            {
                _watching = false;
                _onBackyard?.Invoke();
            }
            else if (IsOnRoad(position, _hatch))
            {
                _watching = false;
                _onRoad?.Invoke();
            }
        }

        public static bool IsInBackyard(Vector3 position) =>
            position.y < GroundFloorMaxY && position.z < BackyardZ && position.z > PlotSouthZ &&
            position.x > PlotWestX && position.x < PlotEastX;

        public static bool IsOnRoad(Vector3 position, Vector3 hatch)
        {
            if (position.z < RoadZ) return false;
            float dx = position.x - hatch.x, dz = position.z - hatch.z;
            return dx * dx + dz * dz >= RoadDistance * RoadDistance;
        }
    }
}
