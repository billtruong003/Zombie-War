using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 run ending (owner-approved V2_Revive and V2_Result mockups), living in the HUD next to
    /// <see cref="RunOverlays"/>. Revive: a lethal hit is held (<see cref="Health.ReviveGate"/>), the
    /// world freezes and the player picks a free ad revive, a coin revive (<see cref="ReviveRules"/>)
    /// or no thanks; seven seconds of silence counts as no thanks. Result: time, record, stats,
    /// coins kept with an ad to double them, missions finished this run, the gun that became
    /// affordable (with a Shop link), Play again and Home.
    /// Built by HordeCall/UI v2/Build Run End (into UI_Hud).
    /// </summary>
    public sealed class RunEndV2 : MonoBehaviour
    {
        [Header("Revive")]
        [SerializeField] private GameObject reviveRoot;
        [Tooltip("The frozen game, blurred, behind the revive panel.")]
        [SerializeField] private RawImage backdrop;
        [Tooltip("A still of the player inside the countdown ring.")]
        [SerializeField] private RawImage portrait;
        [SerializeField] private Image ring;
        [SerializeField] private TMP_Text seconds;
        [SerializeField] private TMP_Text nearMiss;
        [SerializeField] private Image[] hearts = new Image[3];
        [SerializeField] private Sprite heartFull;
        [SerializeField] private Sprite heartEmpty;
        [SerializeField] private TMP_Text runTime;
        [SerializeField] private TMP_Text runKills;
        [SerializeField] private TMP_Text carried;
        [SerializeField] private GameObject bestCard;
        [SerializeField] private TMP_Text bestLabel2;
        [SerializeField] private TMP_Text bestPercent;
        [SerializeField] private Image bestFill;
        [SerializeField] private TMP_Text costNote;
        [SerializeField] private Button adButton;
        [Tooltip("Bar inside the free button that drains with the countdown.")]
        [SerializeField] private Image adDrain;
        [SerializeField] private Button coinButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private Button noButton;

        [Header("Result")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text banner;
        [SerializeField] private TMP_Text time;
        [SerializeField] private GameObject newBest;
        [SerializeField] private TMP_Text bestLabel;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text level;
        [SerializeField] private TMP_Text threat;
        [SerializeField] private TMP_Text coins;
        [SerializeField] private Button doubleButton;
        [SerializeField] private TMP_Text doubleLabel;
        [SerializeField] private TMP_Text gems;
        [SerializeField] private GameObject[] progressRows = new GameObject[3];
        [SerializeField] private TMP_Text[] progressText = new TMP_Text[3];
        [SerializeField] private TMP_Text[] progressTag = new TMP_Text[3];
        [SerializeField] private Button shopLink;
        [SerializeField] private GameObject progressCard;
        [SerializeField] private Button playAgain;
        [SerializeField] private Button home;

        [Header("Unlock (after a run that raised the account level)")]
        [SerializeField] private GameObject unlockRoot;
        [SerializeField] private TMP_Text unlockLevel;
        [SerializeField] private Image unlockTagBg;
        [SerializeField] private TMP_Text unlockTag;
        [SerializeField] private Image unlockFrame;
        [SerializeField] private Image unlockIcon;
        [SerializeField] private TMP_Text unlockBadge;
        [SerializeField] private TMP_Text unlockName;
        [SerializeField] private TMP_Text unlockDesc;
        [SerializeField] private TMP_Text unlockHint;
        [SerializeField] private TMP_Text unlockCount;
        [SerializeField] private Image unlockFill;
        [SerializeField] private Button unlockNext;
        [SerializeField] private TMP_Text unlockNextLabel;
        [SerializeField] private Button unlockTry;
        [SerializeField] private SkillIconSet skillIcons;

        Health _player;
        Coroutine _count;
        long _banked;
        bool _doubled;
        RenderTexture _blur, _still;

        void Awake()
        {
            if (reviveRoot != null) reviveRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            On(adButton, () => RewardedAds.Show("revive", () => { ReviveRules.UseAd(); GetUp(); }));
            On(coinButton, () =>
            {
                if (ReviveRules.TryPayCoin()) GetUp();
                else { UIFeedback.Error(); Toast.Show("Not enough coins"); }
            });
            On(noButton, GiveUp);
            On(doubleButton, () => RewardedAds.Show("double_coins", DoubleCoins));
            On(shopLink, () => { MenuIntent.Next = MenuIntent.Shop; Leave(GameFlow.ReturnToMenu); });
            On(playAgain, () => Leave(GameFlow.RestartGameplay));
            On(home, () => Leave(GameFlow.ReturnToMenu));
            On(unlockNext, ShowNextUnlock);
            On(unlockTry, () => Leave(GameFlow.RestartGameplay));
            if (unlockRoot != null) unlockRoot.SetActive(false);
        }

        void Start() => HookPlayer();

        void Update() { if (_player == null) HookPlayer(); }

        void HookPlayer()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return;
            _player = pc.GetComponent<Health>();
            if (_player != null) _player.ReviveGate = OfferRevive;
        }

        void OnDestroy()
        {
            if (_player != null && _player.ReviveGate == (Func<bool>)OfferRevive) _player.ReviveGate = null;
            ReleaseStills();
        }

        // ------------------------------------------------------------ revive
        bool OfferRevive()
        {
            if (!ReviveRules.CanOffer || reviveRoot == null) return false;
            Time.timeScale = 0f;
            TakeStills();
            reviveRoot.SetActive(true);
            reviveRoot.transform.SetAsLastSibling();
            long cost = ReviveRules.NextCoinCost, carry = ReviveRules.Carried;
            var run = RunState.Current;
            float dur = run != null ? run.Duration : 0f, best = PlayerProfile.BestSurvivalSeconds;
            Set(runTime, HudController.FormatClock(Mathf.FloorToInt(dur)));
            Set(runKills, run != null ? run.Kills.ToString("N0") : "0");
            Set(carried, carry.ToString("N0"));
            Set(nearMiss, best <= 0f ? "Get back up and keep going!"
                : dur < best ? $"Only {HudController.FormatClock(Mathf.CeilToInt(best - dur))} from your best!" : "You are past your best!");
            if (bestCard != null) bestCard.SetActive(best > 0f);
            Set(bestLabel2, $"YOUR BEST {HudController.FormatClock(Mathf.FloorToInt(best))}");
            float pct = best > 0f ? Mathf.Clamp01(dur / best) : 0f;
            Set(bestPercent, $"{Mathf.FloorToInt(pct * 100f)}%");
            Bar(bestFill, pct);
            // Hearts: revives still available this run.
            int left = ReviveRules.MaxRevives - ReviveRules.Used;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].gameObject.SetActive(i < ReviveRules.MaxRevives);
                hearts[i].sprite = i < left ? heartFull : heartEmpty;
            }
            Set(costNote, cost > carry ? $"Coin price doubles each revive · you carry {carry:N0}" : "Coin price doubles each revive");
            Set(coinLabel, cost.ToString("N0"));
            if (adButton != null) adButton.gameObject.SetActive(ReviveRules.AdAvailable);
            if (coinButton != null) coinButton.interactable = PlayerProfile.Coin >= cost;
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(Countdown());
            return true;
        }

        IEnumerator Countdown()
        {
            for (float t = ReviveRules.OfferSeconds; t > 0f; t -= Time.unscaledDeltaTime)
            {
                Set(seconds, Mathf.CeilToInt(t).ToString());
                if (ring != null) ring.fillAmount = t / ReviveRules.OfferSeconds;
                Bar(adDrain, t / ReviveRules.OfferSeconds);
                yield return null;
            }
            GiveUp();
        }

        void GetUp()
        {
            if (_count != null) StopCoroutine(_count);
            reviveRoot.SetActive(false);
            ReleaseStills();
            Time.timeScale = 1f;
            UIFeedback.Confirm();
            _player?.Revive();
        }

        void GiveUp()
        {
            if (_count != null) StopCoroutine(_count);
            if (reviveRoot != null) reviveRoot.SetActive(false);
            ReleaseStills();
            Time.timeScale = 1f;
            _player?.ConfirmDeath();
        }

        // The frozen world, blurred by halving it a few times (bilinear), and a still of the player
        // for the ring. Both are taken once when the offer opens; the world is paused anyway.
        void TakeStills()
        {
            ReleaseStills();
            var cam = Camera.main;
            if (cam == null) return;
            if (backdrop != null)
            {
                var src = RenderTexture.GetTemporary(Mathf.Max(64, Screen.width / 2), Mathf.Max(64, Screen.height / 2), 24);
                Render(cam, src);
                for (int i = 0; i < 3; i++)
                {
                    var d = RenderTexture.GetTemporary(Mathf.Max(8, src.width / 2), Mathf.Max(8, src.height / 2), 0);
                    d.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(src, d);
                    RenderTexture.ReleaseTemporary(src); src = d;
                }
                // Back up twice: bilinear up-steps turn the blocky 1/16 image into a soft blur.
                for (int i = 0; i < 2; i++)
                {
                    var u = RenderTexture.GetTemporary(src.width * 2, src.height * 2, 0);
                    u.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(src, u);
                    RenderTexture.ReleaseTemporary(src); src = u;
                }
                _blur = src; backdrop.texture = _blur;
            }
            if (portrait != null && _player != null) portrait.enabled = Portrait(_player.transform);
        }

        bool Portrait(Transform who)
        {
            const int Layer = 8;   // CharacterPreview: nothing else of the run lives there
            var rs = who.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return false;
            var layers = new int[rs.Length];
            var go = new GameObject("RevivePortraitCam");
            try
            {
                for (int i = 0; i < rs.Length; i++) { layers[i] = rs[i].gameObject.layer; rs[i].gameObject.layer = Layer; }
                var c = go.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.2f, 0.23f, 0.29f, 1f);
                c.cullingMask = 1 << Layer; c.fieldOfView = 26f; c.nearClipPlane = 0.1f; c.farClipPlane = 20f;
                var fwd = who.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.back; fwd.Normalize();
                var aim = who.position + Vector3.up * 1.0f;
                c.transform.position = aim + fwd * 5.2f + Vector3.up * 0.9f;
                c.transform.LookAt(aim);
                _still = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32) { name = "RevivePortrait", antiAliasing = 2 };
                Render(c, _still);
                portrait.texture = _still;
                return true;
            }
            finally
            {
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].gameObject.layer = layers[i];
                Destroy(go);
            }
        }

        // Bars keep their rounded 9-slice ends: the width follows the value, not a fill cut.
        static void Bar(Image i, float v) { if (i != null) i.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(v), 1f); }

        static void Render(Camera c, RenderTexture rt)
        {
            var prev = c.targetTexture;
            c.targetTexture = rt; c.Render(); c.targetTexture = prev;
        }

        void ReleaseStills()
        {
            if (_blur != null) { RenderTexture.ReleaseTemporary(_blur); _blur = null; }
            if (_still != null) { _still.Release(); Destroy(_still); _still = null; }
            if (backdrop != null) backdrop.texture = null;
            if (portrait != null) portrait.texture = null;
        }

        // ------------------------------------------------------------ result
        public void ShowResult(RunClosure.Result result)
        {
            if (resultRoot == null) return;
            var s = result.Summary;
            _banked = result.BankedCoin; _doubled = false;
            resultRoot.SetActive(true);
            resultRoot.transform.SetAsLastSibling();
            Set(banner, s.Outcome == RunOutcome.Died ? "THE HORDE GOT YOU" : "YOU WALKED AWAY");
            UIFx.CountUp(time, Mathf.FloorToInt(s.Duration), 0.7f, v => HudController.FormatClock((int)v));
            if (newBest != null)
            {
                newBest.SetActive(true);
                var img = newBest.GetComponent<Image>();
                ThemeTint.Set(img, result.NewSurvivalRecord ? ThemeRole.Primary : ThemeRole.Card);
                ThemeTint.Set(bestLabel, result.NewSurvivalRecord ? ThemeRole.PrimaryOn : ThemeRole.TextOnSurface);
            }
            Set(bestLabel, result.NewSurvivalRecord ? "NEW BEST" : "BEST " + HudController.FormatClock(Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds)));
            UIFx.CountUp(kills, s.Kills, 0.6f, v => $"{v:N0}", 0.25f);
            Set(level, s.Level.ToString());
            Set(threat, s.PeakThreatTier.ToString());
            UIFx.CountUp(coins, _banked, 0.8f, v => $"{v:N0}", 0.4f);
            Set(gems, $"+{s.Gem}");
            if (doubleButton != null) doubleButton.gameObject.SetActive(_banked > 0);
            Set(doubleLabel, $"WATCH AD · DOUBLE TO {_banked * 2:N0}");

            // Missions this run finished (claimable in the Pass), then the gun it made affordable.
            var done = PassMissions.ActiveFor(DateTime.UtcNow)
                .Where(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id)).Take(2).ToList();
            for (int i = 0; i < progressRows.Length; i++) if (progressRows[i] != null) progressRows[i].SetActive(false);
            int row = 0;
            foreach (var m in done) SetRow(row++, m.title, $"DONE +{m.passXp} XP");
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            var next = guns?.Where(w => w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId) && w.price <= PlayerProfile.Coin)
                            .OrderByDescending(w => w.price).FirstOrDefault();
            // The last row is the next-buy row; it carries the Shop link.
            if (next != null) SetRow(progressRows.Length - 1, $"{next.weaponName} now affordable", "");
            if (progressCard != null) progressCard.SetActive(done.Count > 0 || next != null);
            Time.timeScale = 0f;
            QueueUnlocks(result.AccountLevelsGained);
        }

        // ------------------------------------------------------------ unlocks
        readonly System.Collections.Generic.List<(int level, ZombieWar.Skills.SkillDef def)> _unlocks = new();

        /// Cards the levels just reached unlocked, shown one at a time over the result.
        void QueueUnlocks(int levelsGained)
        {
            _unlocks.Clear();
            if (unlockRoot == null || levelsGained <= 0) return;
            int now = PlayerProfile.AccountLevel;
            var at = new System.Collections.Generic.List<ZombieWar.Skills.SkillDef>();
            for (int lv = now - levelsGained + 1; lv <= now; lv++)
            {
                ZombieWar.Skills.SkillCatalogDefs.UnlockedAt(lv, at);
                foreach (var d in at) _unlocks.Add((lv, d));
            }
            ShowNextUnlock();
        }

        void ShowNextUnlock()
        {
            if (_unlocks.Count == 0) { if (unlockRoot != null) unlockRoot.SetActive(false); return; }
            var (lv, def) = _unlocks[0];
            _unlocks.RemoveAt(0);
            unlockRoot.SetActive(true);
            unlockRoot.transform.SetAsLastSibling();

            Set(unlockLevel, $"ACCOUNT LEVEL {lv}");
            var color = ZombieWar.Skills.SkillDescriptions.LayerColor(def);
            Set(unlockTag, def.layer switch
            {
                ZombieWar.Skills.SkillLayer.Stat => "STAT",
                ZombieWar.Skills.SkillLayer.Universal => "UNIVERSAL",
                _ => "POWER",
            });
            if (unlockTagBg != null) unlockTagBg.color = color;
            if (unlockFrame != null) unlockFrame.color = Color.Lerp(new Color(0.12f, 0.14f, 0.19f), color, 0.35f);
            var sprite = skillIcons != null ? skillIcons.For(def.id) : null;
            if (unlockIcon != null) { unlockIcon.enabled = sprite != null; unlockIcon.sprite = sprite; }
            if (unlockBadge != null) { unlockBadge.enabled = sprite == null; unlockBadge.text = SkillIconSet.Abbreviation(def.displayName); }
            Set(unlockName, def.displayName);
            Set(unlockDesc, ZombieWar.Skills.SkillDescriptions.Describe(def, 1));

            // The evolution this card completes at this level, if any.
            string hint = "";
            foreach (var evo in ZombieWar.Skills.SkillCatalogDefs.All)
                if (evo.IsEvolution && ZombieWar.Skills.SkillCatalogDefs.IsUnlocked(evo, lv) && !ZombieWar.Skills.SkillCatalogDefs.IsUnlocked(evo, lv - 1))
                    hint = $"Also opens the evolution {evo.displayName}";
            Set(unlockHint, hint);

            int total = 0, owned = 0;
            foreach (var d in ZombieWar.Skills.SkillCatalogDefs.All)
            {
                // Bonus cards (heal, coin bag…) are always there: they are not part of the collection.
                if (d.IsEvolution || d.IsOverflow || d.layer == ZombieWar.Skills.SkillLayer.Signature) continue;
                total++;
                if (d.unlockLevel <= lv) owned++;
            }
            Set(unlockCount, $"{owned} / {total}");
            if (unlockFill != null) unlockFill.rectTransform.anchorMax = new Vector2(total > 0 ? owned / (float)total : 0f, 1f);
            Set(unlockNextLabel, _unlocks.Count > 0 ? "NEXT" : "CONTINUE");
            UIFeedback.LevelUp();
        }

        void SetRow(int i, string text, string tag)
        {
            if (i >= progressRows.Length) return;
            if (progressRows[i] != null) progressRows[i].SetActive(true);
            if (i < progressText.Length) Set(progressText[i], text);
            if (i < progressTag.Length && progressTag[i] != null)
            {
                progressTag[i].transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(tag));
                progressTag[i].text = tag;
            }
        }

        void DoubleCoins()
        {
            if (_doubled || _banked <= 0) return;
            _doubled = true;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, _banked);
            UIFeedback.Purchase();
            Set(coins, (_banked * 2).ToString("N0"));
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
            Toast.Show($"+{_banked:N0} coins");
        }

        static void Leave(Action go) { Time.timeScale = 1f; go(); }
        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
        static void On(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }
    }

    /// <summary>Where the menu should go right after it opens (e.g. the result's Shop link).</summary>
    public static class MenuIntent
    {
        public const string Shop = "shop";
        public static string Next;
        public static string Take() { var n = Next; Next = null; return n; }
    }
}
