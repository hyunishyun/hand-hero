using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    public class SpringFlightModelTests
    {
        private const float Dt = 1f / 72f;

        // HANDOFF defaults (FlyingCharacter inspector values).
        private static readonly FlightParams Defaults = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        private static FlightState Run(FlightState s, bool hasTarget, Vector3 target, ArenaBounds bounds, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
                s = SpringFlightModel.Step(s, hasTarget, target, Defaults, bounds, Dt);
            return s;
        }

        [Test]
        public void WithTarget_ConvergesToTarget()
        {
            var target = new Vector3(5f, 2f, -3f);
            FlightState s = Run(new FlightState(), true, target, default, 5f);

            Assert.Less(Vector3.Distance(s.Position, target), 0.05f);
            Assert.Less(s.Velocity.magnitude, 0.1f);
        }

        [Test]
        public void SpeedNeverExceedsMaxSpeed()
        {
            var s = new FlightState();
            var target = new Vector3(1000f, 0f, 0f);
            for (int i = 0; i < 500; i++)
            {
                s = SpringFlightModel.Step(s, true, target, Defaults, default, Dt);
                Assert.LessOrEqual(s.Velocity.magnitude, Defaults.MaxSpeed + 1e-3f);
            }
        }

        [Test]
        public void WithoutTarget_GlidesAndSlowsWithoutGravity()
        {
            var s = new FlightState { Velocity = new Vector3(10f, 0f, 0f) };
            FlightState after = Run(s, false, Vector3.zero, default, 1f);

            Assert.Greater(after.Position.x, 5f, "keeps momentum");
            Assert.Less(after.Velocity.magnitude, 10f, "drag slows it");
            Assert.Greater(after.Velocity.magnitude, 1f, "glide is long, not a stop");
            Assert.AreEqual(0f, after.Position.y, 1e-5f, "no gravity");
        }

        [Test]
        public void SingleStep_MatchesOriginalFlyingCharacterMath()
        {
            var s = new FlightState { Position = new Vector3(1f, 0f, 0f), Velocity = new Vector3(0f, 2f, 0f) };
            var target = new Vector3(3f, 0f, 0f);

            Vector3 v = s.Velocity;
            v += (target - s.Position) * (Defaults.Stiffness * Dt);
            v -= v * (Defaults.Damping * Dt);
            Vector3 expectedPos = s.Position + v * Dt;

            FlightState next = SpringFlightModel.Step(s, true, target, Defaults, default, Dt);
            Assert.That(Vector3.Distance(next.Velocity, v), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(next.Position, expectedPos), Is.LessThan(1e-5f));
        }

        [Test]
        public void ArenaBounds_ClampPositionInsideBox()
        {
            var bounds = new ArenaBounds(new Vector3(0f, 0f, 20f), new Vector3(35f, 20f, 35f));
            var target = new Vector3(100f, 100f, 100f);
            FlightState s = Run(new FlightState { Position = new Vector3(0f, 0f, 20f) }, true, target, bounds, 10f);

            Assert.AreEqual(17.5f, s.Position.x, 1e-4f);
            Assert.AreEqual(10f, s.Position.y, 1e-4f);
            Assert.AreEqual(37.5f, s.Position.z, 1e-4f);
        }

        [Test]
        public void DefaultBounds_DoNotClamp()
        {
            var p = new Vector3(1e4f, -1e4f, 3f);
            Assert.AreEqual(p, default(ArenaBounds).Clamp(p));
        }
    }
}
