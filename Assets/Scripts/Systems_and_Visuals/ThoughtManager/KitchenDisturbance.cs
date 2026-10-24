using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class KitchenDisturbance : MonoBehaviour
    {
        private IntroSequence intro;
        private QuestManager quests;
        private MeshRenderer original;
        private GameObject movingPlate;
        private AudioSource sound;
        private AudioClip scrapeClip, impactClip;
        private bool armed, consumed, running, audioPaused, settled;
        private float eligibleSince;
        private Vector3 start, edge, landing;
        private Quaternion rotation;

        public void Initialize(IntroSequence sequence, PlayerInventory inventory)
        {
            if (quests != null) quests.OnQuestChanged -= OnQuestChanged;
            intro = sequence;
            quests = null;
            BindQuest();
            OnQuestChanged();
        }

        private void OnEnable()
        {
            BindQuest();
            OnQuestChanged();
        }

        private void Start() => BindQuest();

        private void BindQuest()
        {
            if (quests == null) quests = QuestManager.Instance;
            if (quests == null || !isActiveAndEnabled) return;
            quests.OnQuestChanged -= OnQuestChanged;
            quests.OnQuestChanged += OnQuestChanged;
            OnQuestChanged();
        }

        private void StagePlate()
        {
            if (movingPlate != null || intro == null) return;
            GameObject plate = GameObject.Find("Plate_01");
            if (plate != null && plate.activeInHierarchy && Prepare(plate)) original.enabled = false;
        }

        public void ConfigureAudio(AudioClip scrape, AudioClip impact)
        {
            scrapeClip = scrape;
            impactClip = impact;
        }

        private void OnQuestChanged()
        {
            if (consumed || quests == null) return;
            QuestManager.QuestData quest = quests.GetActiveQuest();
            string tag = quests.GetActiveQuestTag();
            bool dinner = tag == "dinner-ingredients" || tag == "dinner-cook" || tag == "dinner-eat";
            if (tag != "room-decoration" && !dinner)
            {
                if (!string.IsNullOrEmpty(tag) && armed) { consumed = true; armed = false; }
                return;
            }
            bool progressed = quest != null && tag == "room-decoration" && quest.currentAmount >= 2;
            if (!progressed && dinner && quests.questList != null)
                foreach (QuestManager.QuestData past in quests.questList)
                    if (past != null && past.questTag == "room-decoration" && past.currentAmount >= 2)
                    { progressed = true; break; }
            if (tag == "room-decoration" || progressed) StagePlate();
            if (!armed && progressed)
            {
                armed = true;
                eligibleSince = Time.time;
            }
        }

        private void Update()
        {
            if (quests == null) BindQuest();
            if (!armed || consumed || running || intro == null || intro.playerTransform == null) return;
            HorrorMomentDirector director = HorrorMomentDirector.Instance;
            if (director == null || !director.CanPresent) { eligibleSince = Time.time; return; }
            if (Time.time - eligibleSince < 1.1f) return;
            if (movingPlate == null) { StagePlate(); if (movingPlate == null) return; }
            if (Vector3.Distance(intro.playerTransform.position, start) > 4.8f) return;
            if (!director.TryBegin(this)) return;
            consumed = true;
            armed = false;
            running = true;
            StartCoroutine(Present(director));
        }

        private bool Prepare(GameObject plate)
        {
            MeshFilter filter = plate.GetComponent<MeshFilter>();
            original = plate.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || original == null || !original.enabled ||
                plate.GetComponent<PickableItem>() != null) return false;
            start = plate.transform.position;
            rotation = plate.transform.rotation;
            Vector3 center = original.bounds.center;
            float radius = Mathf.Max(original.bounds.extents.x, original.bounds.extents.z);
            GameObject supportObject = GameObject.Find("colliders_026");
            Collider support = supportObject != null ? supportObject.GetComponent<Collider>() : null;
            if (support == null || !support.enabled || support.isTrigger) return false;
            Bounds counter = support.bounds;
            if (counter.size.x < radius * 2f + .08f || counter.size.z < radius * 2f + .08f) return false;
            // The authored plate is below the kitchen collider. Stage its visual on the real
            // support top at runtime, keeping the original scene and quest objects intact.
            Vector3 supportedCenter = new Vector3(
                Mathf.Clamp(center.x, counter.min.x + radius + .03f, counter.max.x - radius - .03f),
                counter.max.y + .012f + original.bounds.extents.y,
                Mathf.Clamp(center.z, counter.min.z + radius + .03f, counter.max.z - radius - .03f));
            start += supportedCenter - center;
            float distance = counter.max.x + radius + .09f - supportedCenter.x;
            Vector3 outside = supportedCenter + Vector3.right * distance;
            Vector3 footprint = new Vector3(radius, original.bounds.extents.y + .008f, radius);
            if (Physics.BoxCast(supportedCenter + Vector3.up * .06f, footprint, Vector3.right,
                    out _, Quaternion.identity, distance, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore) ||
                !Physics.Raycast(outside + Vector3.up * .15f, Vector3.down, out RaycastHit floor,
                    3f, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore) ||
                !Physics.BoxCast(outside + Vector3.up * .15f, footprint, Vector3.down,
                    out RaycastHit landingSupport, Quaternion.identity, 3f, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore) ||
                landingSupport.point.y > floor.point.y + .08f ||
                floor.point.y > counter.max.y - .35f || floor.normal.y < .85f) return false;
            edge = start + Vector3.right * distance;
            landing = edge + Vector3.down * (counter.max.y - floor.point.y);
            movingPlate = new GameObject("Kitchen disturbed plate");
            original.transform.position = start;
            movingPlate.transform.SetPositionAndRotation(start, rotation);
            movingPlate.transform.localScale = plate.transform.lossyScale;
            movingPlate.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer visual = movingPlate.AddComponent<MeshRenderer>();
            visual.sharedMaterials = original.sharedMaterials;
            visual.shadowCastingMode = original.shadowCastingMode;
            visual.receiveShadows = original.receiveShadows;
            visual.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            visual.reflectionProbeUsage = original.reflectionProbeUsage;
            visual.renderingLayerMask = original.renderingLayerMask;
            visual.sortingLayerID = original.sortingLayerID;
            visual.sortingOrder = original.sortingOrder;
            sound = HorrorMomentDirector.CreateSource(movingPlate.transform, "Plate audio", 12f);
            sound.minDistance = 1.2f;
            sound.maxDistance = 12f;
            sound.rolloffMode = AudioRolloffMode.Logarithmic;
            sound.dopplerLevel = 0f;
            return true;
        }

        private IEnumerator Present(HorrorMomentDirector director)
        {
            if (scrapeClip != null) sound.PlayOneShot(scrapeClip, .36f);
            float elapsed = 0f;
            while (elapsed < 1.35f)
            {
                if (!director.IsOwner(this)) { CancelPresentation(); yield break; }
                HorrorMomentDirector.SetAudioPaused(sound, !director.GameplayAvailable, ref audioPaused);
                if (!director.GameplayAvailable) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 1.35f);
                movingPlate.transform.position = Vector3.Lerp(start, edge, t * t);
                yield return null;
            }
            sound.Stop();
            elapsed = 0f;
            float fallTime = Mathf.Sqrt(2f * Mathf.Max(.05f, edge.y - landing.y) / Mathf.Max(.1f, Physics.gravity.magnitude));
            while (elapsed < fallTime)
            {
                if (!director.IsOwner(this)) { CancelPresentation(); yield break; }
                HorrorMomentDirector.SetAudioPaused(sound, !director.GameplayAvailable, ref audioPaused);
                if (!director.GameplayAvailable) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallTime);
                movingPlate.transform.position = Vector3.Lerp(edge, landing, t * t);
                movingPlate.transform.rotation = rotation * Quaternion.Euler(0f, 0f, 24f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (impactClip != null) sound.PlayOneShot(impactClip, .62f);
            elapsed = 0f;
            while (elapsed < .55f)
            {
                if (!director.IsOwner(this)) { CancelPresentation(); yield break; }
                HorrorMomentDirector.SetAudioPaused(sound, !director.GameplayAvailable, ref audioPaused);
                if (!director.GameplayAvailable) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .55f);
                movingPlate.transform.position = landing + Vector3.up * (.045f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * (1f - t));
                movingPlate.transform.rotation = rotation * Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t));
                yield return null;
            }
            movingPlate.transform.SetPositionAndRotation(landing, rotation);
            settled = true;
            running = false;
            director.End(this, 18f);
        }

        private void CancelPresentation()
        {
            running = false;
            DisposeVisual();
            HorrorMomentDirector.Instance?.End(this, 18f);
        }

        private void DisposeVisual()
        {
            if (original != null)
            {
                if (movingPlate != null) original.transform.SetPositionAndRotation(settled ? landing : start, rotation);
                original.enabled = true;
            }
            if (sound != null) sound.Stop();
            if (movingPlate != null) Destroy(movingPlate);
            movingPlate = null;
            audioPaused = false;
        }

        private void OnDisable()
        {
            if (quests != null) quests.OnQuestChanged -= OnQuestChanged;
            StopAllCoroutines();
            running = false;
            DisposeVisual();
            HorrorMomentDirector.Instance?.End(this, 18f);
        }
    }
}
