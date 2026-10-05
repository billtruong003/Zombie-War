using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Studio (owner-approved V2_Studio mockup): the character in the middle with six group
    /// hotspots (head, face, top, back, pants, shoes); the chip row lists only the slots of the
    /// picked group, then a film strip of that slot's pieces and a bar for the picked piece. Any
    /// piece can be tried on before buying (visual only; leaving restores the saved outfit), and the
    /// piece in the open slot keeps a breathing outline on the character. Random dresses the character from
    /// owned pieces. Gacha-only pieces point to the Gacha. Up to three looks can be saved.
    /// Built by HordeCall/UI v2/Build Studio.
    /// </summary>
    public sealed class StudioScreen : UIScreen
    {
        [Serializable]
        public sealed class Film { public Button button; public Image bar; public Image icon; public TMP_Text price; public GameObject selected; }

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private RawImage character;
        [SerializeField] private Button[] hotspots = new Button[6];
        [SerializeField] private Button[] looks = new Button[PlayerProfile.MaxLooks];
        [SerializeField] private Button saveLook;
        [SerializeField] private Button randomButton;
        [Tooltip("PieceHighlight material: the outline flashed on the piece that changed.")]
        [SerializeField] private Material highlight;

        [Header("Sheet")]
        [SerializeField] private TMP_Text slotTitle;
        [SerializeField] private TMP_Text ownedLabel;
        [SerializeField] private GameObject gachaTag;
        [SerializeField] private TMP_Text gachaTagText;
        [SerializeField] private Button[] chips = new Button[24];
        [SerializeField] private ScrollEdgeFade chipsFade;
        [SerializeField] private ScrollRect strip;
        [SerializeField] private Film[] films = new Film[MaxPieces];

        [Header("Picked piece")]
        [SerializeField] private Image pickedIcon;
        [SerializeField] private TMP_Text pickedName;
        [SerializeField] private TMP_Text pickedState;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        [SerializeField] private ModularCostumeCatalog catalog;
        [SerializeField] private EconomyConfig economy;
        [SerializeField] private UIScreen gachaScreen;

        public const int MaxPieces = 80;
        /// Hotspot order: HEAD, FACE, TOP, BACK, PANTS, SHOES; each opens its first slot.
        public static readonly string[] HotspotSlots = { "Head", "Eye", "Chest", "Back", "Legs", "Feet" };

        /// The slots behind each hotspot (owner: outfits must read as clear groups). A slot not
        /// listed here falls into the group of its catalog CostumeGroup (head / top / pants).
        public static readonly string[][] HotspotGroups =
        {
            new[] { "Head", "Hair", "HairAccessory" },
            new[] { "Eye", "Brow", "Mouth", "Beard", "Mask", "Eyewear", "Earring" },
            new[] { "Chest", "Hands", "Bracelet", "Watch", "HandAccessory" },
            new[] { "Back" },
            new[] { "Legs" },
            new[] { "Feet" },
        };


        MenuCharacterStage _stage;
        List<ModularCostumeCatalog.SlotDefinition> _slots = new();
        string _slot = "Chest";
        List<ModularCostumeCatalog.PartEntry> _parts = new();
        string _picked;          // itemId being shown (worn or tried)
        List<ModularCostumeCatalog.SlotDefinition> _groupSlots = new();
        SkinnedMeshRenderer _hlRenderer;
        Material _hlMat;

        protected override void Awake()
        {
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(() => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            for (int i = 0; i < hotspots.Length; i++) { int idx = i; if (hotspots[i] != null) hotspots[i].onClick.AddListener(() => SelectSlot(HotspotSlots[idx])); }
            for (int i = 0; i < chips.Length; i++) { int idx = i; if (chips[i] != null) chips[i].onClick.AddListener(() => { if (idx < _groupSlots.Count) SelectSlot(_groupSlots[idx].id); }); }
            for (int i = 0; i < films.Length; i++) { int idx = i; if (films[i]?.button != null) films[i].button.onClick.AddListener(() => Pick(idx)); }
            for (int i = 0; i < looks.Length; i++) { int idx = i; if (looks[i] != null) looks[i].onClick.AddListener(() => WearLook(idx)); }
            if (saveLook != null) saveLook.onClick.AddListener(SaveLook);
            if (randomButton != null) randomButton.onClick.AddListener(RandomOutfit);
            if (actionButton != null) actionButton.onClick.AddListener(Act);
        }

        private void OnEnable() { PlayerProfile.CostumeChanged += Refresh; PlayerProfile.WalletChanged += Refresh; }
        private void OnDisable() { PlayerProfile.CostumeChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh; StopHighlight(); RestoreOutfit(); }

        protected override void OnShow()
        {
            ZombieWar.Audio.RadioDirector.StudioShown();
            _stage = FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (character != null && _stage != null) character.texture = _stage.Texture;
            _slots = catalog != null ? catalog.slotDefinitions.Where(d => !catalog.IsTechnicalCasualSlot(d.id)).OrderBy(d => d.sortOrder).ToList() : new();
            if (catalog != null) PlayerProfile.EnsureValidCostumeLoadout(catalog);
            SelectSlot(_slot);
        }

        protected override void OnHide() => RestoreOutfit();

        void RestoreOutfit() { if (_stage != null && _stage.ModularApplier != null) _stage.ModularApplier.ApplySavedParts(); }

        void SelectSlot(string slot)
        {
            UIFeedback.Tap();
            RestoreOutfit();
            _slot = slot;
            int g = GroupOf(slot);
            _groupSlots = _slots.Where(d => GroupOf(d.id) == g).ToList();
            var s = catalog != null ? catalog.GetSlot(slot) : null;
            _picked = PlayerProfile.GetPart(slot);
            _parts = s != null ? Sorted(s.parts, _picked) : new();
            if (strip != null) strip.horizontalNormalizedPosition = 0f;
            Refresh();
        }

        /// Owner 05/10: the piece being worn comes first, then the owned ones (rarest first), then
        /// what can be bought (coins before gems, cheapest first), gacha-only pieces last. Catalog
        /// order breaks ties. Sorted once per slot, so a purchase never makes the strip jump.
        List<ModularCostumeCatalog.PartEntry> Sorted(IEnumerable<ModularCostumeCatalog.PartEntry> parts, string worn) =>
            parts.OrderBy(p => SortRank(p.itemId, worn))
                 .ThenByDescending(p => economy != null && economy.TryGetCostume(p.itemId, out var e) && PlayerProfile.IsCostumeOwned(p.itemId) ? (int)e.rarity : 0)
                 .ThenBy(p => economy != null && economy.TryGetCostume(p.itemId, out var e) && !PlayerProfile.IsCostumeOwned(p.itemId) ? e.price : 0)
                 .ToList();

        int SortRank(string id, string worn)
        {
            if (id == worn) return 0;
            if (PlayerProfile.IsCostumeOwned(id)) return 1;
            if (economy == null || !economy.TryGetCostume(id, out var e)) return 5;
            if (e.source == AcquireSource.Gacha) return 4;
            return e.currency == WalletCurrency.Gem ? 3 : 2;
        }

        void Pick(int i)
        {
            if (i >= _parts.Count) return;
            var p = _parts[i];
            _picked = p.itemId;
            UIFeedback.Tap();
            if (PlayerProfile.IsCostumeOwned(p.itemId)) { if (catalog != null) PlayerProfile.TryEquipCostume(catalog, p.itemId); }
            else if (_stage != null && _stage.ModularApplier != null) _stage.ModularApplier.Apply(_slot, p);   // try on
            Refresh();
        }

        /// Which hotspot group a slot belongs to (index into HotspotGroups).
        int GroupOf(string slot)
        {
            for (int i = 0; i < HotspotGroups.Length; i++) if (Array.IndexOf(HotspotGroups[i], slot) >= 0) return i;
            var def = catalog != null ? catalog.GetSlotDefinition(slot) : null;
            return def == null ? 0 : def.group switch
            {
                ModularCostumeCatalog.CostumeGroup.Body => 2,
                ModularCostumeCatalog.CostumeGroup.Legs => 4,
                _ => 0,
            };
        }

        CostumeRandomizer _randomizer;
        readonly System.Random _rng = new();

        /// Owned pieces only; see CostumeRandomizer for the clash and colour rules.
        void RandomOutfit()
        {
            if (catalog == null) return;
            // No clipping pieces, colours that go together (CostumeRandomizer).
            _randomizer ??= CostumeRandomizer.Load();
            var outfit = _randomizer.Generate(catalog, PlayerProfile.IsCostumeOwned, _rng, economy != null ? economy.costumeSets : null);
            if (PlayerProfile.TrySetCasualOutfit(catalog, outfit) == PlayerProfile.CostumeEquipResult.Equipped)
            {
                UIFeedback.Equip();
                _picked = PlayerProfile.GetPart(_slot);
                RestoreOutfit();
                Refresh();
            }
            else UIFeedback.Error();
        }

        // ------------------------------------------------------------ picked-piece outline
        /// Owner 2026-09-27: the piece being worn or tried in the open slot always carries a soft,
        /// breathing outline. Followed every frame, so a swapped renderer (new piece, try-on, look,
        /// Random) picks it up without any caller having to remember.
        void Update()
        {
            if (!IsShown || highlight == null) { StopHighlight(); return; }
            var applier = _stage != null ? _stage.ModularApplier : null;
            var r = applier != null ? applier.GetRenderer(_slot) : null;
            if (r != _hlRenderer) { StopHighlight(); if (r != null) StartHighlight(r); }
            if (_hlMat != null)
            {
                var c = _hlMat.color;
                c.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3.2f);
                _hlMat.color = c;
            }
        }

        // The outline is a second renderer sharing the piece's mesh and bones, with the highlight
        // material on EVERY submesh (05/10: appended as an extra material it only drew over the
        // last submesh, so multi-part pieces showed a broken line).
        SkinnedMeshRenderer _hlCopy;

        void StartHighlight(SkinnedMeshRenderer r)
        {
            _hlRenderer = r;
            _hlMat = new Material(highlight) { name = "PieceHighlight (picked)" };
            var go = new GameObject("PieceHighlight");
            go.layer = r.gameObject.layer;
            go.transform.SetParent(r.transform.parent, false);
            go.transform.SetLocalPositionAndRotation(r.transform.localPosition, r.transform.localRotation);
            go.transform.localScale = r.transform.localScale;
            _hlCopy = go.AddComponent<SkinnedMeshRenderer>();
            _hlCopy.sharedMesh = r.sharedMesh;
            _hlCopy.rootBone = r.rootBone;
            _hlCopy.bones = r.bones;
            _hlCopy.localBounds = r.localBounds;
            _hlCopy.updateWhenOffscreen = true;
            _hlCopy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hlCopy.receiveShadows = false;
            var mats = new Material[Mathf.Max(1, r.sharedMesh != null ? r.sharedMesh.subMeshCount : 1)];
            for (int i = 0; i < mats.Length; i++) mats[i] = _hlMat;
            _hlCopy.sharedMaterials = mats;
        }

        void StopHighlight()
        {
            if (_hlCopy != null) Destroy(_hlCopy.gameObject);
            if (_hlMat != null) Destroy(_hlMat);
            _hlCopy = null; _hlRenderer = null; _hlMat = null;
        }

        void Act()
        {
            if (string.IsNullOrEmpty(_picked) || economy == null) return;
            if (PlayerProfile.IsCostumeOwned(_picked)) { Toast.Show("Wearing it"); return; }
            if (!economy.TryGetCostume(_picked, out var item)) return;
            if (item.source == AcquireSource.Gacha)
            {
                UIFeedback.Tap();
                if (gachaScreen != null && AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha)) UIManager.Instance?.Push(gachaScreen);
                else Toast.Show($"Gacha only · unlocks at level {AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha)}");
                return;
            }
            var r = PlayerProfile.TryPurchaseCostume(economy, _picked);
            if (r == PlayerProfile.PurchaseResult.Purchased)
            {
                UIFeedback.Purchase();
                if (catalog != null) PlayerProfile.TryEquipCostume(catalog, _picked);
                Toast.Show($"{item.displayName} is yours");
            }
            else if (r == PlayerProfile.PurchaseResult.InsufficientFunds) { UIFeedback.Error(); Toast.Show(item.currency == WalletCurrency.Gem ? "Not enough gems" : "Not enough coins"); }
        }

        void WearLook(int i)
        {
            var l = PlayerProfile.GetLook(i);
            if (l == null) { Toast.Show("Save a look with +"); return; }
            var res = catalog != null ? PlayerProfile.TrySetCasualOutfit(catalog, l) : PlayerProfile.CostumeEquipResult.InvalidPart;
            if (res == PlayerProfile.CostumeEquipResult.Equipped) { UIFeedback.Equip(); _picked = PlayerProfile.GetPart(_slot); }
            else { UIFeedback.Error(); Toast.Show("That look has a piece you no longer own"); }
        }

        void SaveLook()
        {
            int index = PlayerProfile.LookCount < PlayerProfile.MaxLooks ? PlayerProfile.LookCount : 0;
            if (PlayerProfile.SaveLook(index)) { UIFeedback.Confirm(); Toast.Show($"Saved as look {index + 1}"); Refresh(); }
        }

        /// Piece tile fill per rarity, Common..Legendary (same tints as the gacha rate tiles).
        static readonly ThemeRole[] TileRoles = { ThemeRole.Card, ThemeRole.ClaimTint, ThemeRole.InfoTint, ThemeRole.GemTint, ThemeRole.LegendTint };

        /// The owned count follows the slot title, so a long name ("HAIR ACCESSORY") never runs
        /// into it; the title shrinks first when both would not fit before the gacha tag.
        void PlaceOwnedLabel()
        {
            if (slotTitle == null || ownedLabel == null) return;
            var tr = slotTitle.rectTransform; var or = ownedLabel.rectTransform;
            var head = tr.parent as RectTransform; if (head == null) return;
            float room = head.rect.width - (gachaTag != null && gachaTag.activeSelf ? ((RectTransform)gachaTag.transform).rect.width + 8f : 0f);
            float ownedW = ownedLabel.GetPreferredValues(ownedLabel.text).x;
            float maxTitle = Mathf.Max(40f, room - ownedW - 10f);
            tr.sizeDelta = new Vector2(maxTitle, tr.sizeDelta.y);
            slotTitle.ForceMeshUpdate();
            float titleW = Mathf.Min(slotTitle.GetPreferredValues(slotTitle.text).x, maxTitle);
            if (slotTitle.enableAutoSizing) titleW = Mathf.Min(slotTitle.textBounds.size.x, maxTitle);
            or.anchoredPosition = new Vector2(tr.anchoredPosition.x + titleW + 10f, or.anchoredPosition.y);
            or.sizeDelta = new Vector2(ownedW + 4f, or.sizeDelta.y);
        }

        static Graphic Face(Button b)
        {
            var f = b.transform.Find("Face");
            return f != null ? f.GetComponent<Graphic>() : b.targetGraphic;
        }

        public void Refresh()
        {
            if (!IsShown && !gameObject.activeInHierarchy) return;
            if (coinLabel != null) coinLabel.text = HomeScreen.Short(PlayerProfile.Coin);
            if (gemLabel != null) gemLabel.text = HomeScreen.Short(PlayerProfile.Gem);

            for (int i = 0; i < hotspots.Length; i++)
            {
                if (hotspots[i] == null) continue;
                bool on = i == GroupOf(_slot);
                ThemeTint.Set(Face(hotspots[i]), on ? ThemeRole.Primary : ThemeRole.Card);
                var t = hotspots[i].GetComponentInChildren<TMP_Text>(true); ThemeTint.Set(t, on ? ThemeRole.PrimaryOn : ThemeRole.TextOnSurface);
            }
            for (int i = 0; i < looks.Length; i++)
            {
                if (looks[i] == null) continue;
                bool has = i < PlayerProfile.LookCount;
                looks[i].gameObject.SetActive(has);
                ThemeTint.Set(looks[i].targetGraphic, PlayerProfile.IsWearingLook(i) ? ThemeRole.Primary : ThemeRole.Card);
            }
            if (saveLook != null) saveLook.gameObject.SetActive(true);

            var def = catalog != null ? catalog.GetSlotDefinition(_slot) : null;
            if (slotTitle != null) slotTitle.text = (def != null ? def.displayName : _slot).ToUpperInvariant();
            int owned = _parts.Count(p => PlayerProfile.IsCostumeOwned(p.itemId));
            if (ownedLabel != null) ownedLabel.text = $"{owned} / {_parts.Count} OWNED";
            PlaceOwnedLabel();
            int gachaOnly = _parts.Count(p => economy != null && economy.TryGetCostume(p.itemId, out var e) && e.source == AcquireSource.Gacha);
            if (gachaTag != null) gachaTag.SetActive(gachaOnly > 0);
            if (gachaTagText != null) gachaTagText.text = $"GACHA ONLY {gachaOnly}";

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] == null) continue;
                bool has = i < _groupSlots.Count; chips[i].gameObject.SetActive(has);
                if (!has) continue;
                bool on = _groupSlots[i].id == _slot;
                ThemeTint.Set(chips[i].targetGraphic, on ? ThemeRole.Ink : ThemeRole.Card);
                var t = chips[i].GetComponentInChildren<TMP_Text>(true);
                if (t != null) { t.text = _groupSlots[i].displayName.ToUpperInvariant(); ThemeTint.Set(t, on ? ThemeRole.OnInk : ThemeRole.Dim); }
                if (on && chipsFade != null) chipsFade.Reveal((RectTransform)chips[i].transform);
            }

            for (int i = 0; i < films.Length; i++)
            {
                var f = films[i]; if (f?.button == null) continue;
                bool has = i < _parts.Count; f.button.gameObject.SetActive(has);
                if (!has) continue;
                var p = _parts[i];
                EconomyConfig.CostumeEntry item = default;
                bool known = economy != null && economy.TryGetCostume(p.itemId, out item);
                bool own = PlayerProfile.IsCostumeOwned(p.itemId);
                ThemeTint.Set(f.bar, ThemePalette.Rarity(known ? (int)item.rarity : 0));
                int tier = known ? Mathf.Clamp((int)item.rarity, 0, 4) : 0;
                ThemeTint.Set(f.button.targetGraphic, TileRoles[tier]);
                ItemTileFx.On(f.button.targetGraphic, tier);
                if (f.icon != null) { f.icon.sprite = p.icon; f.icon.enabled = p.icon != null; f.icon.preserveAspect = true; }
                if (f.price != null)
                {
                    f.price.text = own ? "OWNED" : !known ? "-" : item.source == AcquireSource.Gacha ? "GACHA" : $"{item.price:N0}";
                    ThemeTint.Set(f.price, own ? ThemeRole.ClaimLip : known && item.source == AcquireSource.Gacha ? ThemeRole.Rarity4 : known && item.currency == WalletCurrency.Gem ? ThemeRole.GemLip : ThemeRole.TextOnSurface);
                }
                if (f.selected != null) f.selected.SetActive(p.itemId == _picked);
            }

            var picked = _parts.FirstOrDefault(p => p.itemId == _picked);
            bool pickedOwned = !string.IsNullOrEmpty(_picked) && PlayerProfile.IsCostumeOwned(_picked);
            EconomyConfig.CostumeEntry pi = default;
            bool piKnown = economy != null && !string.IsNullOrEmpty(_picked) && economy.TryGetCostume(_picked, out pi);
            if (pickedIcon != null) { pickedIcon.sprite = picked.icon; pickedIcon.enabled = picked.icon != null; pickedIcon.preserveAspect = true; }
            if (pickedName != null) pickedName.text = piKnown ? pi.displayName : string.IsNullOrEmpty(picked.name) ? "Nothing" : picked.name;
            if (pickedState != null) pickedState.text = (pickedOwned ? "WEARING" : "TRYING ON") + (piKnown ? " · " + pi.rarity.ToString().ToUpperInvariant() : "");
            if (actionLabel != null)
                actionLabel.text = pickedOwned || !piKnown ? "WEARING" : pi.source == AcquireSource.Gacha ? "IN GACHA" : $"BUY {pi.price:N0}{(pi.currency == WalletCurrency.Gem ? " GEMS" : "")}";
            if (actionButton != null) actionButton.gameObject.SetActive(!pickedOwned && piKnown);
        }
    }
}
