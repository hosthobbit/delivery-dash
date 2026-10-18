using System.Collections.Generic;
using DeliveryDash.Core;
using NUnit.Framework;
using UnityEngine;

namespace DeliveryDash.Tests
{
    public class ScoringTests
    {
        [Test]
        public void DeliveriesScoreByGradeAndBuildAStreak()
        {
            var s = new ScoreSheet();
            Assert.AreEqual(300, s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Perfect)));   // x1.00
            Assert.AreEqual(250, s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Great)));     // x1.25
            Assert.AreEqual(150, s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good)));      // x1.50
            Assert.AreEqual(3, s.Streak);
            Assert.AreEqual(700, s.Score);
        }

        [Test]
        public void MultiplierIsCapped()
        {
            Assert.AreEqual(3f, ScoreRules.Multiplier(50));
            Assert.AreEqual(1f, ScoreRules.Multiplier(0));
        }

        [Test]
        public void EveryPenaltyResetsTheStreak()
        {
            foreach (var kind in new[] { RoundEventKind.MissedHouse, RoundEventKind.WindowBroken, RoundEventKind.PedestrianHit, RoundEventKind.Crash })
            {
                var s = new ScoreSheet();
                s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good));
                s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good));
                Assert.AreEqual(2, s.Streak);
                Assert.Less(s.Apply(new RoundEvent(kind)), 0, kind.ToString());
                Assert.AreEqual(0, s.Streak, kind.ToString());
                Assert.AreEqual(2, s.BestStreak);
                // next delivery starts again at x1
                Assert.AreEqual(100, s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good)));
            }
        }

        [Test]
        public void ScoreNeverGoesNegative()
        {
            var s = new ScoreSheet();
            s.Apply(new RoundEvent(RoundEventKind.PedestrianHit));
            Assert.AreEqual(0, s.Score);
        }

        [Test]
        public void FinishAddsTimeAndFullRoundBonuses()
        {
            var s = new ScoreSheet();
            for (int i = 0; i < 3; i++) s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good));
            int before = s.Score;
            int bonus = s.Finish(100f, 110f, 3);
            Assert.AreEqual(10 * ScoreRules.SecondsBonusPerSecond + ScoreRules.FullRoundBonus, bonus);
            Assert.AreEqual(before + bonus, s.Score);
            // no time bonus over par, no full-round bonus with a miss
            var t = new ScoreSheet();
            t.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good));
            t.Apply(new RoundEvent(RoundEventKind.MissedHouse));
            Assert.AreEqual(0, t.Finish(200f, 110f, 2));
        }

        [Test]
        public void ReplayMatchesTheLiveScore()
        {
            var s = new ScoreSheet();
            var events = new List<RoundEvent>
            {
                new RoundEvent(RoundEventKind.Delivery, Grade.Perfect), new RoundEvent(RoundEventKind.Delivery, Grade.Letterbox),
                new RoundEvent(RoundEventKind.WindowBroken), new RoundEvent(RoundEventKind.Delivery, Grade.Great),
                new RoundEvent(RoundEventKind.Crash), new RoundEvent(RoundEventKind.MissedHouse),
            };
            foreach (var e in events) s.Apply(e);
            s.Finish(130f, 140f, 5);
            Assert.AreEqual(s.Score, ScoreSheet.Replay(events, 130f, 140f, 5));
        }

        [Test]
        public void AccuracyIsDeliveriesOverThrows()
        {
            var s = new ScoreSheet();
            for (int i = 0; i < 4; i++) s.CountThrow();
            s.Apply(new RoundEvent(RoundEventKind.Delivery, Grade.Good));
            Assert.AreEqual(0.25f, s.Accuracy, 1e-5f);
        }
    }

    public class DeliveryTests
    {
        static readonly Rect Plot = new Rect(5.9f, 10f, 3.8f, 5.4f); // x 5.9..9.7, z 10..15.4
        static readonly Vector3 Door = new Vector3(9.4f, 0f, 12.7f);

        [Test]
        public void GradesByDistanceToTheDoorstep()
        {
            Assert.AreEqual(Grade.Perfect, DeliveryJudge.Judge(new Vector3(9.0f, 0, 12.5f), Plot, Door));
            Assert.AreEqual(Grade.Great, DeliveryJudge.Judge(new Vector3(7.8f, 0, 13.4f), Plot, Door));
            Assert.AreEqual(Grade.Good, DeliveryJudge.Judge(new Vector3(6.1f, 0, 10.2f), Plot, Door));
        }

        [Test]
        public void OutsideTheGardenIsNotADelivery()
        {
            Assert.AreEqual(Grade.None, DeliveryJudge.Judge(new Vector3(4.0f, 0, 12f), Plot, Door));  // pavement
            Assert.AreEqual(Grade.None, DeliveryJudge.Judge(new Vector3(8f, 0, 16.5f), Plot, Door));  // next door
        }

        [Test]
        public void AHouseCanOnlyBeDeliveredOnce()
        {
            var r = new RouteLedger(new[] { 10f, 30f }, 100f);
            Assert.IsTrue(r.Deliver(0));
            Assert.IsFalse(r.Deliver(0));
            Assert.AreEqual(1, r.DeliveredCount);
        }

        [Test]
        public void HousesRiddenPastAreMissedOnce()
        {
            var r = new RouteLedger(new[] { 10f, 30f, 60f }, 100f, 12f);
            var misses = new List<int>();
            Assert.AreEqual(0, r.CollectMisses(21f, misses));
            Assert.AreEqual(1, r.CollectMisses(23f, misses));
            Assert.AreEqual(0, misses[0]);
            Assert.AreEqual(0, r.CollectMisses(24f, misses)); // not reported again
            Assert.IsFalse(r.Deliver(0));                      // and too late to deliver
            Assert.IsTrue(r.Deliver(1));
            Assert.AreEqual(1, r.CloseOut(misses));            // finish line: house 2 still pending
            Assert.AreEqual(2, r.MissedCount);
        }

        [Test]
        public void RouteCompletesAtTheFinishLine()
        {
            var r = new RouteLedger(new[] { 10f }, 500f);
            Assert.IsFalse(r.Finished(499f));
            Assert.IsTrue(r.Finished(500f));
            Assert.AreEqual(0.5f, r.Progress(250f, 0f), 1e-5f);
        }
    }

    public class BallisticsTests
    {
        [Test]
        public void ThrowsLandWhereThePreviewSays()
        {
            var from = new Vector3(0.5f, 1.4f, 3f);
            var to = new Vector3(8.7f, 0.05f, 9.5f);
            float t = Ballistics.FlightTime(Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z)));
            var v = Ballistics.LaunchVelocity(from, to, t);
            var end = Ballistics.PositionAt(from, v, t);
            Assert.Less(Vector3.Distance(end, to), 1e-3f);
            // stepping the way the paper does (fixed steps, analytic positions) reaches the same point
            Vector3 p = from;
            float time = 0f;
            const float dt = 0.02f;
            while (time + dt < t) { time += dt; p = Ballistics.PositionAt(from, v, time); }
            p = Ballistics.PositionAt(from, v, t);
            Assert.Less(Vector3.Distance(p, to), 1e-3f);
        }
    }
}
