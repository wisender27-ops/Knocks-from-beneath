using UnityEngine;

namespace KnocksFromBeneath
{
    // The phone shares the player's hands with inventory tools and carried objects.
    public sealed class PhoneFlashlight : MonoBehaviour
    {
        public const string ModelResource = "Props/PhoneFlashlight";
        private static readonly string[] Introduction = { "Темно... Можно включить фонарик на телефоне." };
        private readonly PhoneFlashlightState state = new PhoneFlashlightState();
        private PlayerInventory inventory;
        private PickupController pickup;
        private PlayerController player;
        private Camera playerCamera;
        private GameObject phone;
        private Light beam;
        private UiKeyHints hint;
        private NeighborVisitSequence neighborVisit;
        private GlassOfWaterScene waterScene;
        private bool sceneBlackout;
        private float flickerRemaining;
        private bool introductionShown;
        private float hintReadyAt;

        public void Initialize(PlayerInventory playerInventory)
        {
            inventory = playerInventory;
            player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            pickup = player != null ? player.GetComponent<PickupController>() : null;
            playerCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
            if (inventory != null) inventory.SetPhoneFlashlight(this);
            BuildHint();
        }

        public void SetThreatScenes(NeighborVisitSequence neighbor, GlassOfWaterScene water)
        {
            neighborVisit = neighbor;
            waterScene = water;
        }

        private void OnEnable()
        {
            GameEvents.OnNightStarted += BeginNight;
            GameEvents.OnDayStarted += BeginDay;
        }

        private void OnDisable()
        {
            GameEvents.OnNightStarted -= BeginNight;
            GameEvents.OnDayStarted -= BeginDay;
            Hide();
            if (hint != null) hint.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.SetPhoneFlashlight(null);
            if (phone != null) Destroy(phone);
            if (beam != null) Destroy(beam.gameObject);
        }

        private void BeginNight()
        {
            state.BeginNight();
            hintReadyAt = Time.time + 2f;
        }

        private void BeginDay()
        {
            flickerRemaining = 0f;
            sceneBlackout = false;
            state.BeginDay();
            ApplyVisibility();
        }

        public void DrainBattery()
        {
            flickerRemaining = 0f;
            state.Drain();
            ApplyVisibility();
        }

        public void Hide()
        {
            flickerRemaining = 0f;
            state.Hide();
            ApplyVisibility();
        }

        private bool GameplayAvailable => player != null && player.isActiveAndEnabled &&
            !player.isCameraLocked && !PlayerController.CursorFree && Time.timeScale > 0f &&
            !PauseMenuController.InputBlocked && !RetroInventoryScreen.IsAnyOpen &&
            !QuestJournalScreen.IsAnyOpen && !MinigameWindow.IsAnyOpen &&
            !NarrativeReadingPanel.IsModal && !FadeTransitionPresentation.IsActive &&
            !PlayerInteraction.IsLockSequencePlaying;

        private void Update()
        {
            bool neighborThreat = neighborVisit != null && neighborVisit.IsPlaying && neighborVisit.IsSilhouetteVisible;
            bool waterThreat = waterScene != null && waterScene.MonsterInView;
            bool threat = neighborThreat || waterThreat;
            if (!threat)
            {
                if (sceneBlackout && flickerRemaining > 0f) Hide();
                sceneBlackout = false;
            }
            else if (!sceneBlackout && player != null &&
                ((neighborThreat &&
                  neighborVisit.frontDoor != null && IsNearNeighborDoor(player.transform.position, neighborVisit.frontDoor.transform.position)) ||
                 waterThreat))
            {
                sceneBlackout = true;
                if (state.IsOn) flickerRemaining = 0.7f;
            }

            if (flickerRemaining > 0f)
            {
                flickerRemaining = Mathf.Max(0f, flickerRemaining - Time.deltaTime);
                if (flickerRemaining <= 0f) Hide();
                else if (beam != null) beam.enabled = Mathf.FloorToInt(flickerRemaining * 12f) % 2 == 0;
            }
            bool handsBusy = pickup != null && pickup.GetHeldObject() != null;
            if (handsBusy && state.IsOn) Hide();
            bool available = state.Available && !sceneBlackout && GameplayAvailable && !handsBusy && playerCamera != null;
            if (hint != null && hint.gameObject.activeSelf != available) hint.gameObject.SetActive(available);
            if (!available) return;

            if (!introductionShown && Time.time >= hintReadyAt && ThoughtManager.Instance != null &&
                ThoughtManager.Instance.TryShowInteractionThoughts("phone-light-intro", Introduction))
                introductionShown = true;

            if (Input.GetKeyDown(KeyCode.F)) TogglePhone();
        }

        public static bool IsNearNeighborDoor(Vector3 position, Vector3 door)
        {
            float dx = position.x - door.x, dz = position.z - door.z;
            return position.y < 2.3f && dx * dx + dz * dz <= 3.5f * 3.5f;
        }

        private void TogglePhone()
        {
            if (state.IsOn) { Hide(); return; }
            if (!EnsureVisual()) return;
            inventory?.Unequip();
            state.Toggle();
            ApplyVisibility();
        }

        private bool EnsureVisual()
        {
            if (phone != null && beam != null) return true;
            GameObject prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null || playerCamera == null)
            {
                Debug.LogError("Phone flashlight model is missing at Resources/" + ModelResource);
                return false;
            }
            phone = Instantiate(prefab, playerCamera.transform, false);
            phone.name = "Phone flashlight (held)";
            phone.transform.localPosition = new Vector3(.22f, -.18f, .42f);
            phone.transform.localRotation = Quaternion.identity;
            var lightObject = new GameObject("Phone flashlight beam");
            lightObject.transform.SetParent(phone.transform, false);
            lightObject.transform.localPosition = new Vector3(.02f, .06f, .03f);
            Vector3 aimPoint = playerCamera.transform.position + playerCamera.transform.forward * 3f;
            lightObject.transform.rotation = Quaternion.LookRotation(aimPoint - lightObject.transform.position, playerCamera.transform.up);
            beam = lightObject.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 4f;
            beam.spotAngle = 58f;
            beam.intensity = 4f;
            beam.color = new Color(1f, .94f, .82f);
            beam.shadows = LightShadows.None;
            ApplyVisibility();
            return true;
        }

        private void ApplyVisibility()
        {
            if (phone != null) phone.SetActive(state.IsOn);
            if (beam != null) beam.enabled = state.IsOn;
        }

        private void BuildHint()
        {
            Canvas canvas = UiStyle.OverlayCanvas(transform, "PhoneLightHintCanvas", 400, new Vector2(1280f, 720f), false);
            hint = UiKeyHints.Create(canvas.transform, "PhoneLightHint", UiStyle.Font(), .85f,
                new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, .8f));
            hint.RectTransform.anchorMin = hint.RectTransform.anchorMax = hint.RectTransform.pivot = new Vector2(0f, 0f);
            hint.RectTransform.anchoredPosition = new Vector2(30f, 65f);
            hint.Set("F", "ФОНАРИК ТЕЛЕФОНА");
            hint.gameObject.SetActive(false);
        }
    }

    public sealed class PhoneFlashlightState
    {
        private bool night;
        private bool drained;
        public bool IsOn { get; private set; }
        public bool Available => night && !drained;

        public void BeginNight() { night = true; }
        public void BeginDay() { night = false; drained = false; IsOn = false; }
        public void Drain() { drained = true; IsOn = false; }
        public void Hide() { IsOn = false; }
        public void Toggle() { if (Available) IsOn = !IsOn; }
    }
}
