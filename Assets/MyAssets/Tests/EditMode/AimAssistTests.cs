using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // ASSIST aim: snap to a target inside a cone around the aim ray, with a wider
    // release cone so a target at the edge never flickers (ADR 8). The radius
    // variant does the same around the CURSOR marker.
    public class AimAssistTests
    {
        private const float Acquire = 8f;
        private const float Release = 11f;
        private static readonly Ray Forward = new Ray(Vector3.zero, Vector3.forward);

        // A point 20 m ahead, `deg` degrees off the +Z axis toward +X.
        private static Vector3 AtAngle(float deg)
        {
            return new Vector3(20f * Mathf.Tan(deg * Mathf.Deg2Rad), 0f, 20f);
        }

        private static int Select(int current, params Vector3[] candidates)
        {
            return AimAssist.SelectByAngle(Forward, new List<Vector3>(candidates), current, Acquire, Release);
        }

        [Test]
        public void SelectByAngle_Within8_Acquires()
        {
            Assert.AreEqual(0, Select(-1, AtAngle(7f)));
        }

        [Test]
        public void SelectByAngle_At9_NoAcquire()
        {
            Assert.AreEqual(-1, Select(-1, AtAngle(9f)));
        }

        [Test]
        public void SelectByAngle_Locked_KeepsUntil11()
        {
            Assert.AreEqual(0, Select(0, AtAngle(10.5f)));
        }

        [Test]
        public void SelectByAngle_Locked_DropsPast11()
        {
            Assert.AreEqual(-1, Select(0, AtAngle(12f)));
        }

        [Test]
        public void SelectByAngle_PicksSmallestAngle()
        {
            Assert.AreEqual(1, Select(-1, AtAngle(6f), AtAngle(2f)));
        }

        [Test]
        public void SelectByAngle_IgnoresBehindOrigin()
        {
            Assert.AreEqual(-1, Select(-1, new Vector3(0f, 0f, -20f)));
        }

        [Test]
        public void SelectByAngle_ZeroAcquire_Disabled()
        {
            var candidates = new List<Vector3> { new Vector3(0f, 0f, 20f) };
            Assert.AreEqual(-1, AimAssist.SelectByAngle(Forward, candidates, -1, 0f, 0f));
        }

        [Test]
        public void SelectByAngle_CurrentMinusOne_PicksFresh()
        {
            Assert.AreEqual(0, Select(-1, AtAngle(3f), AtAngle(5f)));
        }

        [Test]
        public void SelectByRadius_AcquiresWithin2_5_KeepsUntil3_5()
        {
            var cursor = new Vector3(0f, 5f, 20f);
            var near = new List<Vector3> { cursor + new Vector3(2.4f, 0f, 0f) };
            var mid = new List<Vector3> { cursor + new Vector3(3.4f, 0f, 0f) };
            var far = new List<Vector3> { cursor + new Vector3(3.6f, 0f, 0f) };

            Assert.AreEqual(0, AimAssist.SelectByRadius(cursor, near, -1, 2.5f, 3.5f));
            Assert.AreEqual(-1, AimAssist.SelectByRadius(cursor, mid, -1, 2.5f, 3.5f), "3.4 m is outside acquire");
            Assert.AreEqual(0, AimAssist.SelectByRadius(cursor, mid, 0, 2.5f, 3.5f), "locked: kept until 3.5 m");
            Assert.AreEqual(-1, AimAssist.SelectByRadius(cursor, far, 0, 2.5f, 3.5f));
        }

        [Test]
        public void SelectByRadius_ZeroRadius_Disabled()
        {
            var cursor = new Vector3(0f, 5f, 20f);
            var candidates = new List<Vector3> { cursor };
            Assert.AreEqual(-1, AimAssist.SelectByRadius(cursor, candidates, -1, 0f, 0f));
        }
    }
}
