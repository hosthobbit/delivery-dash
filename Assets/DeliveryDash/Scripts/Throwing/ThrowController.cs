using DeliveryDash.Core;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Aiming and throwing. Hold to aim and charge (the longer you hold, the further you can reach);
    /// release to throw. With the mouse the paper goes where you point. With a keyboard-only setup
    /// or a gamepad, Q/E (LB/RB) pick a side and the throw targets the next house on that side.
    /// The preview arc uses the same maths as the paper itself, so they always agree.
    /// </summary>
    public class ThrowController : MonoBehaviour
    {
        public enum AimMode { Mouse, Assisted }

        [Header("References")]
        public BikeController Bike;
        public BikeVisuals Visuals;
        public Transform Hand;
        public Camera Cam;
        public LineRenderer Arc;
        public Transform Reticle;
        public PaperPool Pool;

        [Header("Throw")]
        public float MinRange = 3.5f;
        public float MaxRange = 15f;
        [Tooltip("Seconds of holding to reach full range.")] public float ChargeTime = 0.6f;
        public float Cooldown = 0.22f;
        public float GroundHeight = 0.1f;
        public LayerMask ArcBlockers;

        [Header("Aim assist")]
        [Tooltip("How close to a doorstep the aim must be for assist to help.")] public float AssistRadius = 2.0f;
        [Range(0f, 1f)] public float AssistStrength = 0.45f;

        public AimMode Mode { get; private set; } = AimMode.Mouse;
        public int Side { get; private set; } = 1;
        public bool Charging { get; private set; }
        public float Charge { get; private set; }
        public House AssistedHouse { get; private set; }
        public bool Enabled = true;

        const int ArcPoints = 28;
        readonly Vector3[] arcBuffer = new Vector3[ArcPoints];
        float cooldown;
        Vector2 stickOffset;
        Vector3 lastTarget;
        bool hasTarget;

        void Awake()
        {
            if (Arc) { Arc.positionCount = 0; Arc.enabled = false; }
            if (Reticle) Reticle.gameObject.SetActive(false);
        }

        public void UseMouse() => Mode = AimMode.Mouse;
        public void ChooseSide(int side) { Side = side; Mode = AimMode.Assisted; }
        public void NudgeAim(Vector2 stick) { stickOffset = stick; if (stick.sqrMagnitude > 0.04f) Mode = AimMode.Assisted; }

        public void BeginCharge()
        {
            if (!Enabled || cooldown > 0f || Charging || Bike.Current == BikeController.State.Crashed) return;
            Charging = true;
            Charge = 0f;
        }

        public void CancelCharge()
        {
            Charging = false;
            Charge = 0f;
            ShowPreview(false);
        }

        /// <summary>Release: throw at the current target.</summary>
        public Newspaper Release()
        {
            if (!Charging) return null;
            Charging = false;
            ShowPreview(false);
            if (!hasTarget || Bike.Current == BikeController.State.Crashed) return null;
            return Launch(lastTarget);
        }

        /// <summary>Throw straight at a point (used by the autopilot for automated testing).</summary>
        public Newspaper ThrowAt(Vector3 target) => cooldown > 0f ? null : Launch(target);

        Newspaper Launch(Vector3 target)
        {
            Vector3 from = Hand.position;
            float t = Ballistics.FlightTime(HorizontalDistance(from, target));
            var v = Ballistics.LaunchVelocity(from, target, t);
            var paper = Pool.Spawn(from, v);
            cooldown = Cooldown;
            if (Visuals) Visuals.PlayThrow(target.x < Bike.transform.position.x ? -1 : 1);
            Sfx.Play(Sfx.Kind.Throw, 0.8f);
            if (GameFlow.Instance) GameFlow.Instance.OnThrow();
            return paper;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (!Charging) return;
            Charge = Mathf.Min(1f, Charge + dt / ChargeTime);
            hasTarget = ComputeTarget(out lastTarget);
            if (hasTarget) DrawArc(lastTarget);
            ShowPreview(hasTarget);
        }

        /// <summary>Where would a paper go right now? Range grows with charge; aim assist nudges towards doorsteps.</summary>
        bool ComputeTarget(out Vector3 target)
        {
            Vector3 from = Hand.position;
            Vector3 flatFrom = new Vector3(from.x, GroundHeight, from.z);
            float range = Mathf.Lerp(MinRange, MaxRange, Charge);
            AssistedHouse = null;
            var route = RouteManager.Instance;

            if (Mode == AimMode.Mouse)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null || !Cam) { target = default; return false; }
                var ray = Cam.ScreenPointToRay(mouse.position.ReadValue());
                var plane = new Plane(Vector3.up, new Vector3(0f, GroundHeight, 0f));
                if (!plane.Raycast(ray, out float enter)) { target = default; return false; }
                target = ray.GetPoint(enter);
                // modest aim assist: pull towards a waiting doorstep the cursor is already near
                if (route)
                {
                    House best = null;
                    float bestD = AssistRadius;
                    foreach (var h in route.Subscribers)
                    {
                        if (h.Delivered || h.Missed) continue;
                        float d = FlatDistance(h.Doorstep.position, target);
                        if (d < bestD) { bestD = d; best = h; }
                    }
                    var practice = route.PracticeHouse;
                    if (practice && !practice.Delivered && FlatDistance(practice.Doorstep.position, target) < bestD) best = practice;
                    if (best)
                    {
                        var d = best.Doorstep.position; d.y = GroundHeight;
                        target = Vector3.Lerp(target, d, AssistStrength);
                        AssistedHouse = best;
                    }
                }
            }
            else
            {
                // next waiting house on the chosen side, else a spot over on that side
                var house = route ? route.NextTarget(Bike.transform.position.z, Side, MaxRange + 4f) : null;
                if (house) { target = house.Doorstep.position; AssistedHouse = house; }
                else target = flatFrom + new Vector3(Side * 7f, 0f, 3f + Bike.Speed * 0.3f);
                target += new Vector3(stickOffset.x, 0f, stickOffset.y) * 2.5f;
            }
            target.y = GroundHeight;

            // can't reach further than the current charge allows
            var flat = new Vector3(target.x - flatFrom.x, 0f, target.z - flatFrom.z);
            if (flat.magnitude > range) target = flatFrom + flat.normalized * range;
            if (flat.magnitude < 1.2f) target = flatFrom + (flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.right * Side) * 1.2f;
            target.y = GroundHeight;
            return true;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        void DrawArc(Vector3 target)
        {
            if (!Arc) return;
            Vector3 from = Hand.position;
            float t = Ballistics.FlightTime(HorizontalDistance(from, target));
            var v = Ballistics.LaunchVelocity(from, target, t);
            int count = 0;
            Vector3 prev = from;
            Vector3 end = target;
            for (int i = 0; i < ArcPoints; i++)
            {
                var p = Ballistics.PositionAt(from, v, t * i / (ArcPoints - 1));
                // stop the preview where the paper would hit something (a wall, a window, a car)
                if (i > 0 && Physics.Linecast(prev, p, out var hit, ArcBlockers, QueryTriggerInteraction.Ignore))
                {
                    arcBuffer[count++] = hit.point;
                    end = hit.point;
                    break;
                }
                arcBuffer[count++] = p;
                prev = p;
            }
            Arc.positionCount = count;
            Arc.SetPositions(arcBuffer);
            if (Reticle)
            {
                Reticle.position = end + Vector3.up * 0.03f;
                float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.08f;
                Reticle.localScale = Vector3.one * (AssistedHouse ? 1.25f : 1f) * pulse;
            }
        }

        void ShowPreview(bool on)
        {
            if (Arc) Arc.enabled = on;
            if (Reticle) Reticle.gameObject.SetActive(on);
        }
    }
}
