using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class NarrativeDisplayQueue : MonoBehaviour
    {
        private static NarrativeDisplayQueue _instance;
        private readonly Queue<Request> _requests = new Queue<Request>();
        private bool _running;
        private Coroutine _drain;
        private IEnumerator _activeDisplay;
        public static bool CanShowInteractionHint => !NarrativeReadingPanel.IsModal && (_instance == null || !_instance._running);
        public static event Action<bool> InteractionHintAvailabilityChanged;

        private struct Request
        {
            public MonoBehaviour owner;
            public Func<IEnumerator> display;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
            InteractionHintAvailabilityChanged = null;
        }

        public static void Enqueue(MonoBehaviour owner, Func<IEnumerator> display)
        {
            if (owner == null || !owner.isActiveAndEnabled) return;
            if (_instance == null)
                _instance = new GameObject("NarrativeDisplayQueue").AddComponent<NarrativeDisplayQueue>();
            _instance._requests.Enqueue(new Request { owner = owner, display = display });
            if (!_instance._running)
            {
                _instance._running = true;
                InteractionHintAvailabilityChanged?.Invoke(false);
                _instance._drain = _instance.StartCoroutine(_instance.Drain());
            }
        }

        public static void InterruptAndEnqueue(MonoBehaviour owner, Func<IEnumerator> display)
        {
            if (owner == null || !owner.isActiveAndEnabled) return;
            if (_instance != null)
            {
                if (_instance._drain != null) _instance.StopCoroutine(_instance._drain);
                (_instance._activeDisplay as IDisposable)?.Dispose();
                _instance._activeDisplay = null;
                _instance._requests.Clear();
                _instance._running = false;
            }
            ThoughtManager.Instance?.HideImmediately();
            QuestManager.Instance?.HideToast();
            Enqueue(owner, display);
        }

        private IEnumerator Drain()
        {
            _running = true;
            while (_requests.Count > 0)
            {
                Request request = _requests.Dequeue();
                if (request.owner == null || !request.owner.isActiveAndEnabled) continue;
                // Callbacks can enqueue more text; keep the queue locked until they return.
                IEnumerator display = request.display();
                _activeDisplay = display;
                while (request.owner != null && request.owner.isActiveAndEnabled)
                {
                    bool hasNext;
                    try { hasNext = display.MoveNext(); }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, request.owner);
                        break;
                    }
                    if (!hasNext) break;
                    yield return display.Current;
                }
                (display as IDisposable)?.Dispose();
                _activeDisplay = null;
            }
            _running = false;
            InteractionHintAvailabilityChanged?.Invoke(true);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                (_activeDisplay as System.IDisposable)?.Dispose();
                _activeDisplay = null;
                _requests.Clear();
                _instance = null;
                InteractionHintAvailabilityChanged?.Invoke(true);
            }
        }
    }
}
