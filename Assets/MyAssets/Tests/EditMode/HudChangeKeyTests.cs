using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // HUD change keys (round 3, P3 / GM-1…GM-5): the HUD rebuilds a string only
    // when the integers it shows change.
    public class HudChangeKeyTests
    {
        [Test]
        public void Status_SameShownValues_SameKey()
        {
            HudKey a = RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 150, 79.6f, 120f);
            HudKey b = RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 150, 80.2f, 120f);
            Assert.AreEqual(a, b, "HP 79.6 and 80.2 both show 80");
        }

        [Test]
        public void Status_HordeSecondsWithinSameShownSecond_SameKey()
        {
            HudKey a = RunHudText.StatusKey(2, IslandType.Horde, true, 0, 31.9f, 0, 100f, 100f);
            HudKey b = RunHudText.StatusKey(2, IslandType.Horde, true, 0, 31.1f, 0, 100f, 100f);
            Assert.AreEqual(a, b);
            HudKey c = RunHudText.StatusKey(2, IslandType.Horde, true, 0, 30.9f, 0, 100f, 100f);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Status_EachShownValueChangesTheKey()
        {
            HudKey baseKey = RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 150, 80f, 120f);
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(4, IslandType.Arena, true, 2, 0f, 150, 80f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Elite, true, 2, 0f, 150, 80f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Arena, false, 2, 0f, 150, 80f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Arena, true, 1, 0f, 150, 80f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 151, 80f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 150, 79f, 120f));
            Assert.AreNotEqual(baseKey, RunHudText.StatusKey(3, IslandType.Arena, true, 2, 0f, 150, 80f, 130f));
        }

        [Test]
        public void Status_NegativeHealthShowsZero_SameKeyAsZero()
        {
            Assert.AreEqual(RunHudText.StatusKey(1, IslandType.Arena, false, 0, 0f, 0, 0f, 100f),
                RunHudText.StatusKey(1, IslandType.Arena, false, 0, 0f, 0, -12f, 100f));
        }

        [Test]
        public void Intro_ChangesOncePerShownSecond()
        {
            Assert.AreEqual(RunHudText.IntroKey(1, IslandType.Arena, 2.9f), RunHudText.IntroKey(1, IslandType.Arena, 2.1f));
            Assert.AreNotEqual(RunHudText.IntroKey(1, IslandType.Arena, 2.1f), RunHudText.IntroKey(1, IslandType.Arena, 1.9f));
            Assert.AreNotEqual(RunHudText.IntroKey(1, IslandType.Arena, 2f), RunHudText.IntroKey(2, IslandType.Arena, 2f));
        }

        [Test]
        public void KindsNeverCollide()
        {
            // Same payload ints, different line kinds.
            Assert.AreNotEqual(new HudKey(HudKeyKind.IntroBanner, 1, 0, 3), new HudKey(HudKeyKind.EndBanner, 1, 0, 3));
            Assert.AreNotEqual(HudKey.None, RunHudText.IntroKey(0, IslandType.Arena, 0f));
        }

        [Test]
        public void End_SameRunSameKey_DifferentResultDifferentKey()
        {
            Assert.AreEqual(RunHudText.EndKey(true, 9, 7, 412.3f), RunHudText.EndKey(true, 9, 7, 412.3f));
            Assert.AreNotEqual(RunHudText.EndKey(true, 9, 7, 412.3f), RunHudText.EndKey(false, 9, 7, 412.3f));
        }

        [Test]
        public void IslandName_IsCachedLiteral()
        {
            Assert.AreEqual("HORDE", RunHudText.IslandName(IslandType.Horde));
            Assert.AreSame(RunHudText.IslandName(IslandType.Boss), RunHudText.IslandName(IslandType.Boss));
        }
    }
}
