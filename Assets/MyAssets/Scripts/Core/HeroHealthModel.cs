using System;
using UnityEngine;

namespace HandHero.Core
{
    [Serializable]
    public struct HealthParams
    {
        public float MaxHealth;
        [Tooltip("Seconds of slowed flight after each hit")]
        public float SlowDuration;
        [Tooltip("Flight max-speed multiplier while slowed")]
        [Range(0.1f, 1f)] public float SlowMultiplier;
        [Tooltip("Seconds from death to respawn")]
        public float RespawnDelay;

        // ADR 3 values, same as NetworkedPlayerHealth.
        public static HealthParams Default => new HealthParams
        {
            MaxHealth = 100f,
            SlowDuration = 2f,
            SlowMultiplier = 0.5f,
            RespawnDelay = 3f,
        };
    }

    public enum HitOutcome
    {
        Ignored, // dead already, or no damage
        Damaged,
        Killed,
    }

    // Hit rules from ADR 3 as plain state + timers: hit -> health down + slow;
    // 0 HP -> dead -> respawn after a delay. No knockback, ever.
    // Same semantics as NetworkedPlayerHealth (Networking~): ApplyDamage ==
    // ServerApplyDamage, Tick == the FixedUpdateNetwork respawn check, the
    // timers become TickTimers and the fields [Networked] when ported to Fusion.
    public class HeroHealthModel
    {
        private float _slowTimer;
        private float _respawnTimer;

        public HeroHealthModel(HealthParams p)
        {
            Params = p;
            Reset();
        }

        public HealthParams Params { get; set; }
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        // Bumped per hit (the networked version change-detects this for effects).
        public int DamageCount { get; private set; }

        public float Normalized => Params.MaxHealth > 0f ? CurrentHealth / Params.MaxHealth : 0f;

        // 1 normally, SlowMultiplier while the hit penalty runs.
        public float SpeedMultiplier => _slowTimer > 0f ? Params.SlowMultiplier : 1f;

        public void Reset()
        {
            CurrentHealth = Params.MaxHealth;
            IsDead = false;
            _slowTimer = 0f;
            _respawnTimer = 0f;
        }

        public HitOutcome ApplyDamage(float damage)
        {
            if (IsDead || damage <= 0f) return HitOutcome.Ignored;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            DamageCount++;
            _slowTimer = Params.SlowDuration;

            if (CurrentHealth > 0f) return HitOutcome.Damaged;

            IsDead = true;
            _respawnTimer = Params.RespawnDelay;
            return HitOutcome.Killed;
        }

        // Advances timers. Returns true on the tick the hero respawns.
        public bool Tick(float dt)
        {
            if (_slowTimer > 0f) _slowTimer -= dt;

            if (!IsDead) return false;

            _respawnTimer -= dt;
            if (_respawnTimer > 0f) return false;

            Reset();
            return true;
        }
    }
}
