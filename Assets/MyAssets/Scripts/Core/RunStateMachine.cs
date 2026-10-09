using System;
using System.Collections.Generic;
using UnityEngine;

namespace HandHero.Core
{
    public enum RunPhase
    {
        Idle,          // no run (menu or Quick Match)
        Intro,         // countdown before each island, no control
        Island,        // fighting
        IslandCleared, // short "CLEARED" beat, then the reward chest
        OpenChest,     // pick 1 of the chest's items
        Shop,          // forced before island 5
        ChoosePortal,  // pick the next island + its reward chest
        Victory,       // boss down; stays until ReturnToMenu
        Defeat,        // died without a revive; stays until ReturnToMenu
    }

    [Serializable]
    public struct RunParams
    {
        [Tooltip("Seconds of countdown before every island")]
        public float IntroTime;
        [Tooltip("Seconds the CLEARED beat shows before the chest opens")]
        public float ClearedTime;
        [Tooltip("Seconds to survive on a Horde island")]
        public float HordeTime;
        [Tooltip("Health healed on every clear, before HealOnClear items")]
        public float BaseHealOnClear;
        [Tooltip("Bot max health grows by this share per island (island 1 = x1)")]
        public float EnemyHealthPerIsland;
        [Tooltip("Bots alive at once on Arena and Horde islands")]
        public int MaxAlive;
        public float EliteHealthMult;
        public float EliteDamageMult;
        public float BossHealthMult;
        [Tooltip("Boss fire interval multiplier (< 1 = fires more often)")]
        public float BossFireIntervalMult;
        [Tooltip("Share of max health a revive brings back")]
        [Range(0.1f, 1f)] public float ReviveHealthFraction;

        public static RunParams Default => new RunParams
        {
            IntroTime = 3f,
            ClearedTime = 2f,
            HordeTime = 45f,
            BaseHealOnClear = 25f,
            EnemyHealthPerIsland = 0.15f,
            MaxAlive = 2,
            EliteHealthMult = 3f,
            EliteDamageMult = 1.5f,
            BossHealthMult = 6f,
            BossFireIntervalMult = 0.7f,
            ReviveHealthFraction = 0.5f,
        };
    }

    // What the run director spawns on an island.
    public struct IslandSpec
    {
        public IslandType Type;
        public int BotCount;          // Arena / Elite / Boss: bots to defeat; Horde: unlimited (respawn)
        public int MaxAlive;
        public float HealthMult;      // x bot max health
        public float DamageMult;      // x bot shot damage
        public float FireIntervalMult;
    }

    public static class RunRules
    {
        public const int IslandCount = 9;
        public const int BossIsland = 9;
        public const int ShopBeforeIsland = 5;

        // island: 1-based. Enemy scaling uses islandIndex = island - 1, so island 1 is unscaled.
        public static IslandSpec Island(int island, IslandType type, RunParams p)
        {
            float scale = 1f + p.EnemyHealthPerIsland * (island - 1);
            var spec = new IslandSpec
            {
                Type = type,
                BotCount = 1,
                MaxAlive = 1,
                HealthMult = scale,
                DamageMult = 1f,
                FireIntervalMult = 1f,
            };
            switch (type)
            {
                case IslandType.Arena:
                    spec.BotCount = island <= 2 ? 1 : island <= 5 ? 2 : 3;
                    spec.MaxAlive = Mathf.Min(spec.BotCount, Mathf.Max(1, p.MaxAlive));
                    break;
                case IslandType.Horde:
                    spec.BotCount = 0;
                    spec.MaxAlive = Mathf.Max(1, p.MaxAlive); // 0 would never spawn (BC-7)
                    break;
                case IslandType.Elite:
                    spec.HealthMult = scale * p.EliteHealthMult;
                    spec.DamageMult = p.EliteDamageMult;
                    break;
                case IslandType.Boss:
                    spec.HealthMult = scale * p.BossHealthMult;
                    spec.FireIntervalMult = p.BossFireIntervalMult;
                    break;
            }
            return spec;
        }
    }

    // Single-player RUN mode (autonomous plan R5, decisions Q2/Q3):
    // Idle -> Intro -> Island -> IslandCleared -> OpenChest -> (Shop) -> ChoosePortal
    // -> Intro ... -> island 9 Boss -> Victory, or Defeat on a death without revives.
    // The chest opened after a clear is the reward of the portal that led to the
    // island (PortalRoller); island 1 is fixed, island 9 has no portal choice.
    // Player health lives on the hero; the director asks this class how much to
    // heal after a clear and whether a death is revived.
    public class RunStateMachine
    {
        // The boss island's chest is never opened: clearing it wins the run.
        public static readonly Portal BossPortal = new Portal(IslandType.Boss, ChestType.Epic);

        private readonly System.Random _rng;
        private float _phaseTime;
        private int _islandKills;
        private int _revivesUsed;
        private readonly HeroStatsCache _statsCache = new HeroStatsCache();
        private List<ItemDefinition> _chestChoices = new List<ItemDefinition>();
        private List<Portal> _portals = new List<Portal>();

        public RunStateMachine(RunParams p, System.Random rng)
        {
            Params = p;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Inventory = new Inventory(ItemCatalog.Get);
            Phase = RunPhase.Idle;
        }

        public event Action<RunPhase> PhaseChanged;
        public event Action<bool> PausedChanged;

        public RunParams Params { get; set; }
        public RunPhase Phase { get; private set; }
        public bool IsPaused { get; private set; }
        public Inventory Inventory { get; private set; }
        public CrystalWallet Crystals { get; private set; } = new CrystalWallet();
        // Open only in the Shop phase.
        public Shop CurrentShop { get; private set; }
        public HeroStats Stats => _statsCache.Get(Inventory);

        // 1-based island number (1..9).
        public int Island { get; private set; }
        public Portal CurrentPortal { get; private set; }
        public IslandSpec Spec { get; private set; }
        public int IslandsCleared { get; private set; }
        public int Kills { get; private set; }
        // Unpaused seconds since StartRun, frozen at Victory / Defeat.
        public float RunTime { get; private set; }

        public float PhaseTime => _phaseTime;
        public float PhaseRemaining => Mathf.Max(0f, PhaseDuration(Phase) - _phaseTime);

        // Arena / Elite / Boss: bots still to defeat. Horde: 0.
        public int BotsRemaining => Spec.Type == IslandType.Horde ? 0 : Mathf.Max(0, Spec.BotCount - _islandKills);

        public ChestType OpenedChest => CurrentPortal.Chest;
        public IReadOnlyList<ItemDefinition> ChestChoices => _chestChoices;
        public IReadOnlyList<Portal> Portals => _portals;
        public int RevivesLeft => Mathf.Max(0, Stats.Revives - _revivesUsed);

        public bool StartRun()
        {
            if (Phase != RunPhase.Idle) return false;
            Inventory = new Inventory(ItemCatalog.Get);
            Crystals = new CrystalWallet();
            CurrentShop = null;
            IslandsCleared = 0;
            Kills = 0;
            RunTime = 0f;
            _revivesUsed = 0;
            BeginIsland(1, PortalRoller.FirstIsland);
            return true;
        }

        public void ReturnToMenu()
        {
            SetPaused(false);
            _chestChoices.Clear();
            _portals.Clear();
            CurrentShop = null;
            Enter(RunPhase.Idle);
        }

        // A bot died on the island. Counts for crystals in every island type;
        // clears Arena / Elite / Boss once every bot is down.
        public bool ReportBotKilled()
        {
            // Counted while paused too (BR-8): the kill happened.
            if (Phase != RunPhase.Island) return false;
            Kills++;
            _islandKills++;
            Crystals.Add(Economy.KillReward(Stats));
            if (Spec.Type != IslandType.Horde && _islandKills >= Spec.BotCount) ClearIsland();
            return true;
        }

        // Should the director spawn another bot, given how many are alive now?
        public bool WantsSpawn(int alive)
        {
            if (Phase != RunPhase.Island || alive >= Spec.MaxAlive) return false;
            return Spec.Type == IslandType.Horde || _islandKills + alive < Spec.BotCount;
        }

        // The player died during the run. Returns true if a revive saved the run
        // (heal to ReviveHealth); false = ignored (no run, or already over) or Defeat.
        // Resolved in every live phase and while paused (BC-1): a lethal hit is a
        // fact, and with auto respawn off nothing else would bring the hero back.
        public bool ReportPlayerDeath()
        {
            if (Phase == RunPhase.Idle || Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return false;
            if (RevivesLeft > 0)
            {
                _revivesUsed++;
                return true;
            }
            Enter(RunPhase.Defeat);
            return false;
        }

        public float ReviveHealth(float maxHealth) => maxHealth * Params.ReviveHealthFraction;

        // Player health after a clear: carry-over + base heal + HealOnClear, capped at max.
        public float HealAfterClear(float current, float maxHealth)
        {
            return Mathf.Min(maxHealth, current + Params.BaseHealOnClear + Stats.HealOnClear);
        }

        public bool PickChestItem(int index)
        {
            if (Phase != RunPhase.OpenChest || index < 0 || index >= _chestChoices.Count) return false;
            if (!Inventory.Add(_chestChoices[index].Id)) return false;
            _chestChoices.Clear();
            AfterChest();
            return true;
        }

        public bool BuyShopItem(int index)
        {
            return Phase == RunPhase.Shop && !IsPaused && CurrentShop.TryBuy(index, Crystals);
        }

        public bool RerollShop()
        {
            return Phase == RunPhase.Shop && !IsPaused && CurrentShop.TryReroll(Crystals);
        }

        public bool LeaveShop()
        {
            if (Phase != RunPhase.Shop) return false;
            CurrentShop = null;
            OfferPortals();
            return true;
        }

        public bool ChoosePortal(int index)
        {
            if (Phase != RunPhase.ChoosePortal || index < 0 || index >= _portals.Count) return false;
            Portal chosen = _portals[index];
            _portals.Clear();
            BeginIsland(Island + 1, chosen);
            return true;
        }

        // Debug / test hook: jump to an island and restart its intro.
        public void DebugSetIsland(int island, Portal portal)
        {
            if (Phase == RunPhase.Idle) return;
            BeginIsland(Mathf.Clamp(island, 1, RunRules.IslandCount), portal);
        }

        public bool Pause()
        {
            if (IsPaused || Phase == RunPhase.Idle || Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return false;
            SetPaused(true);
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused) return false;
            SetPaused(false);
            return true;
        }

        public bool TogglePause() => IsPaused ? Resume() : Pause();

        public void Tick(float dt)
        {
            if (IsPaused || Phase == RunPhase.Idle || Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return;
            _phaseTime += dt;
            RunTime += dt;

            switch (Phase)
            {
                case RunPhase.Intro:
                    if (Expired()) Enter(RunPhase.Island);
                    break;
                case RunPhase.Island:
                    if (Spec.Type == IslandType.Horde && Expired()) ClearIsland();
                    break;
                case RunPhase.IslandCleared:
                    if (!Expired()) break;
                    if (Island >= RunRules.BossIsland) Enter(RunPhase.Victory);
                    else OpenChest();
                    break;
            }
        }

        private void BeginIsland(int island, Portal portal)
        {
            Island = island;
            CurrentPortal = portal;
            Spec = RunRules.Island(island, portal.Island, Params);
            _islandKills = 0;
            Enter(RunPhase.Intro);
        }

        private void ClearIsland()
        {
            IslandsCleared++;
            Crystals.Add(Economy.ClearReward(Stats, Crystals.Balance));
            Enter(RunPhase.IslandCleared);
        }

        private void OpenChest()
        {
            _chestChoices = ChestRoller.Roll(CurrentPortal.Chest, Inventory, _rng);
            if (_chestChoices.Count == 0)
            {
                AfterChest(); // nothing left to give: skip the chest
                return;
            }
            Enter(RunPhase.OpenChest);
        }

        private void AfterChest()
        {
            if (Island + 1 == RunRules.ShopBeforeIsland)
            {
                // Priced like the island it stands before (islandIndex = Island).
                CurrentShop = new Shop(Inventory, Island, _rng);
                Enter(RunPhase.Shop);
            }
            else OfferPortals();
        }

        private void OfferPortals()
        {
            int next = Island + 1;
            if (next >= RunRules.BossIsland)
            {
                BeginIsland(next, BossPortal);
                return;
            }
            _portals = PortalRoller.Roll(next, Inventory, _rng);
            Enter(RunPhase.ChoosePortal);
        }

        private bool Expired() => _phaseTime >= PhaseDuration(Phase);

        private float PhaseDuration(RunPhase phase)
        {
            switch (phase)
            {
                case RunPhase.Intro: return Params.IntroTime;
                case RunPhase.Island: return Spec.Type == IslandType.Horde ? Params.HordeTime : 0f;
                case RunPhase.IslandCleared: return Params.ClearedTime;
                default: return 0f;
            }
        }

        private void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            PausedChanged?.Invoke(paused);
        }

        private void Enter(RunPhase phase)
        {
            Phase = phase;
            _phaseTime = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
