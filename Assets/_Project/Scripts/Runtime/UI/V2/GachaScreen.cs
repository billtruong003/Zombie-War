using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Gacha (owner-approved V2_Gacha mockup): banner tabs, the banner art (a gun turning in
    /// the featured skin, or the outfit preview), guarantee progress, public rates, x1 / x10 pulls
    /// (free daily pull, then tickets, then gems) and a results sheet. Rules live in
    /// <see cref="GachaBanners"/>. Built by HordeCall/UI v2/Build Gacha.
    /// </summary>
    public sealed class GachaScreen : UIScreen
    {
        [Serializable]
        public sealed class Tab { public Button button; public TMP_Text sub; public GameObject selected; }

        [Serializable]
        public sealed class Tile { public GameObject root; public Image bg; public TMP_Text label; public TMP_Text note; public Image chest; public Image icon; }

        [Serializable]
        public sealed class NamedSprite { public string name; public Sprite sprite; }

        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private TMP_Text ticketLabel;
        [SerializeField] private Tab[] tabs = new Tab[3];

        [Header("Banner")]
        [SerializeField] private Image bannerBg;
        [SerializeField] private BannerFx bannerFx;
        [Tooltip("The chest show: one chest in the spotlight, and the pops of the x10 grid.")]
        [SerializeField] private GachaSpotlight spotlight;
        [SerializeField] private TMP_Text bannerTimer;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private GunTurntable turntable;
        [SerializeField] private RawImage gunArt;
        [SerializeField] private RawImage outfitArt;
        [SerializeField] private TMP_Text featuredTag;
        [SerializeField] private TMP_Text featuredSub;
        [SerializeField] private TMP_Text pityLabel;
        [SerializeField] private RectTransform pityBar;
        [SerializeField] private Button ratesButton;

        [Header("Pull")]
        [SerializeField] private Button pullOne;
        [SerializeField] private TMP_Text pullOneSub;
        [SerializeField] private Button pullTen;
        [SerializeField] private TMP_Text pullTenSub;
        [SerializeField] private TMP_Text freeLabel;
        [SerializeField] private Tile[] rateTiles = new Tile[5];
        [SerializeField] private TMP_Text note;

        [Header("Sheets")]
        [SerializeField] private GameObject resultsSheet;
        [SerializeField] private Tile[] resultTiles = new Tile[GachaBanners.MultiBoxes];
        [SerializeField] private Button resultsOk;
        [SerializeField] private Button resultsSkip;
        [SerializeField] private TMP_Text resultsTitle;
        [Tooltip("Chest per rarity, Common..Legendary.")]
        [SerializeField] private Sprite[] chests = new Sprite[5];
        [SerializeField] private NamedSprite[] rewardIcons;
        [SerializeField] private GameObject ratesSheet;
        [SerializeField] private TMP_Text ratesText;
        [SerializeField] private Button ratesOk;

        [SerializeField] private EconomyConfig economy;
        [Tooltip("Prize icons: gun icons (UIPrototypeCatalog) and costume piece icons (casual catalog).")]
        [SerializeField] private UIPrototypeCatalog weaponIcons;
        [SerializeField] private ModularCostumeCatalog costumes;
        [SerializeField] private NavBarV2 nav;

        static readonly Color[] TierColor =
        {
            new(0.184f, 0.208f, 0.267f), new(0.122f, 0.227f, 0.149f), new(0.122f, 0.192f, 0.314f), new(0.227f, 0.165f, 0.333f), new(0.29f, 0.165f, 0.047f),
        };
        /// Rate tiles on the screen follow the theme; the results sheet is a dark overlay in every theme.
        static readonly ThemeRole[] TierRoles = { ThemeRole.Card, ThemeRole.ClaimTint, ThemeRole.InfoTint, ThemeRole.GemTint, ThemeRole.LegendTint };
        /// Living banner colours: event (magenta night), outfits (teal street), shards (royal blue,
        /// electric cyan glow; the old navy + gold rays read muddy, owner 2026-09-29). The glow also
        /// tints the eyebrow, the sub line and the pity bar so each banner is one colour family.
        static readonly (Color a, Color b, Color glow)[] BannerPalette =
        {
            (new Color(0.227f, 0.086f, 0.212f), new Color(0.627f, 0.2f, 0.431f), new Color(1f, 0.604f, 0.839f)),
            (new Color(0.086f, 0.2f, 0.243f), new Color(0.165f, 0.478f, 0.549f), new Color(0.624f, 0.941f, 1f)),
            (new Color(0.055f, 0.086f, 0.259f), new Color(0.169f, 0.333f, 0.847f), new Color(0.49f, 0.886f, 1f)),
        };

        static readonly Color[] BannerColor = { new(0.29f, 0.12f, 0.25f), new(0.16f, 0.2f, 0.3f), new(0.12f, 0.19f, 0.31f) };

        int _banner;
        float _tick;
        readonly GachaService.SystemRng _rng = new();

        GachaBanners.Banner B => GachaBanners.All[Mathf.Clamp(_banner, 0, GachaBanners.All.Length - 1)];

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < tabs.Length; i++) { int idx = i; if (tabs[i]?.button != null) tabs[i].button.onClick.AddListener(() => { UIFeedback.Tap(); _banner = idx; Refresh(); }); }
            if (pullOne != null) pullOne.onClick.AddListener(() => DoPull(1));
            if (pullTen != null) pullTen.onClick.AddListener(() => DoPull(10));
            if (ratesButton != null) ratesButton.onClick.AddListener(ShowRates);
            if (resultsOk != null) resultsOk.onClick.AddListener(() => { UIFeedback.Back(); resultsSheet.SetActive(false); });
            if (resultsSkip != null) resultsSkip.onClick.AddListener(() => _skip = true);
            if (ratesOk != null) ratesOk.onClick.AddListener(() => { UIFeedback.Back(); ratesSheet.SetActive(false); });
        }

        private void OnEnable() => PlayerProfile.WalletChanged += Refresh;
        private void OnDisable() => PlayerProfile.WalletChanged -= Refresh;

        protected override void OnShow()
        {
            if (resultsSheet != null) resultsSheet.SetActive(false);
            if (ratesSheet != null) ratesSheet.SetActive(false);
            Refresh();
            ZombieWar.Audio.RadioDirector.GachaShown();
        }

        public override bool OnEscape()
        {
            if (resultsSheet != null && resultsSheet.activeSelf) { resultsSheet.SetActive(false); return true; }
            if (ratesSheet != null && ratesSheet.activeSelf) { ratesSheet.SetActive(false); return true; }
            return false;
        }

        void Update()
        {
            // Rate-row icons once the banner's gun has rendered (the skin prize is a still of it).
            if (!_rateIcons && (_rateIconsAt -= Time.unscaledDeltaTime) <= 0f) { _rateIcons = true; RateIcons(); }
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            if (freeLabel == null) return;
            freeLabel.text = GachaBanners.FreePullReady(DailyRewards.Today) ? "FREE PULL READY" : "FREE PULL IN " + ShopOffers.RefreshIn(DateTime.Now);
        }

        void DoPull(int count)
        {
            int today = DailyRewards.Today;
            var pay = GachaBanners.BestPay(B, count, today);
            if (!GachaBanners.CanPay(B, count, pay, today)) { UIFeedback.Error(); Toast.Show("Not enough gems or tickets"); return; }
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : new List<WeaponData>();
            var results = GachaBanners.Pull(B, count, pay, today, economy, guns, _rng);
            if (results == null) { UIFeedback.Error(); Toast.Show("This banner is empty right now"); return; }
            UIFeedback.Purchase();
            // Frames won in the Gacha: Neon for a x10 on an event banner, Royal for a Legendary.
            if (count >= 10 && B.kind == GachaBanners.Kind.Event) PlayerProfile.AddFrame("frame.neon");
            foreach (var res in results) if (res.tier >= WeaponTier.Legendary) { PlayerProfile.AddFrame("frame.royal"); break; }
            ShowResults(results);
            ZombieWar.Audio.FtueVoice.GachaPulled(results.Exists(r => r.tier >= WeaponTier.Legendary));
            Refresh();
        }

        // ------------------------------------------------------------ chest reveal
        bool _skip;
        Coroutine _reveal;

        void ShowResults(List<GachaBanners.Result> results)
        {
            if (resultsSheet == null) return;
            resultsSheet.SetActive(true);
            resultsSheet.transform.SetAsLastSibling();
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = StartCoroutine(Reveal(results));
        }

        /// <summary>
        /// Owner (2026-09-27): chests with a tween. Every box appears closed, its chest showing the
        /// rarity; then one by one each shakes (harder for rarer boxes), pops open and shows the
        /// prize. A tap skips to the end. x10 opens ten plus the bonus box.
        /// </summary>
        IEnumerator Reveal(List<GachaBanners.Result> results)
        {
            _skip = false;
            if (resultsOk != null) resultsOk.gameObject.SetActive(false);
            if (resultsSkip != null) resultsSkip.gameObject.SetActive(true);
            if (resultsTitle != null) resultsTitle.text = results.Count > 1 ? "OPENING 10 + 1 BONUS" : "OPENING";
            for (int i = 0; i < resultTiles.Length; i++)
            {
                var t = resultTiles[i]; if (t?.root == null) continue;
                bool has = i < results.Count; t.root.SetActive(has);
                if (!has) continue;
                Closed(t, results[i]);
                UIFx.PopIn(t.root.transform, i * 0.035f, 0.4f, 0.25f);
            }
            yield return Wait(0.35f + results.Count * 0.035f);

            // The best box is kept for last and opened in the spotlight (a single pull is just that).
            int best = 0;
            for (int i = 1; i < results.Count && i < resultTiles.Length; i++) if (results[i].tier >= results[best].tier) best = i;
            float gap = 0.1f;
            for (int i = 0; i < results.Count && i < resultTiles.Length; i++)
            {
                if (i == best && spotlight != null) continue;
                var t = resultTiles[i]; var r = results[i];
                int tier = Mathf.Clamp((int)r.tier, 0, 4);
                if (!_skip) yield return Shake(t.chest != null ? t.chest.transform : t.root.transform, 0.14f + tier * 0.06f, 5f + tier * 4f);
                Open(t, r);
                if (!_skip && spotlight != null) spotlight.MiniBurst((RectTransform)t.root.transform, tier);
                if (!_skip) yield return Wait(gap + (r.tier >= WeaponTier.Epic ? 0.2f : 0f));
            }
            if (spotlight != null && best < results.Count && best < resultTiles.Length)
            {
                var r = results[best];
                if (!_skip)
                {
                    int tier = Mathf.Clamp((int)r.tier, 0, 4);
                    var chestSprite = chests != null && tier < chests.Length ? chests[tier] : null;
                    var prize = PrizeSprite(r);
                    yield return spotlight.Play(tier, chestSprite, prize, r.label, NoteFor(r), () => _skip);
                }
                Open(resultTiles[best], r);
                if (!_skip) spotlight.MiniBurst((RectTransform)resultTiles[best].root.transform, (int)r.tier);
            }
            if (resultsTitle != null) resultsTitle.text = results.Count > 1 ? "10 + 1 BONUS" : "RESULT";
            if (resultsSkip != null) resultsSkip.gameObject.SetActive(false);
            if (resultsOk != null) { resultsOk.gameObject.SetActive(true); UIFx.PopIn(resultsOk.transform); }
            _reveal = null;
        }

        void Closed(Tile t, GachaBanners.Result r)
        {
            int tier = Mathf.Clamp((int)r.tier, 0, 4);
            if (t.bg != null) t.bg.color = TierColor[0];
            ItemTileFx.On(t.bg, -1);
            if (t.chest != null)
            {
                t.chest.gameObject.SetActive(true);
                t.chest.sprite = chests != null && tier < chests.Length ? chests[tier] : null;
                t.chest.transform.localScale = Vector3.one; t.chest.transform.localRotation = Quaternion.identity;
            }
            if (t.icon != null) t.icon.gameObject.SetActive(false);
            if (t.label != null) t.label.text = r.bonus ? "BONUS" : "";
            if (t.note != null) t.note.text = "";
        }

        void Open(Tile t, GachaBanners.Result r)
        {
            int tier = Mathf.Clamp((int)r.tier, 0, 4);
            if (t.chest != null) t.chest.gameObject.SetActive(false);
            if (t.bg != null) t.bg.color = TierColor[tier];
            ItemTileFx.On(t.bg, tier);
            if (t.icon != null)
            {
                var sp = PrizeSprite(r);
                t.icon.sprite = sp; t.icon.preserveAspect = true; t.icon.enabled = sp != null; t.icon.gameObject.SetActive(true);
                if (!_skip) UIFx.PopIn(t.icon.transform, 0f, 0.3f, 0.3f);
            }
            if (t.label != null) t.label.text = r.label;
            if (t.note != null) t.note.text = NoteFor(r);
            if (!_skip)
            {
                if (tier >= (int)WeaponTier.Epic) { UIFx.Punch(t.root.transform); UIFeedback.LevelUp(); }
                else UIFeedback.Card();
            }
        }

        Sprite _skinShot;
        string _skinShotId;
        bool _rateIcons;
        float _rateIconsAt;

        /// "In this banner" shows real items too: the skin on the banner gun, an outfit piece or a
        /// gun of each rarity, the ticket, the coins.
        void RateIcons()
        {
            var rates = GachaBanners.RatesFor(B, economy);
            bool waiting = false;
            for (int i = 0; i < rateTiles.Length && i < rates.Length; i++)
            {
                var t = rateTiles[i]; if (t?.icon == null) continue;
                var sp = RateSprite(rates[i], i);
                // The skin still needs the turntable to have drawn the gun: show the gun meanwhile, try again.
                if (sp == null && B.kind == GachaBanners.Kind.Event && i == 0) { waiting = true; sp = MainGunIcon() ?? Named(rates[i].icon); }
                if (sp != null) { t.icon.sprite = sp; t.icon.preserveAspect = true; t.icon.enabled = true; }
            }
            if (waiting && ++_rateTries < 10) { _rateIcons = false; _rateIconsAt = 0.4f; }
        }

        int _rateTries;

        Sprite MainGunIcon()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var main = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
            return main != null && weaponIcons != null ? weaponIcons.GetWeaponIcon(main, true) : null;
        }

        Sprite RateSprite(GachaBanners.Rate rate, int index)
        {
            var bn = B;
            if (bn.kind == GachaBanners.Kind.Event)
            {
                switch (index)
                {
                    case 0: return SkinShot(bn.featuredSkin);
                    case 1: return CostumeOfTier(WeaponTier.Epic, true);
                    case 2: return MainGunIcon() ?? Named(rate.icon);
                    default: return Named(rate.icon);
                }
            }
            if (bn.kind == GachaBanners.Kind.Outfits) return CostumeOfTier(rate.tier, false) ?? Named(rate.icon);
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            var gun = guns?.FirstOrDefault(w => w != null && w.tier == rate.tier);
            return gun != null && weaponIcons != null ? weaponIcons.GetWeaponIcon(gun, true) : Named(rate.icon);
        }

        static readonly string[] ReadableSlots = { "Chest", "Head", "Back", null };

        Sprite CostumeOfTier(WeaponTier tier, bool gachaOnly)
        {
            if (economy?.costumeItems == null || costumes == null) return null;
            // A piece that reads as clothing at 60 px first (a top, a hat, a backpack); the first
            // match used to be a bare body layer, shown as a T-posed mannequin (2026-09-30).
            Sprite any = null;
            foreach (var pass in ReadableSlots)
                foreach (var c in economy.costumeItems)
                    if (c.rarity == tier && (!gachaOnly || c.source == AcquireSource.Gacha) && !string.IsNullOrEmpty(c.itemId)
                        && costumes.TryFindByItemId(c.itemId, out var slot, out var part) && part.icon != null)
                    {
                        if (pass == null) { any ??= part.icon; continue; }
                        if (string.Equals(slot, pass, System.StringComparison.OrdinalIgnoreCase)) return part.icon;
                    }
            return any;
        }

        /// <summary>The prize's own icon: the gun, the outfit piece, the set, the skin on the
        /// banner's gun (a snapshot of the turntable), tickets or coins.</summary>
        Sprite PrizeSprite(GachaBanners.Result r)
        {
            switch (r.prize)
            {
                case GachaBanners.Prize.Gun:
                    var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
                    WeaponData d = null;
                    if (all != null) foreach (var w in all) if (w != null && w.WeaponId == r.id) { d = w; break; }
                    var gi = d != null && weaponIcons != null ? weaponIcons.GetWeaponIcon(d, true) : null;
                    if (gi != null) return gi;
                    break;
                case GachaBanners.Prize.Costume:
                    if (costumes != null && costumes.TryFindByItemId(r.id, out _, out var part) && part.icon != null) return part.icon;
                    break;
                case GachaBanners.Prize.CostumeSet:
                    var set = economy != null ? economy.costumeSets.Find(s => s != null && s.setId == r.id) : null;
                    if (set?.icon != null) return set.icon;
                    break;
                case GachaBanners.Prize.Skin:
                    var shot = SkinShot(r.id) ?? MainGunIcon();
                    if (shot != null) return shot;
                    break;
                case GachaBanners.Prize.Ticket: return Named("Ticket_Gold") ?? Named(r.icon);
                case GachaBanners.Prize.Coin: return Named("Money_Coin") ?? Named(r.icon);
            }
            return Named(r.icon);
        }

        Sprite Named(string n) => string.IsNullOrEmpty(n) ? null : rewardIcons?.FirstOrDefault(x => x.name == n)?.sprite;

        /// A still of the banner's turning gun in its skin, kept while the banner is the same.
        Sprite SkinShot(string skinId)
        {
            if (_skinShot != null && _skinShotId == skinId) return _skinShot;
            var tex = turntable != null ? turntable.Texture : null;
            if (tex == null) return null;
            var prev = RenderTexture.active; RenderTexture.active = tex;
            var t2 = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            t2.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); t2.Apply();
            RenderTexture.active = prev;
            // Nothing drawn yet (the gun loads a moment after the screen opens): no blank icon, no cache.
            bool drawn = false;
            var px = t2.GetPixels32();
            for (int i = 0; i < px.Length; i += 7) if (px[i].a > 20) { drawn = true; break; }
            if (!drawn) { Destroy(t2); return null; }
            if (_skinShot != null) { Destroy(_skinShot.texture); Destroy(_skinShot); }
            _skinShot = Sprite.Create(t2, new Rect(0, 0, t2.width, t2.height), new Vector2(0.5f, 0.5f));
            _skinShotId = skinId;
            return _skinShot;
        }

        static string NoteFor(GachaBanners.Result r)
        {
            string note = r.tickets > 0 ? $"DUPE +{r.tickets} TICKET{(r.tickets > 1 ? "S" : "")}" : r.offRate ? "50/50 LOST · NEXT IS FEATURED" : r.isNew ? "NEW" : "";
            if (r.bonus) note = string.IsNullOrEmpty(note) ? "BONUS" : "BONUS · " + note;
            return note;
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.unscaledDeltaTime) yield return null;
        }

        IEnumerator Shake(Transform tr, float seconds, float degrees)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 8f) * degrees * (0.4f + k));
                tr.localScale = Vector3.one * (1f + 0.12f * k);
                yield return null;
            }
            tr.localRotation = Quaternion.identity;
        }

        void ShowRates()
        {
            UIFeedback.Tap();
            var rates = GachaBanners.RatesFor(B, economy);
            if (ratesText != null)
                ratesText.text = string.Join("\n", rates.Select(r => $"{r.percent:0.##}%   {r.label}")) +
                                 (B.kind == GachaBanners.Kind.Event
                                     ? $"\n\nA Legendary is certain by pull {B.hardPity}. It is the featured prize 50% of the time; if not, the next Legendary is. Pity carries over.\nx10 opens 10 boxes + 1 bonus box. Duplicates turn into tickets."
                                     : "\n\nx10 opens 10 boxes + 1 bonus box. Duplicate outfits turn into tickets; duplicate guns into shards.");
            if (ratesSheet != null) ratesSheet.SetActive(true);
        }

        /// The rate tile only has room for the item: "Legendary: Neon Circuit skin set (50%)" reads
        /// "Neon Circuit skin set", "Rare gun (dupes give shards)" reads "Rare gun". The full line
        /// stays in the Rates sheet.
        static string TileNote(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            int cut = label.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut > 0) label = label.Substring(0, cut);
            int colon = label.IndexOf(": ", System.StringComparison.Ordinal);
            return colon > 0 ? label.Substring(colon + 2) : label;
        }

        public void Refresh()
        {
            int today = DailyRewards.Today;
            if (coinLabel != null) coinLabel.text = HomeScreen.Short(PlayerProfile.Coin);
            if (gemLabel != null) gemLabel.text = HomeScreen.Short(PlayerProfile.Gem);
            if (ticketLabel != null) ticketLabel.text = PlayerProfile.Tickets.ToString();
            for (int i = 0; i < tabs.Length && i < GachaBanners.All.Length; i++)
            {
                var t = tabs[i]; var b = GachaBanners.All[i]; if (t == null) continue;
                int left = GachaBanners.DaysLeft(b, today);
                if (t.sub != null) t.sub.text = left < 0 ? b.subtitle : $"{b.subtitle} · {left}D";
                if (t.selected != null) t.selected.SetActive(i == _banner);
            }

            var bn = B; bool evt = bn.kind == GachaBanners.Kind.Event;
            int bi = Mathf.Clamp(_banner, 0, BannerPalette.Length - 1);
            if (bannerFx != null) bannerFx.SetPalette(BannerPalette[bi].a, BannerPalette[bi].b, BannerPalette[bi].glow);
            else if (bannerBg != null) bannerBg.color = BannerColor[Mathf.Clamp(_banner, 0, BannerColor.Length - 1)];
            var glow = BannerPalette[bi].glow; var soft = Color.Lerp(glow, Color.white, 0.6f);
            if (bannerTimer != null) bannerTimer.color = glow;
            if (featuredSub != null) featuredSub.color = soft;
            if (pityLabel != null) pityLabel.color = soft;
            if (pityBar != null && pityBar.TryGetComponent<Image>(out var pityFill)) pityFill.color = glow;
            int daysLeft = GachaBanners.DaysLeft(bn, today);
            if (bannerTimer != null) bannerTimer.text = evt ? $"EVENT BANNER · ENDS IN {daysLeft}D" : daysLeft < 0 ? "PERMANENT BANNER" : $"OUTFIT BANNER · ENDS IN {daysLeft}D";
            if (bannerTitle != null) bannerTitle.text = evt ? bn.title : bn.kind == GachaBanners.Kind.Shards ? "GUN\nSHARDS" : "STREET\nSTYLE";
            var skin = evt ? Skins.WeaponSkins.Find(bn.featuredSkin) : null;
            if (featuredTag != null) featuredTag.text = evt ? $"FEATURED · {skin?.name.ToUpperInvariant()} SKIN SET" : bn.kind == GachaBanners.Kind.Shards ? "EVERY GUN · DUPES GIVE SHARDS" : "OUTFIT SETS";
            if (featuredSub != null) featuredSub.text = evt ? $"+{Skins.WeaponSkins.DamageBonus(skin) * 100f:0}% damage on any gun · only here" : bn.kind == GachaBanners.Kind.Shards ? "Shards buy stars in the Arsenal" : "Wear them in the Studio";

            bool gunShow = bn.kind != GachaBanners.Kind.Outfits;
            if (gunArt != null) gunArt.gameObject.SetActive(gunShow);
            if (outfitArt != null) outfitArt.gameObject.SetActive(!gunShow);
            if (gunShow && turntable != null)
            {
                var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : new List<WeaponData>();
                var show = guns.Where(g => g.tier >= WeaponTier.Rare).OrderByDescending(g => g.tier).FirstOrDefault() ?? guns.FirstOrDefault();
                turntable.Show(show, skin);
                if (gunArt != null) { gunArt.texture = turntable.Texture; gunArt.color = Color.white; }
            }

            int pity = PlayerProfile.GetPity(bn.pityKey);
            if (evt)
            {
                if (pityLabel != null) pityLabel.text = GachaBanners.FeaturedGuaranteed(bn)
                    ? $"FEATURED CERTAIN IN {GachaBanners.PullsToLegendary(bn)}"
                    : $"LEGENDARY CERTAIN IN {GachaBanners.PullsToLegendary(bn)} · 50/50";
                UIBarClip.Set(pityBar, pity / (float)bn.hardPity);
            }
            else
            {
                var pool = GachaBanners.PoolFor(bn, economy);
                int th = pool != null ? Mathf.Max(1, pool.pityThreshold) : 30;
                if (pityLabel != null) pityLabel.text = $"EPIC OR BETTER IN {Mathf.Max(0, th - pity)}";
                UIBarClip.Set(pityBar, pity / (float)th);
            }

            var p1 = GachaBanners.BestPay(bn, 1, today);
            if (pullOneSub != null) pullOneSub.text = p1 == GachaBanners.Pay.Free ? "FREE TODAY" : $"{bn.ticketSingle} TICKET OR {bn.gemSingle} GEMS";
            if (pullTenSub != null) pullTenSub.text = $"{(PlayerProfile.Tickets >= bn.ticketMulti ? bn.ticketMulti + " TICKETS" : bn.gemMulti + " GEMS")} · 10+1 BOX";

            var rates = GachaBanners.RatesFor(bn, economy);
            for (int i = 0; i < rateTiles.Length; i++)
            {
                var t = rateTiles[i]; if (t?.root == null) continue;
                bool has = i < rates.Length; t.root.SetActive(has);
                if (!has) continue;
                ThemeTint.Set(t.bg, TierRoles[Mathf.Clamp((int)rates[i].tier, 0, 4)]);
                if (t.label != null) t.label.text = $"{rates[i].percent:0.#}%";
                if (t.note != null) t.note.text = TileNote(rates[i].label);
            }
            _rateIcons = false; _rateIconsAt = 0.5f; _rateTries = 0; _skinShotId = null;
            if (note != null) note.text = evt ? "Duplicates turn into tickets. Pity carries over to the next Neon banner."
                                              : "Duplicate outfits turn into tickets, duplicate guns into shards.";
            _tick = 0f; Update();
        }
    }
}
