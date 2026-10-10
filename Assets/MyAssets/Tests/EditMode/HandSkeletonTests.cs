using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Round 5, T4 (D6): the joint layout GhostHands draws (XR Hands joint indices).
    public class HandSkeletonTests
    {
        [Test]
        public void Chains_UseValidJointIndices_AndAtLeastOneBoneEach()
        {
            foreach (int[] chain in HandSkeleton.Chains)
            {
                Assert.GreaterOrEqual(chain.Length, 2);
                foreach (int joint in chain)
                {
                    Assert.GreaterOrEqual(joint, 0);
                    Assert.Less(joint, HandSkeleton.JointCount);
                }
            }
        }

        [Test]
        public void EveryJointButThePalm_IsOnALine()
        {
            var drawn = new HashSet<int>();
            foreach (int[] chain in HandSkeleton.Chains)
                foreach (int joint in chain)
                    drawn.Add(joint);

            for (int j = 0; j < HandSkeleton.JointCount; j++)
            {
                if (j == HandSkeleton.Palm) Assert.IsFalse(drawn.Contains(j), "the palm is a sphere only");
                else Assert.IsTrue(drawn.Contains(j), $"joint {j} is on no line");
            }
        }

        [Test]
        public void FingerChains_RunFromTheWristToEachTip()
        {
            var tips = new HashSet<int>();
            for (int c = 0; c < 5; c++)
            {
                int[] chain = HandSkeleton.Chains[c];
                Assert.AreEqual(HandSkeleton.Wrist, chain[0]);
                int tip = chain[chain.Length - 1];
                Assert.IsTrue(HandSkeleton.IsTip(tip), $"chain {c} ends at {tip}");
                tips.Add(tip);
            }
            Assert.AreEqual(5, tips.Count, "one chain per finger");
        }

        [Test]
        public void TwentyFourFingerBones_ThreeKnuckleBones_NoneTwice()
        {
            var bones = new HashSet<(int, int)>();
            int fingerBones = 0;
            for (int c = 0; c < HandSkeleton.Chains.Length; c++)
            {
                int[] chain = HandSkeleton.Chains[c];
                for (int i = 1; i < chain.Length; i++)
                {
                    int a = System.Math.Min(chain[i - 1], chain[i]);
                    int b = System.Math.Max(chain[i - 1], chain[i]);
                    Assert.IsTrue(bones.Add((a, b)), $"bone {a}-{b} drawn twice");
                    if (c < 5) fingerBones++;
                }
            }
            Assert.AreEqual(24, fingerBones);
            Assert.AreEqual(27, bones.Count);
        }
    }
}
