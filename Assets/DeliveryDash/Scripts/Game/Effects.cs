using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Pooled particle bursts: confetti for deliveries, glass for windows, dust for bumps and crashes.
    /// One particle system per effect, emitted at a point on demand, so nothing is created at runtime.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }
        public ParticleSystem Confetti, Glass, Dust, Sparkle;
        public bool Reduced;   // fewer particles (low quality preset)

        void Awake() { Instance = this; }

        void Emit(ParticleSystem ps, Vector3 at, int count)
        {
            if (!ps) return;
            var p = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            ps.Emit(p, Reduced ? Mathf.Max(1, count / 3) : count);
        }

        public void DeliveryBurst(Vector3 at, bool perfect) { Emit(Confetti, at + Vector3.up * 0.3f, perfect ? 40 : 22); if (perfect) Emit(Sparkle, at + Vector3.up * 0.4f, 12); }
        public void GlassBurst(Vector3 at) => Emit(Glass, at, 26);
        public void DustBurst(Vector3 at) => Emit(Dust, at, 14);
    }
}
