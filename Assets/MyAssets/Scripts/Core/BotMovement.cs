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
