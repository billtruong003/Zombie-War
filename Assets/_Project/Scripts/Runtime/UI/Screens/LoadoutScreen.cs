using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// LOADOUT: pick the ONE weapon the next run is played with (M6 one-weapon contract).
    /// Presentation is authored in the prefab; runtime binds data and handles interaction:
    /// - Ownership comes from PlayerProfile.
    /// - The first slot view shows the run weapon. The prefab still has the retired second and third
    ///   slots and the bomb row; they are hidden here until the owner removes them.
    /// - Tapping a card always shows its details, and equips it when it is owned; otherwise the card
    ///   shakes and nothing changes.
    /// - Refreshes on PlayerProfile.LoadoutChanged.
    /// </summary>
    public sealed class LoadoutScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button shopLinkButton;
        [SerializeField] private UIScreen shopScreen;

        [Header("Data (icon/authoring metadata — KHÔNG phải ownership)")]
        [SerializeField] private UIPrototypeCatalog catalog;

        [Header("Authored views")]
        [SerializeField] private LoadoutSlotView[] slotViews;      // [0] = run weapon; the rest are retired
        [SerializeField] private WeaponItemCardView[] ownedCards;  // 1 card / WeaponData, bake sẵn
        [SerializeField] private Image infoIcon;
        [SerializeField] private TMP_Text infoNameLabel;
        [SerializeField] private Image[] statBars;                 // DMG / TỐC BẮN / TẦM fills

        [Header("M8: the weapon's own cards")]
        [Tooltip("The equipped family's two signature cards, so the player sees what this gun unlocks in a run.")]
        [SerializeField] private TMP_Text signatureCaption;
        [SerializeField] private TMP_Text[] signatureLabels;
        [SerializeField] private Image heroBackdrop;

        private WeaponItemCardView _selected;
        private List<WeaponData> _arsenal;
        private readonly HashSet<string> _warnedIds = new();

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(shopLinkButton, () =>
            {
                if (shopScreen != null) UIManager.Instance.Push(shopScreen);
            });
            if (ownedCards != null)
                foreach (var card in ownedCards)
                {
                    var c = card;
                    if (c != null && c.button != null)
                        c.button.onClick.AddListener(() => OnCardClicked(c));
                }
        }

        private void OnEnable() => PlayerProfile.LoadoutChanged += RefreshFromState;
        private void OnDisable() => PlayerProfile.LoadoutChanged -= RefreshFromState;

        protected override void OnShow()
        {
            // Chuan hoa loadout ngay khi mo man (khong doi vao gameplay): seed starter cho profile
            // moi, canonical hoa id legacy, dam bao sung dang trang bi deu owned — cung mot op
            // da test o PlayerProfile.EnsureValidLoadout, arsenal = 25 card cua man nay.
            PlayerProfile.EnsureValidLoadout(Arsenal);
            PlayerProfile.ClearUnseenWeapons(); // xem súng mới -> tắt badge
            RefreshFromState();
            ShowEquippedDetails();
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        /// Arsenal = mọi WeaponData bake trong cards (nguồn hiển thị duy nhất của màn này).
        /// Card null/data null/id rỗng/id trùng chỉ cảnh báo 1 lần — không chết màn.
        private IReadOnlyList<WeaponData> Arsenal
        {
            get
            {
                if (_arsenal != null) return _arsenal;
                _arsenal = new List<WeaponData>(ownedCards != null ? ownedCards.Length : 0);
                var seenIds = new HashSet<string>();
                if (ownedCards != null)
                    foreach (var card in ownedCards)
                    {
                        var d = card != null ? card.data : null;
                        if (d == null) { WarnOnce("<null-card>", "Card thiếu WeaponData"); continue; }
                        if (string.IsNullOrEmpty(d.WeaponId)) { WarnOnce(d.name, $"WeaponData '{d.name}' thiếu WeaponId"); continue; }
                        if (!seenIds.Add(d.WeaponId)) WarnOnce(d.WeaponId, $"WeaponId trùng lặp '{d.WeaponId}'");
                        _arsenal.Add(d);
                    }
                return _arsenal;
            }
        }

        // ------------------------------------------------ state -> view

        private void RefreshFromState()
        {
            if (!IsShown) return;
            RefreshOwnership();
            RefreshRunWeapon();
        }

        private void RefreshOwnership()
        {
            if (ownedCards == null) return;
            foreach (var card in ownedCards)
            {
                if (card == null || card.data == null) continue;
                // Icon nằm trong UIPrototypeCatalog (asset), không bake trong prefab — bind 1 lần.
                if (card.icon != null && card.icon.sprite == null && catalog != null)
                {
                    var sprite = catalog.GetWeaponIcon(card.data);
                    if (sprite != null) { card.icon.sprite = sprite; card.icon.color = card.data.IconTint; }
                }
                // Ownership thật từ profile — cheatUnlockAll cố tình KHÔNG được hỏi ở đây.
                // Rarity reads from the tile before the name does (M8 mockup).
                var bg = card.transform.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = Color.Lerp(new Color(0.16f, 0.18f, 0.24f), card.data.TierColor, 0.22f);
                bool owned = PlayerProfile.IsWeaponOwned(card.data.WeaponId);
                if (card.lockOverlay != null) card.lockOverlay.SetActive(!owned);
                if (card.ownedBadge != null) card.ownedBadge.SetActive(false); // grid không dùng badge (đè tên)
            }
        }

        private void RefreshRunWeapon()
        {
            if (slotViews == null || slotViews.Length == 0 || slotViews[0] == null) return;
            string id = LoadoutState.WeaponId;
            WeaponData d = LoadoutState.Resolve(id, Arsenal);
            if (d == null && !string.IsNullOrEmpty(id))
                WarnOnce(id, $"Run weapon id '{id}' is not in the catalog — shown empty, NOT replaced");
            slotViews[0].Bind(d, d != null && catalog != null ? catalog.GetWeaponIcon(d) : null);
            slotViews[0].SetSelected(true);
        }

        private void ShowEquippedDetails()
        {
            var equipped = LoadoutState.Resolve(LoadoutState.WeaponId, Arsenal);
            var card = FindCard(equipped) ?? _selected ?? FirstCard();
            if (card != null) ShowDetails(card);
        }

        // ------------------------------------------------ interaction

        private void OnCardClicked(WeaponItemCardView card)
        {
            if (card == null || card.data == null) return;
            ShowDetails(card);

            var result = LoadoutState.TryEquip(card.data);
            if (result == LoadoutState.EquipResult.Equipped) return; // LoadoutChanged refreshes the slot
            UIFx.Shake((RectTransform)card.transform); // locked: details are shown, state is unchanged
        }

        private void ShowDetails(WeaponItemCardView card)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = card;
            card.SetSelected(true);

            var d = card.data;
            if (infoIcon != null)
            {
                infoIcon.sprite = card.icon != null ? card.icon.sprite : null;
                infoIcon.color = infoIcon.sprite != null ? d.IconTint : UITheme.Surface2;
                infoIcon.preserveAspect = true;
            }
            if (infoNameLabel != null)
                infoNameLabel.text =
                    $"{d.weaponName} · <color=#{ColorUtility.ToHtmlStringRGB(d.TierColor)}>{d.tier.ToString().ToUpperInvariant()}</color>";

            if (heroBackdrop != null) heroBackdrop.color = Color.Lerp(new Color(0.12f, 0.14f, 0.19f), d.TierColor, 0.3f);
            BindSignatures(d);

            // Stat bar: chuẩn hoá TẠM (provisional) — chỉ để so sánh tương đối, chưa phải
            // normalization chính thức (Docs/TASK_BREAKDOWN.md mục A).
            SetStat(0, d.damage / 60f);
            SetStat(1, d.fireRate / 15f);
            SetStat(2, d.range / 60f);
        }

        private WeaponItemCardView FindCard(WeaponData data)
        {
            if (data == null || ownedCards == null) return null;
            foreach (var card in ownedCards)
                if (card != null && card.data == data)
                    return card;
            return null;
        }

        private WeaponItemCardView FirstCard()
        {
            if (ownedCards == null) return null;
            foreach (var card in ownedCards)
                if (card != null && card.data != null)
                    return card;
            return null;
        }

        private void SetStat(int i, float v01)
        {
            if (statBars == null || i >= statBars.Length || statBars[i] == null) return;
            var rt = statBars[i].rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(v01), 1f);
        }

        private void BindSignatures(WeaponData d)
        {
            if (signatureCaption != null)
                signatureCaption.text = $"{HubScreen.FamilyName(d.weaponClass)} CARDS IN A RUN";
            if (signatureLabels == null) return;
            int k = 0;
            foreach (var def in ZombieWar.Skills.SkillCatalogDefs.All)
            {
                if (def.layer != ZombieWar.Skills.SkillLayer.Signature || def.family != d.weaponClass) continue;
                if (k < signatureLabels.Length && signatureLabels[k] != null)
                {
                    signatureLabels[k].text = def.displayName;
                    signatureLabels[k].transform.parent.gameObject.SetActive(true);
                }
                k++;
            }
            for (; k < signatureLabels.Length; k++)
                if (signatureLabels[k] != null) signatureLabels[k].transform.parent.gameObject.SetActive(false);
        }

        private void WarnOnce(string key, string message)
        {
            if (_warnedIds.Add(key)) Debug.LogWarning($"[LoadoutScreen] {message}");
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
