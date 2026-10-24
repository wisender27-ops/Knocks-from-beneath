using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class NeighborVisitSequence : MonoBehaviour
    {
        public GameObject neighborPrefab;
        public Transform visitPoint;
        public Transform[] departurePath;
        public Door frontDoor;
        public LightSwitch porchLightSwitch;
        public Transform player;
        public AudioClip knockClip;
        [Min(0.5f)] public float conversationDistance = 3f;
        [Min(0.1f)] public float walkingSpeed = 1.1f;
        public string walkingBoolParameter = "IsWalking";

        private Quaternion closedDoorRotation;
        private bool porchStateCaptured, porchWasOn;
        private GameObject neighbor;
        private AudioSource knockSource;
        private bool played, running, lockCaptured, doorWasLocked, waitingForBed, bedReached, audioPaused;
        public bool IsPlaying => running;
        public bool IsConfigured => neighborPrefab != null && visitPoint != null && frontDoor != null && player != null;
        public bool HasVisited { get; private set; }

        public IEnumerator Play()
        {
            if (played || !isActiveAndEnabled || !IsConfigured)
                yield break;
            played = true;
            running = true;
            closedDoorRotation = frontDoor.transform.rotation;
            doorWasLocked = frontDoor.IsInteractionLocked;
            lockCaptured = true;
            frontDoor.SetInteractionLocked(false);
            try
            {
                if (porchLightSwitch != null)
                {
                    porchWasOn = porchLightSwitch.isOn;
                    porchStateCaptured = true;
                    porchLightSwitch.ForceTurnOn();
                }
                neighbor = Instantiate(neighborPrefab, visitPoint.position, visitPoint.rotation);
                neighbor.SetActive(true);
                knockSource = HorrorMomentDirector.CreateSource(frontDoor.transform, "Neighbor door knocks", 24f);
                knockSource.minDistance = 3f;
                if (knockClip == null) knockClip = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
                yield return Knock();
                if (!running) yield break;
                QuestManager.Instance?.CreateQuest("Проверить, кто стучит в дверь", 1, questTag: "neighbor-visit");
                float reminder = 0f;
                while (running && !CanTalk())
                {
                    if (Playable())
                    {
                        reminder += Time.deltaTime;
                        if (reminder >= 14f)
                        {
                            reminder = 0f;
                            yield return Knock();
                        }
                    }
                    yield return null;
                }
                if (!running) yield break;
                QuestManager.Instance?.RecordQuestCompletion("neighbor-visit");
                yield return Say("СОСЕД", "Привет. Я из дома напротив. Извини, что так поздно.");
                yield return Say("СОСЕД", "Увидел, как ты ходишь на первом этаже. Решил зайти познакомиться.");
                yield return Say("Я", "На первом? Я спал наверху. Только сейчас проснулся.");
                yield return Say("СОСЕД", "Да? Ну... наверное, показалось. В окне мелькнуло что-то.");
                yield return Say("СОСЕД", "Ладно, не буду мешать. Спокойной ночи.");
                if (!running) yield break;
                yield return Leave();
                if (!running) yield break;
                HasVisited = true;
                RestoreDoor();
                waitingForBed = true;
                bedReached = false;
                GameEvents.OnBedTriggerReached += OnBedReached;
                QuestManager.Instance?.CreateQuest("Вернуться в кровать", 1, questTag: "neighbor-return-bed");
                while (running && !bedReached) yield return null;
                if (!running) yield break;
                QuestManager.Instance?.RecordQuestCompletion("neighbor-return-bed");
            }
            finally
            {
                Cleanup();
            }
        }

        private bool Playable() => Time.timeScale > 0f && !RetroInventoryScreen.IsAnyOpen && !QuestJournalScreen.IsAnyOpen;

        private bool CanTalk()
        {
            if (!Playable() || !frontDoor.isOpen || !NarrativeDisplayQueue.CanShowInteractionHint) return false;
            if (Quaternion.Angle(closedDoorRotation, frontDoor.transform.rotation) < 65f) return false;
            Vector3 delta = player.position - visitPoint.position;
            if (Mathf.Abs(delta.y) >= 2.2f || Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > conversationDistance * conversationDistance) return false;
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : player.position + Vector3.up * 1.6f;
            Vector3 target = visitPoint.position + Vector3.up * 1.5f;
            if (Physics.Linecast(eye, target, out RaycastHit hit, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore))
                return neighbor != null && hit.transform.IsChildOf(neighbor.transform);
            return true;
        }

        private IEnumerator Knock()
        {
            for (int i = 0; i < 3; i++)
            {
                while (running && !Playable()) yield return null;
                if (!running) yield break;
                if (knockSource != null && knockClip != null) knockSource.PlayOneShot(knockClip, 0.7f);
                yield return new WaitForSeconds(0.42f);
            }
        }

        private IEnumerator Say(string speaker, string line)
        {
            if (!running || ThoughtManager.Instance == null) yield break;
            bool finished = false;
            ThoughtManager.Instance.ShowDialogue(speaker, new[] { line }, () => finished = true);
            while (running && !finished) yield return null;
        }

        private IEnumerator Leave()
        {
            if (departurePath == null) yield break;
            SetWalking(true);
            foreach (Transform waypoint in departurePath)
            {
                if (waypoint == null) continue;
                while (running && neighbor != null && (neighbor.transform.position - waypoint.position).sqrMagnitude > 0.01f)
                {
                    if (Playable())
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(waypoint.position - neighbor.transform.position, Vector3.up);
                        if (direction.sqrMagnitude > 0.001f)
                            neighbor.transform.rotation = Quaternion.RotateTowards(neighbor.transform.rotation, Quaternion.LookRotation(direction), 180f * Time.deltaTime);
                        neighbor.transform.position = Vector3.MoveTowards(neighbor.transform.position, waypoint.position, walkingSpeed * Time.deltaTime);
                    }
                    yield return null;
                }
            }
            if (neighbor != null) Destroy(neighbor);
            neighbor = null;
        }

        private void SetWalking(bool walking)
        {
            Animator animator = neighbor != null ? neighbor.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(walkingBoolParameter)) return;
            int hash = Animator.StringToHash(walkingBoolParameter);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(hash, walking);
                    return;
                }
        }

        private void OnBedReached()
        {
            if (waitingForBed && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("neighbor-return-bed"))
                bedReached = true;
        }

        private void RestoreDoor()
        {
            if (!lockCaptured) return;
            if (frontDoor != null) frontDoor.SetInteractionLocked(doorWasLocked);
            lockCaptured = false;
        }

        private void Cleanup()
        {
            GameEvents.OnBedTriggerReached -= OnBedReached;
            waitingForBed = false;
            running = false;
            RestoreDoor();
            if (porchStateCaptured && porchLightSwitch != null)
            {
                if (porchWasOn) porchLightSwitch.ForceTurnOn();
                else porchLightSwitch.ForceTurnOff();
            }
            porchStateCaptured = false;
            if (neighbor != null) Destroy(neighbor);
            neighbor = null;
            if (knockSource != null) Destroy(knockSource.gameObject);
            knockSource = null;
        }

        private void Update()
        {
            HorrorMomentDirector.SetAudioPaused(knockSource, !Playable(), ref audioPaused);
        }

        private void OnDisable() => Cleanup();
    }
}
