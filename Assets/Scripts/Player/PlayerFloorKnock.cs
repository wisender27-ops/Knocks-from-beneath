using System;
using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-02: ответный стук. Q — герой стучит в пол: короткий кивок камеры вниз и удар под ногами.
    // Кто слушает (KnockDirector, NightOneScare), подписывается на Knocked. Подсказка «Q» появляется
    // после первого стука снизу и пропадает, когда игрок постучал сам.
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
        private const float HintSeconds = 7f;
        private const int MaxHintShows = 3;

        public static PlayerFloorKnock Instance { get; private set; }

        public event Action<Vector3> Knocked; // точка удара на полу

        public int KnockCount { get; private set; }
        public bool HintVisible => _hint != null && _hint.gameObject.activeSelf;

        private PlayerController _player;
        private Transform _camera;
        private AudioSource _source;
        private AudioClip _clip;
        private float _nextTap;
        private float _dipTime = float.NegativeInfinity;
        private Quaternion _appliedDip = Quaternion.identity;
        private Quaternion _leftRotation;
        private UiKeyHints _hint;
        private float _hintUntil;
        private int _hintShows;

        public void Initialize(PlayerInventory inventory, AudioClip clip)
        {
            _player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            Camera cam = _player != null ? _player.GetComponentInChildren<Camera>(true) : null;
            _camera = cam != null ? cam.transform : null;
            _clip = clip;
            if (_source == null && _player != null)
            {
                _source = HorrorMomentDirector.CreateSource(_player.transform, "Player floor knock", 10f);
                _source.minDistance = 1f;
            }
            if (_hint == null) BuildHint();
        }

        // Стучащий снизу подал голос — напомнить, что ему можно ответить.
        public void NotifyKnockFromBelow()
        {
            if (KnockCount > 0 || _hintShows >= MaxHintShows || _hint == null) return;
            if (Time.time < _hintUntil) return;
            _hintShows++;
            _hintUntil = Time.time + HintSeconds;
        }

        // Удар без клавиши — для Play-проб и сценариев.
        public bool TryKnock()
        {
            if (!CanKnock || Time.time < _nextTap) return false;
            _nextTap = Time.time + TapInterval;
            _dipTime = Time.time;
            KnockCount++;
            _hintUntil = 0f;
            Vector3 floor = _player.transform.position + _player.transform.forward * 0.35f;
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
            if (_hint != null)
            {
                bool show = canKnock && Time.time < _hintUntil;
                if (_hint.gameObject.activeSelf != show) _hint.gameObject.SetActive(show);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null || _player == null || _player.isCameraLocked) return;
            float t = Time.time - _dipTime;
            if (t < 0f || t > DipRise + DipFall) return;
            float k = t < DipRise ? t / DipRise : 1f - (t - DipRise) / DipFall;
            _appliedDip = Quaternion.Euler(DipAngle * Mathf.SmoothStep(0f, 1f, k), 0f, 0f);
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
