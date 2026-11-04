using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Отдельный объект: решётка вентиляции на четырёх болтах. Камера встаёт напротив, виден курсор; держи ЛКМ над болтом —
    // отвёртка встаёт в шлиц и выкручивает его, болт падает на пол. Без всех четырёх решётку не снять. Прогресс при выходе сохраняется.
    public sealed class VentHatch : MinigameWindow
    {
        private const float ViewDistance = 1.15f, ViewFov = 58f, MoveSeconds = 0.45f;
        private const float PickRadius = 62f;    // пикселей при высоте экрана 720
        private const float BoltTravel = 0.035f; // на столько болт выходит из рамы
        private const float BoltTurns = 7f, DriverTurns = 7f;
        private const float DriverAngle = 24f;   // рука чуть сбоку, чтобы инструмент не закрывал болт
        private static readonly Vector3 LandPoint = new Vector3(-41.75f, BasementLayout.FloorY + 0.03f, 116.1f);

        [SerializeField] private Transform[] bolts = new Transform[VentBolts.Count];
        [SerializeField] private Transform driver;
        [SerializeField] private Transform driverTip;
        [SerializeField] private float floorY = BasementLayout.FloorY;
        [SerializeField] private AudioSource turnSource;
        [SerializeField] private AudioSource impactSource;
        [SerializeField] private AudioClip seatClip;
        [SerializeField] private AudioClip[] dropClips = new AudioClip[0];
        [SerializeField] private AudioClip hatchOffClip;

        private readonly VentBolts state = new VentBolts();
        private readonly Vector3[] boltRest = new Vector3[VentBolts.Count];
        private readonly Quaternion[] boltRestRotation = new Quaternion[VentBolts.Count];
        private Action opened;
        private Camera view;
        private Transform eye;
        private Vector3 eyePosition;
        private Quaternion eyeRotation;
        private float eyeFov;
        private Behaviour[] paused;
        private bool[] pausedStates;
        private PlayerInventory inventory;
        private string equippedBefore;
        private int hovered = -1, lastDrop = -1;
        private bool turning, restored;
        private Vector3 driverAt;
        private Quaternion driverRotation;
        private float driverSpin, viewBlend;

        public bool IsUnbolted => state.Finished;
        protected override string WindowName => "Vent Hatch";

        private void Awake()
        {
            for (int i = 0; i < VentBolts.Count; i++)
            {
                boltRest[i] = bolts[i].localPosition;
                boltRestRotation[i] = bolts[i].localRotation;
            }
            if (driver != null) driver.gameObject.SetActive(false);
        }

        public void Configure(Action onOpened) => opened = onOpened;

        public void Begin()
        {
            if (state.Finished || !TryOpen()) return;
            PlayerController player = FindFirstObjectByType<PlayerController>();
            view = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (view == null) { CloseWindow(); return; }
            eye = view.transform;
            eyePosition = eye.position;
            eyeRotation = eye.rotation;
            eyeFov = view.fieldOfView;
            player.StopMotion();
            inventory = player.GetComponent<PlayerInventory>();
            equippedBefore = inventory != null ? inventory.EquippedItemId : string.Empty;
            if (inventory != null) inventory.ActivateItem(string.Empty);
            paused = new Behaviour[] { inventory, player.GetComponentInChildren<PlayerDoorOpen>(), player.GetComponentInChildren<PickupController>(),
                player.GetComponentInChildren<HeadBobbing>(), player.GetComponent<PlayerFloorKnock>(), player };
            pausedStates = new bool[paused.Length];
            for (int i = 0; i < paused.Length; i++)
            {
                pausedStates[i] = paused[i] != null && paused[i].enabled;
                if (paused[i] != null) paused[i].enabled = false;
            }
            restored = false;
            viewBlend = 0f;
            Cursor.visible = true;
            driverAt = HomePoint();
            driverRotation = HomeRotation();
            driver.gameObject.SetActive(true);
            Root.transform.Find("Shade").gameObject.SetActive(false);
        }

        protected override void Build(Transform root)
        {
            TMPro.TextMeshProUGUI hint = Label("Hint", -300f, 26f);
            hint.text = Loc.T("Удерживай ЛКМ над болтом");
            var keys = KeyHints(hint.font, -335f);
            keys.Set("Esc", Loc.T("отойти"));
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            viewBlend = Mathf.MoveTowards(viewBlend, 1f, dt / MoveSeconds);
            ApplyView();
            if (CancelPressed) { Cancel(); return; }

            hovered = FindHovered();
            bool holding = hovered >= 0 && Input.GetMouseButton(0) && viewBlend >= 1f;
            bool seated = holding && DriverSeated(hovered);
            MoveDriver(dt, holding);
            SetTurning(seated);
            if (seated)
            {
                driverSpin += 360f * DriverTurns / VentBolts.HoldSeconds * dt;
                if (state.Hold(hovered, dt)) { StartCoroutine(BoltDrops(hovered)); if (state.Finished) StartCoroutine(TakeOff()); }
                else ApplyBolt(hovered);
            }
        }

        private void ApplyView()
        {
            Vector3 face = transform.TransformPoint(BoltCenter());
            Vector3 stand = face - transform.right * ViewDistance;
            float k = Mathf.SmoothStep(0f, 1f, viewBlend);
            eye.position = Vector3.Lerp(eyePosition, stand, k);
            eye.rotation = Quaternion.Slerp(eyeRotation, Quaternion.LookRotation(face - stand), k);
            view.fieldOfView = Mathf.Lerp(eyeFov, ViewFov, k);
        }

        private Vector3 BoltCenter()
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 point in boltRest) sum += point;
            return sum / VentBolts.Count;
        }

        private int FindHovered()
        {
            float best = PickRadius * Screen.height / 720f;
            int result = -1;
            Vector2 mouse = Input.mousePosition;
            for (int i = 0; i < VentBolts.Count; i++)
            {
                if (state.IsRemoved(i)) continue;
                Vector2 screen = view.WorldToScreenPoint(bolts[i].position);
                float distance = Vector2.Distance(mouse, screen);
                if (distance >= best) continue;
                best = distance;
                result = i;
            }
            return result;
        }

        private bool DriverSeated(int bolt) => (driverAt - SeatPoint(bolt)).sqrMagnitude < 0.0001f;

        private Vector3 SeatPoint(int bolt) => bolts[bolt].position;

        private Vector3 HomePoint() => transform.TransformPoint(BoltCenter()) + transform.right * -0.18f + Vector3.down * 0.3f;

        private Quaternion HomeRotation() => Quaternion.LookRotation(transform.right, Vector3.up) * Quaternion.Euler(-DriverAngle, DriverAngle * 0.5f, 0f);

        private void MoveDriver(float dt, bool holding)
        {
            Vector3 target = holding ? SeatPoint(hovered) : hovered >= 0 ? SeatPoint(hovered) - transform.right * 0.03f : HomePoint();
            driverAt = Vector3.MoveTowards(driverAt, target, 1.2f * dt);
            driverAt = Vector3.Lerp(driverAt, target, 1f - Mathf.Exp(-16f * dt));
            Quaternion aim = HomeRotation();
            driverRotation = Quaternion.Slerp(driverRotation, aim, 1f - Mathf.Exp(-14f * dt));
            driver.rotation = driverRotation * Quaternion.AngleAxis(driverSpin, Vector3.forward);
            driver.position = driverAt + (driver.position - driverTip.position);
        }

        private void ApplyBolt(int bolt)
        {
            float k = state.Progress(bolt);
            bolts[bolt].localRotation = boltRestRotation[bolt] * Quaternion.AngleAxis(360f * BoltTurns * k, Vector3.forward);
            bolts[bolt].position = transform.TransformPoint(boltRest[bolt]) + transform.right * (-BoltTravel * k);
        }

        private void SetTurning(bool value)
        {
            if (value == turning) return;
            turning = value;
            if (value)
            {
                impactSource.PlayOneShot(seatClip, 0.7f);
                turnSource.Play();
            }
            else turnSource.Pause();
        }

        private IEnumerator BoltDrops(int index)
        {
            Transform bolt = bolts[index];
            SetTurning(false);
            bolt.SetParent(null, true);
            Vector3 velocity = -transform.right * 0.5f + Vector3.up * 0.4f;
            Vector3 spin = new Vector3(620f, 340f, 150f);
            while (bolt.position.y > floorY + 0.012f)
            {
                velocity += Physics.gravity * Time.deltaTime;
                bolt.position += velocity * Time.deltaTime;
                bolt.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            impactSource.transform.position = bolt.position;
            impactSource.PlayOneShot(GameSounds.Pick(dropClips, ref lastDrop), 0.8f);
            bolt.position = new Vector3(bolt.position.x, floorY + 0.012f, bolt.position.z);
            bolt.rotation = Quaternion.Euler(90f, bolt.eulerAngles.y, 0f);
        }

        private IEnumerator TakeOff()
        {
            yield return new WaitForSeconds(0.45f);
            // Решётка без болтов отходит от рамы и падает на пол плашмя.
            Transform grille = transform;
            Vector3 from = grille.position, loose = from - grille.right * 0.07f;
            Quaternion rest = grille.rotation, flat = Quaternion.Euler(0f, 12f, 90f);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                grille.position = Vector3.Lerp(from, loose, t / 0.22f);
                ApplyView();
                yield return null;
            }
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                grille.position = Vector3.Lerp(loose, LandPoint, k * k);
                grille.rotation = Quaternion.Slerp(rest, flat, k * k);
                yield return null;
            }
            grille.SetPositionAndRotation(LandPoint, flat);
            impactSource.transform.position = LandPoint;
            impactSource.PlayOneShot(hatchOffClip, 1f);
            yield return new WaitForSeconds(0.35f);
            CloseWindow();
            Restore();
            opened?.Invoke();
        }

        protected override void Cancel()
        {
            CloseWindow();
            Restore();
        }

        private void Restore()
        {
            if (restored || paused == null) return;
            restored = true;
            SetTurning(false);
            driver.gameObject.SetActive(false);
            eye.position = eyePosition;
            eye.rotation = eyeRotation;
            view.fieldOfView = eyeFov;
            for (int i = 0; i < paused.Length; i++) if (paused[i] != null) paused[i].enabled = pausedStates[i];
            if (inventory != null) inventory.ActivateItem(equippedBefore);
        }
    }
}
