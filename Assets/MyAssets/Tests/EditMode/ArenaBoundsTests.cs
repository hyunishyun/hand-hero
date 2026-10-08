using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Aim fallback when the aim ray hits nothing: the reticle goes where the ray
    // leaves the arena box, so it never flies off behind the (tabletop) arena.
    public class ArenaBoundsTests
    {
        // 35 x 20 x 35 arena whose center is 20 m ahead at eye height.
        private static readonly ArenaBounds Arena =
            new ArenaBounds(new Vector3(0f, 1.2f, 20f), new Vector3(35f, 20f, 35f));

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-3f), $"expected {expected}, got {actual}");
        }

        [Test]
        public void RayFromOutside_StraightThrough_ExitsAtBackFace()
        {
            var ray = new Ray(new Vector3(0f, 1.2f, 0f), Vector3.forward);
            AssertNear(new Vector3(0f, 1.2f, 37.5f), Arena.AimFallback(ray));
        }

        [Test]
        public void RayFromOutside_Upward_ExitsAtCeiling()
        {
            // Aimed up 45 degrees from 2.5 m in front of the arena: leaves through the top.
            var ray = new Ray(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1f, 1f).normalized);
            Vector3 p = Arena.AimFallback(ray);
            Assert.AreEqual(11.2f, p.y, 1e-3f);
            Assert.AreEqual(10f, p.z, 1e-3f);
        }

        [Test]
        public void RayFromInside_ExitsWhereItLeaves()
        {
            var ray = new Ray(new Vector3(0f, 1.2f, 20f), Vector3.right);
            AssertNear(new Vector3(17.5f, 1.2f, 20f), Arena.AimFallback(ray));
        }

        [Test]
        public void RayMissingTheBox_ClampsTheFarPointIntoIt()
        {
            // Pointing straight back, away from the arena.
            var ray = new Ray(new Vector3(0f, 1.2f, 0f), Vector3.back);
            Vector3 p = Arena.AimFallback(ray);
            AssertNear(Arena.Clamp(p), p);
            Assert.AreEqual(2.5f, p.z, 1e-3f, "nearest arena face");
        }

        [Test]
        public void DisabledBounds_ReturnsFarPoint()
        {
            var ray = new Ray(Vector3.zero, Vector3.forward);
            AssertNear(new Vector3(0f, 0f, 80f), default(ArenaBounds).AimFallback(ray, 80f));
        }
    }
}
