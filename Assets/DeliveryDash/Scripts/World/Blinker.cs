using UnityEngine;

namespace DeliveryDash
{
    /// <summary>Flashes a light by swapping materials (Belisha beacons at the zebra crossing).</summary>
    public class Blinker : MonoBehaviour
    {
        public Renderer Target;
        public Material On, Off;
        public float Rate = 1.4f;
        bool lit;

        void Update()
        {
            bool now = Mathf.Repeat(Time.time * Rate, 1f) < 0.55f;
            if (now == lit || !Target) return;
            lit = now;
            Target.sharedMaterial = lit ? On : Off;
        }
    }
}
