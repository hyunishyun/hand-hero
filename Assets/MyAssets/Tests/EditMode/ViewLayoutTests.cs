using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // T10: VR arena vs passthrough tabletop. The XR Origin never moves or rotates;
    // tabletop scales it up and lowers the camera offset so the arena looks ~1 m wide on the table.
    public class ViewLayoutTests
    {
        private static readonly Vector3 ArenaCenter = new Vector3(0f, 2f, 20f);
        private const float ArenaWidth = 35f;

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
        {
            Assert.Less(Vector3.Distance(expected, actual), tolerance, $"expected {expected}, got {actual}");
        }

        [Test]
        public void Arena_IsLifeSizeAtEyeHeight()
        {
            ViewLayout layout = ViewLayout.Arena(1.2f);

            Assert.AreEqual(1f, layout.WorldScale);
            AssertNear(new Vector3(0f, 1.2f, 0f), layout.CameraOffsetLocal);
            AssertNear(new Vector3(0f, 0.8f, 20f), layout.ApparentFromEye(ArenaCenter));
        }

        [Test]
        public void Tabletop_PutsTheArenaCenterWhereAsked()
        {
            var p = TabletopParams.Default;
            ViewLayout layout = ViewLayout.Tabletop(ArenaCenter, ArenaWidth, p);

            AssertNear(p.CenterFromEye, layout.ApparentFromEye(ArenaCenter));
        }

        [Test]
        public void Tabletop_ArenaLooksTableWide()
        {
            var p = TabletopParams.Default;
            ViewLayout layout = ViewLayout.Tabletop(ArenaCenter, ArenaWidth, p);

            Vector3 left = layout.ApparentFromEye(ArenaCenter + Vector3.left * ArenaWidth * 0.5f);
            Vector3 right = layout.ApparentFromEye(ArenaCenter + Vector3.right * ArenaWidth * 0.5f);
            Assert.AreEqual(p.TableWidth, Vector3.Distance(left, right), 1e-4f);
            Assert.AreEqual(ArenaWidth / p.TableWidth, layout.WorldScale, 1e-4f);
        }

        [Test]
        public void Tabletop_FloorSitsBelowTheEyeAndInFront()
        {
            var p = TabletopParams.Default;
            ViewLayout layout = ViewLayout.Tabletop(ArenaCenter, ArenaWidth, p);

            // Arena floor (20 m tall arena) and the near edge (35 m deep).
            Vector3 floor = layout.ApparentFromEye(ArenaCenter + Vector3.down * 10f);
            Vector3 nearEdge = layout.ApparentFromEye(ArenaCenter + Vector3.back * 17.5f);
            Assert.Less(floor.y, -0.3f, "floor should be around table height, well below the eye");
            Assert.Greater(nearEdge.z, 0.1f, "nearest arena edge must stay in front of the face");
        }

        [Test]
        public void Tabletop_InvalidWidth_FallsBackToLifeSize()
        {
            var p = TabletopParams.Default;
            p.TableWidth = 0f;
            ViewLayout layout = ViewLayout.Tabletop(ArenaCenter, ArenaWidth, p);

            Assert.AreEqual(1f, layout.WorldScale);
        }
    }
}
