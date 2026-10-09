using System;

namespace HandHero.Core
{
    public enum HudKeyKind
    {
        None,
        RunStatus,
        IntroBanner,
        EndBanner,
        MatchScore,
        MatchBanner,
    }

    // The integers a HUD line shows (round 3, P3 / GM-1…GM-5). The HUD rebuilds
    // and assigns its string only when this key changes, instead of every frame.
    public readonly struct HudKey : IEquatable<HudKey>
    {
        public static readonly HudKey None = default;

        public readonly HudKeyKind Kind;
        public readonly int A, B, C, D, E, F;

        public HudKey(HudKeyKind kind, int a = 0, int b = 0, int c = 0, int d = 0, int e = 0, int f = 0)
        {
            Kind = kind;
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
        }

        public bool Equals(HudKey o) =>
            Kind == o.Kind && A == o.A && B == o.B && C == o.C && D == o.D && E == o.E && F == o.F;

        public override bool Equals(object obj) => obj is HudKey o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind;
                h = h * 31 + A;
                h = h * 31 + B;
                h = h * 31 + C;
                h = h * 31 + D;
                h = h * 31 + E;
                return h * 31 + F;
            }
        }

        public static bool operator ==(HudKey a, HudKey b) => a.Equals(b);
        public static bool operator !=(HudKey a, HudKey b) => !a.Equals(b);

        public override string ToString() => $"{Kind}({A},{B},{C},{D},{E},{F})";
    }
}
