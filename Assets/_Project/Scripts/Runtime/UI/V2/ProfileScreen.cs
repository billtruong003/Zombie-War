using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Profile (owner-approved V2_Profile mockup): live character avatar, editable name, copyable
    /// player ID, account level bar, lifetime stats, collection progress, avatar frames, main gun and
    /// badges. Every number is read from <see cref="PlayerProfile"/>; nothing here is decorative.
    /// Built by HordeCall/UI v2/Build Profile.
    /// </summary>
    public sealed class ProfileScreen : UIScreen
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;

        [Header("Identity")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button editNameButton;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text idLabel;
        [SerializeField] private Button copyIdButton;
        [SerializeField] private TMP_Text levelTag;
        [SerializeField] private RectTransform levelBar;
        [SerializeField] private TMP_Text levelValue;

        [Header("Stats: best, runs, kills, threat, bosses, play time")]
        [SerializeField] private TMP_Text[] stats = new TMP_Text[6];

        [Header("Collection: guns, outfit pieces, gun skins, frames")]
        [SerializeField] private RectTransform[] collectionBars = new RectTransform[4];
        [SerializeField] private TMP_Text[] collectionValues = new TMP_Text[4];
        [SerializeField] private ModularCostumeCatalog costumes;

        [Header("Main gun")]
        [SerializeField] private Image gunTile;
        [SerializeField] private Image gunIcon;
        [SerializeField] private TMP_Text gunName;
        [SerializeField] private TMP_Text gunMeta;
        [SerializeField] private UIPrototypeCatalog catalog;

        [Header("Picture + frame")]
        [SerializeField] private Button avatarButton;
        [SerializeField] private Button framesButton;
        [SerializeField] private TMP_Text framesLabel;
        [SerializeField] private AvatarPicker picker;

        [Header("Badges")]
        [SerializeField] private TMP_Text badgesHeader;
        [SerializeField] private Image[] badgeTiles = new Image[Badges.Length];

        /// Frames and avatars live in AvatarCatalog (owner 2026-09-27).
        public static int FrameCount => AvatarCatalog.Frames.Length;

        struct Badge
        {
            public string value, name; public Color color; public System.Func<bool> earned;
            public Badge(string v, string n, string hex, System.Func<bool> e) { value = v; name = n; ColorUtility.TryParseHtmlString("#" + hex, out color); earned = e; }
        }

        public static int BadgeCount => Badges.Length;

        static readonly Badge[] Badges =
        {
            new("10'", "SURVIVOR", "ffc93c", () => PlayerProfile.BestSurvivalSeconds >= 600f),
            new("1K", "SLAYER", "e5484d", () => PlayerProfile.TotalKills >= 1000),
            new("5", "BOSSES", "8b7bd8", () => PlayerProfile.BossesDefeated >= 5),
            new("T9", "THREAT", "4fa3ff", () => PlayerProfile.PeakThreat >= 9),
            new("100", "RUNS", "5bd68a", () => PlayerProfile.RunsPlayed >= 100),
            new("20'", "VETERAN", "f2994a", () => PlayerProfile.BestSurvivalSeconds >= 1200f),
            new("10K", "REAPER", "e5484d", () => PlayerProfile.TotalKills >= 10000),
            new("25", "HUNTER", "8b7bd8", () => PlayerProfile.BossesDefeated >= 25),
            new("LV20", "LEGEND", "ffc93c", () => PlayerProfile.AccountLevel >= 20),
            new("10", "ARMORY", "4fa3ff", () => PlayerProfile.OwnedWeaponIds.Count >= 10),
        };

        protected override void Awake()
        {
            if (avatarButton != null) avatarButton.onClick.AddListener(() => picker?.Open(false));
            if (framesButton != null) framesButton.onClick.AddListener(() => picker?.Open(true));
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (editNameButton != null) editNameButton.onClick.AddListener(BeginEditName);
            if (copyIdButton != null) copyIdButton.onClick.AddListener(CopyId);
            if (nameInput != null)
            {
                nameInput.characterLimit = 16;
                nameInput.onEndEdit.AddListener(EndEditName);
                nameInput.gameObject.SetActive(false);
            }
        }

        private void OnEnable() => PlayerProfile.AccountChanged += Refresh;
        private void OnDisable() => PlayerProfile.AccountChanged -= Refresh;
        protected override void OnShow() => Refresh();

        public override bool OnEscape()
        {
            if (picker != null && picker.IsOpen) { picker.Close(); return true; }
            return false;
        }
        protected override void OnFocus() => Refresh();

        void Back() { UIFeedback.Back(); UIManager.Instance?.Pop(); }

        void BeginEditName()
        {
            if (nameInput == null) return;
            UIFeedback.Tap();
            nameInput.gameObject.SetActive(true);
            nameInput.text = PlayerProfile.DisplayName;
            nameInput.Select();
            nameInput.ActivateInputField();
        }

        void EndEditName(string value)
        {
            if (nameInput != null) nameInput.gameObject.SetActive(false);
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == PlayerProfile.DisplayName) return;
            if (PlayerProfile.SetDisplayName(value)) { UIFeedback.Confirm(); Toast.Show("Name saved"); }
        }

        void CopyId()
        {
            GUIUtility.systemCopyBuffer = PlayerProfile.PlayerId;
            UIFeedback.Tap();
            Toast.Show("Player ID copied");
        }

        public void Refresh()
        {
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            Set(nameLabel, PlayerProfile.DisplayName);
            Set(idLabel, "ID " + FormatId(PlayerProfile.PlayerId));

            int xp = PlayerProfile.AccountXp, level = PlayerProfile.AccountLevel;
            Set(levelTag, "LV " + level);
            UIBarClip.Set(levelBar, AccountProgress.Progress01(xp));
            int into = xp - AccountProgress.TotalFor(level);
            Set(levelValue, $"{into:N0}/{AccountProgress.CostOf(level):N0}");

            int best = Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds);
            SetStat(0, best > 0 ? HudController.FormatClock(best) : "-");
            SetStat(1, PlayerProfile.RunsPlayed.ToString("N0"));
            SetStat(2, HomeScreen.Short(PlayerProfile.TotalKills));
            SetStat(3, PlayerProfile.PeakThreat.ToString());
            SetStat(4, PlayerProfile.BossesDefeated.ToString("N0"));
            SetStat(5, PlayTime(PlayerProfile.TotalSeconds));

            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            int gunTotal = guns?.Count(w => w != null) ?? 0;
            int gunOwned = guns?.Count(w => w != null && PlayerProfile.IsWeaponOwned(w.WeaponId)) ?? 0;
            SetCollection(0, gunOwned, gunTotal);
            int partTotal = 0, partOwned = 0;
            if (costumes != null)
                foreach (var slot in costumes.slots)
                {
                    if (slot == null || slot.isBaseBody) continue;
                    foreach (var p in slot.parts) { partTotal++; if (PlayerProfile.IsCostumeOwned(p.guid)) partOwned++; }
                }
            SetCollection(1, partOwned, partTotal);
            SetCollection(2, 0, Skins.WeaponSkins.Season1.Length);   // skins are sold from M10 Arsenal/Shop
            // Every badge earned: the Legend frame.
            bool allBadges = true; foreach (var bd in Badges) if (!bd.earned()) { allBadges = false; break; }
            if (allBadges) PlayerProfile.AddFrame("frame.legend");
            int frames = AvatarCatalog.UnlockedFrameCount;
            SetCollection(3, frames, AvatarCatalog.Frames.Length);
            if (framesLabel != null) framesLabel.text = $"{frames} / {AvatarCatalog.Frames.Length} FRAMES";

            RefreshGun(guns);

            int earned = 0;
            for (int i = 0; i < Badges.Length && i < badgeTiles.Length; i++)
            {
                bool on = Badges[i].earned();
                if (on) earned++;
                var tile = badgeTiles[i];
                if (tile == null) continue;
                if (on) ThemeTint.Clear(tile, Badges[i].color); else ThemeTint.Set(tile, ThemeRole.Edge);
                var cg = tile.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = on ? 1f : 0.55f;
            }
            Set(badgesHeader, $"BADGES · {earned} / {Badges.Length}");
        }

        void RefreshGun(System.Collections.Generic.IReadOnlyList<WeaponData> all)
        {
            var d = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
            if (d == null) return;
            Set(gunName, d.weaponName);
            if (gunTile != null) gunTile.color = d.TileColor;
            var icon = catalog != null ? catalog.GetWeaponIcon(d, true) : null;
            if (gunIcon != null) { gunIcon.enabled = icon != null; if (icon != null) { gunIcon.sprite = icon; gunIcon.preserveAspect = true; } }
            int stars = Mathf.Clamp(PlayerProfile.GetWeaponLevel(d.WeaponId), 1, 3);
            Set(gunMeta, $"{HubScreen.FamilyName(d.weaponClass)} · POWER {Mathf.RoundToInt(CombatPower.WeaponPower(d, stars)):N0}");
        }

        void SetStat(int i, string v) { if (i < stats.Length) Set(stats[i], v); }

        void SetCollection(int i, int owned, int total)
        {
            if (i < collectionValues.Length) Set(collectionValues[i], $"{owned}/{total}");
            if (i < collectionBars.Length) UIBarClip.Set(collectionBars[i], total > 0 ? owned / (float)total : 0f);
        }

        public static string FormatId(string id) =>
            string.IsNullOrEmpty(id) || id.Length != 8 ? id : id.Substring(0, 4) + " " + id.Substring(4);

        public static string PlayTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            return m >= 60 ? (m / 60) + "H" : m + "M";
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
    }
}
