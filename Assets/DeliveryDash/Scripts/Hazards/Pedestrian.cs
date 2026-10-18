using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// A neighbour on foot. Crossers wait at the zebra crossing and step out when the rider is still
    /// well back; strollers walk up and down the pavement. Bumping into either costs points (and
    /// gets you a cross "Oi!"); nobody gets hurt.
    /// </summary>
    public class Pedestrian : Hazard, IRiderHazard
    {
        public enum Kind { Crosser, Stroller }

        [Header("Pedestrian")]
        public Kind Behaviour = Kind.Crosser;
        public Vector3 WalkTo;              // crosser: the far kerb; stroller: the other end of the beat
        public float WalkSpeed = 1.35f;
        public Transform Body, LegL, LegR, ArmL, ArmR;

        Vector3 home;
        float startle, hitCooldown, phase;
        bool goingOut = true;

        void Awake() { home = transform.position; phase = Random.value * 10f; }

        public override void ResetHazard()
        {
            base.ResetHazard();
            transform.position = home;
            startle = 0f; goingOut = true;
        }

        protected override void Update()
        {
            hitCooldown -= Time.deltaTime;
            if (Behaviour == Kind.Stroller) { Stroll(Time.deltaTime); Animate(true); return; }
            base.Update();
            if (!Acting) Animate(false);
        }

        protected override void OnWarn()
        {
            base.OnWarn();
            Sfx.Play(Sfx.Kind.Whistle, 0.35f);
        }

        protected override void OnAct() { }

        protected override void Tick(float dt)
        {
            if (startle > 0f) { startle -= dt; Animate(false); return; }
            var to = WalkTo - transform.position;
            to.y = 0f;
            bool walking = to.magnitude > 0.05f;
            if (walking)
            {
                transform.position += to.normalized * Mathf.Min(to.magnitude, WalkSpeed * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 8f);
            }
            else if (Warning) Warning.Show(false);
            Animate(walking);
        }

        void Stroll(float dt)
        {
            if (startle > 0f) { startle -= dt; return; }
            var target = goingOut ? WalkTo : home;
            var to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.1f) { goingOut = !goingOut; return; }
            transform.position += to.normalized * WalkSpeed * 0.7f * dt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 5f);
        }

        void Animate(bool walking)
        {
            phase += Time.deltaTime * (walking ? 9f : 0f);
            float s = walking ? Mathf.Sin(phase) : 0f;
            if (LegL) LegL.localRotation = Quaternion.Euler(s * 32f, 0f, 0f);
            if (LegR) LegR.localRotation = Quaternion.Euler(-s * 32f, 0f, 0f);
            if (ArmL) ArmL.localRotation = Quaternion.Euler(-s * 26f, 0f, startle > 0 ? -120f : 0f);
            if (ArmR) ArmR.localRotation = Quaternion.Euler(s * 26f, 0f, startle > 0 ? 120f : 0f);
            if (Body)
            {
                float spin = startle > 0f ? (1f - startle) * 720f : 0f;
                Body.localPosition = new Vector3(0f, walking ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : 0f, 0f);
                Body.localRotation = Quaternion.Euler(0f, spin, 0f);
            }
        }

        /// <summary>Hit by a paper or a bike: a startled spin and a cross word.</summary>
        public void Startle()
        {
            startle = 1f;
            Sfx.Play(Sfx.Kind.Oi, 0.8f, Random.Range(0.9f, 1.15f));
        }

        public HitResponse OnRiderHit(BikeController bike)
        {
            if (hitCooldown > 0f) return HitResponse.Bump;
            hitCooldown = 2.5f;
            Startle();
            if (GameFlow.Instance) GameFlow.Instance.OnPedestrianHit(transform.position + Vector3.up * 1.8f);
            return HitResponse.Wobble;
        }
    }
}
