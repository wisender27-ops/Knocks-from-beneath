using System;
using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-02: ответный стук. Q — герой стучит в пол: короткий кивок камеры вниз и удар под ногами.
    // Только сидя (присед) и только там, где под полом подвал, — первый этаж дома, не гараж и не
    // второй этаж. Кто слушает (KnockDirector, NightOneScare), подписывается на Knocked. Подсказка
    // появляется после стука снизу, когда игрок окажется над подвалом, и пропадает после своего стука.
    // Порядок 9990 — до PlayerController (10000): кивок снимается раньше, чем тот перезапишет
    // поворот камеры, а в LateUpdate накладывается заново поверх итогового взгляда.
    [DefaultExecutionOrder(9990)]
    [DisallowMultipleComponent]
    public sealed class PlayerFloorKnock : MonoBehaviour
    {
        public const KeyCode Key = KeyCode.Q;
        private const float TapInterval = 0.2f;
        private const float DipAngle = 5f;
        private const float DipRise = 0.06f;
        private const float DipFall = 0.22f;
        private const float JoltDuration = 0.55f;
        private const float HintSeconds = 7f;
        private const int MaxHintShows = 3;
        private const float AreaCheckInterval = 0.2f;
        private const float FloorTolerance = 0.4f;
        private const float FloorProbe = 3f;

        public static PlayerFloorKnock Instance { get; private set; }

        public event Action<Vector3> Knocked; // точка удара на полу

        public int KnockCount { get; private set; }
        public bool HintVisible => _hint != null && _hint.gameObject.activeSelf;
        public bool IsOverBasement => _overBasement;

        private PlayerController _player;
        private Transform _camera;
        private AudioSource _source;
        private AudioClip _clip;
        private float _nextTap;
        private float _dipTime = float.NegativeInfinity;
        private float _joltTime = float.NegativeInfinity;
        private float _joltStrength;
        private Quaternion _appliedDip = Quaternion.identity;
        private Quaternion _leftRotation;
        private UiKeyHints _hint;
        private float _hintUntil;
        private int _hintShows;
        private bool _hintPending;
        private bool _hasArea;
        private Vector2 _areaMin, _areaMax;
        private float _floorY;
        private bool _overBasement;
        private float _nextAreaCheck;
        private CharacterController _capsule;

        public void Initialize(PlayerInventory inventory, AudioClip clip)
        {
            _player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            Camera cam = _player != null ? _player.GetComponentInChildren<Camera>(true) : null;
            _camera = cam != null ? cam.transform : null;
            _clip = clip;
            _capsule = _player != null ? _player.GetComponent<CharacterController>() : null;
            if (_source == null && _player != null)
            {
                _source = HorrorMomentDirector.CreateSource(_player.transform, "Player floor knock", 10f);
                _source.minDistance = 1f;
            }
            if (_hint == null) BuildHint();
        }

        // Где под полом подвал: прямоугольник XZ и высота пола первого этажа.
        public void SetBasementArea(Vector2 min, Vector2 max, float floorY)
        {
            _areaMin = min;
            _areaMax = max;
            _floorY = floorY;
            _hasArea = true;
            _nextAreaCheck = 0f;
        }

        // Стучащий снизу подал голос — напомнить, что ему можно ответить (как только игрок над подвалом).
        public void NotifyKnockFromBelow()
        {
            if (KnockCount > 0 || _hintShows >= MaxHintShows || _hint == null) return;
            if (Time.time < _hintUntil) return;
            _hintPending = true;
        }

        // K-03: снизу простучали ритм — ждут ответа. Подсказка снова, даже если игрок уже стучал.
        public void PromptAnswer()
        {
            if (_hint == null) return;
            _hintPending = true;
        }

        // K-03: игрок сейчас над подвалом (луч не чаще раза в 0.2 с).
        public bool CheckOverBasement()
        {
            UpdateArea(false);
            return _overBasement;
        }

        // Точка на полу перед игроком — туда он стучит и оттуда ему отвечают.
        public Vector3 FloorPoint => _player != null ? _player.transform.position + _player.transform.forward * 0.35f : transform.position;

        // Встряска камеры от удара снизу (градусы в пике, гаснет за JoltDuration).
        public void Jolt(float strength)
        {
            _joltTime = Time.time;
            _joltStrength = strength;
        }

        // Удар без клавиши — для Play-проб и сценариев.
        public bool TryKnock()
        {
            UpdateArea(true);
            if (!CanKnock || !_overBasement || !_player.IsCrouching || Time.time < _nextTap) return false;
            _nextTap = Time.time + TapInterval;
            _dipTime = Time.time;
            KnockCount++;
            _hintUntil = 0f;
            _hintPending = false;
            Vector3 floor = FloorPoint;
            if (_source != null && _clip != null)
            {
                _source.transform.position = floor;
                _source.pitch = UnityEngine.Random.Range(1.05f, 1.18f);
                _source.PlayOneShot(_clip, 0.9f);
            }
            Knocked?.Invoke(floor);
            return true;
        }

        private bool CanKnock
        {
            get
            {
                var director = HorrorMomentDirector.Instance;
                return _player != null && isActiveAndEnabled && !MinigameWindow.IsAnyOpen &&
                    (director != null ? director.GameplayAvailable : Time.timeScale > 0f && !_player.isCameraLocked);
            }
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            RemoveDip();
            if (_hint != null) _hint.gameObject.SetActive(false);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            RemoveDip();
            bool canKnock = CanKnock;
            if (canKnock && Input.GetKeyDown(Key)) TryKnock();
            if (_hint == null) return;
            if (canKnock && (_hintPending || Time.time < _hintUntil)) UpdateArea(false);
            if (_hintPending && canKnock && _overBasement)
            {
                _hintPending = false;
                _hintShows++;
                _hintUntil = Time.time + HintSeconds;
            }
            bool show = canKnock && _overBasement && Time.time < _hintUntil;
            if (show)
            {
                if (_player.IsCrouching) _hint.Set("Q", "ПОСТУЧАТЬ В ПОЛ");
                else _hint.Set("CTRL", "ПРИСЕСТЬ", "Q", "ПОСТУЧАТЬ В ПОЛ");
            }
            if (_hint.gameObject.activeSelf != show) _hint.gameObject.SetActive(show);
        }

        // Луч вниз раз в 0.2 с: пол первого этажа дома, не гараж (он ниже) и не второй этаж.
        private void UpdateArea(bool force)
        {
            if (!force && Time.time < _nextAreaCheck) return;
            _nextAreaCheck = Time.time + AreaCheckInterval;
            _overBasement = !_hasArea; // без настроенной зоны — без ограничения
            if (!_hasArea || _player == null) return;
            Vector3 position = _player.transform.position;
            if (position.x < _areaMin.x || position.x > _areaMax.x || position.z < _areaMin.y || position.z > _areaMax.y)
            {
                _overBasement = false;
                return;
            }
            Vector3 origin = _capsule != null ? _player.transform.TransformPoint(_capsule.center) : position + Vector3.up;
            _overBasement = Physics.Raycast(origin, Vector3.down, out RaycastHit floor, FloorProbe,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(floor.point.y - _floorY) <= FloorTolerance;
        }

        private void LateUpdate()
        {
            if (_camera == null || _player == null || _player.isCameraLocked) return;
            float dip = 0f;
            float t = Time.time - _dipTime;
            if (t >= 0f && t <= DipRise + DipFall)
            {
                float k = t < DipRise ? t / DipRise : 1f - (t - DipRise) / DipFall;
                dip = DipAngle * Mathf.SmoothStep(0f, 1f, k);
            }
            Vector3 jolt = Vector3.zero;
            float jt = Time.time - _joltTime;
            if (jt >= 0f && jt < JoltDuration)
            {
                float a = _joltStrength * Mathf.Pow(1f - jt / JoltDuration, 2f);
                float n = jt * 35f;
                jolt = new Vector3((Mathf.PerlinNoise(n, 0.3f) - 0.3f) * 2f * a,
                    (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * a,
                    (Mathf.PerlinNoise(n, n) - 0.5f) * a);
            }
            if (dip == 0f && jolt == Vector3.zero) return;
            _appliedDip = Quaternion.Euler(dip + jolt.x, jolt.y, jolt.z);
            _camera.localRotation *= _appliedDip;
            _leftRotation = _camera.localRotation;
        }

        // Снимаем только свой кивок: если камеру за кадр повернул кто-то другой (катсцена), не трогаем.
        private void RemoveDip()
        {
            if (_appliedDip == Quaternion.identity) return;
            if (_camera != null && _camera.localRotation == _leftRotation)
                _camera.localRotation *= Quaternion.Inverse(_appliedDip);
            _appliedDip = Quaternion.identity;
        }

        private void BuildHint()
        {
            Canvas canvas = UiStyle.OverlayCanvas(transform, "FloorKnockHintCanvas", 400, new Vector2(1280f, 720f), false);
            TMP_FontAsset font = UiStyle.Font();
            _hint = UiKeyHints.Create(canvas.transform, "FloorKnockHint", font, .85f,
                new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, .8f));
            _hint.RectTransform.anchorMin = _hint.RectTransform.anchorMax = _hint.RectTransform.pivot = new Vector2(0f, 0f);
            _hint.RectTransform.anchoredPosition = new Vector2(30f, 30f);
            _hint.Set("Q", "ПОСТУЧАТЬ В ПОЛ");
            _hint.gameObject.SetActive(false);
        }
    }
}
