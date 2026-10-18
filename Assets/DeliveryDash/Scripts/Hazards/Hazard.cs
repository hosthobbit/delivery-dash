using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Base for moving hazards. Each one warns first (sound + "!" sign), then acts, always leaving
    /// room to get past: they never appear right in front of the rider.
    /// </summary>
    public abstract class Hazard : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Rider this far before the hazard (in Z): show the warning.")] public float WarnDistance = 42f;
        [Tooltip("Rider this far before: start moving.")] public float ActDistance = 30f;
        public WarningSign Warning;

        protected BikeController Bike;
        protected bool Warned, Acting;
        protected float ActTime;

        public void Bind(BikeController bike) { Bike = bike; }

        /// <summary>Put back to the start state (route restart).</summary>
        public virtual void ResetHazard()
        {
            Warned = Acting = false;
            ActTime = 0f;
            if (Warning) Warning.Show(false);
        }

        protected virtual void Update()
        {
            if (!Bike || Bike.Current == BikeController.State.Frozen) return;
            float ahead = transform.position.z - Bike.transform.position.z;
            if (!Warned && ahead < WarnDistance && ahead > 0f) { Warned = true; OnWarn(); }
            if (Warned && !Acting && ahead < ActDistance) { Acting = true; ActTime = 0f; OnAct(); }
            if (Acting) { ActTime += Time.deltaTime; Tick(Time.deltaTime); }
        }

        protected virtual void OnWarn() { if (Warning) Warning.Show(true); }
        protected abstract void OnAct();
        protected abstract void Tick(float dt);
    }
}
