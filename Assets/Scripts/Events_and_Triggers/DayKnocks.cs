using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Решение автора 2026-10-27: хозяин в подвале подаёт голос и днём — редко и тихо, короткой серией
    // под ногами, когда игрок над подвалом (только после первой ночи). Ночной фон стучит сериями (RandomKnock).
    // Звук — через KnockResponder: тот же стучащий, тот же глухой удар сквозь пол; ответить можно Q.
    [DisallowMultipleComponent]
    public sealed class DayKnocks : MonoBehaviour
    {
        private const float FirstDelayMin = 20f, FirstDelayMax = 40f;
        private const float GapMin = 60f, GapMax = 120f;
        private const float KnockInterval = 0.55f;
        private const float Volume = 0.6f;

        private KnockResponder _responder;
        private PlayerFloorKnock _floor;
        private Func<bool> _isActive;
        private float _next = -1f;
        private Coroutine _series;

        public int SeriesPlayed { get; private set; }

        public void Initialize(KnockResponder responder, PlayerFloorKnock floor, Func<bool> isActive)
        {
            _responder = responder;
            _floor = floor;
            _isActive = isActive;
        }

        private void OnDisable() => _series = null; // корутина останавливается вместе с компонентом

        private void Update()
        {
            if (_series != null || _responder == null || _floor == null) return;
            if (_isActive == null || !_isActive()) { _next = -1f; return; }
            if (_next < 0f) { _next = Time.time + UnityEngine.Random.Range(FirstDelayMin, FirstDelayMax); return; }
            if (Time.time < _next) return;

            HorrorMomentDirector horror = HorrorMomentDirector.Instance;
            if (horror != null && (!horror.GameplayAvailable || horror.HasOwner)) return;
            if (_responder.IsBusy || _responder.Suspended || !_floor.CheckOverBasement()) return;
            _series = StartCoroutine(Series());
        }

        private IEnumerator Series()
        {
            int count = UnityEngine.Random.Range(2, 4);
            Vector3 floor = _floor.FloorPoint;
            for (int i = 0; i < count; i++)
            {
                _responder.PlayUnderFloor(floor, UnityEngine.Random.Range(0.9f, 1f), Volume);
                yield return new WaitForSeconds(KnockInterval);
            }
            _floor.NotifyKnockFromBelow();
            SeriesPlayed++;
            _next = Time.time + UnityEngine.Random.Range(GapMin, GapMax);
            _series = null;
        }
    }
}
