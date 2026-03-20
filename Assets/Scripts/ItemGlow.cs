using UnityEngine;

public class ItemGlow : MonoBehaviour
{
    [Header("Настройки")]
    public float visibleDistance = 4f;
    public float fadeDistance = 2f;

    private ParticleSystem _particles;
    private Transform _player;
    private float _baseEmissionRate = 8f;

    void Start()
    {
        // Ищем ParticleSystem на этом объекте или дочерних
        _particles = GetComponentInChildren<ParticleSystem>();

        if (_particles == null)
        {
            Debug.LogError($"[ItemGlow] ParticleSystem не найден на {gameObject.name}!");
            enabled = false;
            return;
        }

        _player = Camera.main.transform;

        // Сразу выключаем emission
        SetEmission(0);
    }

    void Update()
    {
        if (_particles == null || _player == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        if (distance > visibleDistance)
        {
            SetEmission(0);
        }
        else if (distance <= fadeDistance)
        {
            SetEmission(_baseEmissionRate);
        }
        else
        {
            float t = 1f - (distance - fadeDistance) / (visibleDistance - fadeDistance);
            SetEmission(_baseEmissionRate * t);
        }
    }

    void SetEmission(float rate)
    {
        // Правильный способ получить emission модуль
        var emission = _particles.emission;
        emission.rateOverTime = rate;
    }
}