using DeliveryDash.Core;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// A thrown, rolled newspaper. It follows exactly the arc the preview showed (same maths) and
    /// sweeps ahead each step to find what it hits. It can count for one delivery at most; then it
    /// lies on the ground for a bit and goes back to the pool.
    /// </summary>
    public class Newspaper : MonoBehaviour
    {
        public enum State { Idle, Flying, Falling, Resting }

        public float Radius = 0.11f;
        public float Lifetime = 7f;
        public LayerMask HitMask;

        public State Current { get; private set; } = State.Idle;
        public bool Scored { get; private set; }
        public bool Spent { get; private set; }  // broke a window or hit someone: can no longer deliver

        PaperPool pool;
        Vector3 from, velocity, fallVelocity, spinAxis;
        float t, life;
        readonly RaycastHit[] hits = new RaycastHit[6];

        public void Init(PaperPool owner) { pool = owner; }

        public void Launch(Vector3 origin, Vector3 v)
        {
            from = origin; velocity = v; t = 0f; life = 0f;
            Scored = false; Spent = false;
            Current = State.Flying;
            transform.position = origin;
            spinAxis = Vector3.Cross(Vector3.up, new Vector3(v.x, 0f, v.z).normalized);
            if (spinAxis.sqrMagnitude < 1e-4f) spinAxis = Vector3.right;
            gameObject.SetActive(true);
        }

        public void ResetForPool() { Current = State.Idle; }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (Current)
            {
                case State.Flying:
                {
                    t += dt;
                    var next = Ballistics.PositionAt(from, velocity, t);
                    if (!Step(transform.position, next)) transform.position = next;
                    transform.Rotate(spinAxis, 900f * dt, Space.World);
                    break;
                }
                case State.Falling:
                {
                    // after glancing off something: plain gravity until it lands
                    fallVelocity += Ballistics.Gravity * dt;
                    var next = transform.position + fallVelocity * dt;
                    if (!Step(transform.position, next)) transform.position = next;
                    transform.Rotate(spinAxis, 500f * dt, Space.World);
                    break;
                }
                case State.Resting:
                    life += dt;
                    if (life > Lifetime) pool.Return(this);
                    break;
            }
            if (Current != State.Resting && Current != State.Idle && transform.position.y < -2f) pool.Return(this);
        }

        /// <summary>Sweep from a to b; returns true if something was hit (and handled).</summary>
        bool Step(Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return false;
            int n = Physics.SphereCastNonAlloc(a, Radius, d / len, hits, len, HitMask, QueryTriggerInteraction.Collide);
            if (n == 0) return false;
            int best = 0;
            for (int i = 1; i < n; i++) if (hits[i].distance < hits[best].distance) best = i;
            var h = hits[best];
            transform.position = a + d / len * Mathf.Max(0f, h.distance);

            var game = GameFlow.Instance;
            // straight through a letterbox: the best delivery there is
            var letterbox = h.collider.GetComponent<Letterbox>();
            if (letterbox && !Spent && !Scored && game && game.TryDeliver(letterbox.House, Grade.Letterbox, h.point, this))
            {
                pool.Return(this);
                return true;
            }
            var window = h.collider.GetComponentInParent<BreakableWindow>();
            if (window && !window.Broken && !Scored)
            {
                if (window.Break(h.point) && game) game.OnWindowBroken(h.point);
                Spent = true;
                Bounce(h.normal, 0.25f);
                return true;
            }
            var person = h.collider.GetComponentInParent<Pedestrian>();
            if (person && !Spent)
            {
                person.Startle();
                if (game) game.OnPaperHitPerson(h.point);
                Spent = true;
                Bounce(h.normal, 0.2f);
                return true;
            }
            if (h.normal.y > 0.55f) { Land(h.point); return true; }
            Bounce(h.normal, 0.3f);
            return true;
        }

        void Bounce(Vector3 normal, float keep)
        {
            var v = Current == State.Flying ? velocity + Ballistics.Gravity * t : fallVelocity;
            var r = Vector3.Reflect(v, normal) * keep;
            r.y = Mathf.Min(r.y, 1f);
            fallVelocity = r;
            transform.position += normal * 0.05f;
            Current = State.Falling;
        }

        void Land(Vector3 at)
        {
            Current = State.Resting;
            life = 0f;
            transform.position = at + Vector3.up * 0.06f;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);
            Sfx.Play(Sfx.Kind.Land, 0.6f, Random.Range(0.9f, 1.1f));
            if (!Spent && !Scored && GameFlow.Instance) GameFlow.Instance.OnPaperLanded(this, at);
        }

        public void MarkScored() { Scored = true; }
    }
}
