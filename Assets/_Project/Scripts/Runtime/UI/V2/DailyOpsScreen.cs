using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ZombieWar.UI.UIBind;

namespace ZombieWar.UI
{
    /// <summary>
    /// Daily Ops (backlog #15/#19, mockups U6 + F2 approved 05/10): the daily chest card with its
    /// streak, today's four missions with CLAIM, and the chest-open sheet. Built by
    /// HordeCall/UI v2/Build Daily Ops; installed at runtime by <see cref="LateScreens"/>.
    /// </summary>
    public sealed class DailyOpsScreen : UIScreen
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public Image icon;
            public TMP_Text title, count;
            public RectTransform bar;
            public Button claim;
            public GameObject done;
        }

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel, gemLabel;
        [Header("Chest card")]
        [SerializeField] private Button chestCard;
        [SerializeField] private TMP_Text chestTitle, chestSub, streakLabel;
        [SerializeField] private GameObject streakTag;
        [SerializeField] private RectTransform chestBar;
        [SerializeField] private Image[] dayPips = new Image[7];
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private Row[] rows = new Row[DailyOps.Count];
        [Header("Icons: Kill/Survive use the gun; then Card, Station, Elite, Runs")]
        [SerializeField] private Sprite[] templateIcons = new Sprite[6];
        [SerializeField] private UIPrototypeCatalog weaponIcons;
        [Header("Chest sheet")]
        [SerializeField] private GameObject sheet;
        [SerializeField] private TMP_Text sheetSub, sheetShards, sheetNote;
        [SerializeField] private Button sheetClaim, sheetClose;

        static readonly Color PipOn = new(0.11f, 0.84f, 0.66f), PipOff = new(0.77f, 0.87f, 0.95f), PipGun = new(0.61f, 0.56f, 1f);
        float _nextClock;

        protected override void Awake()
        {
            base.Awake();
            On(backButton, () => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            On(chestCard, OpenSheet);
            On(sheetClaim, ClaimChest);
            On(sheetClose, () => Active(sheet, false));
            FtueRadio.RegisterModal(sheet);   // radio subtitles wait while the chest is open
            for (int i = 0; i < rows.Length; i++) { int k = i; if (rows[i]?.claim != null) rows[i].claim.onClick.AddListener(() => Claim(k)); }
        }

        void OnEnable() { PlayerProfile.MissionsChanged += Refresh; PlayerProfile.WalletChanged += Refresh; }
        void OnDisable() { PlayerProfile.MissionsChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh; }

        protected override void OnShow()
        {
            PlayerProfile.RefreshMissionWindow(GameClock.UtcNow);
            Active(sheet, false);
            Refresh();
            ZombieWar.Audio.RadioDirector.MissionsShown();
        }

        int _day = int.MinValue;

        void Update()
        {
            if (Time.unscaledTime < _nextClock) return;
            _nextClock = Time.unscaledTime + 1f;
            // A new game day while the screen is open: its deals, free pull and missions change now,
            // not when the screen is next opened (07/10).
            int today = DailyRewards.Today;
            if (_day != today) { bool first = _day == int.MinValue; _day = today; if (!first) Refresh(); }
            Set(resetLabel, "RESETS IN " + GameClock.UntilNextResetText());
        }

        void Refresh()
        {
            if (!gameObject.activeInHierarchy) return;
            var now = GameClock.UtcNow;
            int today = PassMissions.DayKey(now);
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));

            var ops = PassMissions.ActiveFor(now).Where(m => m.scope == MissionScope.Daily).ToList();
            int claimed = ops.Count(m => PlayerProfile.IsMissionClaimed(m.id));
            bool opened = PlayerProfile.DailyChestOpenedOn(today);
            int streak = PlayerProfile.DailyStreakOn(today);
            Set(chestTitle, opened ? "Daily chest · opened" : $"Daily chest · {claimed} / {ops.Count}");
            Set(chestSub, $"{PlayerProfile.DailyChestGems} GEMS · {PlayerProfile.DailyChestTickets} TICKET · {ShardsToday(streak, opened)} GUN SHARDS");
            Active(streakTag, streak > 0);
            Set(streakLabel, $"STREAK {streak}");
            UIBarClip.Set(chestBar, ops.Count == 0 ? 0f : claimed / (float)ops.Count);
            // D1..D6 then the gun day: the streak's place in its week.
            int filled = streak % PlayerProfile.StreakGunEvery;
            if (filled == 0 && streak > 0 && opened) filled = PlayerProfile.StreakGunEvery;
            for (int i = 0; i < dayPips.Length; i++)
                if (dayPips[i] != null) dayPips[i].color = i < filled ? PipOn : i == dayPips.Length - 1 ? PipGun : PipOff;

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i]; if (r?.root == null) continue;
                bool has = i < ops.Count; r.root.SetActive(has);
                if (!has) continue;
                var m = ops[i];
                bool done = PlayerProfile.IsMissionComplete(m), took = PlayerProfile.IsMissionClaimed(m.id);
                Set(r.title, m.title);
                Set(r.count, PassMissions.ProgressText(m));
                if (r.count != null) r.count.gameObject.SetActive(!done);
                UIBarClip.Set(r.bar, PlayerProfile.GetMissionProgress(m.id) / (float)Mathf.Max(1, m.target));
                if (r.claim != null) r.claim.gameObject.SetActive(done && !took);
                Active(r.done, took);
                if (r.icon != null) { var sp = IconFor(m); r.icon.sprite = sp; r.icon.enabled = sp != null; r.icon.preserveAspect = true; }
            }
        }

        static int ShardsToday(int streak, bool opened)
        {
            int next = opened ? streak : streak + 1;
            return PlayerProfile.DailyChestShards * (next >= PlayerProfile.StreakDoubleAt ? 2 : 1);
        }

        Sprite IconFor(PassMission m)
        {
            if (m is not DailyOps.DailyOp op) return null;
            if (op.weaponId != null && weaponIcons != null)
            {
                var named = WeaponCatalog.Active != null ? WeaponCatalog.Active.DataById(op.weaponId) : null;
                if (named != null) return weaponIcons.GetWeaponIcon(named, true);
            }
            if (op.family.HasValue && weaponIcons != null)
            {
                var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
                var gun = all?.FirstOrDefault(w => w != null && w.weaponClass == op.family.Value && PlayerProfile.IsWeaponOwned(w.WeaponId))
                          ?? all?.FirstOrDefault(w => w != null && w.weaponClass == op.family.Value);
                if (gun != null) return weaponIcons.GetWeaponIcon(gun, true);
            }
            int i = (int)op.template;
            return templateIcons != null && i < templateIcons.Length ? templateIcons[i] : null;
        }

        void Claim(int i)
        {
            var ops = PassMissions.ActiveFor(GameClock.UtcNow).Where(m => m.scope == MissionScope.Daily).ToList();
            if (i >= ops.Count) return;
            if (PlayerProfile.TryClaimMission(ops[i].id))
            {
                UIFeedback.Purchase();
                Toast.Show($"+{ops[i].coinReward} coins · +{DailyOps.ShardsPerMission} shards · +{ops[i].passXp} pass XP");
                if (PlayerProfile.CanClaimDailyChest(GameClock.UtcNow)) OpenSheet();
            }
            Refresh();
        }

        void OpenSheet()
        {
            var now = GameClock.UtcNow;
            int today = PassMissions.DayKey(now);
            if (PlayerProfile.DailyChestOpenedOn(today)) { Toast.Show("Opened today · come back tomorrow"); return; }
            if (!PlayerProfile.CanClaimDailyChest(now)) { Toast.Show("Finish all four Daily Ops to open it"); return; }
            int streak = PlayerProfile.DailyStreakOn(today) + 1;
            Set(sheetSub, $"4 / 4 DONE · STREAK {streak}");
            Set(sheetShards, $"{ShardsToday(streak - 1, false)} shards");
            int toGun = PlayerProfile.StreakGunEvery - streak % PlayerProfile.StreakGunEvery;
            Set(sheetNote, streak % PlayerProfile.StreakGunEvery == 0 ? "DAY 7 · A NEW GUN IS INSIDE"
                         : $"COME BACK TOMORROW · DAY {streak + toGun} GIVES A GUN");
            Active(sheet, true);
            if (sheet != null) UIFx.ModalIn(sheet.transform);
        }

        void ClaimChest()
        {
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            if (!PlayerProfile.TryClaimDailyChest(GameClock.UtcNow, guns, out var r)) { Active(sheet, false); return; }
            UIFeedback.LevelUp();
            var gun = r.newGun != null ? WeaponCatalog.Active?.DataById(r.newGun) : null;
            Toast.Show(gun != null ? $"+{r.gems} gems · +{r.tickets} ticket · {gun.weaponName}!" : $"+{r.gems} gems · +{r.tickets} ticket · +{r.shards} shards", 2.6f);
            Active(sheet, false);
            Refresh();
        }
    }
}
