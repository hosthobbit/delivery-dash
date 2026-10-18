using UnityEngine;

namespace DeliveryDash
{
    /// <summary>One house on the street. Subscribers want a paper; their front garden is the delivery zone.</summary>
    public class House : MonoBehaviour
    {
        [Tooltip("Does this house take a paper?")] public bool Subscriber;
        [Tooltip("Front garden in world XZ: the delivery zone.")] public Rect Plot;
        public Transform Doorstep;
        [Tooltip("Floating paper marker above the gate (subscribers only).")] public Transform Marker;
        public Renderer MarkerRing;
        [HideInInspector] public int RouteIndex = -1;   // index among subscribers, set by RouteManager
        public bool Delivered { get; private set; }
        public bool Missed { get; private set; }

        /// <summary>Which side of the street: -1 left, +1 right.</summary>
        public int Side => Doorstep.position.x < 0 ? -1 : 1;

        public void MarkDelivered()
        {
            Delivered = true;
            if (MarkerRing) MarkerRing.sharedMaterial = RouteManager.Instance ? RouteManager.Instance.DeliveredMarkerMaterial : MarkerRing.sharedMaterial;
        }

        public void MarkMissed()
        {
            Missed = true;
            if (Marker) Marker.gameObject.SetActive(false);
            if (MarkerRing) MarkerRing.gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Subscriber ? Color.green : Color.grey;
            Gizmos.DrawWireCube(new Vector3(Plot.center.x, 0.1f, Plot.center.y), new Vector3(Plot.width, 0.2f, Plot.height));
        }
    }
}
