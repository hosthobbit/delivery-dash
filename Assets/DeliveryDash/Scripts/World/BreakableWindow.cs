using UnityEngine;

namespace DeliveryDash
{
    /// <summary>A pane of glass a paper can break.</summary>
    public class BreakableWindow : MonoBehaviour
    {
        public Renderer Glass;
        public Material BrokenMaterial;
        public bool Broken { get; private set; }

        public bool Break(Vector3 at)
        {
            if (Broken) return false;
            Broken = true;
            if (Glass && BrokenMaterial) Glass.sharedMaterial = BrokenMaterial;
            Effects.Instance?.GlassBurst(at);
            return true;
        }
    }
}
