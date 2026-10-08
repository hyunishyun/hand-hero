using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // Crystal economy of a run (autonomous plan R6, design doc §4, §6.4).
    // Every number is a share of CommonBasePrice, so a rebalance moves them together.
    // islandIndex is 0-based (island 1 = 0), like the enemy scaling in RunRules.
    public static class Economy
    {
        public const int CommonBasePrice = 100;

        public const float InflationPerIsland = 0.05f;
        public const float KillRewardShare = 0.1f;   // 10 crystals per kill
        public const float ClearRewardShare = 0.25f; // 25 crystals per clear
        public const float RerollShare = 0.25f;      // first reroll = 25
        public const float RerollGrowth = 1.5f;
        public const float SpikedHealthCost = 0.33f; // share of max health
        public const float SpikedHealthFloor = 1f;   // a spiked chest never kills

        // Float noise like 300 x 1.2 = 360.00006 must not round up to 361.
        private const double CeilEpsilon = 1e-4;

        public static float RarityMultiplier(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Epic: return 3f;
                case ItemRarity.Legendary: return 8f;
                case ItemRarity.Greed: return 2f;
                default: return 1f;
            }
        }

        public static double Inflation(int islandIndex) => 1.0 + InflationPerIsland * Math.Max(0, islandIndex);

        public static int Price(ItemDefinition item, int islandIndex)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return Ceil(CommonBasePrice * RarityMultiplier(item.Rarity) * Inflation(islandIndex));
        }

        public static int RerollPrice(int rerolls, int islandIndex)
        {
            return Ceil(CommonBasePrice * RerollShare * Inflation(islandIndex) * Math.Pow(RerollGrowth, Math.Max(0, rerolls)));
        }

        public static int KillReward(HeroStats stats)
        {
            return Round(CommonBasePrice * KillRewardShare * stats.CrystalGainMult);
        }

        // Paid on every clear: base + Paycheck, plus interest on the balance before the payout.
        public static int ClearReward(HeroStats stats, int balance)
        {
            double interest = stats.InterestRate * Math.Max(0, balance);
            return Round(CommonBasePrice * ClearRewardShare + stats.CrystalPerClear + interest);
        }

        // Health after opening a spiked chest: pay 33% of max health, never below 1.
        public static float SpikedHealthAfter(float currentHealth, float maxHealth)
        {
            return Math.Max(SpikedHealthFloor, currentHealth - SpikedHealthCost * maxHealth);
        }

        private static int Ceil(double value) => (int)Math.Ceiling(value - CeilEpsilon);

        private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    public class CrystalWallet
    {
        public int Balance { get; private set; }

        public void Add(int amount)
        {
            if (amount > 0) Balance += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }

    public class ShopSlot
    {
        public ShopSlot(ItemDefinition item, int price)
        {
            Item = item;
            Price = price;
        }

        public ItemDefinition Item { get; }
        public int Price { get; }
        public bool Sold { get; internal set; }
    }

    // The shop stop (design doc §4.3, §6.5): pedestals rolled from the Random
    // chest pool, priced by Economy; a reroll refills every pedestal for a
    // price that grows by RerollGrowth each time.
    public class Shop
    {
        public const int PedestalCount = 4;

        private readonly Inventory _inventory;
        private readonly int _islandIndex;
        private readonly Random _rng;
        private readonly List<ShopSlot> _slots = new List<ShopSlot>();

        public Shop(Inventory inventory, int islandIndex, Random rng)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _islandIndex = islandIndex;
            Restock();
        }

        public IReadOnlyList<ShopSlot> Slots => _slots;
        public int Rerolls { get; private set; }
        public int RerollPrice => Economy.RerollPrice(Rerolls, _islandIndex);

        public bool TryBuy(int index, CrystalWallet wallet)
        {
            if (wallet == null || index < 0 || index >= _slots.Count) return false;
            ShopSlot slot = _slots[index];
            if (slot.Sold || !_inventory.CanAdd(slot.Item.Id) || wallet.Balance < slot.Price) return false;
            wallet.TrySpend(slot.Price);
            _inventory.Add(slot.Item.Id);
            slot.Sold = true;
            return true;
        }

        public bool TryReroll(CrystalWallet wallet)
        {
            if (wallet == null || !wallet.TrySpend(RerollPrice)) return false;
            Rerolls++;
            Restock();
            return true;
        }

        private void Restock()
        {
            _slots.Clear();
            foreach (ItemDefinition item in ChestRoller.Roll(ChestType.Random, _inventory, PedestalCount, _rng))
                _slots.Add(new ShopSlot(item, Economy.Price(item, _islandIndex)));
        }
    }
}
