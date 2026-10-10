using System;
using UnityEngine;

namespace HandHero.Core
{
    // How a bot moves with one attack pattern (round 5, T2 / D4). BotBrain reads
    // it in its movement part only; the bot still flies through the same
    // HandInputData -> clutch -> flight model path with the same hand speed cap
    // as the player (ADR fairness). Every zero value reads as Striker's (today's
    // bot), so an archetype serialized before round 5 moves exactly as before.
    [Serializable]
    public struct BotMovement
    {
        public const float DefaultArriveRadius = 1f;
        public const float DefaultDashTimeout = 1.5f;

        [Tooltip("x BotParams.PreferredRange: the distance kept from the player; 0 reads as 1")]
        public float RangeMult;
        [Tooltip("Strafe on the range sphere so the distance stays at the range; off = today's lead point beyond it (Striker)")]
        public bool KeepRange;
        [Tooltip("x BotParams.StrafeLead: how hard the bot pulls sideways while strafing; 0 reads as 1")]
        public float StrafeMult;
        [Tooltip("x BotParams.StrafeSwitchInterval: seconds between strafe direction flips; 0 reads as 1")]
        public float StrafeSwitchMult;
        [Tooltip("x BotParams.VerticalWeave (strafe weave and dash height spread); 0 reads as 1")]
        public float WeaveMult;
        [Tooltip("Arc along the range sphere that one dash covers, meters; 0 = no dash (holds or strafes in place)")]
        public float DashDistance;
        [Tooltip("Before each attack: dash to a new spot, then hold still there while telegraphing and firing (Lancer)")]
        public bool DashBeforeAttack;
        [Tooltip("Seconds before the attack is due that the dash before it sets off, so the telegraph starts about on time (about one dash); 0 = when it is due")]
        public float DashLeadTime;
        [Tooltip("After each attack's last shot: dash to a new spot, then strafe on (Sniper, Lancer)")]
        public bool DashAfterAttack;
        [Tooltip("A dash ends within this distance of its spot, meters; 0 reads as 1")]
        public float ArriveRadius;
        [Tooltip("A dash ends after this many seconds even when a wall stopped it short; 0 reads as 1.5")]
        public float DashTimeout;

        public float RangeScale => RangeMult > 0f ? RangeMult : 1f;
        public float StrafeScale => StrafeMult > 0f ? StrafeMult : 1f;
        public float SwitchScale => StrafeSwitchMult > 0f ? StrafeSwitchMult : 1f;
        public float WeaveScale => WeaveMult > 0f ? WeaveMult : 1f;
        public float ArriveDistance => ArriveRadius > 0f ? ArriveRadius : DefaultArriveRadius;
        public float DashTime => DashTimeout > 0f ? DashTimeout : DefaultDashTimeout;
    }

    // Where the player sits (round 5 deep review, DR-2). The arena's near wall is
    // only 2.5 m in front of the VR seat, so a bot circling the player's hero at
    // its range could fly beside the player or right in front of their face.
    // BotBrain keeps the strafe, approach, dash and evade points of a bot that
    // keeps its range (KeepRange: Sniper, Gunner, Lancer, the boss) where the seat
    // looks: at most MaxYawDegrees off the seat's forward and at least MinDistance
    // from the eye, both seen from above (heights are free). Striker (round 4's
    // bot) ignores it. Default (Enabled false) = no limit.
    public struct BotSeat
    {
        public bool Enabled;
        // World position of the seat's eye.
        public Vector3 Eye;
        // The way the seat faces (world; only its level part counts).
        public Vector3 Forward;
        public float MaxYawDegrees;
        // Level meters from the eye.
        public float MinDistance;

        public static BotSeat At(Vector3 eye, Vector3 forward, float maxYawDegrees, float minDistance)
        {
            return new BotSeat
            {
                Enabled = true,
                Eye = eye,
                Forward = forward,
                MaxYawDegrees = maxYawDegrees,
                MinDistance = minDistance,
            };
        }

        // Turns round the pivot tried each way (5 degrees each, up to half a turn),
        // then halvings that home in on the edge between the last two tries.
        private const int TurnSteps = 36;
        private const int EdgeHalvings = 10;

        // Inside the seat's view (always, when not Enabled).
        public bool Allows(Vector3 p)
        {
            if (!Enabled) return true;
            float dx = p.x - Eye.x;
            float dz = p.z - Eye.z;
            float distSq = dx * dx + dz * dz;
            float min = Mathf.Max(0f, MinDistance);
            if (distSq < min * min) return false;
            if (distSq < 1e-8f) return true; // on the eye with no distance rule: no bearing to judge
            LevelForward(out float fx, out float fz);
            return (dx * fx + dz * fz) / Mathf.Sqrt(distSq) >= Mathf.Cos(MaxYaw * Mathf.Deg2Rad);
        }

        // `p` when the seat allows it; else `p` turned about `pivot`'s vertical to
        // the nearest bearing the seat allows (same height, same level distance
        // from the pivot); when no bearing on that circle is allowed, `p` moved to
        // the edge of the view, at least MinDistance from the eye. No allocation.
        public Vector3 Limit(Vector3 p, Vector3 pivot)
        {
            return Limit(p, pivot, 0f);
        }

        // The same, turning toward `turnSign` first (+1 = the way a BotBrain
        // strafe sign of +1 goes round, Vector3.Cross(up, radial)): all the way
        // round that way before the other. A strafing bot turns back the way it
        // came, so it never cuts across the part of its circle out of view.
        // 0 = the nearest bearing either way.
        public Vector3 Limit(Vector3 p, Vector3 pivot, float turnSign)
        {
            if (Allows(p)) return p;

            float rx = p.x - pivot.x;
            float rz = p.z - pivot.z;
            if (rx * rx + rz * rz > 1e-6f)
            {
                float first = turnSign < 0f ? -1f : 1f;
                if (turnSign == 0f)
                {
                    for (int k = 1; k <= TurnSteps; k++)
                    {
                        if (TryTurn(p, pivot, first, k, out Vector3 a)) return a;
                        if (TryTurn(p, pivot, -first, k, out Vector3 b)) return b;
                    }
                }
                else
                {
                    for (int k = 1; k <= TurnSteps; k++)
                        if (TryTurn(p, pivot, first, k, out Vector3 a)) return a;
                    for (int k = 1; k <= TurnSteps; k++)
                        if (TryTurn(p, pivot, -first, k, out Vector3 b)) return b;
                }
            }
            return EdgeOfView(p);
        }

        // Try `k` (k steps toward `sign`; the try before it was out of view): when
        // it is in view, the edge between the two tries.
        private bool TryTurn(Vector3 p, Vector3 pivot, float sign, int k, out Vector3 result)
        {
            const float step = Mathf.PI / TurnSteps;
            result = p;
            if (!Allows(Turn(p, pivot, sign * k * step))) return false;
            float outside = (k - 1) * step;
            float inside = k * step;
            for (int i = 0; i < EdgeHalvings; i++)
            {
                float mid = 0.5f * (outside + inside);
                if (Allows(Turn(p, pivot, sign * mid))) inside = mid;
                else outside = mid;
            }
            result = Turn(p, pivot, sign * inside);
            return true;
        }

        private float MaxYaw => Mathf.Clamp(MaxYawDegrees, 0f, 180f);

        private void LevelForward(out float fx, out float fz)
        {
            float len = Mathf.Sqrt(Forward.x * Forward.x + Forward.z * Forward.z);
            if (len < 1e-6f)
            {
                fx = 0f;
                fz = 1f;
                return;
            }
            fx = Forward.x / len;
            fz = Forward.z / len;
        }

        // `p` turned `radians` about `pivot`'s vertical (same height).
        private static Vector3 Turn(Vector3 p, Vector3 pivot, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            float rx = p.x - pivot.x;
            float rz = p.z - pivot.z;
            return new Vector3(pivot.x + rx * c + rz * s, p.y, pivot.z + rz * c - rx * s);
        }

        // No bearing round the pivot is in view: `p` turned about the eye onto the
        // nearer edge of the view (just inside it), then out to MinDistance.
        private Vector3 EdgeOfView(Vector3 p)
        {
            LevelForward(out float fx, out float fz);
            float dx = p.x - Eye.x;
            float dz = p.z - Eye.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float ux = fx, uz = fz;
            if (dist > 1e-4f)
            {
                ux = dx / dist;
                uz = dz / dist;
            }

            float edge = Mathf.Max(0f, MaxYaw - 0.01f) * Mathf.Deg2Rad;
            if (ux * fx + uz * fz < Mathf.Cos(edge))
            {
                // Both edges of the view; the one nearer the point's own bearing.
                float c = Mathf.Cos(edge);
                float s = Mathf.Sin(edge);
                float ax = fx * c + fz * s, az = fz * c - fx * s;
                float bx = fx * c - fz * s, bz = fz * c + fx * s;
                bool a = ux * ax + uz * az >= ux * bx + uz * bz;
                ux = a ? ax : bx;
                uz = a ? az : bz;
            }

            float reach = Mathf.Max(dist, Mathf.Max(0f, MinDistance) + 1e-3f);
            return new Vector3(Eye.x + ux * reach, p.y, Eye.z + uz * reach);
        }
    }

    // Movement personalities (round 5, T2 / D4; first guesses, tuned on device).
    // BotArchetypes.Get pairs them with the attack patterns.
    public static class BotMovements
    {
        // Today's bot: circle at PreferredRange with a lead, weave, flip.
        public static BotMovement Striker => new BotMovement
        {
            RangeMult = 1f,
            KeepRange = false,
            StrafeMult = 1f,
            StrafeSwitchMult = 1f,
            WeaveMult = 1f,
            DashDistance = 0f,
            DashBeforeAttack = false,
            DashLeadTime = 0f,
            DashAfterAttack = false,
            ArriveRadius = BotMovement.DefaultArriveRadius,
            DashTimeout = BotMovement.DefaultDashTimeout,
        };

        // Keeps far, barely strafes, and moves to a new spot after each shot.
        public static BotMovement Sniper
        {
            get
            {
                BotMovement m = Striker;
                m.RangeMult = 1.5f;
                m.KeepRange = true;
                m.StrafeMult = 0.4f;
                m.StrafeSwitchMult = 1.5f;
                m.WeaveMult = 0.5f;
                m.DashDistance = 8f;
                m.DashAfterAttack = true;
                return m;
            }
        }

        // Keeps close and strafes wide and often.
        public static BotMovement Gunner
        {
            get
            {
                BotMovement m = Striker;
                m.RangeMult = 0.7f;
                m.KeepRange = true;
                m.StrafeMult = 1.6f;
                m.StrafeSwitchMult = 0.7f;
                m.WeaveMult = 1.25f;
                return m;
            }
        }

        // Dashes to a new spot, holds still through its telegraph and shot, dashes again.
        public static BotMovement Lancer
        {
            get
            {
                BotMovement m = Striker;
                m.KeepRange = true;
                m.DashDistance = 10f;
                m.DashBeforeAttack = true;
                // A 10 m dash takes ~0.9 s, so the attack rate stays round 4's.
                m.DashLeadTime = 0.9f;
                m.DashAfterAttack = true;
                return m;
            }
        }

        // The movement for an archetype's Attack (the boss starts with Gunner bursts).
        public static BotMovement For(BotArchetypeId id)
        {
            switch (id)
            {
                case BotArchetypeId.Sniper: return Sniper;
                case BotArchetypeId.Gunner: return Gunner;
                case BotArchetypeId.Lancer: return Lancer;
                case BotArchetypeId.Boss: return Gunner;
                default: return Striker;
            }
        }

        // The movement for an archetype's AltAttack (the boss's Lancer shots).
        public static BotMovement AltFor(BotArchetypeId id)
        {
            return id == BotArchetypeId.Boss ? Lancer : For(id);
        }
    }
}
