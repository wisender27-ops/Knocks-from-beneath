using System;
using UnityEngine;

public class QuestParticleGate : MonoBehaviour
{
    [Header("Режим")]
    [Tooltip("Если включено — партиклы всегда работают (например, мусор/вонь), независимо от квеста.")]
    [SerializeField] private bool alwaysOn = false;

    [Header("Квест-теги, при которых включать партиклы")]
    [Tooltip("Совпадает с QuestManager.questTag. Можно указать несколько.")]
    [SerializeField] private string[] activeQuestTags;

    [Header("Какие партиклы контролировать")]
    [Tooltip("Если список пуст — будут собраны все ParticleSystem на объекте и в детях.")]
    [SerializeField] private ParticleSystem[] particleSystems;

    [Tooltip("Собирать партиклы из детей, если particleSystems пуст.")]
    [SerializeField] private bool includeChildren = true;

    void Awake()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = includeChildren
                ? GetComponentsInChildren<ParticleSystem>(true)
                : GetComponents<ParticleSystem>();
        }
    }

    void OnEnable()
    {
        QuestManager.OnActiveQuestTagChanged += HandleQuestChanged;
        Refresh();
    }

    void OnDisable()
    {
        QuestManager.OnActiveQuestTagChanged -= HandleQuestChanged;
    }

    void HandleQuestChanged(string _)
    {
        Refresh();
    }

    void Refresh()
    {
        bool shouldEmit = alwaysOn || MatchesActiveQuestTag();

        if (particleSystems == null) return;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null) continue;

            if (shouldEmit)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                if (ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    bool MatchesActiveQuestTag()
    {
        if (QuestManager.Instance == null) return false;
        if (activeQuestTags == null || activeQuestTags.Length == 0) return false;

        string current = QuestManager.Instance.GetActiveQuestTag();
        if (string.IsNullOrEmpty(current)) return false;

        for (int i = 0; i < activeQuestTags.Length; i++)
        {
            string tag = activeQuestTags[i];
            if (string.IsNullOrEmpty(tag)) continue;
            if (string.Equals(current, tag, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

