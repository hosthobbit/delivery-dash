using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Everything about how the bike looks: leaning into turns, spinning wheels, pedalling legs, the
    /// throwing arm, wobbles and the cartoon tumble. Collision never depends on any of this.
    /// </summary>
    public class BikeVisuals : MonoBehaviour
    {
        public BikeController Bike;
        [Tooltip("Rotates for lean/tumble; the whole bike and rider.")] public Transform LeanRoot;
        public Transform FrontWheel, BackWheel, Crank, PedalLeft, PedalRight, Handlebar;
        [Tooltip("Rider thighs and shins, animated with the cranks.")] public Transform ThighL, ThighR, ShinL, ShinR;
        public Transform ThrowArm;
        public Renderer[] BlinkWhenShielded = new Renderer[0];

        [Header("Tuning")]
        public float WheelRadius = 0.34f;
        public float MaxLean = 24f;
        public float CrankRatio = 0.45f;

        float wheelAngle, crankAngle, lean, armT = 1f;

        public void PlayThrow(int side) { armT = 0f; armSide = side; }
        int armSide = 1;

        void LateUpdate()
        {
            if (!Bike) return;
            float dt = Time.deltaTime;
            float speed = Bike.Speed;

            wheelAngle += speed / WheelRadius * Mathf.Rad2Deg * dt;
            if (FrontWheel) FrontWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (BackWheel) BackWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (Handlebar) Handlebar.localRotation = Quaternion.Euler(0f, Bike.SteerInput * 18f, 0f);

            // pedal only when actually driving the bike forward
            bool pedalling = Bike.Current == BikeController.State.Riding && speed > 0.5f;
            if (pedalling) crankAngle += speed / WheelRadius * Mathf.Rad2Deg * CrankRatio * dt;
            if (Crank) Crank.localRotation = Quaternion.Euler(crankAngle, 0f, 0f);
            if (PedalLeft) PedalLeft.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (PedalRight) PedalRight.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            float a = crankAngle * Mathf.Deg2Rad;
            if (ThighL) ThighL.localRotation = Quaternion.Euler(-35f + Mathf.Sin(a) * 28f, 0f, 0f);
            if (ThighR) ThighR.localRotation = Quaternion.Euler(-35f - Mathf.Sin(a) * 28f, 0f, 0f);
            if (ShinL) ShinL.localRotation = Quaternion.Euler(55f - Mathf.Cos(a) * 25f, 0f, 0f);
            if (ShinR) ShinR.localRotation = Quaternion.Euler(55f + Mathf.Cos(a) * 25f, 0f, 0f);

            // lean into turns (more when faster), wobble after knocks, tip over when crashed
            float targetLean = -Bike.SteerInput * MaxLean * Mathf.Clamp01(speed / Bike.CruiseSpeed);
            float roll, pitch = 0f;
            if (Bike.Current == BikeController.State.Crashed)
            {
                float k = 1f - Mathf.Clamp01(Bike.StateTimer / Bike.CrashTime);
                roll = Mathf.Lerp(lean, 78f, Mathf.Clamp01(k * 3.5f));
                pitch = Mathf.Sin(Mathf.Clamp01(k * 2.5f) * Mathf.PI) * -14f;
            }
            else
            {
                lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-dt * 8f));
                float w = Bike.WobbleAmount;
                roll = lean + Mathf.Sin(Time.time * 28f) * 9f * w;
            }
            if (LeanRoot) LeanRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);

            // throwing arm: a quick overarm flick to the chosen side
            if (ThrowArm)
            {
                armT = Mathf.Min(1f, armT + dt * 4f);
                float swing = Mathf.Sin(armT * Mathf.PI);
                ThrowArm.localRotation = Quaternion.Euler(-60f * swing, 0f, armSide * 70f * swing);
            }

            // blink while shielded after a respawn
            if (BlinkWhenShielded.Length > 0)
            {
                bool show = !Bike.Shielded || Mathf.Repeat(Time.time * 10f, 1f) > 0.35f;
                foreach (var r in BlinkWhenShielded) if (r) r.enabled = show;
            }
        }
    }
}
