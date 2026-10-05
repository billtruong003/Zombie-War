using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ZombieWar.UI.UIBind;

namespace ZombieWar.UI
{
    /// <summary>
    /// Gun mastery (backlog #21/#23, mockups U7 + F3 approved 05/10): one gun's mastery level and XP,
    /// the ten level rewards, its family's account-wide bonus, the evolution card with EVOLVE, and how
    /// the gun earns XP. EVOLVE opens the evolution moment. Opened from the Arsenal's MASTERY button.
    /// </summary>
    public sealed class MasteryScreen : UIScreen
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleLabel, coinLabel, gemLabel;
        [SerializeField] private Image gunIcon;
        [SerializeField] private TMP_Text rarityLabel, powerLabel;
        [SerializeField] private Image[] stars = new Image[3];
        [SerializeField] private TMP_Text masteryTitle, xpLabel, legend;
        [SerializeField] private RectTransform xpBar;
        [SerializeField] private Image[] levelPips = new Image[GunMastery.MaxLevel];
        [SerializeField] private TMP_Text familyTitle, bonus1, bonus2;
        [SerializeField] private TMP_Text evoTitle, evoTrait, evoState;
        [SerializeField] private Button evolveButton;
        [SerializeField] private TMP_Text evolveLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;
        [SerializeField] private UIPrototypeCatalog weaponIcons;
        [Header("Evolution moment")]
        [SerializeField] private GameObject evoSheet;
        [SerializeField] private Image evoGun;
        [SerializeField] private TMP_Text evoSheetSub, evoSheetEffect, evoSheetBonus;
        [SerializeField] private Button evoSheetEquip;

        static readonly Color PipOn = new(0.11f, 0.84f, 0.66f), PipOff = new(0.77f, 0.87f, 0.95f), PipBonus = new(0.61f, 0.56f, 1f);
        static readonly Color StarOn = Color.white, StarOff = new(1f, 1f, 1f, 0.25f);
        static WeaponData _pending;
        WeaponData _gun;

        /// <summary>Opens the screen on a gun.</summary>
        public static void Open(WeaponData gun)
        {
            if (gun == null) return;
            _pending = gun;
            LateScreens.Open<MasteryScreen>();
        }

        protected override void Awake()
        {
            base.Awake();
            On(backButton, () => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            On(evolveButton, Evolve);
            On(equipButton, Equip);
            On(evoSheetEquip, () => { Active(evoSheet, false); Equip(); });
            FtueRadio.RegisterModal(evoSheet);
        }

        protected override void OnShow()
        {
            _gun = _pending ?? _gun;
            Active(evoSheet, false);
            Refresh();
        }

        void Refresh()
        {
            if (_gun == null) return;
            string id = _gun.WeaponId;
            Set(titleLabel, _gun.weaponName.ToUpperInvariant());
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            if (gunIcon != null && weaponIcons != null) { gunIcon.sprite = weaponIcons.GetWeaponIcon(_gun, true); gunIcon.preserveAspect = true; }
            Set(rarityLabel, $"{_gun.tier.ToString().ToUpperInvariant()} · {DailyOps.FamilyName(_gun.weaponClass).ToUpperInvariant()}");
            int starCount = PlayerProfile.GetWeaponLevel(id);
            for (int i = 0; i < stars.Length; i++) if (stars[i] != null) stars[i].color = i < starCount ? StarOn : StarOff;
            Set(powerLabel, $"POWER {Mathf.RoundToInt(CombatPower.WeaponPower(_gun, Mathf.Max(1, starCount))):N0}");

            var stat = PlayerProfile.GunStat(id);
            int level = GunMastery.LevelFor(stat.masteryXp);
            Set(masteryTitle, $"Mastery {level} / {GunMastery.MaxLevel}");
            Set(xpLabel, level >= GunMastery.MaxLevel ? $"{stat.masteryXp:N0} XP · MAX"
                         : $"{stat.masteryXp:N0} / {GunMastery.XpToReach(level + 1):N0} XP");
            UIBarClip.Set(xpBar, level >= GunMastery.MaxLevel ? 1f : stat.masteryXp / (float)GunMastery.XpToReach(level + 1));
            for (int i = 0; i < levelPips.Length; i++)
            {
                if (levelPips[i] == null) continue;
                bool bonusLevel = i + 1 == GunMastery.BonusLevel1 || i + 1 == GunMastery.BonusLevel2;
                levelPips[i].color = i < level ? PipOn : bonusLevel ? PipBonus : PipOff;
            }
            var next = level < GunMastery.MaxLevel ? GunMastery.RewardFor(level + 1) : default;
            Set(legend, level < GunMastery.MaxLevel ? $"NEXT: LEVEL {level + 1} · {next.Label.ToUpperInvariant()}" : "MASTERED");

            var f = _gun.weaponClass;
            Set(familyTitle, $"{DailyOps.FamilyName(f).ToUpperInvariant()} FAMILY BONUS · ALL GUNS");
            Set(bonus1, $"Mastery 5: {GunMastery.BonusText(f, 1)}" + (level >= GunMastery.BonusLevel1 ? " · active" : " · locked"));
            Set(bonus2, $"Mastery 10: {GunMastery.BonusText(f, 2)}" + (level >= GunMastery.BonusLevel2 ? " · active" : " · locked"));
            if (bonus1 != null) bonus1.color = level >= GunMastery.BonusLevel1 ? new Color(0f, 0.68f, 0.5f) : new Color(0.29f, 0.4f, 0.52f);
            if (bonus2 != null) bonus2.color = level >= GunMastery.BonusLevel2 ? new Color(0f, 0.68f, 0.5f) : new Color(0.29f, 0.4f, 0.52f);

            var block = PlayerProfile.EvolveBlock(id);
            Set(evoTitle, $"EVOLUTION · ACCOUNT LEVEL {GunEvolution.AccountLevelNeeded}");
            Set(evoTrait, GunEvolution.TraitText(f));
            Set(evoState, $"{GunEvolution.StarsNeeded} stars: {(starCount >= GunEvolution.StarsNeeded ? "done" : $"{starCount}/{GunEvolution.StarsNeeded}")} · " +
                          $"Mastery {GunMastery.MaxLevel}: {(level >= GunMastery.MaxLevel ? "done" : $"{level}/{GunMastery.MaxLevel}")}");
            Set(evolveLabel, block == GunEvolution.Block.None ? "EVOLVE" : block == GunEvolution.Block.AlreadyEvolved ? "EVOLVED" : "EVOLVE · LOCKED");
            if (evolveButton != null)
            {
                if (!evolveButton.TryGetComponent(out CanvasGroup cg)) cg = evolveButton.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = block == GunEvolution.Block.None ? 1f : 0.55f;
            }

            bool owned = PlayerProfile.IsWeaponOwned(id), carried = PlayerProfile.EquippedWeaponId == id;
            if (equipButton != null) equipButton.gameObject.SetActive(owned);
            Set(equipLabel, carried ? "EQUIPPED" : "EQUIP");
        }

        void Evolve()
        {
            if (_gun == null) return;
            var block = PlayerProfile.EvolveBlock(_gun.WeaponId);
            if (block == GunEvolution.Block.Stars)
            {
                // Stars are bought in the Arsenal, which opened this screen: take the player back there.
                UIFeedback.Error();
                Toast.Show($"{GunEvolution.BlockText(block)} · upgrade them in the Arsenal");
                UIManager.Instance?.Pop();
                return;
            }
            if (block != GunEvolution.Block.None) { UIFeedback.Error(); Toast.Show(GunEvolution.BlockText(block)); return; }
            if (!PlayerProfile.TryEvolveGun(_gun.WeaponId)) return;
            UIFeedback.LevelUp();
            ZombieWar.Audio.RadioDirector.GunEvolved();
            if (evoGun != null) { evoGun.sprite = gunIcon != null ? gunIcon.sprite : null; evoGun.preserveAspect = true; evoGun.enabled = evoGun.sprite != null; }
            Set(evoSheetSub, $"{_gun.weaponName.ToUpperInvariant()} · MASTERY {GunMastery.MaxLevel} · ★★★");
            Set(evoSheetEffect, GunEvolution.TraitText(_gun.weaponClass));
            Set(evoSheetBonus, $"{DailyOps.FamilyName(_gun.weaponClass)} family: {GunMastery.BonusText(_gun.weaponClass, 2)}");
            Active(evoSheet, true);
            if (evoSheet != null) UIFx.ModalIn(evoSheet.transform);
            Refresh();
        }

        void Equip()
        {
            if (_gun == null || !PlayerProfile.IsWeaponOwned(_gun.WeaponId)) return;
            if (PlayerProfile.EquippedWeaponId != _gun.WeaponId) { PlayerProfile.SetEquippedWeapon(_gun.WeaponId); UIFeedback.Equip(); }
            Refresh();
        }
    }
}
