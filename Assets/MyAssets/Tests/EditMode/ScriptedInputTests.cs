using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Headset-free playthrough: a scripted "fist -> drag -> release -> aim -> pinch"
    // drives the same Core logic the scene controllers use.
    public class ScriptedInputTests
    {
        private const float Dt = 1f / 72f;
        private const float Scale = 60f;

        private static readonly FlightParams Flight = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        [Test]
        public void Script_FramesAdvanceInOrder_ThenDefault()
        {
            var script = new ScriptedHandInputSource().Grab().Release();

            Assert.IsFalse(script.Current.ClutchHeld, "before the first Advance");
            Assert.IsTrue(script.Advance());
            Assert.IsTrue(script.Current.ClutchHeld);
            Assert.IsTrue(script.Advance());
            Assert.IsFalse(script.Current.ClutchHeld);
            Assert.IsFalse(script.Advance());
            Assert.IsFalse(script.Advance());
            Assert.AreEqual(default(HandInputData).ClutchHeld, script.Current.ClutchHeld);
        }

        [Test]
        public void Script_AimStaysUntilLost()
        {
            var script = new ScriptedHandInputSource()
                .Aim(Vector3.zero, Vector3.forward)
                .Idle(2)
                .LoseAim();

            int aimed = 0;
            while (script.Advance())
                if (script.Current.HasAim) aimed++;
            Assert.AreEqual(3, aimed);
        }

        [Test]
        public void FullSequence_HeroFliesWithDrag_GlidesOnRelease_FiresOncePerPinch()
        {
            var aimDir = new Vector3(0f, 0f, 1f);
            var script = new ScriptedHandInputSource()
                .Grab()
                .Drag(new Vector3(0.1f, 0f, 0f), 36)  // 10 cm right over 0.5 s -> target +6 m
                .Drag(Vector3.zero, 72)               // hold still for 1 s
                .Release()
                .Idle(36)
                .Aim(new Vector3(0f, 1.2f, 0f), aimDir)
                .Pinch()
                .Idle(5);

            var mapper = new ClutchMapper();
            var state = new FlightState { Position = new Vector3(0f, 2f, 20f) };
            Vector3 start = state.Position;
            Vector3 target = Vector3.zero;
            bool hasTarget = false;
            int fires = 0;
            Vector3 releasePosition = Vector3.zero;
            HandInputData lastAim = default;

            while (script.Advance())
            {
                HandInputData input = script.Current;

                ClutchResult clutch = mapper.Step(input, state.Position, Scale);
                if (clutch.Clutched)
                {
                    target = clutch.Target;
                    hasTarget = true;
                }
                if (clutch.JustReleased)
                {
                    hasTarget = false;
                    releasePosition = state.Position;
                }

                if (input.HasAim) lastAim = input;
                if (input.FireTriggered) fires++;

                state = SpringFlightModel.Step(state, hasTarget, target, Flight, default, Dt);
            }

            Assert.That(Vector3.Distance(start + new Vector3(6f, 0f, 0f), target), Is.LessThan(1e-3f),
                "relative mapping: 10 cm * 60");
            Assert.Greater(releasePosition.x, 5f, "hero reached the dragged target");
            Assert.Less(Mathf.Abs(releasePosition.y - start.y), 1e-3f);
            Assert.Greater(state.Position.x, releasePosition.x - 1e-4f, "glide keeps drifting, no snap back");
            Assert.AreEqual(1, fires);
            Assert.IsTrue(lastAim.HasAim);
            Assert.AreEqual(aimDir, lastAim.AimDirection);
        }
    }
}
