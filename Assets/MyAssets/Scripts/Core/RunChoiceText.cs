using UnityEngine;

namespace HandHero.Core
{
    // Labels, colors and layout of the RUN choice panels (autonomous plan R9):
    // portal buttons, chest item cards and shop pedestals. TextMeshPro rich text;
    // ASCII only so the default font atlas has every glyph.
    public static class RunChoiceText
    {
        private static readonly Color CommonColor = new Color(0.15f, 0.2f, 0.3f);
        private static readonly Color EpicColor = new Color(0.38f, 0.16f, 0.55f);
        private static readonly Color LegendaryColor = new Color(0.6f, 0.42f, 0.08f);
        private static readonly Color GreedColor = new Color(0.55f, 0.1f, 0.12f);

        public static string ChestName(ChestType chest) => chest.ToString().ToUpperInvariant() + " CHEST";

        public static string PortalLabel(Portal portal)
        {
            return portal.Island.ToString().ToUpperInvariant() + "\n<size=70%>" + ChestName(portal.Chest) + "</size>";
        }

        public static string PortalHeader(int nextIsland) => $"CHOOSE ISLAND {nextIsland}/{RunRules.IslandCount}";

        public static string ChestHeader(ChestType chest)
        {
            return chest == ChestType.Spiked
                ? ChestName(chest) + "  (-33% HP)  -  PICK ONE"
                : ChestName(chest) + "  -  PICK ONE";
        }

        // Card text: name, the level the pick brings, rarity word, one-line description.
        public static string ItemCard(ItemDefinition item, Inventory inventory)
        {
            return $"<b>{item.Name}</b>\n<size=75%>Lv {inventory.Level(item.Id) + 1}  {Rarity(item)}</size>\n"
                + $"<size=60%>{item.Description}</size>";
        }

        public static string ShopSlot(ShopSlot slot, Inventory inventory)
        {
            string status = slot.Sold ? "SOLD"
                : !inventory.CanAdd(slot.Item.Id) ? "OWNED"
                : $"Lv {inventory.Level(slot.Item.Id) + 1}  -  {slot.Price} CRYSTALS";
            return $"<b>{slot.Item.Name}</b>\n<size=75%>{status}</size>\n<size=60%>{slot.Item.Description}</size>";
        }

        public static bool CanBuy(ShopSlot slot, Inventory inventory, CrystalWallet wallet)
        {
            return !slot.Sold && inventory.CanAdd(slot.Item.Id) && wallet.Balance >= slot.Price;
        }

        public static string ShopHeader(int balance) => $"SHOP  -  {balance} CRYSTALS";

        public static string RerollLabel(int price) => $"REROLL\n<size=70%>{price} CRYSTALS</size>";

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Epic: return EpicColor;
                case ItemRarity.Legendary: return LegendaryColor;
                case ItemRarity.Greed: return GreedColor;
                default: return CommonColor;
            }
        }

        // Unavailable shop pedestals (sold, owned, too expensive).
        public static Color Dimmed(Color color) => Color.Lerp(color, Color.black, 0.6f);

        // Panel-local button centers: up to 3 in one centered row, 4 in a 2x2 grid
        // (a Big Chests chest), so the panel never grows wider than three buttons.
        public static Vector2[] Layout(int count, float spacingX, float spacingY)
        {
            var positions = new Vector2[Mathf.Max(0, count)];
            if (count == 4)
            {
                for (int i = 0; i < 4; i++)
                    positions[i] = new Vector2((i % 2 - 0.5f) * spacingX, (0.5f - i / 2) * spacingY);
                return positions;
            }
            for (int i = 0; i < count; i++)
                positions[i] = new Vector2((i - (count - 1) * 0.5f) * spacingX, 0f);
            return positions;
        }

        private static string Rarity(ItemDefinition item) => item.Rarity.ToString().ToUpperInvariant();
    }
}
