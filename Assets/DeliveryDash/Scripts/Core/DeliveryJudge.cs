using UnityEngine;

namespace DeliveryDash.Core
{
    /// <summary>
    /// Grades where a paper landed. A subscriber's whole front garden counts as the delivery zone
    /// (generous); landing near the doorstep is better, and straight through the letterbox is best.
    /// </summary>
    public static class DeliveryJudge
    {
        public const float PerfectRadius = 1.1f;
        public const float GreatRadius = 2.3f;

        /// <summary>Grade for a landing point, given the garden plot (XZ rectangle) and the doorstep.</summary>
        public static Grade Judge(Vector3 landing, Rect plotXZ, Vector3 doorstep)
        {
            var p = new Vector2(landing.x, landing.z);
            if (!plotXZ.Contains(p)) return Grade.None;
            float d = Vector2.Distance(p, new Vector2(doorstep.x, doorstep.z));
            if (d <= PerfectRadius) return Grade.Perfect;
            if (d <= GreatRadius) return Grade.Great;
            return Grade.Good;
        }

        public static string Label(Grade g)
        {
            switch (g)
            {
                case Grade.Letterbox: return "Through the Letterbox!";
                case Grade.Perfect: return "Perfect Delivery!";
                case Grade.Great: return "Great Delivery";
                case Grade.Good: return "Delivered";
                default: return "";
            }
        }
    }

    /// <summary>Projectile maths shared by the trajectory preview and the thrown paper, so they always agree.</summary>
    public static class Ballistics
    {
        public static readonly Vector3 Gravity = new Vector3(0f, -14f, 0f); // a touch heavier than real: snappier arcs

        /// <summary>Flight time for a throw of horizontal distance `d` metres.</summary>
        public static float FlightTime(float d) => 0.42f + 0.045f * d;

        /// <summary>Launch velocity that takes a paper from `from` to `to` in `time` seconds.</summary>
        public static Vector3 LaunchVelocity(Vector3 from, Vector3 to, float time)
        {
            return (to - from - 0.5f * Gravity * time * time) / time;
        }

        public static Vector3 PositionAt(Vector3 from, Vector3 velocity, float t) => from + velocity * t + 0.5f * Gravity * t * t;
    }

    /// <summary>Tracks which subscriber houses are done, missed or still ahead, and when the route is complete.</summary>
    public class RouteLedger
    {
        public enum State { Pending, Delivered, Missed }
        readonly float[] houseZ;
        readonly State[] states;
        public readonly float FinishZ;
        public readonly float MissMargin;

        public RouteLedger(float[] subscriberZ, float finishZ, float missMargin = 12f)
        {
            houseZ = (float[])subscriberZ.Clone();
            states = new State[houseZ.Length];
            FinishZ = finishZ;
            MissMargin = missMargin;
        }

        public int Count => houseZ.Length;
        public State this[int i] => states[i];
        public int DeliveredCount { get { int n = 0; foreach (var s in states) if (s == State.Delivered) n++; return n; } }
        public int MissedCount { get { int n = 0; foreach (var s in states) if (s == State.Missed) n++; return n; } }

        /// <summary>Mark a house delivered. False if it was already delivered or missed (a paper only counts once).</summary>
        public bool Deliver(int i)
        {
            if (i < 0 || i >= states.Length || states[i] != State.Pending) return false;
            states[i] = State.Delivered;
            return true;
        }

        /// <summary>Houses the rider has now ridden well past without delivering. Call every frame; returns each miss once.</summary>
        public int CollectMisses(float riderZ, System.Collections.Generic.List<int> into)
        {
            into.Clear();
            for (int i = 0; i < houseZ.Length; i++)
                if (states[i] == State.Pending && riderZ > houseZ[i] + MissMargin) { states[i] = State.Missed; into.Add(i); }
            return into.Count;
        }

        public bool Finished(float riderZ) => riderZ >= FinishZ;

        /// <summary>At the finish line, anything still pending is a miss.</summary>
        public int CloseOut(System.Collections.Generic.List<int> into)
        {
            into.Clear();
            for (int i = 0; i < states.Length; i++) if (states[i] == State.Pending) { states[i] = State.Missed; into.Add(i); }
            return into.Count;
        }

        public float Progress(float riderZ, float startZ) => Mathf.Clamp01((riderZ - startZ) / Mathf.Max(1f, FinishZ - startZ));
    }
}
