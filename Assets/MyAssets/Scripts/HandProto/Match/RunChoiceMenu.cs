using System.Collections.Generic;
using HandHero.Core;
using TMPro;
using UnityEngine;

// RUN choice panels (autonomous plan R9), world-fixed at the menu spot and
// pressed with the same point + pinch pointer as the menu (HandMenu turns the
// pointer on while IsShowing):
//   ChoosePortal -> portal panel: next island + its reward chest
//   OpenChest    -> chest panel: item cards (name, level after the pick, rarity color)
//   StartRelic   -> chest panel: STARTING RELIC, unlocked relic cards + a NONE card (S5)
//   Shop         -> shop panel: 4 pedestals with prices, REROLL, LEAVE
// The heroes are already out of control in these phases (RunDirector only
// enables them on an island), and the panels hide while the run is paused.
public class RunChoiceMenu : MonoBehaviour
{
    [SerializeField] private RunDirector run;

    [Header("Portal panel")]
    [SerializeField] private GameObject portalPanel;
    [SerializeField] private TMP_Text portalHeader;
    [SerializeField] private HandMenuButton[] portalButtons;

    [Header("Chest panel")]
    [SerializeField] private GameObject chestPanel;
    [SerializeField] private TMP_Text chestHeader;
    [SerializeField] private HandMenuButton[] chestCards;

    [Header("Shop panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private TMP_Text shopHeader;
    [SerializeField] private HandMenuButton[] shopSlots;
    [SerializeField] private HandMenuButton rerollButton;
    [SerializeField] private HandMenuButton leaveButton;

    [Header("Layout")]
    [Tooltip("Meters between button centers across a row (portal buttons, chest cards)")]
    [SerializeField] private float spacingX = 0.95f;
    [Tooltip("Meters between the two rows of a 4-card chest")]
    [SerializeField] private float spacingY = 0.68f;

    [Header("Input")]
    [Tooltip("Seconds a new panel ignores presses, so a quick second pinch can't pick on a panel not yet read")]
    [SerializeField] private float armDelay = 0.4f;

    private RunPhase _shownPhase = RunPhase.Idle;
    private bool _dirty;
    private float _armedAt;
    private int _shopKey;

    // True while a choice panel is up; HandMenu enables the pointer then.
    public bool IsShowing
    {
        get
        {
            RunStateMachine r = run != null ? run.Run : null;
            return r != null && !r.IsPaused && IsChoicePhase(r.Phase);
        }
    }

    private static bool IsChoicePhase(RunPhase phase)
    {
        return phase == RunPhase.ChoosePortal || phase == RunPhase.OpenChest || phase == RunPhase.Shop
            || phase == RunPhase.StartRelic;
    }

    private static bool UsesChestPanel(RunPhase phase) => phase == RunPhase.OpenChest || phase == RunPhase.StartRelic;

    // Chest cards serve the reward chest and the starting relic chest; in the
    // latter the card after the relics is NONE.
    private bool PickCard(int index)
    {
        RunStateMachine r = run.Run;
        if (r == null || r.Phase != RunPhase.StartRelic) return run.PickChestItem(index);
        return index < r.StartRelicChoices.Count ? run.PickStartRelic(index) : run.SkipStartRelic();
    }

    private void Awake()
    {
        for (int i = 0; i < Length(portalButtons); i++)
        {
            int index = i;
            if (portalButtons[i] != null)
                portalButtons[i].SetCustomAction(() => Choose(() => run.ChoosePortal(index), SfxId.PortalPick));
        }
        for (int i = 0; i < Length(chestCards); i++)
        {
            int index = i;
            if (chestCards[i] != null)
                chestCards[i].SetCustomAction(() => Choose(() => PickCard(index), SfxId.ItemPick));
        }
        for (int i = 0; i < Length(shopSlots); i++)
        {
            int index = i;
            if (shopSlots[i] != null)
                shopSlots[i].SetCustomAction(() => Choose(() => run.BuyShopItem(index), SfxId.ShopBuy));
        }
        if (rerollButton != null) rerollButton.SetCustomAction(() => Choose(() => run.RerollShop(), SfxId.Reroll));
        if (leaveButton != null) leaveButton.SetCustomAction(() => Choose(() => run.LeaveShop(), SfxId.MenuPress));
    }

    // A refused choice (not enough crystals, sold out, paused) plays the deny sound.
    private void Choose(System.Func<bool> choice, SfxId sound)
    {
        if (run == null || Time.unscaledTime < _armedAt) return;
        if (choice())
        {
            _dirty = true;
            SfxPlayer.PlayUi(sound);
        }
        else SfxPlayer.PlayUi(SfxId.Denied);
    }

    private void LateUpdate()
    {
        RunPhase phase = IsShowing ? run.Run.Phase : RunPhase.Idle;
        if (phase != _shownPhase)
        {
            _shownPhase = phase;
            _armedAt = Time.unscaledTime + armDelay;
            _dirty = true;
        }

        SetActive(portalPanel, phase == RunPhase.ChoosePortal);
        SetActive(chestPanel, UsesChestPanel(phase));
        SetActive(shopPanel, phase == RunPhase.Shop);

        // Debug keys buy and leave through RunDirector directly: catch those too.
        if (phase == RunPhase.Shop)
        {
            int shopKey = ShopKey(run.Run);
            if (shopKey != _shopKey) _dirty = true;
            _shopKey = shopKey;
        }

        if (!_dirty) return;
        _dirty = false;
        switch (phase)
        {
            case RunPhase.ChoosePortal: RefreshPortals(run.Run); break;
            case RunPhase.OpenChest: RefreshChest(run.Run); break;
            case RunPhase.StartRelic: RefreshStartRelic(run.Run); break;
            case RunPhase.Shop: RefreshShop(run.Run); break;
        }
    }

    // Up to 3 relics + NONE: 4 cards use the same 2x2 layout as a Big Chests chest.
    private void RefreshStartRelic(RunStateMachine r)
    {
        if (chestHeader != null) chestHeader.text = RunChoiceText.StartRelicHeader;
        IReadOnlyList<ItemDefinition> relics = r.StartRelicChoices;
        ShowButtons(chestCards, relics.Count + 1, (button, i) =>
        {
            bool none = i >= relics.Count;
            button.SetLabel(none ? RunChoiceText.NoneCard : RunChoiceText.ItemCard(relics[i], r.Inventory));
            button.SetIdleColor(RunChoiceText.RarityColor(none ? ItemRarity.Common : relics[i].Rarity));
        });
    }

    private void RefreshPortals(RunStateMachine r)
    {
        if (portalHeader != null) portalHeader.text = RunChoiceText.PortalHeader(r.Island + 1);
        IReadOnlyList<Portal> portals = r.Portals;
        ShowButtons(portalButtons, portals.Count, (button, i) => button.SetLabel(RunChoiceText.PortalLabel(portals[i])));
    }

    private void RefreshChest(RunStateMachine r)
    {
        if (chestHeader != null) chestHeader.text = RunChoiceText.ChestHeader(r.OpenedChest);
        IReadOnlyList<ItemDefinition> items = r.ChestChoices;
        ShowButtons(chestCards, items.Count, (button, i) =>
        {
            button.SetLabel(RunChoiceText.ItemCard(items[i], r.Inventory));
            button.SetIdleColor(RunChoiceText.RarityColor(items[i].Rarity));
        });
    }

    private void RefreshShop(RunStateMachine r)
    {
        Shop shop = r.CurrentShop;
        if (shop == null) return;
        if (shopHeader != null) shopHeader.text = RunChoiceText.ShopHeader(r.Crystals.Balance);

        IReadOnlyList<ShopSlot> slots = shop.Slots;
        for (int i = 0; i < Length(shopSlots); i++)
        {
            HandMenuButton button = shopSlots[i];
            if (button == null) continue;
            bool used = i < slots.Count;
            SetActive(button.gameObject, used);
            if (!used) continue;

            ShopSlot slot = slots[i];
            Color color = RunChoiceText.RarityColor(slot.Item.Rarity);
            button.SetLabel(RunChoiceText.ShopSlot(slot, r.Inventory));
            button.SetIdleColor(RunChoiceText.CanBuy(slot, r.Inventory, r.Crystals) ? color : RunChoiceText.Dimmed(color));
        }

        if (rerollButton != null)
        {
            int price = shop.RerollPrice;
            rerollButton.SetLabel(RunChoiceText.RerollLabel(price));
            Color idle = RunChoiceText.RarityColor(ItemRarity.Common);
            rerollButton.SetIdleColor(r.Crystals.Balance >= price ? idle : RunChoiceText.Dimmed(idle));
        }
    }

    // Shows the first `count` buttons in the RunChoiceText layout, hides the rest.
    private void ShowButtons(HandMenuButton[] buttons, int count, System.Action<HandMenuButton, int> fill)
    {
        Vector2[] layout = RunChoiceText.Layout(Mathf.Min(count, Length(buttons)), spacingX, spacingY);
        for (int i = 0; i < Length(buttons); i++)
        {
            HandMenuButton button = buttons[i];
            if (button == null) continue;
            bool used = i < layout.Length;
            SetActive(button.gameObject, used);
            if (!used) continue;
            button.transform.localPosition = new Vector3(layout[i].x, layout[i].y, 0f);
            fill(button, i);
        }
    }

    private static int ShopKey(RunStateMachine r)
    {
        Shop shop = r.CurrentShop;
        if (shop == null) return 0;
        int key = r.Crystals.Balance * 31 + shop.Rerolls;
        // Index loop: foreach over IReadOnlyList boxes its enumerator every frame (GM-7).
        var slots = shop.Slots;
        for (int i = 0; i < slots.Count; i++) key = key * 2 + (slots[i].Sold ? 1 : 0);
        return key;
    }

    private static int Length(HandMenuButton[] buttons) => buttons != null ? buttons.Length : 0;

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
