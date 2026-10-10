using System;
using UnityEngine;

namespace HandHero.Core
{
    public enum BotArchetypeId
    {
        Striker, // today's bot
        Sniper,
        Gunner,
        Lancer,
        Boss,
    }

    // Archetype silhouette parts the RunBot prefab carries (bit = RunBot.shapes index).
    [Flags]
    public enum BotShape
    {
        None = 0,
        Needle = 1 << 0, // Sniper: tall needle + thin barrel
        Block = 1 << 1,  // Gunner: chunky wide block
        Lance = 1 << 2,  // Lancer: long lance forward
    }

    // How a bot telegraphs and fires one attack (round 4, S6 / D7). Movement,
    // health and the hit receiver never read it (bots keep the player's rules).
    // Striker = today's bot: every multiplier 1, one shot, its own colors.
    [Serializable]
    public struct BotAttack
    {
        [Tooltip("x BotParams.TelegraphTime")]
        public float TelegraphMult;
        [Tooltip("x FireInterval and its jitter, counted from the attack's last shot")]
        public float IntervalMult;
        [Tooltip("x shot damage, on top of the island's damage scaling")]
        public float DamageMult;
        [Tooltip("Shots per telegraph; every shot after the first re-aims at the target")]
        public int BurstCount;
        [Tooltip("Seconds between burst shots (not below the player's 0.35 s shot cooldown)")]
        public float BurstGap;
        [Tooltip("x beam width (the charge shot's full width is 4)")]
        public float BeamWidthMult;
        [Tooltip("x seconds the beam stays visible")]
        public float BeamDurationMult;
        [Tooltip("Beam color; alpha 0 = the bot's own beam color")]
        public Color BeamColor;
        [Tooltip("x telegraph line width")]
        public float TelegraphWidthMult;
        [Tooltip("Pitch of the enemy fire sound (lower = longer, heavier)")]
        public float FirePitch;
    }

    [Serializable]
    public struct BotArchetype
    {
        [Tooltip("Which archetype this entry tunes (RunDirector looks entries up by it)")]
        public BotArchetypeId Id;
        [Tooltip("Body tint; alpha 0 = the bot material's color")]
        public Color BodyColor;
        [Tooltip("Silhouette parts shown on the run bot (Needle, Block, Lance); None = the plain bot")]
        public BotShape Shapes;
        [Tooltip("How it telegraphs and fires (the boss's first pattern)")]
        public BotAttack Attack;
        [Tooltip("Second pattern, used only when SwitchEvery > 0 (the boss)")]
        public BotAttack AltAttack;
        [Tooltip("Attacks before switching between Attack and AltAttack; 0 = never")]
        public int SwitchEvery;
    }

    // The four archetypes plus the boss (first guesses, round 4; tuned on device).
    // RunDirector serializes a copy (Defaults) so the numbers are tunable in the scene.
    public static class BotArchetypes
    {
        public static BotAttack StrikerAttack => new BotAttack
        {
            TelegraphMult = 1f,
            IntervalMult = 1f,
            DamageMult = 1f,
            BurstCount = 1,
            BurstGap = 0f,
            BeamWidthMult = 1f,
            BeamDurationMult = 1f,
            BeamColor = Color.clear,
            TelegraphWidthMult = 1f,
            FirePitch = 1f,
        };

        // Long warning, slow heavy shot, a thin beam that lingers.
        public static BotAttack SniperAttack
        {
            get
            {
                BotAttack a = StrikerAttack;
                a.TelegraphMult = 1.6f;
                a.IntervalMult = 1.8f;
                a.DamageMult = 1.8f;
                a.BeamWidthMult = 0.5f;
                a.BeamDurationMult = 4f;
                a.BeamColor = new Color(0.8f, 0.7f, 1f);
                a.TelegraphWidthMult = 0.5f;
                a.FirePitch = 0.7f;
                return a;
            }
        }

        // One short warning, then three light shots that follow the target.
        public static BotAttack GunnerAttack
        {
            get
            {
                BotAttack a = StrikerAttack;
                a.TelegraphMult = 0.8f;
                a.DamageMult = 0.45f;
                a.BurstCount = 3;
                // The player's shot cooldown is 0.35 s; a shorter gap would only buffer.
                a.BurstGap = 0.36f;
                a.BeamWidthMult = 0.8f;
                // Round 5 (F3-4): was the CURSOR aim orange (1, 0.6, 0.15); now chartreuse
                // (hue 90 deg), far from the target and cursor oranges (25-32 deg) and
                // 30 deg from the yellow ASSIST reticle (60 deg).
                a.BeamColor = new Color(0.6f, 1f, 0.2f);
                a.FirePitch = 1.35f;
                return a;
            }
        }

        // A long wide warning and one slow, wide, heavy shot: dodge it.
        public static BotAttack LancerAttack
        {
            get
            {
                BotAttack a = StrikerAttack;
                a.TelegraphMult = 1.8f;
                a.IntervalMult = 2.5f;
                a.DamageMult = 2.2f;
                a.BeamWidthMult = 4f;
                a.BeamDurationMult = 2f;
                a.BeamColor = new Color(1f, 0.3f, 0.8f);
                a.TelegraphWidthMult = 2.5f;
                a.FirePitch = 0.55f;
                return a;
            }
        }

        public static BotArchetype Get(BotArchetypeId id)
        {
            switch (id)
            {
                case BotArchetypeId.Sniper:
                    return new BotArchetype
                    {
                        Id = id, BodyColor = new Color(0.62f, 0.45f, 0.95f), Shapes = BotShape.Needle,
                        Attack = SniperAttack, AltAttack = SniperAttack,
                    };
                case BotArchetypeId.Gunner:
                    // Round 5 (F3-4): chartreuse (hue ~87 deg), not the old orange (1, 0.5, 0.1)
                    // that matched the greybox targets and the CURSOR aim marker.
                    return new BotArchetype
                    {
                        Id = id, BodyColor = new Color(0.6f, 1f, 0.1f), Shapes = BotShape.Block,
                        Attack = GunnerAttack, AltAttack = GunnerAttack,
                    };
                case BotArchetypeId.Lancer:
                    return new BotArchetype
                    {
                        Id = id, BodyColor = new Color(0.95f, 0.2f, 0.65f), Shapes = BotShape.Lance,
                        Attack = LancerAttack, AltAttack = LancerAttack,
                    };
                case BotArchetypeId.Boss:
                    // Gunner bursts and Lancer shots, two of each in turn.
                    return new BotArchetype
                    {
                        Id = id, BodyColor = new Color(0.55f, 0.05f, 0.1f), Shapes = BotShape.Block | BotShape.Lance,
                        Attack = GunnerAttack, AltAttack = LancerAttack, SwitchEvery = 2,
                    };
                default:
                    return new BotArchetype
                    {
                        Id = BotArchetypeId.Striker, BodyColor = Color.clear, Shapes = BotShape.None,
                        Attack = StrikerAttack, AltAttack = StrikerAttack,
                    };
            }
        }

        // Every archetype once, indexed by BotArchetypeId.
        public static BotArchetype[] Defaults()
        {
            var all = new BotArchetype[5];
            for (int i = 0; i < all.Length; i++) all[i] = Get((BotArchetypeId)i);
            return all;
        }

        // Literals (no enum ToString per hit) for run telemetry.
        public static string Name(BotArchetypeId id)
        {
            switch (id)
            {
                case BotArchetypeId.Sniper: return "Sniper";
                case BotArchetypeId.Gunner: return "Gunner";
                case BotArchetypeId.Lancer: return "Lancer";
                case BotArchetypeId.Boss: return "Boss";
                default: return "Striker";
            }
        }
    }
}
