using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ZombieWar.UI.UIBind;

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
        [Tooltip("G12.8: the ad button's video icon, its label, and the retired v2 FTUE tag (kept hidden).")]
        [SerializeField] private GameObject adVideo;
        [SerializeField] private TMP_Text adLabel;
        [SerializeField] private GameObject adFtueTag;
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
        [SerializeField] private TMP_Text shopLinkLabel;
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
        [Tooltip("G12.8: the unlock panel's sub line, try label and collection row.")]
        [SerializeField] private TMP_Text unlockSub;
        [SerializeField] private TMP_Text unlockTryLabel;
        [SerializeField] private TMP_Text unlockCollectionLabel;
        [SerializeField] private GameObject unlockCollectionBar;
        [SerializeField] private TMP_Text unlockHint;
        [SerializeField] private TMP_Text unlockCount;
        [SerializeField] private Image unlockFill;
        [SerializeField] private Button unlockNext;
        [SerializeField] private TMP_Text unlockNextLabel;
        [SerializeField] private Button unlockTry;
        [SerializeField] private SkillIconSet skillIcons;
        [Tooltip("FTUE v2: icons of the features LV2 (missions + pass), LV3 (gacha) and LV5 (gun stars) open.")]
        [SerializeField] private Sprite[] featureIcons = new Sprite[3];

        Health _player;
        Coroutine _count;
        bool _freeRevive;
        long _banked;
        bool _doubled;
        RenderTexture _blur, _still;

        void Awake()
        {
            FtueRadio.RegisterModal(reviveRoot);   // radio subtitles hide during the revive offer and unlock popups
            FtueRadio.RegisterModal(unlockRoot);
            FtueRadio.RegisterModal(resultRoot);   // and on the result, where a chatter line covered the run time
            if (reviveRoot != null) reviveRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            On(adButton, () =>
            {
                // FTUE v2: the first revive ever needs no ad.
                if (_freeRevive) { _freeRevive = false; ReviveRules.UseFree(); Ftue.Complete(Ftue.Revive); GetUp(); return; }
                // The offer waits while the ad plays: the countdown ending under it confirmed the
                // death, and a coin tap before the ad opened paid twice (07/10).
                HoldOffer(true);
                if (!RewardedAds.Show("revive", () =>
                    {
                        if (reviveRoot != null && !reviveRoot.activeSelf) return;   // nothing left to revive: keep the ad revive
                        ReviveRules.UseAd(); GetUp();
                    }, () => HoldOffer(false)))
                {
                    HoldOffer(false);
                    Set(adLabel, "NO AD RIGHT NOW");   // the menu's toast is off during a run
                    UIFeedback.Error();
                }
            });
            On(coinButton, () =>
            {
                if (ReviveRules.TryPayCoin()) GetUp();
                else { UIFeedback.Error(); Toast.Show("Not enough coins"); }
            });
            On(noButton, GiveUp);
            On(doubleButton, () =>
            {
                if (RewardedAds.Show("double_coins", DoubleCoins)) return;
                Set(doubleLabel, "NO AD RIGHT NOW");   // the menu's toast is off during a run
                UIFeedback.Error();
            });
            // FTUE v2: guns are bought in the Arsenal now, so the affordable-gun row goes there.
            On(shopLink, () => { MenuIntent.Next = MenuIntent.Arsenal; Leave(GameFlow.ReturnToMenu); });
            On(playAgain, () => Leave(GameFlow.RestartGameplay));
            On(home, () => Leave(GameFlow.ReturnToMenu));
            On(unlockNext, ShowNextUnlock);
            On(unlockTry, () =>
            {
                if (_featureIntent != null) { MenuIntent.Next = _featureIntent; Leave(GameFlow.ReturnToMenu); }
                else Leave(GameFlow.RestartGameplay);
            });
            if (unlockRoot != null) unlockRoot.SetActive(false);
        }

        void Start() => HookPlayer();

        // Lives in the run's HUD, so one player per lifetime: search (throttled) only until hooked.
        // The old per-frame search kept running for the whole result screen once the player was gone.
        bool _hooked;
        float _nextHookTry;

        void Update()
        {
            if (_hooked || Time.unscaledTime < _nextHookTry) return;
            _nextHookTry = Time.unscaledTime + 0.25f;
            HookPlayer();
        }

        void HookPlayer()
        {
            if (_hooked) return;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return;
            _player = pc.GetComponent<Health>();
            if (_player != null) { _player.ReviveGate = OfferRevive; _hooked = true; }
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
            _freeRevive = !Ftue.Done(Ftue.Revive);
            if (_freeRevive) Set(costNote, "Your first revive is free, no ad. Later: one ad per run, then coins.");
            if (_freeRevive) ZombieWar.Audio.FtueVoice.FreeReviveOffered();
            if (adButton != null)
            {
                adButton.gameObject.SetActive(_freeRevive || ReviveRules.AdAvailable);
                if (adVideo != null) adVideo.SetActive(!_freeRevive);
                if (adFtueTag != null) adFtueTag.SetActive(false);   // v2 tag; the radio card explains
                // The label says what the button costs: the first-ever revive is free with no ad.
                Set(adLabel, _freeRevive ? "REVIVE FREE" : "WATCH AD · REVIVE");
                if (_freeRevive) FtueV3.Revive(adButton.transform as RectTransform);
            }
            if (coinButton != null) coinButton.interactable = PlayerProfile.Coin >= cost;
            if (adButton != null) adButton.interactable = true;    // a revive ad held them last time
            if (noButton != null) noButton.interactable = true;
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(Countdown());
            return true;
        }

        static readonly string[] SecondLabels = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        IEnumerator Countdown()
        {
            // The first-ever revive is the FTUE step: no clock, like the first level-up card. With the
            // 7 s timer a newcomer still reading HQ's card lost the free revive and the run (QA 04/10).
            var bubble = seconds != null ? seconds.transform.parent : null;   // the "N SEC" badge
            if (bubble != null && bubble != transform) bubble.gameObject.SetActive(!_freeRevive);
            if (_freeRevive)
            {
                if (ring != null) ring.fillAmount = 1f;
                Bar(adDrain, 1f);
                yield break;
            }
            int shown = -1;
            for (float t = ReviveRules.OfferSeconds; t > 0f; t -= Time.unscaledDeltaTime)
            {
                int whole = Mathf.CeilToInt(t);
                if (whole != shown) { shown = whole; Set(seconds, whole < SecondLabels.Length ? SecondLabels[whole] : whole.ToString()); }
                if (ring != null) ring.fillAmount = t / ReviveRules.OfferSeconds;
                Bar(adDrain, t / ReviveRules.OfferSeconds);
                yield return null;
            }
            GiveUp();
        }

        /// While a revive ad plays: the clock stops and the other revive buttons wait. Released
        /// (clock restarted) when no ad could play or it closed without the reward.
        void HoldOffer(bool hold)
        {
            if (reviveRoot == null || !reviveRoot.activeSelf) return;
            if (_count != null) { StopCoroutine(_count); _count = null; }
            if (adButton != null) adButton.interactable = !hold;
            if (noButton != null) noButton.interactable = !hold;
            if (coinButton != null) coinButton.interactable = !hold && PlayerProfile.Coin >= ReviveRules.NextCoinCost;
            if (!hold) _count = StartCoroutine(Countdown());
        }

        void GetUp()
        {
            if (reviveRoot != null && !reviveRoot.activeSelf) return;   // the death was already confirmed
            if (_count != null) StopCoroutine(_count);
            reviveRoot.SetActive(false);
            ReleaseStills();
            Time.timeScale = 1f;
            UIFeedback.Confirm();
            if (_player == null) return;
            _player.Revive();
            MechanicItems.ReviveClear(_player.transform.position);
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
            Online.Interstitials.NoteRunEnded(s.Duration);
            Online.CloudSave.Push();
            long gift = NewcomerGift(_banked);
            ZombieWar.Audio.FtueVoice.ResultShown(gift, result);
            if (gift > 0 && shopLink != null) FtueV3.Result(shopLink.transform as RectTransform);
            resultRoot.SetActive(true);
            resultRoot.transform.SetAsLastSibling();
            Set(banner, s.Outcome == RunOutcome.Died ? "THE HORDE GOT YOU" : "YOU WALKED AWAY");
            UIFx.CountUp(time, Mathf.FloorToInt(s.Duration), 0.7f, v => HudController.FormatClock((int)v));
            if (newBest != null)
            {
                newBest.SetActive(true);
                var img = newBest.GetComponent<Image>();
                ThemeTint.Set(img, result.NewScoreRecord ? ThemeRole.Primary : ThemeRole.Card);
                ThemeTint.Set(bestLabel, result.NewScoreRecord ? ThemeRole.PrimaryOn : ThemeRole.TextOnSurface);
            }
            // The record is the kill score (owner 27/09), not the time survived.
            Set(bestLabel, ScoreLine(s.Score, PlayerProfile.BestScore, result.NewScoreRecord, gift > 0));
            // The pill grows with its line ("SCORE 450 · BEST 5,000" is wider than "NEW BEST").
            if (newBest != null && bestLabel != null)
            {
                var pill = (RectTransform)newBest.transform;
                pill.sizeDelta = new Vector2(bestLabel.GetPreferredValues(bestLabel.text).x + 56f, pill.sizeDelta.y);
            }
            UIFx.CountUp(kills, s.Kills, 0.6f, v => $"{v:N0}", 0.25f);
            Set(level, s.Level.ToString());
            Set(threat, s.PeakThreatTier.ToString());
            UIFx.CountUp(coins, _banked, 0.8f, v => $"{v:N0}", 0.4f);
            Set(gems, $"+{s.Gem}");
            if (doubleButton != null) doubleButton.gameObject.SetActive(_banked > 0);
            Set(doubleLabel, $"WATCH AD · DOUBLE TO {_banked * 2:N0}");

            // Missions this run finished (claimable in the Pass), then the gun it made affordable.
            var done = PassMissions.ActiveFor(GameClock.UtcNow)
                .Where(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id)).Take(2).ToList();
            for (int i = 0; i < progressRows.Length; i++) if (progressRows[i] != null) progressRows[i].SetActive(false);
            int row = 0;
            if (gift > 0) SetRow(row++, $"Newcomer gift +{gift:N0} · total {_banked + gift:N0}", "FIRST RUN");
            // Mockup F1 (05/10): Daily Ops, the gun's mastery, achievements - each says where to claim.
            var ops = PassMissions.ActiveFor(GameClock.UtcNow).Where(m => m.scope == MissionScope.Daily).ToList();
            int opsDone = ops.Count(PlayerProfile.IsMissionComplete), opsReady = ops.Count(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id));
            if (opsReady > 0 && row < progressRows.Length) SetRow(row++, $"Daily Ops {opsDone} / {ops.Count} · +{DailyOps.ShardsPerMission * opsReady} shards", "CLAIM ›");
            var gunData = WeaponCatalog.Active?.DataById(PlayerProfile.EquippedWeaponId);
            ZombieWar.Audio.RadioDirector.MasteryUp(result.MasteryBefore, result.MasteryAfter);
            if (result.MasteryXp > 0 && row < progressRows.Length)
                SetRow(row++, $"{(gunData != null ? gunData.weaponName : "Gun")} mastery +{result.MasteryXp:N0} XP · level {result.MasteryAfter}",
                       result.MasteryAfter > result.MasteryBefore ? "LV UP ›" : "");
            foreach (var a in AchievementTracker.ThisRun) { if (row >= progressRows.Length) break; SetRow(row++, $"Achievement: {a.title}", $"+{a.gems} GEMS ›"); }
            foreach (var m in done) { if (row >= progressRows.Length - 1) break; if (m.scope == MissionScope.Daily) continue; SetRow(row++, m.title, $"DONE +{m.passXp} XP"); }
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            var next = guns?.Where(w => w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId) && w.price <= PlayerProfile.Coin)
                            .OrderByDescending(w => w.price).FirstOrDefault();
            // The last row is the next-buy row; its link goes to the Arsenal, where guns are bought.
            bool nextRow = next != null && row < progressRows.Length;
            if (nextRow) SetRow(row++, $"{next.weaponName} now affordable", "");
            Set(shopLinkLabel, "Arsenal ›");
            // The link sits on the last row: only when that row is the next-buy one (or the FTUE
            // newcomer gift points at it), or it lands on another row's tag (QA 05/10).
            if (shopLink != null) shopLink.gameObject.SetActive(gift > 0 || (nextRow && row == progressRows.Length));
            if (progressCard != null) progressCard.SetActive(row > 0 || next != null);
            Time.timeScale = 0f;
            QueueUnlocks(result.AccountLevelsGained);
        }

        /// <summary>The pill under the time: the run's score, against the best one.</summary>
        public static string ScoreLine(long score, long best, bool newRecord, bool firstRun) =>
            firstRun ? $"FIRST RUN · SCORE {score:N0}"
            : newRecord ? $"NEW BEST · {score:N0}"
            : $"SCORE {score:N0} · BEST {best:N0}";

        /// FTUE v2: the first run tops the coins it kept up to the price of the cheapest gun
        /// ("Newcomer gift", owner 01/10), so the first visit to the Arsenal can buy one. Once only.
        static long NewcomerGift(long banked)
        {
            if (Ftue.Done(Ftue.Gift)) return 0;
            Ftue.Complete(Ftue.Gift);
            if (PlayerProfile.RunsPlayed > 1) return 0;   // a player from before FTUE v2 gets nothing
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            var cheapest = guns?.Where(w => w != null && w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId)).OrderBy(w => w.price).FirstOrDefault();
            long gift = cheapest != null ? cheapest.price - banked : 0;
            if (gift <= 0) return 0;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, gift);
            return gift;
        }

        // ------------------------------------------------------------ unlocks
        /// An entry is a card that level unlocked, or (feature >= 0) a feature popup: 0 missions +
        /// pass (LV2), 1 gacha (LV3), 2 gun stars (LV5).
        readonly System.Collections.Generic.List<(int level, ZombieWar.Skills.SkillDef def, int feature)> _unlocks = new();
        string _featureIntent;

        static int FeatureAt(int level) => level == AccountProgress.RequiredLevel(AccountProgress.Feature.Pass) ? 0
            : level == AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha) ? 1
            : level == AccountProgress.RequiredLevel(AccountProgress.Feature.GunStars) ? 2 : -1;

        /// Cards the levels just reached unlocked, shown one at a time over the result.
        void QueueUnlocks(int levelsGained)
        {
            _unlocks.Clear();
            if (unlockRoot == null || levelsGained <= 0) return;
            int now = PlayerProfile.AccountLevel;
            var at = new System.Collections.Generic.List<ZombieWar.Skills.SkillDef>();
            for (int lv = now - levelsGained + 1; lv <= now; lv++)
            {
                int feature = FeatureAt(lv);
                if (feature >= 0 && !Ftue.Done(Ftue.Unlock(lv))) _unlocks.Add((lv, null, feature));
                ZombieWar.Skills.SkillCatalogDefs.UnlockedAt(lv, at);
                foreach (var d in at) _unlocks.Add((lv, d, -1));
            }
            ShowNextUnlock();
        }

        void ShowNextUnlock()
        {
            if (_unlocks.Count == 0) { if (unlockRoot != null) unlockRoot.SetActive(false); return; }
            var (lv, def, feature) = _unlocks[0];
            _unlocks.RemoveAt(0);
            unlockRoot.SetActive(true);
            unlockRoot.transform.SetAsLastSibling();
            if (unlockDesc != null)
            {
                // A feature's description runs to two or three lines; the prefab's text is single-line.
                unlockDesc.textWrappingMode = TextWrappingModes.Normal;
                unlockDesc.enableAutoSizing = true;
                unlockDesc.fontSizeMin = 26f;
                unlockDesc.fontSizeMax = 39f;
            }
            if (feature >= 0) { ShowFeature(lv, feature); return; }
            _featureIntent = null;
            Set(unlockSub, "New skill unlocked!");
            Set(unlockTryLabel, "TRY IT NOW");
            if (unlockCollectionBar != null) unlockCollectionBar.SetActive(true);
            Set(unlockCollectionLabel, "SKILL COLLECTION");

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

        /// FTUE v2 (mockup FTUE2_10..12): the same popup names the feature the level opened, with a
        /// button that goes straight to it.
        void ShowFeature(int lv, int feature)
        {
            Ftue.Complete(Ftue.Unlock(lv));
            var (name, desc, panel, cta, intent, color) = feature switch
            {
                0 => ("MISSIONS + PASS", "Daily and weekly missions give Pass XP. Pass levels give guns, skins and gems.",
                      "Your missions are waiting", "SEE MISSIONS", MenuIntent.Pass, new Color(0.11f, 0.84f, 0.66f)),
                1 => ("GACHA", GachaBanners.IntroLine(),
                      "1 free pull ready", "FREE PULL", MenuIntent.Gacha, new Color(0.66f, 0.45f, 1f)),
                _ => ("GUN STARS", "Spend shards and coins to add stars to a gun: more damage, faster fire.",
                      "Stars open on every gun you own", "UPGRADE MY GUN", MenuIntent.Arsenal, new Color(1f, 0.69f, 0.16f)),
            };
            _featureIntent = intent;
            Set(unlockLevel, $"ACCOUNT LEVEL {lv}");
            Set(unlockSub, "New feature unlocked!");
            Set(unlockTag, "NEW FEATURE");
            if (unlockTagBg != null) unlockTagBg.color = color;
            if (unlockFrame != null) unlockFrame.color = Color.Lerp(new Color(0.12f, 0.14f, 0.19f), color, 0.35f);
            var sprite = featureIcons != null && feature < featureIcons.Length ? featureIcons[feature] : null;
            if (unlockIcon != null) { unlockIcon.enabled = sprite != null; unlockIcon.sprite = sprite; }
            if (unlockBadge != null) unlockBadge.enabled = false;
            Set(unlockName, name);
            Set(unlockDesc, desc);
            Set(unlockHint, "");
            Set(unlockCollectionLabel, panel);
            Set(unlockCount, "NEW");
            if (unlockCollectionBar != null) unlockCollectionBar.SetActive(false);
            Set(unlockTryLabel, cta);
            Set(unlockNextLabel, "LATER");
            UIFeedback.LevelUp();
            FtueV3.Unlock(feature, unlockTry != null ? (RectTransform)unlockTry.transform : null, () => _featureIntent == intent && unlockRoot != null && unlockRoot.activeInHierarchy);
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

        static void Leave(Action go) { Time.timeScale = 1f; Online.Interstitials.ThenGo(go); }
#if UNITY_EDITOR
        /// G12.8: wires the widgets the screen used to find by path; returns the paths not found.
        public System.Collections.Generic.List<string> EditorWire()
        {
            var m = new System.Collections.Generic.List<string>();
            var ad = adButton != null ? adButton.transform : null;
            adVideo = WireUtil.Node(ad, "Face/Video", m);
            adLabel = WireUtil.Find<TMP_Text>(ad, "Face/Label", m);
            var tag = ad != null ? ad.Find("FtueTag") : null;
            adFtueTag = tag != null ? tag.gameObject : null;   // optional: a retired widget
            shopLinkLabel = WireUtil.Find<TMP_Text>(shopLink != null ? shopLink.transform : null, "T", m);
            var col = unlockRoot != null ? unlockRoot.transform.Find("Safe/Col") : null;
            unlockSub = WireUtil.Find<TMP_Text>(col, "Sub", m);
            unlockTryLabel = WireUtil.Find<TMP_Text>(col, "Try/Face/Label", m);
            unlockCollectionLabel = WireUtil.Find<TMP_Text>(col, "Collection/L", m);
            unlockCollectionBar = WireUtil.Node(col, "Collection/Bar", m);
            return m;
        }

        public System.Collections.Generic.List<string> EditorUnwired() => WireUtil.Nulls(
            ("adVideo", adVideo), ("adLabel", adLabel), ("shopLinkLabel", shopLinkLabel), ("unlockSub", unlockSub),
            ("unlockTryLabel", unlockTryLabel), ("unlockCollectionLabel", unlockCollectionLabel),
            ("unlockCollectionBar", unlockCollectionBar));
#endif
    }

    /// <summary>Where the menu should go right after it opens (e.g. the result's Shop link).</summary>
    public static class MenuIntent
    {
        public const string Shop = "shop";
        public const string Arsenal = "arsenal", Pass = "pass", Gacha = "gacha";
        public static string Next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Next = null;
        public static string Take() { var n = Next; Next = null; return n; }
    
}
}
