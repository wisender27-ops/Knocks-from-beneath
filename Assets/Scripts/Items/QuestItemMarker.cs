using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class QuestItemMarker : MonoBehaviour
    {
        private static readonly List<QuestItemMarker> markers = new List<QuestItemMarker>();
        private static QuestItemMarkerPresentation presentation;
        private Renderer[] renderers;
        private Collider[] colliders;
        private SimpleItem simple;
        private CollectableItem collectable;
        private PickableItem pickable;
        private TrashPile trash;
        private PieQuestItem pie;
        private string[] explicitTags;
        private System.Func<bool> condition;
        private float opacity, nextVisibilityCheck, distance, phase;
        private bool unobstructed;
        private Vector2 screenPoint;
        private float visibleDistance = MinVisibleDistance;

        // Раньше метку было видно только с 4 м — подходя к комнате, игрок её не замечал.
        // Меньше этого расстояния не даём, даже если у предмета в сцене стоит старое значение.
        public const float MinVisibleDistance = 9f;
        public float VisibleDistance
        {
            get => visibleDistance;
            set => visibleDistance = Mathf.Max(MinVisibleDistance, value);
        }

        // Тёплый янтарный — тот же, что у активного значка руки (InteractionPresentation).
        private static readonly Color Accent = new Color32(236, 190, 112, 255);
        private static readonly Color Core = new Color32(255, 246, 222, 255);
        private static readonly Color Outline = new Color32(24, 18, 12, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            markers.Clear();
            presentation = null;
            SceneManager.sceneLoaded -= ConfigureScene;
            SceneManager.sceneLoaded += ConfigureScene;
        }

        private static void ConfigureScene(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is SimpleItem || component is CollectableItem || component is PickableItem ||
                        component is TrashPile || component is PieQuestItem)
                        Ensure(component.gameObject);
        }

        public static QuestItemMarker Ensure(GameObject item)
        {
            var marker = item.GetComponent<QuestItemMarker>();
            return marker != null ? marker : item.AddComponent<QuestItemMarker>();
        }

        public void ConfigureTags(string[] tags) => explicitTags = tags;
        // Для предметов без квестового тега: метка видна, пока условие истинно.
        public void ConfigureCondition(System.Func<bool> condition) => this.condition = condition;

        public static void SuppressLegacyParticles(Transform root)
        {
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                var emission = particles.emission;
                emission.enabled = false;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = false;
                if (particles.gameObject != root.gameObject) particles.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            simple = GetComponent<SimpleItem>();
            collectable = GetComponent<CollectableItem>();
            pickable = GetComponent<PickableItem>();
            trash = GetComponent<TrashPile>();
            pie = GetComponent<PieQuestItem>();
            var visual = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (!(renderer is ParticleSystemRenderer) && renderer.name != "PixelOutline") visual.Add(renderer);
            renderers = visual.ToArray();
            colliders = GetComponentsInChildren<Collider>(true);
            // Своя фаза покачивания, чтобы соседние метки не двигались строем.
            phase = (GetInstanceID() & 1023) * .37f;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!markers.Contains(this)) markers.Add(this);
            if (presentation == null)
            {
                presentation = new GameObject("Quest item indicators").AddComponent<QuestItemMarkerPresentation>();
                DontDestroyOnLoad(presentation.gameObject);
            }
        }

        private void OnDisable()
        {
            markers.Remove(this);
            opacity = 0f;
        }

        private bool IsRequired()
        {
            var quests = QuestManager.Instance;
            if (quests == null || quests.GetActiveQuest() == null) return false;
            if (simple != null) return simple.IsAvailable && quests.IsItemRequired(simple.itemType);
            if (trash != null) return quests.IsQuestActive("trash-collect");
            if (pie != null) return quests.IsQuestActive("pie-take") || quests.IsQuestActive("pie-eat");
            if (pickable != null && !string.IsNullOrEmpty(pickable.placementQuestTag))
                return pickable.enabled && quests.IsQuestActive(pickable.placementQuestTag);
            if (collectable != null)
            {
                switch (collectable.currentItemType)
                {
                    case CollectableItem.ItemType.Trash: return quests.IsQuestActive("trash-delivery");
                    case CollectableItem.ItemType.Box: return quests.IsQuestActive("box-delivery") || quests.IsQuestActive("box-collect");
                    case CollectableItem.ItemType.Grocery: return quests.IsQuestActive("dinner-ingredients");
                }
            }
            if (condition != null) return condition();
            if (explicitTags != null)
                foreach (string tag in explicitTags) if (quests.IsQuestActive(tag)) return true;
            return false;
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (found) return true;
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }

        private void Refresh(Camera camera, GameObject held)
        {
            float targetOpacity = 0f;
            if (IsRequired() && GetComponentInParent<PlayerInventory>() == null &&
                (held == null || (held != gameObject && !transform.IsChildOf(held.transform))) && TryGetBounds(out Bounds bounds))
            {
                Vector3 eye = camera.transform.position;
                distance = Vector3.Distance(eye, bounds.center);
                Vector3 viewport = camera.WorldToViewportPoint(bounds.center + Vector3.up * (bounds.extents.y + .07f));
                if (distance < VisibleDistance && viewport.z > camera.nearClipPlane &&
                    viewport.x > .025f && viewport.x < .975f && viewport.y > .025f && viewport.y < .975f)
                {
                    if (Time.unscaledTime >= nextVisibilityCheck)
                    {
                        nextVisibilityCheck = Time.unscaledTime + .12f;
                        Vector3 direction = bounds.center - eye;
                        unobstructed = !Physics.Raycast(eye, direction.normalized, out RaycastHit hit,
                            direction.magnitude, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore) ||
                            hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
                    }
                    if (unobstructed)
                    {
                        screenPoint = new Vector2(viewport.x * Screen.width, (1f - viewport.y) * Screen.height);
                        targetOpacity = Mathf.Clamp01((VisibleDistance - distance) / 1.5f);
                    }
                }
            }
            opacity = Mathf.MoveTowards(opacity, targetOpacity, Time.unscaledDeltaTime * 5f);
        }

        internal static void RefreshAll(Camera camera, GameObject held)
        {
            for (int i = 0; i < markers.Count; i++)
                if (markers[i] != null) markers[i].Refresh(camera, held);
        }

        internal static void DrawAll()
        {
            if (Event.current.type != EventType.Repaint || Time.timeScale == 0f || FadeTransitionPresentation.IsActive || ArrivalCutscene.IsPlaying ||
                RetroInventoryScreen.IsAnyOpen || QuestJournalScreen.IsAnyOpen || MinigameWindow.IsAnyOpen || !NarrativeDisplayQueue.CanShowInteractionHint) return;
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            float unit = Mathf.Clamp(Screen.height / 720f, .75f, 2.5f);
            float time = Time.unscaledTime;
            foreach (var marker in markers)
            {
                if (marker == null || marker.opacity < .02f) continue;
                float alpha = marker.opacity;
                // Вблизи метка чуть крупнее, издалека — компактнее, но не исчезающе мелкая.
                float near = Mathf.InverseLerp(marker.VisibleDistance, 1.5f, marker.distance);
                float size = unit * Mathf.Lerp(9f, 12f, near);
                float breath = .5f + .5f * Mathf.Sin(time * 2.6f + marker.phase);
                // Медленное покачивание: движущееся пятно глаз замечает раньше неподвижного.
                Vector2 point = marker.screenPoint + new Vector2(0f, Mathf.Sin(time * 2.1f + marker.phase) * 2.5f * unit - 6f * unit);

                // Штырёк вниз — показывает, к какому предмету относится метка.
                float stem = 7f * unit, stemWidth = Mathf.Max(2f, 1.6f * unit);
                GUI.color = new Color(Outline.r, Outline.g, Outline.b, alpha * .7f);
                GUI.DrawTexture(new Rect(point.x - stemWidth / 2f - 1f, point.y + size * .5f, stemWidth + 2f, stem + 1f), Texture2D.whiteTexture);
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, alpha * .85f);
                GUI.DrawTexture(new Rect(point.x - stemWidth / 2f, point.y + size * .5f, stemWidth, stem), Texture2D.whiteTexture);

                GUIUtility.RotateAroundPivot(45f, point);
                // Мягкий «дышащий» ореол.
                float halo = size * Mathf.Lerp(1.7f, 2.2f, breath);
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, alpha * Mathf.Lerp(.12f, .26f, breath));
                GUI.DrawTexture(Centered(point, halo), Texture2D.whiteTexture);
                // Тёмная обводка — читается и на светлой стене, и в темноте.
                GUI.color = new Color(Outline.r, Outline.g, Outline.b, alpha * .85f);
                GUI.DrawTexture(Centered(point, size + 3f * unit), Texture2D.whiteTexture);
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, alpha);
                GUI.DrawTexture(Centered(point, size), Texture2D.whiteTexture);
                GUI.color = new Color(Core.r, Core.g, Core.b, alpha * Mathf.Lerp(.75f, 1f, breath));
                GUI.DrawTexture(Centered(point, size * .42f), Texture2D.whiteTexture);
                GUI.matrix = previousMatrix;
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private static Rect Centered(Vector2 point, float size) =>
            new Rect(point.x - size / 2f, point.y - size / 2f, size, size);
    }

    internal sealed class QuestItemMarkerPresentation : MonoBehaviour
    {
        private Camera view;
        private PickupController pickup;
        private void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (view == null) return;
            if (pickup == null) pickup = FindFirstObjectByType<PickupController>();
            QuestItemMarker.RefreshAll(view, pickup != null ? pickup.GetHeldObject() : null);
        }
        private void OnGUI() => QuestItemMarker.DrawAll();
    }
}
