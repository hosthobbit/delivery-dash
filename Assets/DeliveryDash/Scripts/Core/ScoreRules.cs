using System;
using System.Collections.Generic;

namespace DeliveryDash.Core
{
    public enum Grade { None = 0, Good = 1, Great = 2, Perfect = 3, Letterbox = 4 }

    /// <summary>Things that happen on a round that affect the score. The public scoreboard replays this list.</summary>
    public enum RoundEventKind { Delivery, MissedHouse, WindowBroken, PedestrianHit, Crash }

    [Serializable]
    public struct RoundEvent
    {
        public RoundEventKind Kind;
        public Grade Grade; // deliveries only
        public RoundEvent(RoundEventKind kind, Grade grade = Grade.None) { Kind = kind; Grade = grade; }
    }

    /// <summary>
    /// Scoring rules in one place, free of Unity so they can be unit tested and mirrored exactly by
    /// the scoreboard server (delivery-scores.php).
    /// </summary>
    public static class ScoreRules
    {
        public const int Good = 100, Great = 200, Perfect = 300, Letterbox = 400;
        public const int MissedHousePenalty = 50, WindowPenalty = 150, PedestrianPenalty = 200, CrashPenalty = 100;
        public const float StreakStep = 0.25f, MaxMultiplier = 3f;
        public const int FullRoundBonus = 1000;
        public const int SecondsBonusPerSecond = 25;

        public static int BasePoints(Grade g)
        {
            switch (g)
            {
                case Grade.Good: return Good;
                case Grade.Great: return Great;
                case Grade.Perfect: return Perfect;
                case Grade.Letterbox: return Letterbox;
                default: return 0;
            }
        }

        /// <summary>Multiplier for a delivery made with `streak` successful deliveries before it.</summary>
        public static float Multiplier(int streak) => Math.Min(MaxMultiplier, 1f + StreakStep * streak);

        /// <summary>Time bonus for finishing under par.</summary>
        public static int TimeBonus(float seconds, float parSeconds) => Math.Max(0, (int)Math.Floor(parSeconds - seconds)) * SecondsBonusPerSecond;
    }

    /// <summary>Running score for one round. Feed it events; it keeps totals, streak and the breakdown.</summary>
    public class ScoreSheet
    {
        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Delivered { get; private set; }
        public int Missed { get; private set; }
        public int Windows { get; private set; }
        public int Pedestrians { get; private set; }
        public int Crashes { get; private set; }
        public int Throws { get; private set; }
        public int DeliveryPoints { get; private set; }
        public int PenaltyPoints { get; private set; }
        public int BonusPoints { get; private set; }
        public readonly int[] GradeCounts = new int[5];
        public readonly List<RoundEvent> Events = new List<RoundEvent>();

        public float Multiplier => ScoreRules.Multiplier(Streak);

        /// <summary>Apply an event. Returns the points it changed the score by (negative for penalties).</summary>
        public int Apply(RoundEvent e)
        {
            Events.Add(e);
            int delta;
            switch (e.Kind)
            {
                case RoundEventKind.Delivery:
                    delta = (int)Math.Round(ScoreRules.BasePoints(e.Grade) * ScoreRules.Multiplier(Streak));
                    DeliveryPoints += delta;
                    Delivered++;
                    GradeCounts[(int)e.Grade]++;
                    Streak++;
                    BestStreak = Math.Max(BestStreak, Streak);
                    break;
                case RoundEventKind.MissedHouse: delta = -ScoreRules.MissedHousePenalty; Missed++; Streak = 0; break;
                case RoundEventKind.WindowBroken: delta = -ScoreRules.WindowPenalty; Windows++; Streak = 0; break;
                case RoundEventKind.PedestrianHit: delta = -ScoreRules.PedestrianPenalty; Pedestrians++; Streak = 0; break;
                case RoundEventKind.Crash: delta = -ScoreRules.CrashPenalty; Crashes++; Streak = 0; break;
                default: delta = 0; break;
            }
            if (delta < 0) PenaltyPoints += -delta;
            Score = Math.Max(0, Score + delta);
            return delta;
        }

        public void CountThrow() => Throws++;

        /// <summary>End of the route: time bonus and the full-round bonus if every house got its paper.</summary>
        public int Finish(float seconds, float parSeconds, int housesOnRoute)
        {
            int bonus = ScoreRules.TimeBonus(seconds, parSeconds);
            if (Delivered == housesOnRoute && housesOnRoute > 0) bonus += ScoreRules.FullRoundBonus;
            BonusPoints = bonus;
            Score += bonus;
            return bonus;
        }

        /// <summary>Share of throws that delivered a paper (0..1).</summary>
        public float Accuracy => Throws == 0 ? 0f : Math.Min(1f, (float)Delivered / Throws);

        /// <summary>Recompute a final score from an event list (what the scoreboard server does).</summary>
        public static int Replay(IEnumerable<RoundEvent> events, float seconds, float parSeconds, int housesOnRoute)
        {
            var s = new ScoreSheet();
            foreach (var e in events) s.Apply(e);
            s.Finish(seconds, parSeconds, housesOnRoute);
            return s.Score;
        }
    }
}
