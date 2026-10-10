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
        private float _stunTimer;
        private float _stunMultiplier = 1f;

        public HeroHealthModel(HealthParams p)
        {
            Params = p;
            Reset();
        }

        public HealthParams Params { get; set; }
        // Off during a run (BR-5, BC-1): a dead hero waits for Revive / Reset
        // instead of coming back for free after RespawnDelay.
        public bool AutoRespawn { get; set; } = true;
        // Demo mode (round 5, T4 / D6): hits still count (DamageCount, the slow
        // penalty, so every hit effect plays) but health never drops and the hero
        // never dies. A mode flag like AutoRespawn: Reset keeps it.
        public bool Invulnerable { get; set; }
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        // Bumped per hit (the networked version change-detects this for effects).
        public int DamageCount { get; private set; }

        public float Normalized => Params.MaxHealth > 0f ? CurrentHealth / Params.MaxHealth : 0f;

        public bool IsStunned => _stunTimer > 0f;

        // 1 normally, SlowMultiplier while the hit penalty runs; a shockwave stun
        // can slow further (the stronger slow wins, they don't stack).
        public float SpeedMultiplier
        {
            get
            {
                float m = _slowTimer > 0f ? Params.SlowMultiplier : 1f;
                return IsStunned ? Mathf.Min(m, _stunMultiplier) : m;
            }
        }

        public void Reset()
        {
            CurrentHealth = Params.MaxHealth;
            IsDead = false;
            _slowTimer = 0f;
            _respawnTimer = 0f;
            _stunTimer = 0f;
        }

        // Shockwave (T5, Q7): slow/stagger with no damage and no knockback.
        // Restarts the stun; ignored while dead.
        public void ApplyStun(float duration, float multiplier)
        {
            if (IsDead || duration <= 0f) return;
            _stunTimer = duration;
            _stunMultiplier = Mathf.Clamp01(multiplier);
        }

        public HitOutcome ApplyDamage(float damage)
        {
            if (IsDead || damage <= 0f) return HitOutcome.Ignored;
            if (Invulnerable)
            {
                DamageCount++;
                _slowTimer = Params.SlowDuration;
                return HitOutcome.Damaged;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            DamageCount++;
            _slowTimer = Params.SlowDuration;

            if (CurrentHealth > 0f) return HitOutcome.Damaged;

            IsDead = true;
            _respawnTimer = Params.RespawnDelay;
            return HitOutcome.Killed;
        }

        // Run items (R7) change the max mid-run: a gain also heals by the gain,
        // a loss only clamps. The dead stay dead (and at 0).
        public void ChangeMaxHealth(float newMax)
        {
            HealthParams p = Params;
            float gain = newMax - p.MaxHealth;
            p.MaxHealth = newMax;
            Params = p;
            if (IsDead) return;
            CurrentHealth = Mathf.Min(newMax, CurrentHealth + Mathf.Max(0f, gain));
        }

        // Heal on island clear (R5/R8). Capped at max; ignored while dead.
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(Params.MaxHealth, CurrentHealth + amount);
        }

        // Carry-over and the spiked chest (R8): sets health without ever killing.
        public void SetHealth(float health)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Clamp(health, 1f, Params.MaxHealth);
        }

        // Advances timers. Returns true on the tick the hero respawns.
        public bool Tick(float dt)
        {
            if (_slowTimer > 0f) _slowTimer -= dt;
            if (_stunTimer > 0f) _stunTimer -= dt;

            if (!IsDead || !AutoRespawn) return false;

            _respawnTimer -= dt;
            if (_respawnTimer > 0f) return false;

            Reset();
            return true;
        }
    }
}
