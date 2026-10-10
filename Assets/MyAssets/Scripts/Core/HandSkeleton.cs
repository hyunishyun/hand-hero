namespace HandHero.Core
{
    // XR Hands joint layout (round 5, T4: GhostHands). 26 joints per hand, index =
    // XRHandJointIDUtility.ToIndex(id): 0 wrist, 1 palm, then each finger from its
    // metacarpal to its tip (the thumb has no intermediate joint):
    // thumb 2-5, index 6-10, middle 11-15, ring 16-20, little 21-25.
    // Core has no XR Hands reference, so the layout is plain indices here.
    public static class HandSkeleton
    {
        public const int JointCount = 26;
        public const int Wrist = 0;
        public const int Palm = 1;

        // One bone line per chain: each finger from the wrist to its tip, then the
        // knuckle bar across the proximal joints (index to little).
        public static readonly int[][] Chains =
        {
            new[] { 0, 2, 3, 4, 5 },
            new[] { 0, 6, 7, 8, 9, 10 },
            new[] { 0, 11, 12, 13, 14, 15 },
            new[] { 0, 16, 17, 18, 19, 20 },
            new[] { 0, 21, 22, 23, 24, 25 },
            new[] { 7, 12, 17, 22 },
        };

        public static bool IsTip(int joint)
        {
            return joint == 5 || joint == 10 || joint == 15 || joint == 20 || joint == 25;
        }
    }
}
