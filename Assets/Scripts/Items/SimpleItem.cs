using UnityEngine;

namespace KnocksFromBeneath
{

// Перечисление типов предметов (всего 3)
public enum ItemType
{
    Crowbar,
    Flashlight,
    Hammer
}

public class SimpleItem : MonoBehaviour
{
    public ItemType itemType; // Выбираем тип в инспекторе Unity

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private ParticleSystem[] _particles;
    private Rigidbody _rigidbody;
    private bool[] _rendererStates;
    private bool[] _colliderStates;
    private bool[] _particleStates;
    private bool _wasKinematic;
    private bool _wasDetectingCollisions;

    public bool IsAvailable { get; private set; }

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _rigidbody = GetComponent<Rigidbody>();

        _rendererStates = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _rendererStates[i] = _renderers[i].enabled;

        _colliderStates = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++)
            _colliderStates[i] = _colliders[i].enabled;

        _particleStates = new bool[_particles.Length];
        for (int i = 0; i < _particles.Length; i++)
            _particleStates[i] = _particles[i].isPlaying;

        if (_rigidbody != null)
        {
            _wasKinematic = _rigidbody.isKinematic;
            _wasDetectingCollisions = _rigidbody.detectCollisions;
        }
    }

    private void OnEnable()
    {
        QuestManager.OnActiveQuestTagChanged += HandleQuestChanged;
        RefreshAvailability();
    }

    private void OnDisable()
    {
        QuestManager.OnActiveQuestTagChanged -= HandleQuestChanged;
    }

    private void HandleQuestChanged(string _)
    {
        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        SetAvailable(QuestManager.Instance != null && QuestManager.Instance.IsItemRequired(itemType));
    }

    private void SetAvailable(bool available)
    {
        IsAvailable = available;

        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].enabled = available && _rendererStates[i];

        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = available && _colliderStates[i];

        for (int i = 0; i < _particles.Length; i++)
        {
            if (available && _particleStates[i])
                _particles[i].Play();
            else
                _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (_rigidbody == null) return;

        _rigidbody.isKinematic = available ? _wasKinematic : true;
        _rigidbody.detectCollisions = available && _wasDetectingCollisions;
        if (!available)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
}
