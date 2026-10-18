using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// A car backing slowly out of a driveway. Hazard lights and reversing beeps come first; it then
    /// rolls back into the near lane and waits, leaving the far side clear. Hitting it is a crash.
    /// </summary>
    public class ReversingCar : Hazard, IRiderHazard
    {
        [Header("Car")]
        [Tooltip("How far into the road it backs (world X it stops at).")] public float StopX = 1.0f;
        public float ReverseSpeed = 1.6f;
        public float WaitTime = 5f;
        public Renderer[] Indicators = new Renderer[0];
        public Renderer[] ReverseLights = new Renderer[0];
        public Material LightOn, IndicatorOff, ReverseOff;
        public Transform[] Wheels = new Transform[0];

        Vector3 home;
        float wait, beepTimer, wheelAngle;
        enum Phase { Parked, Reversing, Waiting, Returning }
        Phase phase;

        void Awake() { home = transform.position; }

        public override void ResetHazard()
        {
            base.ResetHazard();
            transform.position = home;
            phase = Phase.Parked;
            SetLights(false, false);
        }

        protected override void OnWarn()
        {
            base.OnWarn();
            beepTimer = 0f;
        }

        protected override void OnAct() { phase = Phase.Reversing; }

        protected override void Update()
        {
            base.Update();
            if (!Warned || phase == Phase.Returning && !Acting) return;
            // hazard lights flash and the reversing beeper sounds until it's back in the drive
            bool flash = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
            SetLights(flash, phase == Phase.Reversing || phase == Phase.Waiting);
            if (phase != Phase.Returning && (beepTimer -= Time.deltaTime) <= 0f)
            {
                float dist = Bike ? Mathf.Abs(transform.position.z - Bike.transform.position.z) : 30f;
                Sfx.Play(Sfx.Kind.Beep, Mathf.Clamp01(1.2f - dist / 50f));
                beepTimer = 1.2f;
            }
        }

        protected override void Tick(float dt)
        {
            float side = Mathf.Sign(home.x);
            switch (phase)
            {
                case Phase.Reversing:
                {
                    float targetX = side * StopX;
                    float x = Mathf.MoveTowards(transform.position.x, targetX, ReverseSpeed * dt);
                    wheelAngle -= Mathf.Abs(x - transform.position.x) / 0.32f * Mathf.Rad2Deg;
                    transform.position = new Vector3(x, transform.position.y, transform.position.z);
                    if (Mathf.Approximately(x, targetX)) { phase = Phase.Waiting; wait = WaitTime; }
                    break;
                }
                case Phase.Waiting:
                    // pulls back in once the rider is safely past
                    wait -= dt;
                    if (wait <= 0f && Bike.transform.position.z > transform.position.z + 4f) phase = Phase.Returning;
                    break;
                case Phase.Returning:
                {
                    float x = Mathf.MoveTowards(transform.position.x, home.x, ReverseSpeed * dt);
                    wheelAngle += Mathf.Abs(x - transform.position.x) / 0.32f * Mathf.Rad2Deg;
                    transform.position = new Vector3(x, transform.position.y, transform.position.z);
                    if (Mathf.Approximately(x, home.x)) { Acting = false; SetLights(false, false); if (Warning) Warning.Show(false); enabled = false; }
                    break;
                }
            }
            foreach (var w in Wheels) if (w) w.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        }

        void SetLights(bool indicators, bool reversing)
        {
            foreach (var r in Indicators) if (r) r.sharedMaterial = indicators ? LightOn : IndicatorOff;
            foreach (var r in ReverseLights) if (r) r.sharedMaterial = reversing ? LightOn : ReverseOff;
        }

        public HitResponse OnRiderHit(BikeController bike) => bike.Speed > 2.5f ? HitResponse.Crash : HitResponse.Bump;
    }
}
