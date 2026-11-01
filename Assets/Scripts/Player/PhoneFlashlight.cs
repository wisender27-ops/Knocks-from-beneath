using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // The phone shares the player's hands with inventory tools and carried objects.
    public sealed class PhoneFlashlight : MonoBehaviour
    {
        public const string ModelResource = "Props/PhoneFlashlight";
        public const string DeadDescription = "Мой телефон. Батарея села — экран больше не включается.";
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
        private float flickerRemaining;
        private bool introductionShown, usedOnce;
        private float hintReadyAt;
        // Первая подсказка крупная и пульсирует по центру, пока игрок не включит фонарик сам.
        private UiKeyHints firstHint;
        private CanvasGroup firstHintGroup;
        private const float BlackoutFlicker = 0.7f;
        private const float RefusedFlicker = 0.35f;

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
            if (firstHint != null) firstHint.gameObject.SetActive(false);
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

        public bool IsDead => !state.Available;
        public Light Beam => beam;

        public bool CanShowControlHint => isActiveAndEnabled && !IsDead && GameplayAvailable && playerCamera != null &&
            (pickup == null || pickup.GetHeldObject() == null);

        private void Update()
        {
            // Одно правило на обе сцены: пока оно рядом, телефон мигает, гаснет и не включается.
            bool neighborThreat = neighborVisit != null && neighborVisit.IsPlaying && neighborVisit.IsSilhouetteVisible &&
                player != null && neighborVisit.frontDoor != null &&
                IsNearNeighborDoor(player.transform.position, neighborVisit.frontDoor.transform.position);
            bool waterThreat = waterScene != null && waterScene.MonsterInView;
            bool wasBlackout = state.Blackout;
            if (state.UpdateThreat(neighborThreat || waterThreat))
            {
                if (state.IsOn) flickerRemaining = BlackoutFlicker;
            }
            else if (wasBlackout && !state.Blackout && flickerRemaining > 0f) Hide();

            if (flickerRemaining > 0f)
            {
                flickerRemaining = Mathf.Max(0f, flickerRemaining - Time.deltaTime);
                if (flickerRemaining <= 0f) Hide();
                else if (beam != null) beam.enabled = Mathf.FloorToInt(flickerRemaining * 12f) % 2 == 0;
            }
            bool handsBusy = pickup != null && pickup.GetHeldObject() != null;
            if (handsBusy && state.IsOn) Hide();
            bool available = !IsDead && GameplayAvailable && !handsBusy && playerCamera != null;
            UpdateFirstHint(available);
            if (!available) return;

            if (state.IsNight && !introductionShown && Time.time >= hintReadyAt && ThoughtManager.Instance != null &&
                ThoughtManager.Instance.TryShowInteractionThoughts("phone-light-intro", Introduction))
                introductionShown = true;

            if (Input.GetKeyDown(KeyCode.F)) TryUse();
        }

        private void UpdateFirstHint(bool available)
        {
            bool show = available && introductionShown && !usedOnce && state.IsNight;
            if (firstHint == null)
            {
                if (!show) return;
                BuildFirstHint();
            }
            if (firstHint.gameObject.activeSelf != show) firstHint.gameObject.SetActive(show);
            if (!show) return;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4.5f);
            firstHintGroup.alpha = Mathf.Lerp(.7f, 1f, pulse);
            firstHint.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.07f, pulse);
        }

        private void BuildFirstHint()
        {
            Canvas canvas = UiStyle.OverlayCanvas(transform, "PhoneFlashlightHintCanvas", 400, new Vector2(1280f, 720f), false);
            TMP_FontAsset font = UiStyle.Font();
            firstHint = UiKeyHints.Create(canvas.transform, "PhoneFlashlightHint", font, 1.7f, UiStyle.Amber);
            firstHint.RectTransform.anchorMin = firstHint.RectTransform.anchorMax = firstHint.RectTransform.pivot = new Vector2(.5f, .5f);
            firstHint.RectTransform.anchoredPosition = new Vector2(0f, -120f);
            firstHint.Set("F", "ВКЛЮЧИТЬ ФОНАРИК НА ТЕЛЕФОНЕ");
            firstHintGroup = firstHint.gameObject.AddComponent<CanvasGroup>();
            firstHint.gameObject.SetActive(false);
        }

        public static bool IsNearNeighborDoor(Vector3 position, Vector3 door)
        {
            float dx = position.x - door.x, dz = position.z - door.z;
            return position.y < 2.3f && dx * dx + dz * dz <= 3.5f * 3.5f;
        }

        public bool TryUse()
        {
            if (!isActiveAndEnabled || IsDead) return false;
            if (state.IsOn) { Hide(); return true; }
            if (playerCamera == null ||
                (pickup != null && pickup.GetHeldObject() != null)) return false;
            if (!EnsureVisual()) return false;
            usedOnce = true;
            inventory?.Unequip();
            state.Toggle();
            ApplyVisibility();
            // Рядом с ним телефон только вспыхивает и снова гаснет.
            if (state.Blackout)
            {
                flickerRemaining = RefusedFlicker;
                return false;
            }
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
        // Сюжетная разрядка необратима: дальше телефон только предмет в инвентаре.
        public bool Available { get; private set; } = true;
        public bool Blackout { get; private set; }

        // true — угроза только что появилась: включённый свет должен мигнуть и погаснуть.
        public bool UpdateThreat(bool threat)
        {
            bool began = threat && !Blackout;
            Blackout = threat;
            return began;
        }

        public void BeginNight() { IsNight = true; }
        public void BeginDay() { IsNight = false; }
        public void Drain() { IsOn = false; Available = false; }
        public void Hide() { IsOn = false; }
        public void Toggle() { if (Available) IsOn = !IsOn; }
    }
}
