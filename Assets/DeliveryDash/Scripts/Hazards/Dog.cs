using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// An excitable dog. It barks from its garden first, then dashes out to the edge of the road and
    /// chases alongside for a few seconds, staying on its own side. Running into it means a wobble.
    /// </summary>
    public class Dog : Hazard, IRiderHazard
    {
        [Header("Dog")]
        public float RunSpeed = 8.5f;
        public float ChaseTime = 3.2f;
        [Tooltip("How far into the road (from the kerb) the dog will come.")] public float RoadReach = 1.6f;
        public Transform Body, Tail, LegFL, LegFR, LegBL, LegBR;

        Vector3 home;
        Quaternion homeRot;
        enum Phase { Waiting, Dash, Chase, Home }
        Phase phase;
        float t, run, hitCooldown, barkTimer;

        void Awake() { home = transform.position; homeRot = transform.rotation; }

        public override void ResetHazard()
        {
            base.ResetHazard();
            transform.SetPositionAndRotation(home, homeRot);
            phase = Phase.Waiting;
        }

        protected override void OnWarn()
        {
            base.OnWarn();
            Sfx.Play(Sfx.Kind.Bark, 0.9f);
            barkTimer = 0.45f;
        }

        protected override void OnAct() { phase = Phase.Dash; t = 0f; }

        protected override void Update()
        {
            base.Update();
            hitCooldown -= Time.deltaTime;
            // tail never stops wagging
            if (Tail) Tail.localRotation = Quaternion.Euler(-30f, Mathf.Sin(Time.time * 18f) * 35f, 0f);
            if (Warned && phase != Phase.Home && (barkTimer -= Time.deltaTime) <= 0f)
            {
                Sfx.Play(Sfx.Kind.Bark, 0.7f, Random.Range(0.9f, 1.1f));
                barkTimer = Random.Range(0.6f, 1.0f);
            }
        }

        protected override void Tick(float dt)
        {
            t += dt;
            float side = Mathf.Sign(home.x);
            float edgeX = side * (3.5f - RoadReach);   // never further than this into the road
            Vector3 target;
            switch (phase)
            {
                case Phase.Dash:
                    // to the kerb a little ahead of the rider: visible, and easy to steer round
                    target = new Vector3(side * 3.6f, home.y, Mathf.Max(home.z - 6f, Bike.transform.position.z + 7f));
                    if (MoveTo(target, RunSpeed, dt) || t > 2.5f) { phase = Phase.Chase; t = 0f; }
                    break;
                case Phase.Chase:
                    // trot alongside the rider, on its own side of the road
                    target = new Vector3(edgeX, home.y, Bike.transform.position.z + 1.2f);
                    MoveTo(target, Mathf.Max(RunSpeed * 0.8f, Bike.Speed + 1f), dt);
                    if (t > ChaseTime) { phase = Phase.Home; t = 0f; if (Warning) Warning.Show(false); }
                    break;
                case Phase.Home:
                    if (MoveTo(home, RunSpeed * 0.6f, dt)) { transform.rotation = homeRot; Acting = false; Warned = true; phase = Phase.Waiting; enabled = false; }
                    break;
            }
            Animate(dt);
        }

        bool MoveTo(Vector3 target, float speed, float dt)
        {
            var d = target - transform.position;
            d.y = 0f;
            if (d.magnitude < 0.15f) return true;
            transform.position += d.normalized * Mathf.Min(d.magnitude, speed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 10f);
            run += dt * speed * 3.2f;
            return false;
        }

        void Animate(float dt)
        {
            float s = Mathf.Sin(run);
            if (LegFL) LegFL.localRotation = Quaternion.Euler(s * 40f, 0f, 0f);
            if (LegBR) LegBR.localRotation = Quaternion.Euler(s * 40f, 0f, 0f);
            if (LegFR) LegFR.localRotation = Quaternion.Euler(-s * 40f, 0f, 0f);
            if (LegBL) LegBL.localRotation = Quaternion.Euler(-s * 40f, 0f, 0f);
            if (Body) Body.localPosition = new Vector3(0f, Mathf.Abs(s) * 0.06f, 0f);
        }

        public HitResponse OnRiderHit(BikeController bike)
        {
            if (hitCooldown > 0f) return HitResponse.Bump;
            hitCooldown = 2f;
            Sfx.Play(Sfx.Kind.Bark, 1f, 1.3f);
            if (GameFlow.Instance) GameFlow.Instance.Feedback("Woof! Watch the dog!", transform.position + Vector3.up * 1.3f, FeedbackKind.Warning);
            return HitResponse.Wobble;
        }
    }
}
