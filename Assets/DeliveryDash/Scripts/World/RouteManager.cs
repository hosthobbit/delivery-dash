using System.Collections.Generic;
using DeliveryDash.Core;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// The street: which houses take a paper, where the route starts and ends, and the delivery
    /// ledger. Judges where papers land and notices houses the rider has ridden past.
    /// </summary>
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }

        [Header("Route")]
        public float StartZ = 0f;
        public float FinishZ = 880f;
        [Tooltip("Par time in seconds: finishing faster earns a time bonus.")] public float ParSeconds = 150f;
        public float PracticeStartZ = -70f;

        [Header("Houses (filled in by the builder)")]
        public House[] Houses = new House[0];
        public House PracticeHouse;
        public Pothole[] Potholes = new Pothole[0];

        [Header("Look")]
        public Material DeliveredMarkerMaterial;
        public float MarkerBob = 0.15f;

        public RouteLedger Ledger { get; private set; }
        public List<House> Subscribers { get; } = new List<House>();
        readonly List<int> missBuffer = new List<int>(8);
        float[] markerBase;

        void Awake()
        {
            Instance = this;
            foreach (var h in Houses) if (h.Subscriber) Subscribers.Add(h);
            Subscribers.Sort((a, b) => a.Doorstep.position.z.CompareTo(b.Doorstep.position.z));
            var zs = new float[Subscribers.Count];
            for (int i = 0; i < zs.Length; i++) { Subscribers[i].RouteIndex = i; zs[i] = Subscribers[i].Doorstep.position.z; }
            Ledger = new RouteLedger(zs, FinishZ, 11f);
            markerBase = new float[Houses.Length];
            for (int i = 0; i < Houses.Length; i++) if (Houses[i].Marker) markerBase[i] = Houses[i].Marker.localPosition.y;
        }

        void OnValidate()
        {
            if (FinishZ <= StartZ) FinishZ = StartZ + 100f;
        }

        void Update()
        {
            // all markers bob and turn together: one loop instead of one Update per house
            float t = Time.time;
            for (int i = 0; i < Houses.Length; i++)
            {
                var m = Houses[i].Marker;
                if (!m || !m.gameObject.activeSelf) continue;
                var p = m.localPosition;
                p.y = markerBase[i] + Mathf.Sin(t * 2.4f + i) * MarkerBob;
                m.localPosition = p;
                m.localRotation = Quaternion.Euler(0f, t * 90f, 0f);
            }
            if (PracticeHouse && PracticeHouse.Marker)
                PracticeHouse.Marker.localRotation = Quaternion.Euler(0f, t * 90f, 0f);
        }

        /// <summary>Houses the rider has just ridden past without a delivery.</summary>
        public List<int> CollectMisses(float riderZ)
        {
            Ledger.CollectMisses(riderZ, missBuffer);
            foreach (int i in missBuffer) Subscribers[i].MarkMissed();
            return missBuffer;
        }

        public List<int> CloseOut()
        {
            Ledger.CloseOut(missBuffer);
            foreach (int i in missBuffer) Subscribers[i].MarkMissed();
            return missBuffer;
        }

        /// <summary>
        /// Where did a paper land? Returns the house whose garden it's in (subscriber or not) and the
        /// grade if that house is still waiting for its paper.
        /// </summary>
        public House HouseAt(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            foreach (var h in Houses) if (h.Plot.Contains(q)) return h;
            if (PracticeHouse && PracticeHouse.Plot.Contains(q)) return PracticeHouse;
            return null;
        }

        /// <summary>The next undelivered subscriber ahead of z on a side (0 = either), within `range` metres.</summary>
        public House NextTarget(float z, int side, float range)
        {
            if (PracticeHouse && !PracticeHouse.Delivered && z < StartZ)
            {
                float dz = PracticeHouse.Doorstep.position.z - z;
                if (dz > -4f && dz < range && (side == 0 || side == PracticeHouse.Side)) return PracticeHouse;
            }
            House best = null;
            float bestDz = float.MaxValue;
            foreach (var h in Subscribers)
            {
                if (h.Delivered || h.Missed) continue;
                if (side != 0 && h.Side != side) continue;
                float dz = h.Doorstep.position.z - z;
                if (dz < -4f || dz > range) continue;
                if (dz < bestDz) { bestDz = dz; best = h; }
            }
            return best;
        }

        public float Progress(float riderZ) => Ledger.Progress(riderZ, StartZ);
    }
}
