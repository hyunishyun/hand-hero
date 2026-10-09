using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Enemy variety (round 4, S6 / D7): BotBrain's attack reads the archetype
    // (telegraph time, burst, interval, boss pattern); movement never does.
    public class BotAttackTests
    {
        private const float Dt = 1f / 72f;
        private static readonly ArenaBounds Arena = new ArenaBounds(new Vector3(0f, 2f, 20f), new Vector3(35f, 20f, 35f));
        private static readonly Vector3 Enemy = new Vector3(0f, 2f, 8f);
        private static readonly Vector3 Self = Enemy + new Vector3(0f, 0f, 12f);

        private static BotAttack Striker => BotArchetypes.Get(BotArchetypeId.Striker).Attack;
        private static BotAttack Sniper => BotArchetypes.Get(BotArchetypeId.Sniper).Attack;
        private static BotAttack Gunner => BotArchetypes.Get(BotArchetypeId.Gunner).Attack;
        private static BotAttack Lancer => BotArchetypes.Get(BotArchetypeId.Lancer).Attack;

        private static BotParams Quiet()
        {
            BotParams p = BotParams.Default;
            p.ReactionTime = 0f;
            p.AimErrorDegrees = 0f;
            p.FireIntervalJitter = 0f;
            p.FireInterval = 1f;
            p.TelegraphTime = 0.5f;
            return p;
        }

        private static BotBrain Brain(BotAttack attack, BotAttack alt = default, int switchEvery = 0, int seed = 1)
        {
            var brain = new BotBrain(Quiet(), seed);
            brain.SetAttacks(attack, alt, switchEvery);
            return brain;
        }

        private struct Event
        {
            public float Time;
            public bool Telegraph; // false = a shot
            public BotAttack Shot;
        }

        // A standing bot facing a standing enemy: telegraph starts and shots in time order.
        private static List<Event> Run(BotBrain brain, float seconds)
        {
            var events = new List<Event>();
            bool wasTelegraphing = false;
            for (int i = 0; i * Dt < seconds; i++)
            {
                HandInputData input = brain.Step(Self, true, Enemy, Arena, Dt);
                if (brain.IsTelegraphing && !wasTelegraphing) events.Add(new Event { Time = i * Dt, Telegraph = true });
                wasTelegraphing = brain.IsTelegraphing;
                if (input.FireTriggered) events.Add(new Event { Time = i * Dt, Shot = brain.LastShot });
            }
            return events;
        }

        // Shots per attack (the shots after each telegraph start).
        private static List<List<BotAttack>> Attacks(List<Event> events)
        {
            var attacks = new List<List<BotAttack>>();
            foreach (Event e in events)
            {
                if (e.Telegraph) attacks.Add(new List<BotAttack>());
                else if (attacks.Count > 0) attacks[attacks.Count - 1].Add(e.Shot);
            }
            return attacks;
        }

        [Test]
        public void Striker_ReproducesTodaysBot_FrameForFrame()
        {
            var today = new BotBrain(BotParams.Default, 9);
            var striker = new BotBrain(BotParams.Default, 9);
            striker.SetAttacks(Striker, Striker, 0);

            int shots = 0;
            for (int i = 0; i < 72 * 30; i++)
            {
                Vector3 enemy = Enemy + new Vector3(Mathf.Sin(i * 0.02f) * 6f, 0f, 0f);
                if (i == 400 || i == 1200)
                {
                    today.NotifyHit();
                    striker.NotifyHit();
                }
                HandInputData a = today.Step(Self, true, enemy, Arena, Dt);
                HandInputData b = striker.Step(Self, true, enemy, Arena, Dt);
                Assert.AreEqual(a.FireTriggered, b.FireTriggered, $"fire, frame {i}");
                Assert.IsTrue(a.ClutchDelta.Equals(b.ClutchDelta), $"move, frame {i}");
                Assert.IsTrue(a.AimDirection.Equals(b.AimDirection), $"aim, frame {i}");
                Assert.AreEqual(today.IsTelegraphing, striker.IsTelegraphing, $"telegraph, frame {i}");
                Assert.AreEqual(today.TelegraphProgress, striker.TelegraphProgress, $"progress, frame {i}");
                if (a.FireTriggered) shots++;
            }
            Assert.Greater(shots, 5);
        }

        [Test]
        public void Telegraph_LastsTheAttacksMultiple()
        {
            List<Event> events = Run(Brain(Sniper), 6f);
            Assert.IsTrue(events[0].Telegraph);
            Assert.IsFalse(events[1].Telegraph);
            Assert.That(events[1].Time - events[0].Time, Is.EqualTo(0.5f * 1.6f).Within(2f * Dt));
        }

        [Test]
        public void TelegraphProgress_SpansTheLongerTelegraph()
        {
            BotBrain brain = Brain(Sniper);
            for (int i = 0; i < 72 * 3 && !brain.IsTelegraphing; i++) brain.Step(Self, true, Enemy, Arena, Dt);
            Assert.IsTrue(brain.IsTelegraphing);
            for (float t = 0f; t < 0.4f; t += Dt) brain.Step(Self, true, Enemy, Arena, Dt);
            Assert.That(brain.TelegraphProgress, Is.EqualTo(0.5f).Within(0.05f));
            Assert.AreEqual(Sniper.TelegraphWidthMult, brain.ActiveAttack.TelegraphWidthMult);
        }

        [Test]
        public void Gunner_FiresABurst_AfterOneTelegraph()
        {
            List<Event> events = Run(Brain(Gunner), 4f);
            Assert.IsTrue(events[0].Telegraph);
            for (int i = 1; i <= 3; i++) Assert.IsFalse(events[i].Telegraph, $"shot {i}");
            Assert.IsTrue(events[4].Telegraph, "then the next attack");

            Assert.That(events[1].Time - events[0].Time, Is.EqualTo(0.5f * Gunner.TelegraphMult).Within(2f * Dt));
            Assert.That(events[2].Time - events[1].Time, Is.EqualTo(Gunner.BurstGap).Within(2f * Dt));
            Assert.That(events[3].Time - events[2].Time, Is.EqualTo(Gunner.BurstGap).Within(2f * Dt));
            // The interval runs from the burst's last shot.
            Assert.That(events[4].Time - events[3].Time, Is.EqualTo(1f * Gunner.IntervalMult).Within(2f * Dt));
        }

        [Test]
        public void BurstShots_ReAimAtTheTarget()
        {
            BotBrain brain = Brain(Gunner);
            Vector3 firstAim = Vector3.zero;
            for (int i = 0; i < 72 * 3; i++)
            {
                HandInputData input = brain.Step(Self, true, Enemy, Arena, Dt);
                if (!input.FireTriggered) continue;
                firstAim = input.AimDirection;
                break;
            }
            Assert.AreNotEqual(Vector3.zero, firstAim);

            Vector3 moved = Enemy + new Vector3(5f, 0f, 0f);
            Vector3 secondAim = Vector3.zero;
            for (int i = 0; i < 72; i++)
            {
                HandInputData input = brain.Step(Self, true, moved, Arena, Dt);
                if (!input.FireTriggered) continue;
                secondAim = input.AimDirection;
                break;
            }
            Assert.AreNotEqual(Vector3.zero, secondAim, "the burst goes on");
            Assert.Less(Vector3.Angle(moved - Self, secondAim), 0.01f, "the second shot follows the target");
            Assert.Greater(Vector3.Angle(firstAim, secondAim), 5f);
        }

        [Test]
        public void Hit_DuringABurst_DropsTheRestOfIt()
        {
            BotBrain brain = Brain(Gunner);
            for (int i = 0; i < 72 * 3; i++)
                if (brain.Step(Self, true, Enemy, Arena, Dt).FireTriggered) break;

            brain.NotifyHit();
            bool telegraphed = false;
            bool shotBeforeTelegraph = false;
            for (int i = 0; i < 72 * 4; i++)
            {
                HandInputData input = brain.Step(Self, true, Enemy, Arena, Dt);
                telegraphed |= brain.IsTelegraphing;
                if (input.FireTriggered)
                {
                    shotBeforeTelegraph = !telegraphed;
                    break;
                }
            }
            Assert.IsTrue(telegraphed, "a new attack telegraphs again");
            Assert.IsFalse(shotBeforeTelegraph, "no leftover burst shot after the dodge");
        }

        [Test]
        public void LastShot_IsTheFiringAttack()
        {
            List<Event> events = Run(Brain(Sniper), 4f);
            Event shot = events.Find(e => !e.Telegraph);
            Assert.AreEqual(1.8f, shot.Shot.DamageMult, 1e-5f);
        }

        [Test]
        public void Interval_ScalesWithTheAttack()
        {
            List<Event> events = Run(Brain(Sniper), 8f);
            Assert.IsTrue(events[2].Telegraph);
            Assert.That(events[2].Time - events[1].Time, Is.EqualTo(1f * 1.8f).Within(2f * Dt));
        }

        [Test]
        public void Boss_SwitchesPattern_EveryNAttacks()
        {
            List<List<BotAttack>> attacks = Attacks(Run(Brain(Gunner, Lancer, 2), 30f));
            Assert.GreaterOrEqual(attacks.Count, 6);
            int[] expectedShots = { 3, 3, 1, 1, 3, 3 };
            for (int i = 0; i < expectedShots.Length; i++)
            {
                Assert.AreEqual(expectedShots[i], attacks[i].Count, $"attack {i}");
                float damage = i % 4 < 2 ? Gunner.DamageMult : Lancer.DamageMult;
                foreach (BotAttack shot in attacks[i]) Assert.AreEqual(damage, shot.DamageMult, 1e-5f, $"attack {i}");
            }
        }

        [Test]
        public void Reseed_StartsThePatternOver()
        {
            BotBrain brain = Brain(Gunner, Lancer, 1);
            int shots = 0;
            for (int i = 0; i < 72 * 5 && shots < 3; i++)
                if (brain.Step(Self, true, Enemy, Arena, Dt).FireTriggered) shots++;
            Assert.AreEqual(3, shots, "one Gunner burst done: the next would be the Lancer");

            brain.Reseed(2);
            for (int i = 0; i < 72 * 5; i++)
            {
                if (!brain.Step(Self, true, Enemy, Arena, Dt).FireTriggered) continue;
                Assert.AreEqual(Gunner.DamageMult, brain.LastShot.DamageMult, 1e-5f);
                return;
            }
            Assert.Fail("no shot after the reseed");
        }
    }
}
