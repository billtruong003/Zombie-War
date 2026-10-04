using System;
using BillGameCore;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.Audio
{
    /// <summary>
    /// Voices the FTUE v2 steps with the agents' radio lines (script v3, Review/Lore/HordeCall_Radio_VO_v3.md).
    /// Most lines follow events the game already fires (an FTUE step done, a station paid out, a
    /// first kill, low health); the rest are one-line calls from the screens that show a step
    /// (<see cref="HomeShown"/>, <see cref="CardOffered"/> …). Every line plays once ever.
    /// </summary>
    public sealed class FtueVoice : MonoBehaviour
    {
        const float PollEvery = 0.25f, MoveNudgeAfter = 4f, HomeNudgeAfter = 10f, HurtBelow = 0.4f;

        static FtueVoice _instance;
        bool _subscribed;
        float _nextPoll, _moveSince = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[ZombieWar.FtueVoice]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FtueVoice>();
        }

        static string Line(string agent, string key) => $"vo_{agent}_ftue_{key}";

        // Ids read by the 4 Hz poll, built once instead of four interpolations per poll.
        static readonly string RelayDone = Line("jiho", "relay_done"), RelayOut = Line("jiho", "relay_out"),
            CacheDone = Line("mai", "cache_done"), CachePoor = Line("mai", "cache_poor"),
            MoveLine = Line("riley", "move"), MoveNudge = Line("riley", "move_nudge");
        bool _pollFinished;

        // ------------------------------------------------------------ calls from screens
        /// <summary>Home is on screen. First launch: Riley clears the recruit, then nudges toward PLAY.
        /// After the first run: Mai opens the daily gifts.</summary>
        public static void HomeShown(Func<bool> stillOnHome)
        {
            if (HomeScreenFirstRun)
            {
                if (RadioVoice.SayOnce(Line("riley", "home")))
                    RadioVoice.Nudge(Line("riley", "home_nudge"), HomeNudgeAfter, () => HomeScreenFirstRun && (stillOnHome?.Invoke() ?? false));
            }
            else if (!RadioVoice.SayOnce(Line("mai", "daily"))) RadioDirector.HomeShown(stillOnHome);
        }

        static bool HomeScreenFirstRun => PlayerProfile.RunsPlayed == 0;

        /// <summary>The first level-up card offer (no timer) is on screen.</summary>
        public static void CardOffered() => RadioVoice.SayOnce(Line("chen", "card"));

        /// <summary>The coach callout for a station kind appeared for the first time.</summary>
        public static void StationCallout(StationKind kind) => RadioVoice.SayOnce(kind switch
        {
            StationKind.SignalRelay => Line("jiho", "relay"),
            StationKind.SupplyCache => Line("mai", "cache"),
            StationKind.BossBeacon => Line("kaito", "beacon"),
            StationKind.SupplyDrop => Line("mai", "drop"),
            _ => Line("jiho", "heal"),
        });

        /// <summary>The first chest is open: how evolutions work, then "claim it".</summary>
        public static void ChestOpened()
        {
            if (RadioVoice.SayOnce(Line("chen", "chest"))) RadioVoice.SayOnce(Line("chen", "chest_claim"));
        }

        /// <summary>The free first revive is offered.</summary>
        public static void FreeReviveOffered() => RadioVoice.SayOnce(Line("riley", "revive"));

        /// <summary>The run result is on screen; the first one gets Riley's debrief and, when the
        /// newcomer gift paid out, Mai's top-up line.</summary>
        public static void ResultShown(long newcomerGift, RunClosure.Result result)
        {
            if (PlayerProfile.RunsPlayed > 1) { RadioDirector.RunResult(result); return; }
            if (!RadioVoice.SayOnce(Line("riley", "result"))) return;
            if (newcomerGift > 0) RadioVoice.SayOnce(Line("mai", "result_gift"));
        }

        /// <summary>The Arsenal opened pointing at the first gun to buy.</summary>
        public static void FirstGunShown() => RadioVoice.SayOnce(Line("lukas", "gun"));

        /// <summary>The Arsenal closed while the first gun was still unbought.</summary>
        public static void FirstGunLeft()
        {
            if (RadioVoice.Said(Line("lukas", "gun")) && !Ftue.Done(Ftue.Gun)) RadioVoice.SayOnce(Line("lukas", "gun_leave"));
        }

        /// <summary>The missions / Pass screen opened (after the LV2 popup).</summary>
        public static void MissionsShown()
        {
            if (!RadioVoice.Said(Line("chen", "lv2"))) { RadioDirector.MissionsShown(); return; }
            if (!RadioVoice.SayOnce(Line("chen", "lv2_done"))) RadioDirector.MissionsShown();
        }

        /// <summary>A gacha pull finished (after the LV3 popup).</summary>
        public static void GachaPulled(bool legendary)
        {
            if (!RadioVoice.Said(Line("mai", "lv3"))) { RadioDirector.GachaResult(legendary); return; }
            if (!RadioVoice.SayOnce(Line("mai", "lv3_done"))) RadioDirector.GachaResult(legendary);
        }

        /// <summary>A gun gained a star (after the LV5 popup): the last FTUE step, then Riley signs off.</summary>
        public static void StarUpgraded()
        {
            if (!RadioVoice.Said(Line("lukas", "lv5"))) { RadioDirector.GunStarred(); return; }
            if (RadioVoice.SayOnce(Line("lukas", "lv5_done"))) RadioVoice.SayOnce(Line("riley", "graduate"));
            else RadioDirector.GunStarred();
        }

        // ------------------------------------------------------------ events
        void Update()
        {
            if (!_subscribed && Bill.IsReady) Subscribe();
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollEvery;
            Poll();
        }

        void Subscribe()
        {
            _subscribed = true;
            Bill.Events.Subscribe<FtueStepEvent>(OnStep);
            Bill.Events.Subscribe<StationCompletedEvent>(OnStation);
            Bill.Events.Subscribe<ZombieKilledEvent>(OnKill);
            Bill.Events.Subscribe<PlayerDamagedEvent>(OnDamaged);
        }

        void OnDestroy()
        {
            if (_subscribed && Bill.IsReady)
            {
                Bill.Events.Unsubscribe<FtueStepEvent>(OnStep);
                Bill.Events.Unsubscribe<StationCompletedEvent>(OnStation);
                Bill.Events.Unsubscribe<ZombieKilledEvent>(OnKill);
                Bill.Events.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
            }
            if (_instance == this) _instance = null;
        }

        static void OnStep(FtueStepEvent e)
        {
            string s = e.Step;
            if (s == Ftue.Move) RadioVoice.SayOnce(Line("riley", "move_done"));
            else if (s == Ftue.XpGlow) RadioVoice.SayOnce(Line("kaito", "xp_done"));
            else if (s == Ftue.Card) RadioVoice.SayOnce(Line("chen", "card_done"));
            else if (s == Ftue.Revive) RadioVoice.SayOnce(Line("riley", "revive_done"));
            else if (s == Ftue.Gun) RadioVoice.SayOnce(Line("lukas", "gun_done"));
            else if (s == Ftue.Station(StationKind.BossBeacon)) RadioVoice.SayOnce(Line("kaito", "beacon_go"));
            else if (s == Ftue.Item(PickupEffect.Magnet)) RadioVoice.SayOnce(Line("mai", "magnet"));
            else if (s == Ftue.Item(PickupEffect.Bomb)) RadioVoice.SayOnce(Line("lukas", "bomb"));
            else if (s == Ftue.Item(PickupEffect.Freeze)) RadioVoice.SayOnce(Line("jiho", "freeze"));
            else if (s == Ftue.Unlock(AccountProgress.RequiredLevel(AccountProgress.Feature.Pass))) RadioVoice.SayOnce(Line("chen", "lv2"));
            else if (s == Ftue.Unlock(AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha))) RadioVoice.SayOnce(Line("mai", "lv3"));
            else if (s == Ftue.Unlock(AccountProgress.RequiredLevel(AccountProgress.Feature.GunStars))) RadioVoice.SayOnce(Line("lukas", "lv5"));
        }

        static void OnStation(StationCompletedEvent e) => RadioVoice.SayOnce(e.Kind switch
        {
            StationKind.SignalRelay => Line("jiho", "relay_done"),
            StationKind.SupplyCache => Line("mai", "cache_done"),
            StationKind.BossBeacon => Line("riley", "beacon_kill"),
            StationKind.SupplyDrop => Line("mai", "drop_done"),
            _ => Line("jiho", "heal_done"),
        });

        static void OnKill(ZombieKilledEvent e)
        {
            if (!Ftue.Done(Ftue.XpGlow)) RadioVoice.SayOnce(Line("kaito", "xp"));
        }

        static void OnDamaged(PlayerDamagedEvent e)
        {
            if (e.Normalized < HurtBelow && !Ftue.Done(Ftue.Station(StationKind.HealZone))) RadioVoice.SayOnce(Line("mai", "hurt"));
        }

        // ------------------------------------------------------------ polled run steps
        void Poll()
        {
            if (_pollFinished) return;
            var run = RunState.Current;
            if (run == null || run.IsOver || Time.timeScale <= 0f) { _moveSince = -1f; return; }

            // First run: "drag to move", then a nudge if the recruit has not moved after a few seconds.
            if (!Ftue.Done(Ftue.Move))
            {
                // The nudge clock only runs while the radio is quiet.
                if (RadioVoice.SayOnce(MoveLine) || _moveSince < 0f || RadioVoice.Busy) _moveSince = Time.unscaledTime;
                else if (Time.unscaledTime - _moveSince > MoveNudgeAfter) RadioVoice.SayOnce(MoveNudge);
            }

            var player = PlayerMovement.Instance;
            if (player == null) return;
            var pos = player.transform.position;

            // The first relay: stepped in, then out before it filled.
            bool relayStarted = Ftue.Done(Ftue.Station(StationKind.SignalRelay)) && !RadioVoice.Said(RelayDone);
            // The first cache: standing on it without the coins to pay.
            bool cacheStarted = Ftue.Done(Ftue.Station(StationKind.SupplyCache)) && !RadioVoice.Said(CacheDone);
            if (!relayStarted && !cacheStarted)
            {
                // Every polled line is spent for good once both stations had their outcome: stop polling.
                if (Ftue.Done(Ftue.Move) && (RadioVoice.Said(RelayDone) || RadioVoice.Said(RelayOut))
                    && (RadioVoice.Said(CacheDone) || RadioVoice.Said(CachePoor))) _pollFinished = true;
                return;
            }
            var stations = Station.Active;
            for (int i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                if (s == null || s.Finished || s.Gone || s.Signal == null) continue;
                bool inside = s.Signal.Contains(pos);
                if (relayStarted && s.Anchor.kind == StationKind.SignalRelay && !inside && s.Progress01 > 0.05f)
                    RadioVoice.SayOnce(RelayOut);
                if (cacheStarted && s.Anchor.kind == StationKind.SupplyCache && inside && run.Coin < s.CachePrice)
                    RadioVoice.SayOnce(CachePoor);
            }
        }
    }
}
