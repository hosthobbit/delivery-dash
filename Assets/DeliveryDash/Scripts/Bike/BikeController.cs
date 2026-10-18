using System;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>What happens when the rider runs into something.</summary>
    public enum HitResponse { Bump, Wobble, Crash }

    /// <summary>Anything the rider can run into that reacts (dogs, pedestrians, cars).</summary>
    public interface IRiderHazard
    {
        HitResponse OnRiderHit(BikeController bike);
    }

    /// <summary>
    /// Arcade bicycle: kinematic, forgiving and stable. The bike cruises along on its own; the player
    /// speeds up, brakes and steers. Collision is a sphere sweep; leaning and pedalling are purely
    /// visual and live in BikeVisuals.
    /// </summary>
    public class BikeController : MonoBehaviour
    {
        public enum State { Frozen, Riding, Wobbling, Crashed }

        [Header("Speed (m/s)")]
        public float CruiseSpeed = 6.5f;
        public float MaxSpeed = 10f;
        public float Acceleration = 5f;
        public float Braking = 10f;
        public float CoastRate = 2.5f;

        [Header("Steering")]
        [Tooltip("Largest angle away from straight down the street.")] public float MaxHeading = 38f;
        public float SteerRate = 150f;
        public float StraightenRate = 120f;
        [Tooltip("Street edges the rider can't leave (garden walls).")] public float MinX = -5.25f, MaxX = 5.25f;

        [Header("Collisions")]
        public float Radius = 0.42f;
        public LayerMask Obstacles;
        [Tooltip("Hitting something solid faster than this is a crash; slower is a bump.")] public float CrashSpeed = 4.4f;
        public float CrashTime = 1.3f;
        public float WobbleTime = 0.9f;
        public float RespawnShield = 1.6f;

        [Header("Kerb")]
        public float RoadHalfWidth = 3.5f;
        public float KerbHeight = 0.13f;

        public State Current { get; private set; } = State.Frozen;
        public float Speed { get; private set; }
        public float Heading { get; private set; }            // degrees, + = right
        public float SteerInput { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float StateTimer { get; private set; }
        public bool Shielded => shield > 0f;
        public float WobbleAmount => Current == State.Wobbling ? StateTimer / WobbleTime : 0f;

        /// <summary>(kind, where) for score and feedback: "crash", "bump", "pothole", or a hazard's own report.</summary>
        public event Action<string, Vector3> Hit;
        public event Action Respawned;

        float steer, accel, brake;
        float shield;
        float baseY;
        readonly RaycastHit[] hits = new RaycastHit[8];
        Vector3 crashFrom;

        void Awake() { baseY = transform.position.y; }

        public void SetInput(float steerAxis, float accelerate, float brakeAmount)
        {
            steer = Mathf.Clamp(steerAxis, -1f, 1f);
            accel = Mathf.Clamp01(accelerate);
            brake = Mathf.Clamp01(brakeAmount);
        }

        public void Freeze(bool frozen)
        {
            if (frozen) { Current = State.Frozen; Speed = 0f; Velocity = Vector3.zero; }
            else if (Current == State.Frozen) Current = State.Riding;
        }

        public void PlaceAt(Vector3 p)
        {
            transform.SetPositionAndRotation(new Vector3(p.x, baseY, p.z), Quaternion.identity);
            Heading = 0f; Speed = 0f; Velocity = Vector3.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            shield = Mathf.Max(0f, shield - dt);
            SteerInput = Mathf.MoveTowards(SteerInput, Current == State.Crashed ? 0f : steer, dt * 6f);

            switch (Current)
            {
                case State.Frozen: return;
                case State.Crashed:
                    StateTimer -= dt;
                    Speed = Mathf.MoveTowards(Speed, 0f, 25f * dt);
                    if (StateTimer <= 0f) Respawn();
                    break;
                case State.Wobbling:
                    StateTimer -= dt;
                    if (StateTimer <= 0f) Current = State.Riding;
                    break;
            }
            if (Current == State.Crashed) { Velocity = Vector3.zero; return; }

            // speed: cruise by default, faster while accelerating, brake down to a stop
            float target = accel > 0.05f ? Mathf.Lerp(CruiseSpeed, MaxSpeed, accel) : brake > 0.05f ? 0f : CruiseSpeed;
            float rate = accel > 0.05f ? Acceleration : brake > 0.05f ? Braking * brake : CoastRate;
            if (Current == State.Wobbling) target = Mathf.Min(target, CruiseSpeed * 0.6f);
            Speed = Mathf.MoveTowards(Speed, target, rate * dt);

            // heading: steer, and drift back to straight when let go; stable at any speed
            // you can always turn, even stopped, so you can pull away from anything in front of you
            if (Mathf.Abs(steer) > 0.05f) Heading += steer * SteerRate * dt;
            else Heading = Mathf.MoveTowards(Heading, 0f, StraightenRate * dt);
            Heading = Mathf.Clamp(Heading, -MaxHeading, MaxHeading);

            float rad = Heading * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Vector3 move = dir * (Speed * dt);
            move = Sweep(move, dir);

            var p = transform.position + move;
            // garden walls: slide along, turning back towards the street
            if (p.x < MinX) { p.x = MinX; Heading = Mathf.Max(Heading, 0f); }
            if (p.x > MaxX) { p.x = MaxX; Heading = Mathf.Min(Heading, 0f); }
            // up onto the pavement (visual bump only)
            float targetY = baseY + (Mathf.Abs(p.x) > RoadHalfWidth ? KerbHeight : 0f);
            p.y = Mathf.MoveTowards(transform.position.y, targetY, dt * 1.6f);
            Velocity = (p - transform.position) / dt;
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Heading, 0f));

            CheckPotholes();
        }

        /// <summary>Move without passing through obstacles; react to whatever we touch.</summary>
        Vector3 Sweep(Vector3 move, Vector3 dir)
        {
            float dist = move.magnitude;
            if (dist < 1e-5f) return move;
            Vector3 origin = transform.position + Vector3.up * 0.55f;
            int n = Physics.SphereCastNonAlloc(origin, Radius, dir, hits, dist + 0.05f, Obstacles, QueryTriggerInteraction.Collide);
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance <= 0f && hits[i].point == Vector3.zero) continue; // started overlapping: ignore
                if (hits[i].distance < bestD) { bestD = hits[i].distance; best = i; }
            }
            if (best < 0) return move;
            var h = hits[best];
            if (Shielded) return move; // just respawned: ride out of whatever is nearby

            var hazard = h.collider.GetComponentInParent<IRiderHazard>();
            HitResponse response;
            if (hazard != null) response = hazard.OnRiderHit(this);
            else response = Speed >= CrashSpeed && Vector3.Dot(-h.normal, dir) > 0.55f ? HitResponse.Crash : HitResponse.Bump;

            Vector3 allowed = dir * Mathf.Max(0f, h.distance - 0.02f);
            switch (response)
            {
                case HitResponse.Crash: Crash(h.point); return allowed;
                case HitResponse.Wobble: StartWobble(); Hit?.Invoke("wobble", h.point); return move * 0.3f;
                default:
                    // glance off: keep the sideways part of the motion, lose speed
                    Speed *= 0.45f;
                    Heading += Vector3.Cross(h.normal, Vector3.up).y > 0 ? -12f : 12f;
                    StartWobble(0.5f);
                    Hit?.Invoke("bump", h.point);
                    var slide = Vector3.ProjectOnPlane(move, h.normal);
                    slide.y = 0f;
                    return allowed + slide * 0.5f;
            }
        }

        void CheckPotholes()
        {
            var route = RouteManager.Instance;
            if (!route || Current != State.Riding) return;
            var p = transform.position;
            foreach (var hole in route.Potholes)
            {
                var d = hole.transform.position - p;
                if (Mathf.Abs(d.z) < hole.Radius + 0.3f && Mathf.Abs(d.x) < hole.Radius + 0.15f)
                {
                    Speed *= 0.55f;
                    StartWobble();
                    Hit?.Invoke("pothole", hole.transform.position);
                    break;
                }
            }
        }

        public void StartWobble(float scale = 1f)
        {
            if (Current == State.Crashed || Current == State.Frozen) return;
            Current = State.Wobbling;
            StateTimer = WobbleTime * scale;
        }

        public void Crash(Vector3 at)
        {
            if (Current == State.Crashed || Shielded) return;
            Current = State.Crashed;
            StateTimer = CrashTime;
            crashFrom = transform.position;
            Hit?.Invoke("crash", at);
        }

        /// <summary>Back on the bike: a little behind the crash, in the nearest clear spot, briefly shielded.</summary>
        void Respawn()
        {
            float z = crashFrom.z - 1.2f;
            float[] lanes = { crashFrom.x, 1.8f, -1.8f, 0f, 3.0f, -3.0f };
            float x = 0f;
            foreach (float candidate in lanes)
            {
                var c = new Vector3(Mathf.Clamp(candidate, MinX, MaxX), 0.6f, z);
                if (!Physics.CheckSphere(c, Radius + 0.35f, Obstacles, QueryTriggerInteraction.Collide)) { x = c.x; break; }
            }
            transform.SetPositionAndRotation(new Vector3(x, baseY, z), Quaternion.identity);
            Heading = 0f;
            Speed = CruiseSpeed * 0.4f;
            shield = RespawnShield;
            Current = State.Riding;
            Respawned?.Invoke();
        }
    }
}
