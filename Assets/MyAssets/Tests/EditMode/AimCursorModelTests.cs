using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // CURSOR aim: the right-hand fist drags a 3D marker with the same relative
    // mapping as the left-hand puppeteer, clamped to the arena.
    public class AimCursorModelTests
    {
        private const float Scale = 60f;
        private static readonly ArenaBounds Bounds =
            new ArenaBounds(new Vector3(0f, 10f, 20f), new Vector3(35f, 20f, 35f));

        private AimCursorModel _cursor;

        [SetUp]
        public void SetUp()
        {
            _cursor = new AimCursorModel();
            _cursor.Reset(Bounds.Center);
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");
        }

        [Test]
        public void Reset_SetsPosition()
        {
            var p = new Vector3(3f, 4f, 25f);
            _cursor.Reset(p);
            AssertNear(p, _cursor.Position);
        }

        [Test]
        public void Drag_MovesByDeltaTimesScale()
        {
            _cursor.Step(true, Vector3.zero, Scale, Bounds);
            Vector3 p = _cursor.Step(true, new Vector3(0.01f, 0f, 0.02f), Scale, Bounds);
            AssertNear(Bounds.Center + new Vector3(0.6f, 0f, 1.2f), p);
            Assert.IsTrue(_cursor.IsDragging);
        }

        [Test]
        public void Release_KeepsPosition()
        {
            _cursor.Step(true, Vector3.zero, Scale, Bounds);
            Vector3 dragged = _cursor.Step(true, new Vector3(0.01f, 0f, 0f), Scale, Bounds);

            Vector3 released = _cursor.Step(false, Vector3.one, Scale, Bounds);
            AssertNear(dragged, released);
            Assert.IsFalse(_cursor.IsDragging);
        }

        [Test]
        public void Regrab_NoJump()
        {
            _cursor.Step(true, Vector3.zero, Scale, Bounds);
            Vector3 dragged = _cursor.Step(true, new Vector3(0.01f, 0f, 0f), Scale, Bounds);
            _cursor.Step(false, Vector3.zero, Scale, Bounds);

            // The hand moved while open; HandClutchSampler reports zero delta on the
            // grab frame, so the regrab starts from the marker, not from the hand.
            Vector3 regrab = _cursor.Step(true, Vector3.zero, Scale, Bounds);
            AssertNear(dragged, regrab);
            Vector3 moved = _cursor.Step(true, new Vector3(0f, 0.01f, 0f), Scale, Bounds);
            AssertNear(dragged + new Vector3(0f, 0.6f, 0f), moved);
        }

        [Test]
        public void Drag_ClampedToBounds()
        {
            _cursor.Step(true, Vector3.zero, Scale, Bounds);
            Vector3 p = _cursor.Step(true, new Vector3(1f, 0f, 0f), Scale, Bounds);
            Assert.AreEqual(17.5f, p.x, 1e-4f);
        }

        [Test]
        public void Reset_WhileHeld_RegrabsFromNewPosition()
        {
            _cursor.Step(true, Vector3.zero, Scale, Bounds);
            _cursor.Step(true, new Vector3(0.05f, 0f, 0f), Scale, Bounds);

            var p = new Vector3(-2f, 8f, 18f);
            _cursor.Reset(p);
            Vector3 after = _cursor.Step(true, new Vector3(0.01f, 0f, 0f), Scale, Bounds);
            AssertNear(p + new Vector3(0.6f, 0f, 0f), after);
        }
    }
}
