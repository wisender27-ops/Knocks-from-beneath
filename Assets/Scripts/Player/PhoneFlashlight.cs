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

        public bool CanShowControlHint => isActiveAndEnabled && GameplayAvailable && playerCamera != null &&
            (pickup == null || pickup.GetHeldObject() == null);

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
            bool available = GameplayAvailable && !handsBusy && playerCamera != null;
            if (!available) return;

            if (state.IsNight && !introductionShown && Time.time >= hintReadyAt && ThoughtManager.Instance != null &&
                ThoughtManager.Instance.TryShowInteractionThoughts("phone-light-intro", Introduction))
                introductionShown = true;

            if (Input.GetKeyDown(KeyCode.F)) TryUse();
        }

        public static bool IsNearNeighborDoor(Vector3 position, Vector3 door)
        {
            float dx = position.x - door.x, dz = position.z - door.z;
            return position.y < 2.3f && dx * dx + dz * dz <= 3.5f * 3.5f;
        }

        public bool TryUse()
        {
            if (!isActiveAndEnabled) return false;
            if (state.IsOn) { Hide(); return true; }
            if (playerCamera == null ||
                (pickup != null && pickup.GetHeldObject() != null)) return false;
            if (!EnsureVisual()) return false;
            inventory?.Unequip();
            state.Toggle();
            ApplyVisibility();
            return state.IsOn;
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

    }

    public sealed class PhoneFlashlightState
    {
        public bool IsNight { get; private set; }
        public bool IsOn { get; private set; }
        public bool Available => true;

        public void BeginNight() { IsNight = true; }
        public void BeginDay() { IsNight = false; }
        public void Drain() { IsOn = false; }
        public void Hide() { IsOn = false; }
        public void Toggle() { IsOn = !IsOn; }
    }
}
