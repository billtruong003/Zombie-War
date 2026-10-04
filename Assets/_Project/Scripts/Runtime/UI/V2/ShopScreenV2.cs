using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Shop (owner-approved R2_Shop mockup): section chips (Featured, Deals, Gems, Guns, Skins)
    /// over one scrolling page: the one-time starter pack hero with its countdown, the boutique look
    /// of the week, today's deals, gem packs with bonus ribbons, no ads, coin guns and skin sets. Rules
    /// and grants live in <see cref="ShopOffers"/>; real money goes through <see cref="Purchases"/>.
    /// Built by HordeCall/UI v2/Build Shop.
    /// </summary>
    public sealed class ShopScreenV2 : UIScreen
    {
        [Serializable]
        public sealed class Cell
        {
            public Button button;
            public Image tile;
            public Image icon;
            public TMP_Text title;
            public TMP_Text price;
            public GameObject done;
        }

        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private Button[] chips = new Button[5];
        [SerializeField] private ScrollRect page;
        [SerializeField] private RectTransform[] sections = new RectTransform[5];

        [Header("Deals")]
        [SerializeField] private TMP_Text dealsTimer;
        [SerializeField] private Cell[] deals = new Cell[3];

        [Header("Boutique")]
        [SerializeField] private TMP_Text lookName;
        [SerializeField] private TMP_Text lookSub;
        [SerializeField] private Image lookIcon;
        [SerializeField] private Button tryOnButton;
        [SerializeField] private Button lookBuyButton;
        [SerializeField] private TMP_Text lookBuyLabel;

        [Header("Starter pack hero")]
        [SerializeField] private GameObject hero;
        [SerializeField] private TMP_Text heroTimer;
        [SerializeField] private Button heroButton;
        [SerializeField] private TMP_Text heroPrice;
        [SerializeField] private TMP_Text heroWas;

        [Header("Gem packs (ShopOffers.GemPackIds order) and no ads")]
        [SerializeField] private Cell[] gemPacks = new Cell[4];
        [SerializeField] private TMP_Text[] gemBonus = new TMP_Text[4];
        [SerializeField] private Cell noAdsCell;

        [Header("Skin sets: biohazard, neon, gilded")]
        [SerializeField] private Cell[] skinSets = new Cell[3];

        [Header("Guns")]
        [SerializeField] private Cell[] guns = new Cell[MaxGuns];

        [SerializeField] private EconomyConfig economy;
        [SerializeField] private UIPrototypeCatalog catalog;
        [SerializeField] private UIScreen studioScreen;
        [SerializeField] private UIScreen gachaScreen;
        [SerializeField] private NavBarV2 nav;

        public const int MaxGuns = 64;
        static readonly string[] SkinIds = { "biohazard", "neon", "gilded" };

        ShopOffers.Deal[] _deals = new ShopOffers.Deal[0];
        List<WeaponData> _forSale = new();
        EconomyConfig.CostumeSetEntry _look;
        float _tick;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < chips.Length; i++) { int idx = i; if (chips[i] != null) chips[i].onClick.AddListener(() => Jump(idx)); }
            for (int i = 0; i < deals.Length; i++) { int idx = i; if (deals[i]?.button != null) deals[i].button.onClick.AddListener(() => BuyDeal(idx)); }
            for (int i = 0; i < gemPacks.Length && i < ShopOffers.GemPackIds.Length; i++) { string id = ShopOffers.GemPackIds[i]; if (gemPacks[i]?.button != null) gemPacks[i].button.onClick.AddListener(() => BuyPack(id)); }
            if (noAdsCell?.button != null) noAdsCell.button.onClick.AddListener(() => BuyPack("pack.noads"));
            if (heroButton != null) heroButton.onClick.AddListener(() => BuyPack("pack.starter"));
            for (int i = 0; i < skinSets.Length; i++) { int idx = i; if (skinSets[i]?.button != null) skinSets[i].button.onClick.AddListener(() => TapSkin(idx)); }
            for (int i = 0; i < guns.Length; i++) { int idx = i; if (guns[i]?.button != null) guns[i].button.onClick.AddListener(() => BuyGun(idx)); }
            if (tryOnButton != null) tryOnButton.onClick.AddListener(() => { UIFeedback.Tap(); if (studioScreen != null) UIManager.Instance?.Push(studioScreen); else Toast.Show("Coming soon"); });
            if (lookBuyButton != null) lookBuyButton.onClick.AddListener(BuyLook);
        }

        private void OnEnable() { PlayerProfile.WalletChanged += Refresh; PlayerProfile.LoadoutChanged += Refresh; PlayerProfile.CostumeChanged += Refresh; }
        private void OnDisable() { PlayerProfile.WalletChanged -= Refresh; PlayerProfile.LoadoutChanged -= Refresh; PlayerProfile.CostumeChanged -= Refresh; }
        protected override void OnShow() { Refresh(); Jump(0, false); ZombieWar.Audio.RadioDirector.ShopShown(); }
        protected override void OnFocus() => Refresh();

        void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            if (dealsTimer != null) dealsTimer.text = "NEW IN " + ShopOffers.RefreshIn();
            var left = ShopOffers.StarterLeft(GameClock.UtcNow);
            if (hero != null && hero.activeSelf != left > TimeSpan.Zero) hero.SetActive(left > TimeSpan.Zero);
            if (heroTimer != null) heroTimer.text = "ONE TIME · ENDS " + ShopOffers.Clock(left);
        }

        void Jump(int i, bool feedback = true)
        {
            if (feedback) UIFeedback.Tap();
            for (int c = 0; c < chips.Length; c++)
            {
                if (chips[c] == null) continue;
                ThemeTint.Set(chips[c].targetGraphic, c == i ? ThemeRole.Ink : ThemeRole.Card);
                var t = chips[c].GetComponentInChildren<TMP_Text>(true); ThemeTint.Set(t, c == i ? ThemeRole.OnInk : ThemeRole.Dim);
            }
            if (page == null || i >= sections.Length || sections[i] == null) return;
            Canvas.ForceUpdateCanvases();
            var content = page.content;
            float max = Mathf.Max(0f, content.rect.height - page.viewport.rect.height);
            float y = Mathf.Clamp(-sections[i].anchoredPosition.y - sections[i].rect.height * sections[i].pivot.y, 0f, max);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            page.velocity = Vector2.zero;
        }

        void BuyDeal(int i)
        {
            if (i >= _deals.Length) return;
            int today = DailyRewards.Today; var d = _deals[i];
            if (ShopOffers.IsDealBought(today, d.slot)) { Toast.Show("Sold out · new deals tomorrow"); return; }
            if (d.pay == ShopOffers.Pay.Ad)
            {
                RewardedAds.Show("shop_deal", () => { if (ShopOffers.BuyDeal(today, d)) { UIFeedback.Purchase(); Toast.Show("Got " + d.Title); } });
                return;
            }
            if (ShopOffers.BuyDeal(today, d)) { UIFeedback.Purchase(); Toast.Show("Got " + d.Title); }
            else { UIFeedback.Error(); Toast.Show(d.pay == ShopOffers.Pay.Gem ? "Not enough gems" : "Not enough coins"); }
        }

        void BuyPack(string id)
        {
            var p = ShopOffers.FindPack(id);
            if (!ShopOffers.CanBuyPack(p)) { Toast.Show("Already bought"); return; }
            Purchases.Buy(p.id, p.price, () => { if (ShopOffers.GrantPack(p)) { UIFeedback.Purchase(); Refresh(); } });
        }

        void TapSkin(int i)
        {
            string id = SkinIds[i];
            if (PlayerProfile.IsSkinOwned(id)) { Toast.Show("Owned · equip it in the Arsenal"); return; }
            switch (ShopOffers.SourceOf(id))
            {
                case ShopOffers.SkinSource.Gems:
                    if (ShopOffers.BuySkinWithGems(id)) { UIFeedback.Purchase(); Toast.Show("Skin set unlocked · equip it in the Arsenal"); }
                    else { UIFeedback.Error(); Toast.Show("Not enough gems"); }
                    break;
                case ShopOffers.SkinSource.Gacha:
                    UIFeedback.Tap();
                    if (gachaScreen != null && AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha)) UIManager.Instance?.Push(gachaScreen);
                    else Toast.Show("Win it in the Gacha");
                    break;
                default: BuyPack("pack.legend"); break;
            }
        }

        void BuyLook()
        {
            if (_look == null || economy == null) return;
            var r = PlayerProfile.TryPurchaseCostumeSet(economy, _look.setId);
            switch (r)
            {
                case PlayerProfile.PurchaseResult.Purchased: UIFeedback.Purchase(); Toast.Show($"{_look.displayName} is yours · wear it in the Studio"); break;
                case PlayerProfile.PurchaseResult.AlreadyOwned: Toast.Show("Owned"); break;
                case PlayerProfile.PurchaseResult.InsufficientFunds: UIFeedback.Error(); Toast.Show("Not enough gems"); break;
                default: UIFeedback.Error(); Toast.Show("Not available"); break;
            }
        }

        void BuyGun(int i)
        {
            if (i >= _forSale.Count) return;
            var d = _forSale[i];
            var r = PlayerProfile.TryPurchaseWeapon(d.WeaponId, d.price);
            if (r == PlayerProfile.PurchaseResult.Purchased) { UIFeedback.Purchase(); Toast.Show($"{d.weaponName} unlocked · equip it in the Arsenal"); }
            else if (r == PlayerProfile.PurchaseResult.InsufficientFunds) { UIFeedback.Error(); Toast.Show("Not enough coins"); }
            else Toast.Show("Owned");
        }

        public void Refresh()
        {
            int today = DailyRewards.Today;
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : new List<WeaponData>();

            _deals = ShopOffers.DealsFor(today, all);
            for (int i = 0; i < deals.Length && i < _deals.Length; i++)
            {
                var c = deals[i]; var d = _deals[i]; if (c == null) continue;
                bool sold = ShopOffers.IsDealBought(today, d.slot);
                Set(c.title, d.Title);
                Set(c.price, sold ? "SOLD" : d.pay == ShopOffers.Pay.Ad ? "FREE" : d.price.ToString("N0"));
                if (c.done != null) c.done.SetActive(sold);
                // Top band: the gun's rarity for shards, gem purple for tickets, green for the free one.
                ThemeTint.Set(c.tile, d.kind == ShopOffers.Kind.Shards && d.gun != null ? ThemePalette.Rarity(Mathf.Clamp((int)d.gun.tier, 0, 4))
                    : d.kind == ShopOffers.Kind.Tickets ? ThemeRole.Gem : ThemeRole.Claim);
                if (c.icon != null)
                {
                    var gunIcon = d.kind == ShopOffers.Kind.Shards && d.gun != null && catalog != null ? catalog.GetWeaponIcon(d.gun, true) : null;
                    if (gunIcon != null) { c.icon.sprite = gunIcon; c.icon.preserveAspect = true; }
                }
            }

            _look = ShopOffers.LookOfTheWeek(economy, today);
            if (_look != null)
            {
                bool owned = PlayerProfile.IsCostumeSetOwned(_look);
                Set(lookName, _look.displayName.ToUpperInvariant());
                Set(lookSub, $"{_look.itemIds.Count} pieces · look of the week");
                if (lookIcon != null && _look.icon != null) { lookIcon.sprite = _look.icon; lookIcon.enabled = true; }
                long price = 0; WalletCurrency cur = WalletCurrency.Gem;
                if (economy != null) economy.TryGetCostumeSetPrice(_look, out cur, out price);
                Set(lookBuyLabel, owned ? "OWNED" : $"{price:N0} {(cur == WalletCurrency.Gem ? "GEMS" : "COINS")}");
            }

            var starter = ShopOffers.FindPack("pack.starter");
            if (hero != null) hero.SetActive(ShopOffers.StarterLeft(GameClock.UtcNow) > TimeSpan.Zero);
            if (starter != null) { Set(heroPrice, starter.price); Set(heroWas, $"<s>{starter.was}</s>"); }
            for (int i = 0; i < gemPacks.Length && i < ShopOffers.GemPackIds.Length; i++)
            {
                var c = gemPacks[i]; var p = ShopOffers.FindPack(ShopOffers.GemPackIds[i]); if (c == null || p == null) continue;
                Set(c.title, p.gems.ToString("N0")); Set(c.price, p.price);
                if (i < gemBonus.Length) Set(gemBonus[i], p.bonusPercent > 0 ? $"+{p.bonusPercent}% BONUS" : "");
            }
            var noAds = ShopOffers.FindPack("pack.noads");
            if (noAdsCell != null && noAds != null)
            {
                bool sold = !ShopOffers.CanBuyPack(noAds);
                Set(noAdsCell.price, sold ? "OWNED" : noAds.price);
                if (noAdsCell.done != null) noAdsCell.done.SetActive(sold);
            }

            for (int i = 0; i < skinSets.Length; i++)
            {
                var c = skinSets[i]; if (c == null) continue;
                string id = SkinIds[i]; var set = Skins.WeaponSkins.Find(id);
                bool owned = PlayerProfile.IsSkinOwned(id);
                Set(c.title, $"{set?.name} +{Skins.WeaponSkins.DamageBonus(id) * 100f:0}%");
                Set(c.price, owned ? "OWNED" : ShopOffers.SourceOf(id) switch
                {
                    ShopOffers.SkinSource.Gems => $"{ShopOffers.BiohazardGems} GEMS",
                    ShopOffers.SkinSource.Gacha => "IN GACHA",
                    _ => "LEGEND PACK $9.99",
                });
                if (c.done != null) c.done.SetActive(owned);
            }

            _forSale = all.Where(w => w != null && w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId)).OrderBy(w => w.price).ToList();
            var gunSub = sections.Length > 3 && sections[3] != null ? sections[3].Find("Sub")?.GetComponent<TMP_Text>() : null;
            Set(gunSub, _forSale.Count > 0 ? "COINS" : "ALL OWNED");
            for (int i = 0; i < guns.Length; i++)
            {
                var c = guns[i]; if (c?.button == null) continue;
                bool has = i < _forSale.Count; c.button.transform.parent.gameObject.SetActive(has);   // the whole card
                if (!has) continue;
                var d = _forSale[i];
                Set(c.title, d.weaponName); Set(c.price, d.price.ToString("N0"));
                if (c.tile != null) c.tile.color = d.TileColor;
                var icon = catalog != null ? catalog.GetWeaponIcon(d, true) : null;
                if (c.icon != null) { c.icon.enabled = icon != null; if (icon != null) { c.icon.sprite = icon; c.icon.preserveAspect = true; } }
            }
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
    }
}
