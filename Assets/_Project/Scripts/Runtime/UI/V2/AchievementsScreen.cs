using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ZombieWar.UI.UIBind;

namespace ZombieWar.UI
{
    /// <summary>
    /// Achievements (backlog #22, mockup U8 approved 05/10): the ten achievements with progress, DONE,
    /// or the gem reward to claim. Opened from the Profile's achievements card.
    /// </summary>
    public sealed class AchievementsScreen : UIScreen
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public Image icon;
            public TMP_Text title, sub, done;
            public RectTransform bar;
            public Button claim;
            public TMP_Text claimLabel;
        }

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel, gemLabel, countLabel;
        [SerializeField] private Row[] rows = new Row[15];
        [SerializeField] private Sprite lockedIcon, unlockedIcon;

        protected override void Awake()
        {
            base.Awake();
            On(backButton, () => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            for (int i = 0; i < rows.Length; i++) { int k = i; if (rows[i]?.claim != null) rows[i].claim.onClick.AddListener(() => Claim(k)); }
        }

        void OnEnable() => PlayerProfile.WalletChanged += Refresh;
        void OnDisable() => PlayerProfile.WalletChanged -= Refresh;

        protected override void OnShow() => Refresh();

        void Refresh()
        {
            if (!gameObject.activeInHierarchy) return;
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            Set(countLabel, $"{Achievements.UnlockedCount} / {Achievements.All.Count}");
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i]; if (r?.root == null) continue;
                bool has = i < Achievements.All.Count; r.root.SetActive(has);
                if (!has) continue;
                var a = Achievements.All[i];
                bool unlocked = Achievements.IsUnlocked(a.id), claimed = Achievements.IsClaimed(a.id);
                Set(r.title, a.title);
                Set(r.sub, a.description.ToUpperInvariant());
                if (r.icon != null) { r.icon.sprite = unlocked ? unlockedIcon : lockedIcon; r.icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f); }
                float p = unlocked ? 1f : Achievements.Progress(a.id);
                if (r.bar != null) { r.bar.parent.gameObject.SetActive(!unlocked); UIBarClip.Set(r.bar, p); }
                if (r.claim != null) r.claim.gameObject.SetActive(unlocked && !claimed);
                Set(r.claimLabel, $"+{a.gems}");
                Active(r.done != null ? r.done.gameObject : null, claimed);
            }
        }

        void Claim(int i)
        {
            if (i >= Achievements.All.Count) return;
            var a = Achievements.All[i];
            if (!Achievements.ClaimReward(a.id)) return;
            UIFeedback.Purchase();
            Toast.Show($"{a.title} · +{a.gems} gems");
            Refresh();
        }
    }
}
