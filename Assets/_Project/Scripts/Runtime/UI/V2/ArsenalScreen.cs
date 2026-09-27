using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Skins;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Arsenal (owner-approved V2_Arsenal mockup): the selected gun turning in 3D (drag it, or
    /// open the big 360 view), stars bought with shards and coin, the skin row (one owned set fits
    /// every gun and adds damage), and the gun grid. Tapping an owned gun equips it; an unowned gun
    /// shows where to get it. Built by HordeCall/UI v2/Build Arsenal.
    /// </summary>
    public sealed class ArsenalScreen : UIScreen
    {
        [Serializable]
        public sealed class GunCell
        {
            public Button button;
            public Image tile;
            public Image icon;
            public TMP_Text name;
            public Image[] stars;
            public GameObject shopTag;
            public GameObject selected;
        }

        [Serializable]
        public sealed class SkinCell
        {
            public Button button;
            public TMP_Text bonus;
            public GameObject lockTag;
            public TMP_Text source;
            public GameObject selected;
        }

        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;

        [Header("Selected gun")]
        [SerializeField] private GunTurntable turntable;
        [SerializeField] private RawImage preview;
        [SerializeField] private Image previewBg;
        [SerializeField] private TMP_Text gunName;
        [SerializeField] private TMP_Text tierLabel;
        [SerializeField] private Image tierBg;
        [SerializeField] private Image[] stars = new Image[3];
        [SerializeField] private RectTransform dmgBar;
        [SerializeField] private TMP_Text dmgBonus;
        [SerializeField] private RectTransform rateBar;
        [SerializeField] private Button starButton;
        [SerializeField] private TMP_Text starLabel;
        [SerializeField] private Button viewButton;
        [SerializeField] private GameObject bigView;
        [SerializeField] private RawImage bigPreview;
        [SerializeField] private Button bigClose;

        [Header("Skins")]
        [SerializeField] private SkinCell[] skins = new SkinCell[6];

        [Header("Guns")]
        [SerializeField] private TMP_Text gunsHeader;
        [SerializeField] private GunCell[] cells = new GunCell[ArsenalScreen.MaxGuns];
        [SerializeField] private EconomyConfig economy;
        [SerializeField] private UIPrototypeCatalog catalog;
        [SerializeField] private UIScreen shopScreen;
        [SerializeField] private NavBarV2 nav;

        public const int MaxGuns = 64;

        static readonly HashSet<string> PassSkins = new() { PassRewards.FreeSkin, PassRewards.PremiumSkinMid, PassRewards.PremiumSkinTop };
        static readonly Color StarOff = new(0.25f, 0.27f, 0.33f, 1f), Locked = new(0.227f, 0.255f, 0.322f);

        List<WeaponData> _guns = new();
        WeaponData _selected;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < cells.Length; i++) { int idx = i; if (cells[i]?.button != null) cells[i].button.onClick.AddListener(() => TapGun(idx)); }
            for (int i = 0; i < skins.Length; i++) { int idx = i; if (skins[i]?.button != null) skins[i].button.onClick.AddListener(() => TapSkin(idx)); }
            if (starButton != null) starButton.onClick.AddListener(Star);
            if (viewButton != null) viewButton.onClick.AddListener(() => { UIFeedback.Tap(); if (bigView != null) bigView.SetActive(true); });
            if (bigClose != null) bigClose.onClick.AddListener(() => { UIFeedback.Back(); if (bigView != null) bigView.SetActive(false); });
        }

        private void OnEnable() { PlayerProfile.LoadoutChanged += Refresh; PlayerProfile.WalletChanged += Refresh; }
        private void OnDisable() { PlayerProfile.LoadoutChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh; }

        protected override void OnShow()
        {
            if (bigView != null) bigView.SetActive(false);
            _guns = (WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : new List<WeaponData>())
                .Where(w => w != null)
                .OrderByDescending(w => PlayerProfile.IsWeaponOwned(w.WeaponId)).ThenBy(w => w.tier).ThenBy(w => w.price).ToList();
            _selected = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, _guns);
            Refresh();
        }

        protected override void OnFocus() => Refresh();

        public override bool OnEscape()
        {
            if (bigView != null && bigView.activeSelf) { bigView.SetActive(false); return true; }
            return false;
        }

        void TapGun(int i)
        {
            if (i >= _guns.Count) return;
            var d = _guns[i];
            _selected = d;
            if (PlayerProfile.IsWeaponOwned(d.WeaponId))
            {
                if (PlayerProfile.EquippedWeaponId != d.WeaponId) { PlayerProfile.SetEquippedWeapon(d.WeaponId); UIFeedback.Equip(); }
                else UIFeedback.Tap();
            }
            else UIFeedback.Tap();
            Refresh();
        }

        void TapSkin(int i)
        {
            var sets = WeaponSkins.Season1;
            if (i >= sets.Length || _selected == null) return;
            var set = sets[i];
            if (!PlayerProfile.IsSkinOwned(set.id)) { UIFeedback.Error(); Toast.Show(PassSkins.Contains(set.id) ? $"{set.name}: get it from the Pass" : $"{set.name}: get it in the Shop"); return; }
            if (!PlayerProfile.IsWeaponOwned(_selected.WeaponId)) { UIFeedback.Error(); Toast.Show("Own this gun to skin it"); return; }
            bool on = PlayerProfile.GetEquippedSkin(_selected.WeaponId) == set.id;
            PlayerProfile.SetEquippedSkin(_selected.WeaponId, on ? null : set.id);
            UIFeedback.Equip();
            Toast.Show(on ? "Skin removed" : $"{set.name} on {_selected.weaponName} · +{WeaponSkins.DamageBonus(set) * 100f:0}% damage");
        }

        void Star()
        {
            if (_selected == null) return;
            if (!PlayerProfile.IsWeaponOwned(_selected.WeaponId))
            {
                UIFeedback.Tap();
                if (shopScreen != null) UIManager.Instance?.Push(shopScreen); else Toast.Show("Get it in the Shop");
                return;
            }
            var r = PlayerProfile.TryUpgradeWeapon(_selected, economy);
            switch (r)
            {
                case PlayerProfile.WeaponUpgradeResult.Upgraded: UIFeedback.LevelUp(); Toast.Show($"{_selected.weaponName} reached {PlayerProfile.GetWeaponLevel(_selected.WeaponId)} stars"); break;
                case PlayerProfile.WeaponUpgradeResult.MaxLevel: Toast.Show("Max stars"); break;
                case PlayerProfile.WeaponUpgradeResult.InsufficientShards: UIFeedback.Error(); Toast.Show("Need more shards · Gacha gives shards"); break;
                case PlayerProfile.WeaponUpgradeResult.InsufficientGold: UIFeedback.Error(); Toast.Show("Not enough coins"); break;
                default: UIFeedback.Error(); Toast.Show("Could not upgrade"); break;
            }
        }

        (int shards, long coin) StarCost(WeaponData d, int level)
        {
            if (economy == null || level >= 3) return (0, 0);
            int t = Mathf.Clamp((int)d.tier, 0, 4);
            var st = level == 1 ? economy.weaponStar2ShardCost : economy.weaponStar3ShardCost;
            var gt = level == 1 ? economy.weaponStar2GoldCost : economy.weaponStar3GoldCost;
            return (st != null && st.Length > t ? st[t] : 0, gt != null && gt.Length > t ? gt[t] : 0);
        }

        public void Refresh()
        {
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            if (_selected == null && _guns.Count > 0) _selected = _guns[0];
            RefreshSelected();
            RefreshSkins();
            RefreshGrid();
        }

        void RefreshSelected()
        {
            var d = _selected; if (d == null) return;
            bool owned = PlayerProfile.IsWeaponOwned(d.WeaponId);
            int level = Mathf.Max(1, PlayerProfile.GetWeaponLevel(d.WeaponId));
            var skin = owned ? WeaponSkins.Find(PlayerProfile.GetEquippedSkin(d.WeaponId)) : null;
            Set(gunName, d.weaponName);
            Set(tierLabel, d.tier.ToString().ToUpperInvariant());
            if (tierBg != null) tierBg.color = d.TierColor;
            if (previewBg != null) previewBg.color = d.TileColor;
            for (int i = 0; i < stars.Length; i++) if (stars[i] != null) stars[i].color = owned && i < level ? Color.white : StarOff;

            float maxDmg = Mathf.Max(1f, _guns.Max(w => WeaponUpgradeMath.EffectiveDamage(w, 3)));
            float maxRate = Mathf.Max(0.01f, _guns.Max(w => WeaponUpgradeMath.EffectiveFireRate(w, 3)));
            float bonus = WeaponSkins.DamageBonus(skin);
            UIBarClip.Set(dmgBar, Mathf.Clamp01(WeaponUpgradeMath.EffectiveDamage(d, level) * (1f + bonus) / maxDmg));
            UIBarClip.Set(rateBar, Mathf.Clamp01(WeaponUpgradeMath.EffectiveFireRate(d, level) / maxRate));
            if (dmgBonus != null) { dmgBonus.gameObject.SetActive(bonus > 0f); dmgBonus.text = $"+{bonus * 100f:0}%"; }

            if (!owned) Set(starLabel, d.price > 0 ? $"GET IN SHOP · {d.price:N0}" : "GET IN SHOP");
            else if (level >= 3) Set(starLabel, "MAX STARS");
            else
            {
                var (need, coin) = StarCost(d, level);
                Set(starLabel, $"STAR {level + 1} · {PlayerProfile.GetWeaponShards(d.WeaponId)}/{need} · {HomeScreen.Short(coin)}");
            }

            if (turntable != null)
            {
                turntable.Show(d, skin);
                if (preview != null) { preview.texture = turntable.Texture; preview.color = Color.white; }
                if (bigPreview != null) { bigPreview.texture = turntable.Texture; bigPreview.color = Color.white; }
            }
        }

        void RefreshSkins()
        {
            var sets = WeaponSkins.Season1;
            string on = _selected != null ? PlayerProfile.GetEquippedSkin(_selected.WeaponId) : null;
            for (int i = 0; i < skins.Length; i++)
            {
                var c = skins[i]; if (c == null) continue;
                if (i >= sets.Length) { if (c.button != null) c.button.gameObject.SetActive(false); continue; }
                bool owned = PlayerProfile.IsSkinOwned(sets[i].id);
                Set(c.bonus, $"+{WeaponSkins.DamageBonus(sets[i]) * 100f:0}% DMG");
                if (c.bonus != null) c.bonus.gameObject.SetActive(owned);
                if (c.lockTag != null) c.lockTag.SetActive(!owned);
                Set(c.source, PassSkins.Contains(sets[i].id) ? "PASS" : "SHOP");
                if (c.selected != null) c.selected.SetActive(owned && on == sets[i].id);
            }
        }

        void RefreshGrid()
        {
            int ownedCount = _guns.Count(w => PlayerProfile.IsWeaponOwned(w.WeaponId));
            Set(gunsHeader, $"{ownedCount} / {_guns.Count} OWNED");
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i]; if (c?.button == null) continue;
                bool has = i < _guns.Count; c.button.gameObject.SetActive(has);
                if (!has) continue;
                var d = _guns[i];
                bool owned = PlayerProfile.IsWeaponOwned(d.WeaponId);
                int level = PlayerProfile.GetWeaponLevel(d.WeaponId);
                Set(c.name, d.weaponName);
                if (c.tile != null) c.tile.color = owned ? d.TileColor : Locked;
                var icon = catalog != null ? catalog.GetWeaponIcon(d, owned) : null;
                if (c.icon != null) { c.icon.enabled = icon != null; if (icon != null) { c.icon.sprite = icon; c.icon.preserveAspect = true; } }
                if (c.stars != null) for (int s = 0; s < c.stars.Length; s++) if (c.stars[s] != null) { c.stars[s].gameObject.SetActive(owned); c.stars[s].color = s < level ? Color.white : StarOff; }
                if (c.shopTag != null) c.shopTag.SetActive(!owned);
                if (c.selected != null) c.selected.SetActive(_selected == d);
            }
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
    }
}
