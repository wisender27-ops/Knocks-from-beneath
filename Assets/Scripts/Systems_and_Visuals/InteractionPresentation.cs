using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    [DefaultExecutionOrder(30000)]
    public sealed class InteractionPresentation : MonoBehaviour
    {
        public static bool HasControlHints { get; private set; }
        private CrosshairJuice source;
        private PlayerInteraction actor;
        private PickupController pickup;
        private PlayerController player;
        private PlayerDoorOpen doors;
        private Canvas canvas;
        private RectTransform root, marker, controls;
        private CanvasGroup markerGroup, controlsGroup, dotGroup;
        private InteractionHandGraphic openHand, closedHand;
        private InteractionRingGraphic ring;
        private TextMeshProUGUI context;
        private readonly RectTransform[] chips = new RectTransform[5];
        private readonly TextMeshProUGUI[] keyLabels = new TextMeshProUGUI[5], actionLabels = new TextMeshProUGUI[5];
        private readonly RaycastHit[] occlusion = new RaycastHit[12];
        private Transform anchor;
        private Renderer anchorVisual;
        private Vector3 localPoint, lastPoint;
        private Vector2 screenVelocity;
        private GameObject lastHeld;
        private Door lastDoor;
        private bool lastWorking, legacyCursorEnabled, legacyHintEnabled, rejected;
        private float alpha, closure, punch, feedbackUntil, nextOcclusionCheck, grabbedAt;
        // Сколько секунд значок «сжатой руки» виден на предмете после захвата. Дальше он
        // гаснет: пока предмет в руках, значок только дублирует то, что и так видно.
        private const float HeldMarkerSeconds = .35f;
        private bool unobstructed = true;
        private int controlCount;
        private string controlSignature;
        private static readonly Color Ivory = new Color32(239, 234, 220, 255);
        private static readonly Color Active = new Color32(225, 193, 139, 255);
        private static readonly Color Unavailable = new Color32(205, 150, 126, 255);
        public Transform AnchoredTarget => anchor;
        public Vector3 AnchoredPoint => anchor != null ? anchor.TransformPoint(localPoint) : lastPoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => HasControlHints = false;

        public void Initialize(CrosshairJuice owner)
        {
            if (canvas != null) return;
            source = owner;
            actor = owner.interaction;
            if (actor == null) return;
            pickup = actor.pickup;
            player = actor.GetComponent<PlayerController>();
            doors = actor.GetComponentInChildren<PlayerDoorOpen>();
            legacyCursorEnabled = source.cursorImage != null && source.cursorImage.enabled;
            legacyHintEnabled = source.hintText != null && source.hintText.enabled;
            if (source.cursorImage != null) source.cursorImage.enabled = false;
            if (source.hintText != null) source.hintText.enabled = false;
            BuildInterface(source.hintText != null ? source.hintText.font : null);
            actor.InteractionAttempted += OnAttempt;
        }

        private void OnAttempt(InteractionFocus focus)
        {
            if (!focus.IsValid) return;
            Attach(focus.Target, focus.Point, true);
            rejected = focus.Blocked;
            feedbackUntil = Time.unscaledTime + .28f;
            punch = rejected ? -.12f : .15f;
        }

        private void Attach(Transform target, Vector3 point, bool force = false)
        {
            if (target == null) return;
            if (anchor != target || force)
            {
                anchor = target;
                anchorVisual = target.GetComponentInChildren<Renderer>();
                localPoint = target.InverseTransformPoint(point);
                screenVelocity = Vector2.zero;
                nextOcclusionCheck = 0f;
                punch = .1f;
            }
            lastPoint = point;
        }

        private void LateUpdate()
        {
            if (canvas == null || actor == null || actor.playerCamera == null) return;
            bool hidden = source == null || !source.isActiveAndEnabled || Time.timeScale <= 0f ||
                RetroInventoryScreen.IsAnyOpen || QuestJournalScreen.IsAnyOpen ||
                NarrativeReadingPanel.IsModal || FadeTransitionPresentation.IsActive || CornerPeekSequence.IsPlaying ||
                !actor.isActiveAndEnabled;
            if (hidden)
            {
                markerGroup.alpha = controlsGroup.alpha = dotGroup.alpha = 0f;
                HasControlHints = false;
                return;
            }
            GameObject held = pickup != null ? pickup.GetHeldObject() : null;
            Door heldDoor = doors != null ? doors.HeldDoor : null;
            bool working = actor.HasActiveAction && (held == null || actor.IsEatingPie);
            bool cameraLocked = player != null && player.isCameraLocked;
            if (cameraLocked && heldDoor == null && !working)
            {
                markerGroup.alpha = controlsGroup.alpha = dotGroup.alpha = 0f;
                HasControlHints = false;
                return;
            }
            bool engaged = held != null || heldDoor != null || working;
            if (held != lastHeld || heldDoor != lastDoor || working != lastWorking)
            {
                if (held != null && held != lastHeld) grabbedAt = Time.unscaledTime;
                punch = engaged ? .16f : .2f;
                rejected = false;
                if (!engaged && (lastHeld != null || lastDoor != null || lastWorking))
                    feedbackUntil = Time.unscaledTime + .22f;
            }
            if (heldDoor != null)
                Attach(heldDoor.transform, doors.HeldContactPoint, heldDoor != lastDoor);
            else if (working && actor.ActionTarget != null)
                Attach(actor.ActionTarget, actor.ActionPoint, working != lastWorking);
            else if (held != null)
            {
                if (anchor == null || !(anchor == held.transform || anchor.IsChildOf(held.transform)))
                    Attach(held.transform, pickup.HeldObjectCenter);
            }
            else if (Time.unscaledTime >= feedbackUntil)
            {
                var focus = source.CurrentFocus;
                if (focus.IsValid)
                {
                    Attach(focus.Target, focus.Point);
                    rejected = focus.Blocked;
                }
                else anchor = null;
            }
            lastHeld = held;
            lastDoor = heldDoor;
            lastWorking = working;
            bool hasTarget = anchor != null || Time.unscaledTime < feedbackUntil;
            Vector3 point = anchor != null ? anchor.TransformPoint(localPoint) : lastPoint;
            lastPoint = point;
            Vector3 viewport = actor.playerCamera.WorldToViewportPoint(point);
            bool onScreen = hasTarget && viewport.z > actor.playerCamera.nearClipPlane &&
                viewport.x > .015f && viewport.x < .985f && viewport.y > .015f && viewport.y < .985f;
            if (!onScreen && held != null && anchorVisual != null)
            {
                viewport = actor.playerCamera.WorldToViewportPoint(anchorVisual.bounds.center);
                onScreen = viewport.z > actor.playerCamera.nearClipPlane && viewport.x > .015f && viewport.x < .985f &&
                    viewport.y > .015f && viewport.y < .985f;
            }
            if (onScreen && held == null && Time.unscaledTime >= nextOcclusionCheck)
            {
                nextOcclusionCheck = Time.unscaledTime + .08f;
                unobstructed = Visible(point);
            }
            bool visible = onScreen && (held != null ? Time.unscaledTime - grabbedAt < HeldMarkerSeconds : unobstructed);
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? 12f : 10f));
            markerGroup.alpha = alpha;
            if (onScreen)
            {
                Vector2 screen = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
                if (engaged) screen.y = Mathf.Max(screen.y, 90f * Screen.height / 720f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
                marker.anchoredPosition = alpha < .08f ? local :
                    Vector2.SmoothDamp(marker.anchoredPosition, local, ref screenVelocity, .035f, Mathf.Infinity, Time.unscaledDeltaTime);
            }
            closure = Mathf.MoveTowards(closure, engaged || Time.unscaledTime < feedbackUntil ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            openHand.canvasRenderer.SetAlpha(1f - closure);
            closedHand.canvasRenderer.SetAlpha(closure);
            Color tint = rejected ? Unavailable : engaged ? Active : Ivory;
            if (openHand.color != tint) { openHand.color = tint; closedHand.color = tint; }
            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 1.5f);
            marker.localScale = Vector3.one * (Mathf.Lerp(.86f, 1f, alpha) + punch);
            float progress = working ? actor.ActionProgress : -1f;
            ring.gameObject.SetActive(working || Time.unscaledTime < feedbackUntil);
            ring.color = new Color(tint.r, tint.g, tint.b, working ? .72f : .5f);
            ring.Progress = progress >= 0f ? progress : working ? .22f : 1f;
            ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, working && progress < 0f ? -Time.unscaledTime * 180f : 0f);
            UpdateControls(held, heldDoor, working);
            bool showControls = controlCount > 0 && NarrativeDisplayQueue.CanShowInteractionHint;
            controlsGroup.alpha = Mathf.MoveTowards(controlsGroup.alpha, showControls ? 1f : 0f, Time.unscaledDeltaTime * 10f);
            HasControlHints = showControls;
            dotGroup.alpha = Mathf.MoveTowards(dotGroup.alpha, !hasTarget && !engaged ? .65f : 0f, Time.unscaledDeltaTime * 12f);
        }

        private bool Visible(Vector3 point)
        {
            Vector3 origin = actor.playerCamera.transform.position;
            Vector3 delta = point - origin;
            float distance = delta.magnitude;
            if (distance < .02f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, occlusion, distance - .015f,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = occlusion[i].collider.transform;
                if (hit.IsChildOf(actor.transform) || (anchor != null && (hit.IsChildOf(anchor) || anchor.IsChildOf(hit)))) continue;
                return false;
            }
            return true;
        }

        private void UpdateControls(GameObject held, Door heldDoor, bool working)
        {
            string hint = source.CurrentHint ?? "";
            bool manipulate = held != null && pickup.CanManipulateHeldObject;
            var focus = source.CurrentFocus;
            string signature = (held != null ? "carry" : heldDoor != null ? "door" : working ? "work" : "hover") +
                "|" + hint + "|" + manipulate + "|" + focus.IsDoor + "|" + focus.Blocked + "|" + (held != null && pickup.IsCarryObstructed);
            if (controlSignature == signature) return;
            controlSignature = signature;
            controlCount = 0;
            context.text = "";
            if (heldDoor != null)
            {
                Control("МЫШЬ", "ТЯНУТЬ ДВЕРЬ");
                Control("E", "ОТПУСТИТЬ КЛАВИШУ");
            }
            else if (working)
                Control("", actor.IsEatingPie ? "ЕСТЬ..." : "ВЫПОЛНЯЕТСЯ...");
            else if (held != null)
            {
                bool specific = hint.StartsWith("E ") || hint.StartsWith("Нажмите E");
                Control("E", specific ? CleanHint(hint) : "ОПУСТИТЬ");
                if (manipulate)
                {
                    Control("R / ПКМ", "ВРАЩАТЬ");
                    Control("КОЛЕСО", "БЛИЖЕ / ДАЛЬШЕ");
                    Control("СКМ", "СБРОС ВРАЩЕНИЯ");
                }
                Control("ЛКМ", "БРОСИТЬ");
                if (!specific && !string.IsNullOrEmpty(hint)) context.text = hint;
                if (pickup.IsCarryObstructed) context.text = "Нет места для предмета — отойдите от препятствия";
            }
            else if (focus.IsValid)
            {
                Control(focus.Blocked && !focus.IsDoor ? "" : "E", focus.Prompt);
                if (focus.IsDoor && !focus.Blocked) Control("E + МЫШЬ", "УДЕРЖИВАТЬ И ТЯНУТЬ");
            }
            float total = 0f;
            for (int i = 0; i < controlCount; i++) total += chips[i].sizeDelta.x + (i == 0 ? 0f : 20f);
            float x = -total * .5f;
            float scale = Mathf.Min(1f, 1140f / Mathf.Max(1f, total));
            controls.localScale = Vector3.one * scale;
            for (int i = 0; i < chips.Length; i++)
            {
                chips[i].gameObject.SetActive(i < controlCount);
                if (i >= controlCount) continue;
                chips[i].anchoredPosition = new Vector2(x, 0f);
                x += chips[i].sizeDelta.x + 20f;
            }
        }

        private static string CleanHint(string hint)
        {
            if (hint.StartsWith("E — ")) return hint.Substring(4);
            if (hint.StartsWith("Нажмите E, чтобы ")) return hint.Substring(16);
            return hint;
        }

        private void Control(string key, string label)
        {
            if (controlCount >= chips.Length) return;
            int i = controlCount++;
            keyLabels[i].text = key;
            actionLabels[i].text = label;
            float keyWidth = string.IsNullOrEmpty(key) ? 0f : Mathf.Max(32f, keyLabels[i].GetPreferredValues(key).x + 18f);
            var keyRect = keyLabels[i].transform.parent as RectTransform;
            keyRect.gameObject.SetActive(keyWidth > 0f);
            keyRect.sizeDelta = new Vector2(keyWidth, 32f);
            keyLabels[i].rectTransform.sizeDelta = new Vector2(keyWidth, 32f);
            float labelWidth = actionLabels[i].GetPreferredValues(label).x + 2f;
            actionLabels[i].rectTransform.anchoredPosition = new Vector2(keyWidth + (keyWidth > 0f ? 10f : 0f), 0f);
            actionLabels[i].rectTransform.sizeDelta = new Vector2(labelWidth, 34f);
            chips[i].sizeDelta = new Vector2(keyWidth + labelWidth + (keyWidth > 0f ? 10f : 0f), 36f);
        }

        private void OnDestroy()
        {
            if (actor != null) actor.InteractionAttempted -= OnAttempt;
            if (source != null)
            {
                if (source.cursorImage != null) source.cursorImage.enabled = legacyCursorEnabled;
                if (source.hintText != null) source.hintText.enabled = legacyHintEnabled;
            }
            HasControlHints = false;
        }

        private void OnDisable()
        {
            HasControlHints = false;
            if (markerGroup != null) markerGroup.alpha = controlsGroup.alpha = dotGroup.alpha = 0f;
        }

        private void BuildInterface(TMP_FontAsset font)
        {
            var g = new GameObject("InteractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            g.transform.SetParent(transform, false);
            canvas = g.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = g.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            root = g.GetComponent<RectTransform>();
            marker = Rect("WorldHand", root, Vector2.one * .5f, new Vector2(50, 50));
            markerGroup = marker.gameObject.AddComponent<CanvasGroup>(); markerGroup.alpha = 0f;
            var open = Rect("OpenHand", marker, Vector2.one * .5f, new Vector2(44, 44));
            openHand = open.gameObject.AddComponent<InteractionHandGraphic>();
            openHand.raycastTarget = false; openHand.color = Ivory;
            var closed = Rect("ClosedHand", marker, Vector2.one * .5f, new Vector2(44, 44));
            closedHand = closed.gameObject.AddComponent<InteractionHandGraphic>();
            closedHand.Closed = true; closedHand.raycastTarget = false; closedHand.color = Ivory;
            ring = Rect("ActionProgress", marker, Vector2.one * .5f, new Vector2(54, 54)).gameObject.AddComponent<InteractionRingGraphic>();
            ring.raycastTarget = false;
            var dot = Rect("IdleAim", root, Vector2.one * .5f, new Vector2(4, 4));
            dot.gameObject.AddComponent<Image>().color = Ivory;
            dotGroup = dot.gameObject.AddComponent<CanvasGroup>(); dotGroup.alpha = .65f;
            controls = Rect("Controls", root, new Vector2(.5f, 0), new Vector2(1140, 36));
            controls.anchoredPosition = new Vector2(0, 36);
            controlsGroup = controls.gameObject.AddComponent<CanvasGroup>(); controlsGroup.alpha = 0f;
            for (int i = 0; i < chips.Length; i++)
            {
                chips[i] = Rect("Control" + i, controls, Vector2.one * .5f, new Vector2(120, 36));
                chips[i].pivot = new Vector2(0, .5f);
                var key = Rect("Key", chips[i], new Vector2(0, .5f), new Vector2(34, 32));
                key.pivot = new Vector2(0, .5f);
                key.gameObject.AddComponent<Image>().color = Ivory;
                keyLabels[i] = Text("KeyLabel", key, font, 15f, new Color32(35, 31, 26, 255));
                keyLabels[i].rectTransform.pivot = new Vector2(0, .5f);
                keyLabels[i].rectTransform.anchorMin = keyLabels[i].rectTransform.anchorMax = new Vector2(0, .5f);
                keyLabels[i].alignment = TextAlignmentOptions.Center;
                actionLabels[i] = Text("Action", chips[i], font, 18f, Ivory);
                actionLabels[i].rectTransform.pivot = new Vector2(0, .5f);
                actionLabels[i].rectTransform.anchorMin = actionLabels[i].rectTransform.anchorMax = new Vector2(0, .5f);
                actionLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
                chips[i].gameObject.SetActive(false);
            }
            context = Text("Context", controls, font, 17f, new Color32(190, 181, 162, 255));
            context.rectTransform.anchorMin = context.rectTransform.anchorMax = Vector2.one * .5f;
            context.rectTransform.anchoredPosition = new Vector2(0, 46);
            context.rectTransform.sizeDelta = new Vector2(1000, 30);
            context.alignment = TextAlignmentOptions.Center;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var rect = Rect(name, parent, Vector2.one * .5f, new Vector2(120, 36));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.richText = false;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.text = "";
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.sizeDelta = size;
            return r;
        }
    }
}
