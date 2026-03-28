using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class RainZoneSwitcher : MonoBehaviour
{
    [Header("Audio")]
    public AudioMixerSnapshot enterSnapshot;
    public AudioMixerSnapshot exitSnapshot;
    public float transitionTime = 3f;

    [Header("Fog")]
    public float enterFogDensity;
    public float exitFogDensity;

    // Local tracking (per trigger) to avoid double-counting when the player has multiple colliders.
    private readonly HashSet<Collider> _playerCollidersInsideThisTrigger = new HashSet<Collider>();

    private Coroutine exitCoroutine;

    // Global tracking across ALL RainZoneSwitcher triggers.
    private static int s_playerInsideAnyRainZone = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        if (!_playerCollidersInsideThisTrigger.Add(other)) return;

        s_playerInsideAnyRainZone++;

        // ❗ отменяем выход, если он был
        if (exitCoroutine != null)
        {
            StopCoroutine(exitCoroutine);
            exitCoroutine = null;
        }

        // Only apply "enter" when we transition 0 -> 1 across all zones.
        if (s_playerInsideAnyRainZone != 1) return;

        enterSnapshot.TransitionTo(transitionTime);
        FogController.Instance.SetFog(enterFogDensity, transitionTime);

        Debug.Log("Вход в зону: " + gameObject.name);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        if (!_playerCollidersInsideThisTrigger.Remove(other)) return;

        s_playerInsideAnyRainZone--;
        if (s_playerInsideAnyRainZone < 0) s_playerInsideAnyRainZone = 0;

        // Only apply "exit" when we transition 1 -> 0 across all zones.
        if (s_playerInsideAnyRainZone > 0) return;

        // ❗ запускаем отложенный выход
        exitCoroutine = StartCoroutine(DelayedExit());
    }

    private IEnumerator DelayedExit()
    {
        yield return new WaitForSeconds(0.1f); // можно 0.05–0.2

        if (s_playerInsideAnyRainZone <= 0)
        {
            exitSnapshot.TransitionTo(transitionTime);
            FogController.Instance.SetFog(exitFogDensity, transitionTime);

            Debug.Log("Выход из зоны: " + gameObject.name);
        }

        exitCoroutine = null;
    }

    private static bool IsPlayerCollider(Collider col)
    {
        // Prefer root tag check so child colliders without the tag still count.
        if (col == null) return false;
        var root = col.transform != null ? col.transform.root : null;
        return root != null && root.CompareTag("Player");
    }
}