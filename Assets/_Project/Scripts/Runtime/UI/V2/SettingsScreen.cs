using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Settings (owner-approved V2_Settings mockup). Sound, vibration, graphics, frame rate and
    /// notifications persist through <see cref="GameSettings"/>. Server rows (Google Play Games,
    /// cloud save) stay hidden behind <see cref="FeatureFlags.Backend"/>. "Delete my data" asks
    /// first, then wipes the profile and returns to Home.
    /// Built by HordeCall/UI v2/Build Settings.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        [Serializable]
        public sealed class Switch
        {
            public Button button;
            public Image track;
            public RectTransform knob;
        }

        [SerializeField] private Button backButton;

        [Header("Sound")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Switch vibration;

        [Header("Game")]
        [SerializeField] private Button[] graphics = new Button[3];
        [SerializeField] private Button[] fps = new Button[2];
        [SerializeField] private Button languageRow;
        [SerializeField] private Switch notifications;

        [Header("Account")]
        [SerializeField] private GameObject[] backendRows;
        [SerializeField] private Button restoreRow;
        [SerializeField] private Button playerIdRow;
        [SerializeField] private TMP_Text playerIdValue;

        [Header("Support")]
        [SerializeField] private Button helpRow;
        [SerializeField] private Button adPrivacyRow;
        [SerializeField] private Button privacyRow;
        [SerializeField] private Button deleteRow;
        [SerializeField] private GameObject deleteConfirm;
        [SerializeField] private Button deleteCancel;
        [SerializeField] private Button deleteYes;
        [SerializeField] private TMP_Text versionLabel;

        static readonly Color SegOn = new(0.957f, 0.945f, 0.918f), SegOnText = new(0.149f, 0.165f, 0.212f),
            SegOffText = new(0.604f, 0.631f, 0.690f), SwitchOn = new(0.357f, 0.839f, 0.541f), SwitchOff = new(0.227f, 0.255f, 0.322f);

        protected override void Awake()
        {
            base.Awake();
            On(backButton, () => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => GameSettings.Music = v);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => GameSettings.Sfx = v);
            On(vibration?.button, () => { GameSettings.Haptics = !GameSettings.Haptics; UIFeedback.Tap(); Refresh(); });
            On(notifications?.button, () => { GameSettings.Notifications = !GameSettings.Notifications; UIFeedback.Tap(); Refresh(); });
            for (int i = 0; i < graphics.Length; i++) { int g = i; On(graphics[i], () => { GameSettings.Quality = (GameSettings.Graphics)g; UIFeedback.Tap(); Refresh(); }); }
            for (int i = 0; i < fps.Length; i++) { int f = i; On(fps[i], () => { GameSettings.Fps = f == 0 ? 30 : 60; UIFeedback.Tap(); Refresh(); }); }
            On(languageRow, () => Toast.Show("More languages coming soon"));
            On(restoreRow, () => Toast.Show("No purchases to restore"));
            On(playerIdRow, () => { GUIUtility.systemCopyBuffer = PlayerProfile.PlayerId; UIFeedback.Tap(); Toast.Show("Player ID copied"); });
            On(helpRow, () => Toast.Show("Coming soon"));
            On(adPrivacyRow, () => Toast.Show("Coming soon"));
            On(privacyRow, () => Toast.Show("Coming soon"));
            On(deleteRow, () => { UIFeedback.Tap(); if (deleteConfirm != null) deleteConfirm.SetActive(true); });
            On(deleteCancel, () => { UIFeedback.Back(); if (deleteConfirm != null) deleteConfirm.SetActive(false); });
            On(deleteYes, DeleteData);
            if (backendRows != null) foreach (var r in backendRows) if (r != null) r.SetActive(FeatureFlags.Backend);
        }

        protected override void OnShow()
        {
            if (deleteConfirm != null) deleteConfirm.SetActive(false);
            Refresh();
        }

        public override bool OnEscape()
        {
            if (deleteConfirm != null && deleteConfirm.activeSelf) { deleteConfirm.SetActive(false); return true; }
            return false;
        }

        void DeleteData()
        {
            if (deleteConfirm != null) deleteConfirm.SetActive(false);
            PlayerProfile.DeleteAllData();
            PlayerPrefs.DeleteKey("ftue_done");
            UIFeedback.Confirm();
            Toast.Show("Your data was deleted");
            UIManager.Instance?.PopTo<HomeScreen>();
        }

        public void Refresh()
        {
            musicSlider?.SetValueWithoutNotify(GameSettings.Music);
            sfxSlider?.SetValueWithoutNotify(GameSettings.Sfx);
            SetSwitch(vibration, GameSettings.Haptics);
            SetSwitch(notifications, GameSettings.Notifications);
            SetSegment(graphics, (int)GameSettings.Quality);
            SetSegment(fps, GameSettings.Fps >= 60 ? 1 : 0);
            if (playerIdValue != null) playerIdValue.text = ProfileScreen.FormatId(PlayerProfile.PlayerId);
            if (versionLabel != null) versionLabel.text = $"HordeCall {Application.version} · Season 1";
        }

        static void SetSwitch(Switch s, bool on)
        {
            if (s == null) return;
            if (s.track != null) s.track.color = on ? SwitchOn : SwitchOff;
            if (s.knob != null)
            {
                var a = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                s.knob.anchorMin = s.knob.anchorMax = a; s.knob.pivot = a;
                s.knob.anchoredPosition = new Vector2(on ? -8f : 8f, 0f);
            }
        }

        static void SetSegment(Button[] seg, int active)
        {
            for (int i = 0; i < seg.Length; i++)
            {
                if (seg[i] == null) continue;
                bool on = i == active;
                var img = seg[i].targetGraphic as Image;
                if (img != null) img.color = on ? SegOn : new Color(0, 0, 0, 0);
                var t = seg[i].GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.color = on ? SegOnText : SegOffText;
            }
        }

        static void On(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }
    }
}
