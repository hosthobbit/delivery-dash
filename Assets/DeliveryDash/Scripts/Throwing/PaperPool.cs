using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>Recycles newspapers: no allocations while riding, and a hard cap on how many exist.</summary>
    public class PaperPool : MonoBehaviour
    {
        public Newspaper Prefab;
        public int Size = 24;

        readonly Queue<Newspaper> free = new Queue<Newspaper>();
        readonly List<Newspaper> live = new List<Newspaper>();

        void Awake()
        {
            for (int i = 0; i < Size; i++)
            {
                var p = Instantiate(Prefab, transform);
                p.name = $"Paper {i}";
                p.Init(this);
                p.gameObject.SetActive(false);
                free.Enqueue(p);
            }
        }

        public Newspaper Spawn(Vector3 at, Vector3 velocity)
        {
            // pool empty: recycle the oldest paper lying about
            Newspaper p = free.Count > 0 ? free.Dequeue() : null;
            if (!p && live.Count > 0) { p = live[0]; live.RemoveAt(0); }
            if (!p) return null;
            live.Add(p);
            p.Launch(at, velocity);
            return p;
        }

        public void Return(Newspaper p)
        {
            if (!live.Remove(p)) return;
            p.ResetForPool();
            p.gameObject.SetActive(false);
            free.Enqueue(p);
        }

        public void ReturnAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) Return(live[i]);
        }
    }
}
