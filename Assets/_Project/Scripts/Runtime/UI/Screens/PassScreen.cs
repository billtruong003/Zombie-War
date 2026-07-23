using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 08 BATTLE PASS: season XP (PlayerProfile.PassXp) + 3 quest row bind mission backend thật
    /// (PassMissions rotation UTC + PlayerProfile progress/claim). Track thưởng theo level và
    /// premium strip vẫn là presentation (chưa có backend reward track) — không fake claim.
    /// </summary>
    public sealed class PassScreen : UIScreen
    {
        /// <summary>XP mỗi level Pass. Provisional — chưa có bảng level authored riêng.</summary>
        public const int XpPerLevel = 500;

        [Serializable]
        private struct QuestRow
        {
            public TMP_Text nameLabel;
            public RectTransform barFill;
            public TMP_Text counter;
            public Button claim;
        }

        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button gachaLinkButton;
        [SerializeField] private ShopScreen shopScreen;

        [Header("Season")]
        [SerializeField] private TMP_Text seasonLevelLabel;
        [SerializeField] private RectTransform seasonFill;

        [Header("Quests (bind mission backend)")]
        [SerializeField] private QuestRow[] questRows;

        private readonly List<PassMission> _visible = new();

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(gachaLinkButton, () =>
            {
                if (shopScreen == null) return;
                shopScreen.OpenTab(1);   // link Gacha phải mở đúng tab Gacha, không phải Weapons
                UIManager.Instance.Push(shopScreen);
            });
            if (questRows != null)
                for (int i = 0; i < questRows.Length; i++)
                {
                    int row = i;
                    Wire(questRows[i].claim, () => ClaimRow(row));
                }
        }

        private void OnEnable() => PlayerProfile.MissionsChanged += Refresh;
        private void OnDisable() => PlayerProfile.MissionsChanged -= Refresh;

        protected override void OnShow()
        {
            PlayerProfile.RefreshMissionWindow(DateTime.UtcNow);
            Refresh();
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        // ------------------------------------------------ binding

        private void Refresh()
        {
            if (!IsShown && !gameObject.activeInHierarchy) return;
            RefreshSeason();
            RefreshQuests();
        }

        private void RefreshSeason()
        {
            int xp = PlayerProfile.PassXp;
            int level = xp / XpPerLevel + 1;
            int into = xp % XpPerLevel;
            if (seasonLevelLabel != null)
                seasonLevelLabel.text = $"LEVEL {level}  ·  {into}/{XpPerLevel} XP";
            if (seasonFill != null)
                seasonFill.anchorMax = new Vector2(Mathf.Clamp01(into / (float)XpPerLevel), 1f);
        }

        /// 3 row hiển thị các mission active đáng chú ý nhất: claim được trước,
        /// rồi đang dở theo % giảm dần, mission đã claim xếp cuối.
        private void RefreshQuests()
        {
            if (questRows == null || questRows.Length == 0) return;

            _visible.Clear();
            _visible.AddRange(PassMissions.ActiveFor(DateTime.UtcNow));
            _visible.Sort((a, b) => Priority(b).CompareTo(Priority(a)));

            for (int i = 0; i < questRows.Length; i++)
            {
                var row = questRows[i];
                if (i >= _visible.Count) { SetRowEmpty(row); continue; }

                var mission = _visible[i];
                int progress = PlayerProfile.GetMissionProgress(mission.id);
                bool claimed = PlayerProfile.IsMissionClaimed(mission.id);
                bool claimable = !claimed && progress >= mission.target;

                if (row.nameLabel != null) row.nameLabel.text = mission.title;
                if (row.barFill != null)
                    row.barFill.anchorMax = new Vector2(
                        claimed ? 1f : Mathf.Clamp01(progress / (float)mission.target), 1f);
                if (row.counter != null)
                {
                    row.counter.gameObject.SetActive(!claimable);
                    row.counter.text = claimed ? "CLAIMED" : $"{progress}/{mission.target}";
                    row.counter.color = claimed ? UITheme.Green : UITheme.TextDim;
                }
                if (row.claim != null)
                    row.claim.gameObject.SetActive(claimable);
            }
        }

        private static float Priority(PassMission m)
        {
            if (PlayerProfile.IsMissionClaimed(m.id)) return -1f;
            int progress = PlayerProfile.GetMissionProgress(m.id);
            if (progress >= m.target) return 2f;                    // claim được — lên đầu
            return progress / (float)m.target;                      // đang dở — theo %
        }

        private static void SetRowEmpty(QuestRow row)
        {
            if (row.nameLabel != null) row.nameLabel.text = "—";
            if (row.barFill != null) row.barFill.anchorMax = new Vector2(0f, 1f);
            if (row.counter != null) { row.counter.gameObject.SetActive(true); row.counter.text = ""; }
            if (row.claim != null) row.claim.gameObject.SetActive(false);
        }

        private void ClaimRow(int index)
        {
            if (index < 0 || index >= _visible.Count) return;
            var mission = _visible[index];
            if (PlayerProfile.TryClaimMission(mission.id))
            {
                var row = questRows[index];
                UIFx.Punch(row.nameLabel != null ? row.nameLabel.transform : transform);
                // MissionsChanged đã bắn từ TryClaimMission → Refresh tự chạy, wallet cluster tự cộng.
            }
            else if (questRows[index].claim != null)
            {
                UIFx.Shake((RectTransform)questRows[index].claim.transform);
            }
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
