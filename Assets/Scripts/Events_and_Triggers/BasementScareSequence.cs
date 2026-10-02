using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class BasementScareSequence : MonoBehaviour
    {
        public enum ScarePhase { Ready, Listening, Reveal, Attack, Recovery, Finished }

        [SerializeField] private GameObject creature;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private Light[] basementLights;
        [SerializeField] private AudioSource creatureVoice;
        [SerializeField] private AudioSource impactAudio;
        [SerializeField] private AudioClip breathClip;
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AudioClip stepClip;
        [SerializeField] private FirstPersonKnockdown knockdown;
        [SerializeField, Min(0.1f)] private float recognitionTime = 0.85f;
        [SerializeField, Min(0.1f)] private float attackDuration = 0.52f;
        [SerializeField, Range(0.05f, 1f)] private float beamExposure = 0.18f;

        public ScarePhase Phase { get; private set; }
        public bool Completed { get; private set; }

        private static readonly int ObserveState = Animator.StringToHash("Observe");
        private static readonly int AttackState = Animator.StringToHash("Attack");
        private Transform head;
        private Transform chest;
        private Transform leftLeg;
        private Transform rightLeg;
        private Camera playerCamera;
        private PlayerInventory inventory;
        private PlayerController player;
        private Light beam;
        private Vector3 creaturePosition;
        private Quaternion creatureRotation;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private float cameraFov;
        private float beamIntensity;
        private bool beamEnabled;
        private bool playerEnabled;
        private bool cameraLocked;
        private bool inventoryEnabled;
        private float[] lampIntensities;
        private bool[] lampStates;
        private bool prepared;
        private bool captured;
        private bool controlCaptured;
        private float headWeight;
        private float charge;
        private Vector3 lookPosition;
        private Vector3 attackCameraForward;
        private Quaternion beamRotation;
        private HeadBobbing headBobbing;
        private bool headBobbingEnabled;
        private PlayerInteraction interaction;
        private PlayerDoorOpen doorInput;
        private bool interactionEnabled;
        private bool doorInputEnabled;
        private Rigidbody playerBody;
        private bool bodyWasKinematic;
        private RigidbodyConstraints bodyConstraints;
        private Vector3 bodyVelocity;
        private Vector3 bodyAngularVelocity;

        public void Prepare()
        {
            if (prepared || creature == null) return;
            prepared = true;
            creaturePosition = creature.transform.position;
            creatureRotation = creature.transform.rotation;
            lampIntensities = new float[basementLights.Length];
            lampStates = new bool[basementLights.Length];
            for (int i = 0; i < basementLights.Length; i++)
            {
                Light lamp = basementLights[i];
                if (lamp == null) continue;
                lampIntensities[i] = lamp.intensity;
                lampStates[i] = lamp.enabled;
                lamp.intensity = i == 0 ? 0.08f : 0f;
            }
            creature.SetActive(false);
            if (creatureVoice != null) creatureVoice.transform.position = creaturePosition + Vector3.up * 1.2f;
        }

        public IEnumerator Play(Camera camera, PlayerInventory playerInventory)
        {
            if (creature == null || animator == null || camera == null ||
                playerInventory == null || playerInventory.flashlightLightSource == null)
            {
                Debug.LogError("[BasementScareSequence] Missing scene references.", this);
                yield break;
            }

            Prepare();
            playerCamera = camera;
            inventory = playerInventory;
            player = inventory.GetComponent<PlayerController>();
            beam = inventory.flashlightLightSource;
            beamIntensity = beam.intensity;
            beamEnabled = beam.enabled;
            beamRotation = beam.transform.localRotation;
            captured = true;
            Phase = ScarePhase.Listening;
            if (creatureVoice != null && breathClip != null)
                creatureVoice.PlayOneShot(breathClip, 0.3f);

            // Игрок сам обнаруживает силуэт; управление забирается только на ударе.
            yield return new WaitForSeconds(0.32f);
            SetBasementLight(0f);
            beam.enabled = false;
            yield return new WaitForSeconds(0.075f);
            creature.SetActive(true);
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Play(ObserveState, 0, 0.12f);
            animator.Update(0f);
            CacheBones();
            beam.intensity = beamIntensity * beamExposure;
            beam.enabled = true;
            Phase = ScarePhase.Reveal;

            for (float elapsed = 0f; elapsed < recognitionTime; elapsed += Time.deltaTime)
            {
                headWeight = Mathf.SmoothStep(0f, 0.8f, elapsed / recognitionTime);
                lookPosition = playerCamera.transform.position;
                yield return null;
            }

            CaptureControl();
            Phase = ScarePhase.Attack;
            Vector3 end = ResolveAttackPosition();
            Vector3 forward = end - creaturePosition;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
                creature.transform.rotation = Quaternion.LookRotation(forward);
            animator.CrossFadeInFixedTime(AttackState, 0.055f, 0, 0.35f);
            if (creatureVoice != null)
            {
                creatureVoice.Stop();
                if (attackClip != null) creatureVoice.PlayOneShot(attackClip, 0.85f);
            }

            bool impactPlayed = false;
            bool stepPlayed = false;
            for (float elapsed = 0f; elapsed < attackDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / attackDuration);
                charge = t;
                headWeight = 0.9f;
                lookPosition = playerCamera.transform.position;
                // Быстрый набор скорости и торможение перед лицом, без телепорта в камеру.
                float travel = 1f - Mathf.Pow(1f - t, 2.2f);
                creature.transform.position = Vector3.Lerp(creaturePosition, end, travel);
                if (!stepPlayed && t >= 0.32f)
                {
                    stepPlayed = true;
                    if (creatureVoice != null && stepClip != null)
                        creatureVoice.PlayOneShot(stepClip, 0.55f);
                }
                if (!impactPlayed && t >= 0.78f)
                {
                    impactPlayed = true;
                    if (impactAudio != null) impactAudio.Play();
                }
                yield return null;
            }

            creature.transform.position = end;
            yield return new WaitForSeconds(0.11f);
            Phase = ScarePhase.Recovery;
            beam.enabled = false;
            creature.SetActive(false);
            headWeight = 0f;
            if (creatureVoice != null) creatureVoice.Stop();
            beam.transform.localRotation = beamRotation;
            if (knockdown != null)
                yield return knockdown.Play(playerCamera, inventory.transform, cameraPosition,
                    cameraRotation, cameraFov, beam);
            else
                yield return new WaitForSeconds(0.34f);

            Completed = true;
            Phase = ScarePhase.Finished;
            Restore();
        }

        private void CacheBones()
        {
            if (!animator.isHuman) return;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }

        private void CaptureControl()
        {
            cameraPosition = playerCamera.transform.localPosition;
            cameraRotation = playerCamera.transform.localRotation;
            attackCameraForward = playerCamera.transform.forward;
            headBobbing = playerCamera.GetComponentInParent<HeadBobbing>();
            headBobbingEnabled = headBobbing != null && headBobbing.enabled;
            if (headBobbing != null) headBobbing.enabled = false;
            cameraFov = playerCamera.fieldOfView;
            playerEnabled = player != null && player.enabled;
            cameraLocked = player != null && player.isCameraLocked;
            inventoryEnabled = inventory.enabled;
            interaction = inventory.GetComponent<PlayerInteraction>();
            doorInput = inventory.GetComponent<PlayerDoorOpen>();
            interactionEnabled = interaction != null && interaction.enabled;
            doorInputEnabled = doorInput != null && doorInput.enabled;
            playerBody = inventory.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                bodyWasKinematic = playerBody.isKinematic;
                bodyConstraints = playerBody.constraints;
                bodyVelocity = playerBody.linearVelocity;
                bodyAngularVelocity = playerBody.angularVelocity;
                playerBody.isKinematic = true;
                playerBody.constraints = RigidbodyConstraints.FreezeAll;
            }
            if (interaction != null) interaction.enabled = false;
            if (doorInput != null) doorInput.enabled = false;
            controlCaptured = true;
            if (player != null)
            {
                player.isCameraLocked = true;
                player.enabled = false;
            }
            inventory.enabled = false;
        }

        private Vector3 ResolveAttackPosition()
        {
            Vector3 end = attackPoint != null ? attackPoint.position : creaturePosition;
            Vector3 flatCamera = playerCamera.transform.position;
            flatCamera.y = end.y;
            Vector3 direction = flatCamera - end;
            if (direction.sqrMagnitude > 0.01f)
                end = flatCamera - direction.normalized * 0.85f;
            end.x = Mathf.Clamp(end.x, creaturePosition.x - 0.35f, creaturePosition.x + 0.35f);
            end.z = Mathf.Clamp(end.z, creaturePosition.z + 0.5f, attackPoint != null ? attackPoint.position.z : end.z);
            RaycastHit hit;
            if (Physics.Raycast(end + Vector3.up * 3f, Vector3.down, out hit, 8f,
                    PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore))
                end.y = hit.point.y + 0.08f;
            return end;
        }

        private void LateUpdate()
        {
            if (!captured || !creature.activeSelf) return;
            if (creatureVoice != null) creatureVoice.transform.position = creature.transform.position + Vector3.up * 1.2f;
            // После Animator: сохраняем живое тело, добавляя направленный взгляд и один шаг рывка.
            if (Phase == ScarePhase.Attack)
            {
                float stride = Mathf.Sin(charge * Mathf.PI * 2f) * 24f;
                if (leftLeg != null) leftLeg.localRotation *= Quaternion.Euler(stride, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation *= Quaternion.Euler(-stride, 0f, 0f);
                if (chest != null) chest.localRotation *= Quaternion.Euler(12f * Mathf.Sin(charge * Mathf.PI), 0f, -6f);
            }
            if (head != null && headWeight > 0f)
            {
                Quaternion aim = Quaternion.LookRotation(lookPosition - head.position);
                head.rotation = Quaternion.Slerp(head.rotation, aim, headWeight);
                if (Phase == ScarePhase.Attack)
                {
                    Vector3 direction = Vector3.Slerp(attackCameraForward,
                        (head.position - playerCamera.transform.position).normalized, charge * 0.95f);
                    playerCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
                // Фонарик в этой сцене не обязан быть дочерним объектом камеры.
                beam.transform.rotation = Quaternion.LookRotation(head.position - beam.transform.position, Vector3.up);
            }
            if (Phase != ScarePhase.Attack) return;
            float impact = Mathf.Exp(-Mathf.Abs(charge - 0.82f) * 14f);
            float shake = 0.014f * impact;
            playerCamera.transform.localPosition = cameraPosition + new Vector3(
                Mathf.Sin(charge * 93f) * shake, -0.06f * impact,
                -0.025f * impact);
            playerCamera.transform.localRotation *= Quaternion.Euler(-3f * impact, 0f, 2.5f * impact);
            playerCamera.fieldOfView = cameraFov - 8f * charge + 4f * impact;
        }

        private void SetBasementLight(float intensity)
        {
            for (int i = 0; i < basementLights.Length; i++)
                if (basementLights[i] != null) basementLights[i].intensity = intensity;
        }

        public void Restore()
        {
            if (knockdown != null) knockdown.Restore();
            headWeight = 0f;
            if (creature != null)
            {
                creature.SetActive(false);
                if (prepared) creature.transform.SetPositionAndRotation(creaturePosition, creatureRotation);
            }
            if (creatureVoice != null) creatureVoice.Stop();
            if (captured && beam != null)
            {
                beam.intensity = beamIntensity;
                beam.enabled = beamEnabled;
                beam.transform.localRotation = beamRotation;
            }
            if (controlCaptured)
            {
                if (playerCamera != null)
                {
                    playerCamera.transform.localPosition = cameraPosition;
                    playerCamera.transform.localRotation = cameraRotation;
                    playerCamera.fieldOfView = cameraFov;
                }
                if (playerBody != null)
                {
                    playerBody.constraints = bodyConstraints;
                    playerBody.isKinematic = bodyWasKinematic;
                    if (!bodyWasKinematic)
                    {
                        playerBody.linearVelocity = bodyVelocity;
                        playerBody.angularVelocity = bodyAngularVelocity;
                    }
                }
                if (interaction != null) interaction.enabled = interactionEnabled;
                if (doorInput != null) doorInput.enabled = doorInputEnabled;
                if (player != null)
                {
                    player.isCameraLocked = cameraLocked;
                    player.enabled = playerEnabled;
                }
                if (inventory != null) inventory.enabled = inventoryEnabled;
                if (headBobbing != null) headBobbing.enabled = headBobbingEnabled;
            }
            if (!Completed && impactAudio != null) impactAudio.Stop();
            if (prepared && !Completed)
                for (int i = 0; i < basementLights.Length; i++)
                    if (basementLights[i] != null)
                    {
                        basementLights[i].intensity = lampIntensities[i];
                        basementLights[i].enabled = lampStates[i];
                    }
            if (!Completed)
            {
                prepared = false;
                Phase = ScarePhase.Ready;
            }
            controlCaptured = false;
            captured = false;
        }

        private void OnDisable() => Restore();
    }
}
