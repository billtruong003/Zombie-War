using System;
using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// Project QA controls hosted by the persistent Bootstrap scene.
    /// In release builds the component stays serializable but creates no UI and performs no work.
    /// </summary>
    public sealed class ZombieWarCheatPanel : MonoBehaviour
    {
        [SerializeField] private WeaponData[] weapons = Array.Empty<WeaponData>();
        [SerializeField] private CampaignCatalog campaignCatalog;
        [SerializeField] private ModularCostumeCatalog[] costumeCatalogs = Array.Empty<ModularCostumeCatalog>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
        private const int OverlaySortOrder = 10000;
        private static readonly Color PanelColor = new(0.035f, 0.045f, 0.06f, 0.98f);
        private static readonly Color ButtonColor = new(0.12f, 0.15f, 0.20f, 1f);
        private static readonly Color AccentColor = new(0.20f, 0.72f, 0.43f, 1f);
        private static readonly Color DangerColor = new(0.72f, 0.18f, 0.20f, 1f);

        private GameObject _panel;
        private Text _status;
        private bool _commandsRegistered;
        private bool _godMode;
        private float _resetArmedUntil;
        private Font _font;
        private Health _godHealth;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
        }

        private void Update()
        {
            if (!_commandsRegistered && Bill.IsReady)
                RegisterCommands();

            if (!_godMode) return;
            var player = PlayerMovement.Instance;
            var health = player != null ? player.GetComponent<Health>() : null;
            BindGodHealth(health);
            if (_godHealth != null && !_godHealth.IsDead && _godHealth.Current < _godHealth.Max)
                _godHealth.ResetHealth();
        }

        private void OnDisable()
        {
            BindGodHealth(null);
        }

        private void RegisterCommands()
        {
            var cheat = Bill.Cheat;
            if (cheat == null) return;

            cheat.Register("zw.coin", () => AddWallet(PlayerProfile.CurrencyKind.Coin, 100000), "Add 100,000 Coin");
            cheat.Register("zw.gold", () => AddWallet(PlayerProfile.CurrencyKind.Gold, 10000), "Add 10,000 Gold");
            cheat.Register("zw.gem", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 1000), "Add 1,000 Gem");
            cheat.Register("zw.unlock.weapons", UnlockWeapons, "Unlock every authored weapon");
            cheat.Register("zw.unlock.costumes", UnlockCostumes, "Unlock every authored costume");
            cheat.Register("zw.unlock.stages", UnlockStages, "Unlock every campaign stage");
            cheat.Register("zw.heal", HealPlayer, "Restore player HP");
            cheat.Register("zw.god", ToggleGodMode, "Toggle player god mode");
            cheat.Register("zw.xp", AddRunXp, "Add 1,000 run XP");
            cheat.Register("zw.runcoin", AddRunCoin, "Add 1,000 run Coin");
            cheat.Register("zw.killall", KillAllZombies, "Kill every active zombie");
            cheat.Register("zw.win", WinRun, "Complete the current run");
            cheat.Register("zw.restart", RestartRun, "Restart current stage");
            cheat.Register("zw.home", ReturnToMap, "Return to campaign map/menu");
            _commandsRegistered = true;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("[ZombieWar.Cheats]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var tab = CreateButton(canvasGo.transform, "CheatTab", "CHEAT", new Color(0.05f, 0.06f, 0.08f, 0.48f), 24);
            var tabRt = (RectTransform)tab.transform;
            tabRt.anchorMin = tabRt.anchorMax = tabRt.pivot = new Vector2(0f, 1f);
            tabRt.anchoredPosition = new Vector2(18f, -18f);
            tabRt.sizeDelta = new Vector2(132f, 54f);
            tab.onClick.AddListener(() => SetPanelVisible(true));

            _panel = CreateRect(canvasGo.transform, "CheatOverlay", PanelColor).gameObject;
            Stretch((RectTransform)_panel.transform);

            var header = CreateText(_panel.transform, "Header", "ZOMBIE WAR — QA CHEATS", 42,
                FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            SetAnchored(header.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -32f), new Vector2(760f, 72f));

            var close = CreateButton(_panel.transform, "Close", "CLOSE", DangerColor, 28);
            SetAnchored((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(190f, 72f));
            close.onClick.AddListener(() => SetPanelVisible(false));

            _status = CreateText(_panel.transform, "Status", "Ready", 25, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color(0.72f, 0.82f, 0.76f));
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.offsetMin = new Vector2(40f, 24f);
            statusRt.offsetMax = new Vector2(-40f, 78f);

            var viewport = CreateRect(_panel.transform, "Viewport", new Color(0f, 0f, 0f, 0.12f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(32f, 96f);
            viewport.offsetMax = new Vector2(-32f, -126f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 24);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;

            AddSection(content, "PROFILE / DATA");
            AddAction(content, "+100K COIN", () => AddWallet(PlayerProfile.CurrencyKind.Coin, 100000));
            AddAction(content, "+10K GOLD", () => AddWallet(PlayerProfile.CurrencyKind.Gold, 10000));
            AddAction(content, "+1K GEM", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 1000));
            AddAction(content, "UNLOCK ALL WEAPONS", UnlockWeapons);
            AddAction(content, "UNLOCK ALL COSTUMES", UnlockCostumes);
            AddAction(content, "UNLOCK ALL STAGES", UnlockStages);
            AddAction(content, "RESET PROFILE — PRESS TWICE", ResetProfile, true);

            AddSection(content, "CURRENT RUN");
            AddAction(content, "HEAL FULL", HealPlayer);
            AddAction(content, "TOGGLE GOD MODE", ToggleGodMode);
            AddAction(content, "+1,000 XP", AddRunXp);
            AddAction(content, "+1,000 RUN COIN", AddRunCoin);
            AddAction(content, "KILL ALL ZOMBIES", KillAllZombies);
            AddAction(content, "WIN RUN NOW", WinRun);

            AddSection(content, "FLOW / DEBUG");
            AddAction(content, "TIME ×0.5", () => SetTimeScale(0.5f));
            AddAction(content, "TIME ×1", () => SetTimeScale(1f));
            AddAction(content, "TIME ×2", () => SetTimeScale(2f));
            AddAction(content, "TIME ×4", () => SetTimeScale(4f));
            AddAction(content, "RESTART STAGE", RestartRun);
            AddAction(content, "BACK TO MAP", ReturnToMap);
            AddAction(content, "OPEN BILL CONSOLE", OpenConsole);

            SetPanelVisible(false);
        }

        private void AddSection(Transform parent, string label)
        {
            var text = CreateText(parent, label.Replace(" ", "_"), label, 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, AccentColor);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
        }

        private void AddAction(Transform parent, string label, Action action, bool danger = false)
        {
            var button = CreateButton(parent, label.Replace(" ", "_"), label, danger ? DangerColor : ButtonColor, 28);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;
            button.onClick.AddListener(() => Safe(label, action));
        }

        private void Safe(string label, Action action)
        {
            try
            {
                action();
                Debug.Log($"[ZombieWarCheats] {label}");
            }
            catch (Exception e)
            {
                SetStatus($"{label} failed: {e.Message}");
                Debug.LogException(e, this);
            }
        }

        private void AddWallet(PlayerProfile.CurrencyKind kind, long amount)
        {
            PlayerProfile.Add(kind, amount);
            SetStatus($"{kind}: {PlayerProfile.GetBalance(kind):N0}");
        }

        private void UnlockWeapons()
        {
            int count = PlayerProfile.UnlockAllWeaponsForDev(weapons);
            SetStatus($"Weapons unlocked: +{count} ({PlayerProfile.OwnedWeaponIds.Count} owned)");
        }

        private void UnlockCostumes()
        {
            int count = 0;
            for (int i = 0; i < costumeCatalogs.Length; i++)
                count += PlayerProfile.UnlockAllCostumes(costumeCatalogs[i]);
            SetStatus($"Costume entries unlocked: +{count}");
        }

        private void UnlockStages()
        {
            if (campaignCatalog == null) throw new InvalidOperationException("CampaignCatalog is not wired.");
            for (int i = 0; i < campaignCatalog.Levels.Count; i++)
                PlayerProfile.MarkLevelCompleted(campaignCatalog.Levels[i].levelId);
            SetStatus($"Campaign stages unlocked: {campaignCatalog.Count}");
        }

        private void ResetProfile()
        {
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 3f;
                SetStatus("RESET ARMED — press again within 3 seconds");
                return;
            }
            _resetArmedUntil = 0f;
            PlayerProfile.ResetForDev();
            SetStatus("Profile reset. Reopen screens to refresh all data.");
        }

        private void HealPlayer()
        {
            var player = PlayerMovement.Instance;
            var health = player != null ? player.GetComponent<Health>() : null;
            if (health == null) throw new InvalidOperationException("No active player.");
            health.ResetHealth();
            SetStatus($"Player HP: {health.Max:N0}/{health.Max:N0}");
        }

        private void ToggleGodMode()
        {
            _godMode = !_godMode;
            if (_godMode)
            {
                var player = PlayerMovement.Instance;
                BindGodHealth(player != null ? player.GetComponent<Health>() : null);
                HealPlayer();
            }
            else
            {
                BindGodHealth(null);
            }
            SetStatus($"God mode: {(_godMode ? "ON" : "OFF")}");
        }

        private void BindGodHealth(Health health)
        {
            if (_godHealth == health) return;
            if (_godHealth != null) _godHealth.OnDamaged -= OnGodDamaged;
            _godHealth = health;
            if (_godHealth != null) _godHealth.OnDamaged += OnGodDamaged;
        }

        private void OnGodDamaged(float _)
        {
            if (_godMode && _godHealth != null)
                _godHealth.ResetHealth();
        }

        private void AddRunXp()
        {
            var run = RunState.Current ?? throw new InvalidOperationException("No active run.");
            int levels = run.AddXp(1000);
            SetStatus($"Run level {run.Level}; gained {levels} level(s)");
        }

        private void AddRunCoin()
        {
            var run = RunState.Current ?? throw new InvalidOperationException("No active run.");
            run.AddCurrency(PlayerProfile.CurrencyKind.Coin, 1000);
            SetStatus($"Run Coin: {run.Coin:N0}");
        }

        private void KillAllZombies()
        {
            var zombies = FindObjectsByType<ZombieBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < zombies.Length; i++)
                zombies[i].TakeDamage(float.MaxValue);
            SetStatus($"Killed {zombies.Length} active zombie(s)");
        }

        private void WinRun()
        {
            if (RunState.Current == null) throw new InvalidOperationException("No active run.");
            Bill.Events?.Fire(new AllWavesClearedEvent());
            SetStatus("Victory event fired");
            SetPanelVisible(false);
        }

        private void RestartRun()
        {
            Time.timeScale = 1f;
            SetPanelVisible(false);
            GameFlow.RestartGameplay();
        }

        private void ReturnToMap()
        {
            Time.timeScale = 1f;
            SetPanelVisible(false);
            GameFlow.ReturnToMenu();
        }

        private void OpenConsole()
        {
            SetPanelVisible(false);
            Bill.Cheat?.SetVisible(true);
        }

        private void SetTimeScale(float value)
        {
            Time.timeScale = value;
            SetStatus($"Time scale: {value:0.0}×");
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        private void SetStatus(string message)
        {
            if (_status != null) _status.text = message;
        }

        private Button CreateButton(Transform parent, string name, string label, Color color, int fontSize)
        {
            var rect = CreateRect(parent, name, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var text = CreateText(rect, "Label", label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, 12f);
            text.raycastTarget = false;
            return button;
        }

        private RectTransform CreateRect(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = go.GetComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            text.text = value;
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
#else
        private void Awake()
        {
            enabled = false;
        }
#endif
    }
}
