#if UNITY_ANDROID && !UNITY_EDITOR
#define HC_PLAY
#endif
using System;
using System.Collections;
using BillGameCore;
using UnityEngine;
using UnityEngine.SceneManagement;
#if HC_PLAY
using Google.Play.AppUpdate;
using Google.Play.Common;
using Google.Play.Review;
using Unity.Notifications.Android;
#endif

namespace ZombieWar.Online
{
    /// <summary>
    /// The Android-only extras around a run (06/10, Round 8):
    ///  - local reminders (no server): a new game day's rewards, and a nudge after two quiet days;
    ///    the Android 13 permission is asked once, after the second run, never during the FTUE;
    ///  - Google Play's own review card after a new best score, at most every 30 days and 3 times;
    ///  - Google Play's immediate update when the server says this version is too old
    ///    (<see cref="RemoteConfig.UpdateRequired"/>).
    /// The editor and other platforms do nothing.
    /// </summary>
    public sealed class PlayServices : MonoBehaviour
    {
        const string ChannelId = "hc_daily";
        const string AskedKey = "hc.notify.asked", ReviewAtKey = "hc.review.at", ReviewCountKey = "hc.review.count";
        const int ReviewMinRuns = 5, ReviewMaxTimes = 3, ReviewGapDays = 30;

        static PlayServices _instance;
        bool _reviewPending, _askPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null || Application.isBatchMode) return;
            var go = new GameObject("[PlayServices]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayServices>();
        }

        void Start()
        {
#if HC_PLAY
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                ChannelId, "Daily rewards", "New daily chest and Daily Ops", Importance.Default));
#endif
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        bool _subscribed;

        // Bill boots in the same frame phase as this object: wait for it rather than guess the order.
        void Update()
        {
            if (_subscribed || !Bill.IsReady) return;
            _subscribed = true;
            Bill.Events.Subscribe<RunFinishedEvent>(OnRunFinished);
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (_subscribed && Bill.IsReady) Bill.Events.Unsubscribe<RunFinishedEvent>(OnRunFinished);
            if (_instance == this) _instance = null;
        }

        void OnRunFinished(RunFinishedEvent e)
        {
            int runs = PlayerProfile.RunsPlayed;
            if (runs >= 2 && PlayerPrefs.GetInt(AskedKey, 0) == 0) _askPending = true;
            if (e.Result.NewScoreRecord && runs >= ReviewMinRuns && ReviewAllowed(DateTime.UtcNow)) _reviewPending = true;
        }

        /// <summary>Both prompts wait for the menu, so they never cover the result screen.</summary>
        void OnSceneChanged(Scene _, Scene next)
        {
            if (next.name != GameFlow.MenuScene) return;
            if (_askPending) { _askPending = false; StartCoroutine(AskNotificationPermission()); }
            else if (_reviewPending) { _reviewPending = false; StartCoroutine(AskReview()); }
        }

        public static bool ReviewAllowed(DateTime utcNow)
        {
            if (PlayerPrefs.GetInt(ReviewCountKey, 0) >= ReviewMaxTimes) return false;
            return !DateTime.TryParse(PlayerPrefs.GetString(ReviewAtKey, ""), null, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
                || utcNow - last >= TimeSpan.FromDays(ReviewGapDays);
        }

        IEnumerator AskNotificationPermission()
        {
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
            yield return new WaitForSecondsRealtime(1.5f);
#if HC_PLAY
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) yield return null;
            GameAnalytics.Log("notify_permission", ("status", request.Status.ToString().ToLowerInvariant()));
#endif
        }

        IEnumerator AskReview()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            PlayerPrefs.SetString(ReviewAtKey, DateTime.UtcNow.ToString("o"));
            PlayerPrefs.SetInt(ReviewCountKey, PlayerPrefs.GetInt(ReviewCountKey, 0) + 1);
            PlayerPrefs.Save();
            GameAnalytics.Log("review_prompt");
#if HC_PLAY
            var manager = new ReviewManager();
            var request = manager.RequestReviewFlow();
            yield return request;
            if (request.Error != ReviewErrorCode.NoError) yield break;
            yield return manager.LaunchReviewFlow(request.GetResult());
#endif
        }

        /// <summary>Google Play's full-screen update for a version the server no longer accepts.
        /// False when Play has no update to offer (side-loaded build, no newer version yet).</summary>
        public static void TryImmediateUpdate(Action<bool> started)
        {
            if (_instance == null) { started?.Invoke(false); return; }
            _instance.StartCoroutine(_instance.ImmediateUpdate(started));
        }

        IEnumerator ImmediateUpdate(Action<bool> started)
        {
#if HC_PLAY
            var manager = new AppUpdateManager();
            var info = manager.GetAppUpdateInfo();
            yield return info;
            if (!info.IsSuccessful || info.GetResult().UpdateAvailability != UpdateAvailability.UpdateAvailable)
            { started?.Invoke(false); yield break; }
            started?.Invoke(true);
            yield return manager.StartUpdate(info.GetResult(), AppUpdateOptions.ImmediateAppUpdateOptions());
#else
            started?.Invoke(false);
            yield break;
#endif
        }

        // ── reminders: scheduled when the game goes to the background, cleared when it comes back ──
        void OnApplicationPause(bool paused)
        {
            if (paused) ScheduleReminders(); else ClearReminders();
        }

        void OnApplicationQuit() => ScheduleReminders();

        void ScheduleReminders()
        {
#if HC_PLAY
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            if (!PlayerProfile.HasProfile || PlayerProfile.RunsPlayed < 1) return;
            var gameNow = GameClock.UtcNow;
            // Fire times are on the device clock: keep the same distance from now as in game time.
            DateTime Local(DateTime utc) => DateTime.Now + (utc - gameNow);
            AndroidNotificationCenter.SendNotification(new AndroidNotification(
                "A new day at the front", "Your daily chest and new Daily Ops are ready.",
                Local(GameClock.NextResetUtc(gameNow).AddMinutes(5))), ChannelId);
            AndroidNotificationCenter.SendNotification(new AndroidNotification(
                "The horde is getting bold", "Two quiet days on the radio. Your squad needs you back.",
                DateTime.Now.AddHours(48)), ChannelId);
#endif
        }

        void ClearReminders()
        {
#if HC_PLAY
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#endif
        }
    }
}
