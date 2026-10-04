using System;
using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// Project QA controls hosted by the persistent Bootstrap scene.
    /// In release builds the component stays serializable but creates no UI and performs no work.
    ///
    /// M8 (owner 2026-09-29, "upgrade the cheats for the new features"): a live readout of the
    /// numbers QA keeps checking, and eight tabs — wallet and tickets, guns (own/equip any gun,
    /// shards, max stars, skins), gacha (pity on the edge, free pull again, a rate simulation),
    /// meta (pass XP, new day, missions, outfits), run, every skill card, time/flow and profile.
    /// </summary>
    public sealed class ZombieWarCheatPanel : MonoBehaviour
    {
        [SerializeField] private WeaponData[] weapons = Array.Empty<WeaponData>();
        [SerializeField] private ModularCostumeCatalog[] costumeCatalogs = Array.Empty<ModularCostumeCatalog>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
        private const int OverlaySortOrder = 10000;
        private static readonly Color PanelColor = new(0.035f, 0.045f, 0.06f, 1f);   // opaque: 0.98 reads see-through in linear space
        private static readonly Color ButtonColor = new(0.12f, 0.15f, 0.20f, 1f);
        private static readonly Color AccentColor = new(0.20f, 0.72f, 0.43f, 1f);
        private static readonly Color DangerColor = new(0.72f, 0.18f, 0.20f, 1f);

        private static readonly string[] TabNames = { "WALLET", "GUNS", "GACHA", "META", "RUN", "SKILLS", "FLOW", "PROFILE", "MAP", "LOOK" };

        private GameObject _panel;
        private Text _status, _info;
        private bool _commandsRegistered;
        private bool _godMode;
        private float _resetArmedUntil, _infoTick;
        private Font _font;
        private Health _godHealth;
        private readonly Image[] _tabButtons = new Image[TabNames.Length];
        private RectTransform _content, _row;
        private ScrollRect _scroll;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
        }

        private void Update()
        {
            if (!_commandsRegistered && Bill.IsReady)
                RegisterCommands();
            if (_panel != null && _panel.activeSelf && (_infoTick -= Time.unscaledDeltaTime) <= 0f) { _infoTick = 0.5f; RefreshInfo(); }

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
            cheat.Register("zw.gem", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 1000), "Add 1,000 Gem");
            cheat.Register("zw.tickets", () => AddTickets(10), "Add 10 gacha tickets");
            cheat.Register("zw.pity.edge", () => SetPityEdge(1), "Every banner one pull from its guarantee");
            cheat.Register("zw.newday", ResetDaily, "Free pull, stamp, welcome and missions fresh again");
            cheat.Register("zw.ftue.reset", Ftue.ResetAll, "Forget every FTUE step: the first-time hints play again");
            cheat.Register("zw.unlock.weapons", UnlockWeapons, "Unlock every authored weapon");
            cheat.Register("zw.unlock.costumes", UnlockCostumes, "Unlock every authored costume");
            cheat.Register("zw.heal", HealPlayer, "Restore player HP");
            cheat.Register("zw.god", ToggleGodMode, "Toggle player god mode");
            cheat.Register("zw.xp", AddRunXp, "Add 1,000 run XP");
            cheat.Register<string>("zw.skill", GrantSkill, "Grant one rank of a card: zw.skill auto.orbit");
            cheat.Register<string>("zw.skill.max", GrantSkillMax, "Max a card (and its evolution partner if any)");
            cheat.Register("zw.runcoin", AddRunCoin, "Add 1,000 run Coin");
            cheat.Register("zw.killall", KillAllZombies, "Kill every active zombie");
            cheat.Register("zw.threat", RaiseThreat, "Raise run threat by one tier");
            cheat.Register("zw.end", EndRun, "End the current run (walk away)");
            cheat.Register("zw.restart", RestartRun, "Restart the run");
            cheat.Register("zw.home", ReturnToMap, "Return to the menu");
            cheat.Register<string>("zw.map.go", LoadMap, "Load a map now (restarts the run): zw.map.go forest");
            // QA repro shortcuts (04/10 playthrough): reach revive, chest, item and station moments on demand.
            cheat.Register("zw.die", ForceLethalHit, "Take a lethal hit (opens the revive offer if one is left)");
            cheat.Register("zw.chest", SpawnChestHere, "Drop a chest next to the player");
            cheat.Register<string>("zw.item", SpawnItemHere, "Drop a mechanic item: zw.item Magnet|Bomb|Freeze");
            cheat.Register<string>("zw.station", SpawnStationHere, "Place a station ahead: zw.station BossBeacon");
            cheat.Register<int>("zw.acclevel", SetAccountLevel, "Raise the account to a level: zw.acclevel 5");
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

            var tab = CreateButton(canvasGo.transform, "CheatTab", "QA", new Color(0.05f, 0.06f, 0.08f, 0.48f), 22);
            var tabRt = (RectTransform)tab.transform;
            tabRt.anchorMin = tabRt.anchorMax = tabRt.pivot = new Vector2(0f, 1f);
            // M10 UI audit: a slim tab on the left edge, below the Home side rails and above the
            // joystick, so it covers no button in the menu or in a run (dev builds only).
            tabRt.anchoredPosition = new Vector2(0f, -880f);
            tabRt.sizeDelta = new Vector2(56f, 72f);
            tab.onClick.AddListener(() => SetPanelVisible(true));

            _panel = CreateRect(canvasGo.transform, "CheatOverlay", PanelColor).gameObject;
            Stretch((RectTransform)_panel.transform);

            var header = CreateText(_panel.transform, "Header", "HORDECALL — QA CHEATS", 42,
                FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            SetAnchored(header.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(760f, 72f));

            var close = CreateButton(_panel.transform, "Close", "CLOSE", DangerColor, 28);
            SetAnchored((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(190f, 72f));
            close.onClick.AddListener(() => SetPanelVisible(false));

            _info = CreateText(_panel.transform, "Info", "", 24, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));
            SetAnchored(_info.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -108f), new Vector2(1000f, 72f));

            // Tabs: two rows of five.
            var tabs = CreateRect(_panel.transform, "Tabs", new Color(0, 0, 0, 0));
            SetAnchored(tabs, new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(1016f, 144f));
            var grid = tabs.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196f, 66f); grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var b = CreateButton(tabs, "Tab" + TabNames[i], TabNames[i], ButtonColor, 26);
                _tabButtons[i] = b.GetComponent<Image>();
                b.onClick.AddListener(() => ShowTab(idx));
            }

            _status = CreateText(_panel.transform, "Status", "Ready", 25, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color(0.72f, 0.82f, 0.76f));
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.offsetMin = new Vector2(40f, 20f);
            statusRt.offsetMax = new Vector2(-40f, 90f);

            var viewport = CreateRect(_panel.transform, "Viewport", new Color(0f, 0f, 0f, 0.12f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(32f, 100f);
            viewport.offsetMax = new Vector2(-32f, -344f);

            _content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _content.SetParent(viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 24);
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 42f;

            ShowTab(0);
            SetPanelVisible(false);
        }

        // ------------------------------------------------------------------ tabs

        private void ShowTab(int tab)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
                if (_tabButtons[i] != null) _tabButtons[i].color = i == tab ? AccentColor : ButtonColor;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            _row = null;
            switch (tab)
            {
                case 0:
                    AddSection(_content, "CURRENCY");
                    AddAction(_content, "+100K COIN", () => AddWallet(PlayerProfile.CurrencyKind.Coin, 100000));
                    AddAction(_content, "+1M COIN", () => AddWallet(PlayerProfile.CurrencyKind.Coin, 1000000));
                    AddAction(_content, "+1K GEM", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 1000));
                    AddAction(_content, "+10K GEM", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 10000));
                    AddAction(_content, "+10 TICKETS", () => AddTickets(10));
                    AddAction(_content, "+100 TICKETS", () => AddTickets(100));
                    AddAction(_content, "ZERO COIN + GEM", ZeroWallet, true);
                    AddAction(_content, "ZERO TICKETS", ZeroTickets, true);
                    break;
                case 1:
                    AddSection(_content, "EQUIPPED GUN");
                    AddAction(_content, "UNLOCK ALL WEAPONS", UnlockWeapons);
                    AddAction(_content, "+100 SHARDS", () => AddShards(100));
                    AddAction(_content, "MAX STARS", MaxStarsEquipped);
                    AddAction(_content, "UNLOCK ALL GUN SKINS", () => SetStatus($"Skins unlocked: +{PlayerProfile.DevUnlockAllSkins()}"));
                    AddSection(_content, "TAP A GUN TO OWN AND EQUIP IT");
                    AddGunList();
                    break;
                case 2:
                    AddSection(_content, "PITY / PULLS");
                    AddAction(_content, "NEXT PULL IS LEGENDARY", () => SetPityEdge(1));
                    AddAction(_content, "10 PULLS FROM LEGENDARY", () => SetPityEdge(10));
                    AddAction(_content, "RESET ALL PITY", () => SetPityEdge(-1));
                    AddAction(_content, "FREE PULL AGAIN TODAY", ResetDaily);
                    AddAction(_content, "+10 TICKETS", () => AddTickets(10));
                    AddAction(_content, "+2,700 GEM (10 × x10)", () => AddWallet(PlayerProfile.CurrencyKind.Gem, 2700));
                    AddSection(_content, "SIMULATE (NOTHING IS GRANTED)");
                    AddAction(_content, "EVENT RATES OVER 10,000 PULLS", SimulateRates);
                    break;
                case 3:
                    AddSection(_content, "PASS / DAILY / MISSIONS");
                    AddAction(_content, "+500 PASS XP", () => AddPassXp(500));
                    AddAction(_content, "+5,000 PASS XP", () => AddPassXp(5000));
                    AddAction(_content, "NEW DAY", ResetDaily);
                    AddAction(_content, "COMPLETE MISSIONS", CompleteMissions);
                    AddSection(_content, "OUTFITS");
                    AddAction(_content, "UNLOCK ALL COSTUMES", UnlockCostumes);
                    break;
                case 4:
                    AddSection(_content, "CURRENT RUN");
                    AddAction(_content, "HEAL FULL", HealPlayer);
                    AddAction(_content, "TOGGLE GOD MODE", ToggleGodMode);
                    AddAction(_content, "+1,000 XP", AddRunXp);
                    AddAction(_content, "+1,000 RUN COIN", AddRunCoin);
                    AddAction(_content, "KILL ALL ZOMBIES", KillAllZombies);
                    AddAction(_content, "THREAT +1 TIER", RaiseThreat);
                    AddAction(_content, "END RUN (WALK AWAY)", EndRun);
                    AddAction(_content, "RESTART RUN", RestartRun);
                    break;
                case 5:
                    AddSection(_content, "TAP: +1 RANK (IN A RUN)");
                    foreach (var def in ZombieWar.Skills.SkillCatalogDefs.All)
                    {
                        var d = def;
                        AddAction(_content, d.displayName.ToUpperInvariant(), () => GrantSkill(d.id), false, 22);
                    }
                    break;
                case 6:
                    AddSection(_content, "TIME");
                    AddAction(_content, "TIME ×0.5", () => SetTimeScale(0.5f));
                    AddAction(_content, "TIME ×1", () => SetTimeScale(1f));
                    AddAction(_content, "TIME ×2", () => SetTimeScale(2f));
                    AddAction(_content, "TIME ×4", () => SetTimeScale(4f));
                    AddSection(_content, "FLOW");
                    AddAction(_content, "BACK TO MENU", ReturnToMap);
                    AddAction(_content, "OPEN BILL CONSOLE", OpenConsole);
                    break;
                case 7:
                    AddSection(_content, "ACCOUNT");
                    AddAction(_content, "+5,000 ACCOUNT XP", AddAccountXp);
                    AddAction(_content, "UNLOCK ALL FRAMES", () => SetStatus($"Frames unlocked: +{PlayerProfile.DevUnlockAllFrames()}"));
                    AddSection(_content, "DANGER");
                    AddAction(_content, "RESET PROFILE — PRESS TWICE", ResetProfile, true);
                    break;
                case 8:
                    AddMapList();
                    break;
                case 9:
                    AddLookTab();
                    break;
            }
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            RefreshInfo();
        }

        /// Outline looks on trial (02/10): switch the line live in a run to compare them.
        private void AddLookTab()
        {
            AddSection(_content, "OUTLINE COLOUR");
            AddAction(_content, "GAME DEFAULT", () => { OutlineLook.Colour = null; OutlineLook.Tint = null; SetStatus("Outline: game default"); });
            for (int i = 0; i < OutlineLook.Presets.Length; i++)
            {
                int k = i;
                var p = OutlineLook.Presets[i];
                AddAction(_content, p.code + " " + p.label.ToUpperInvariant(), () => { OutlineLook.Apply(k); SetStatus("Outline: " + p.code + " " + p.label); }, false, 22);
            }
            AddSection(_content, "OUTLINE WIDTH");
            AddAction(_content, "2 PX", () => { OutlineLook.Thickness = 2; SetStatus("Outline width 2 px"); });
            AddAction(_content, "3 PX", () => { OutlineLook.Thickness = 3; SetStatus("Outline width 3 px"); });
            AddAction(_content, "OUTLINE OFF", () => { OutlineLook.Hidden = true; SetStatus("Outline off"); });
            AddAction(_content, "OUTLINE ON", () => { OutlineLook.Hidden = false; SetStatus("Outline on"); });
            AddSection(_content, "GRAPHICS TIER (NOW " + GraphicsTier.Current.ToString().ToUpperInvariant() + ")");
            AddAction(_content, "LOW", () => SetTier(GraphicsTier.Level.Low));
            AddAction(_content, "MID", () => SetTier(GraphicsTier.Level.Mid));
            AddAction(_content, "HIGH", () => SetTier(GraphicsTier.Level.High));
            AddAction(_content, "DEVICE DEFAULT", () => { GraphicsTier.ResetToDetected(); SetStatus("Tier: " + GraphicsTier.Current + " (detected)"); ShowTab(9); });
            AddMapLooks();
            AddSection(_content, "POINT LIGHTS");
            AddAction(_content, "TEST LAMPS AROUND HERO", SpawnTestLamps);
            AddAction(_content, "REMOVE TEST LAMPS", ClearTestLamps);
        }

        /// The playing map's trial looks (02/10): its five colour grades and other ground / fluid
        /// options, switched live; MAP DEFAULT puts the map's own look back.
        private void AddMapLooks()
        {
            var streamer = World.BakedMapStreamer.Active;
            if (streamer == null || streamer.Theme == null) return;
            var theme = streamer.Theme;
            AddSection(_content, "MAP LOOK · " + theme.id.ToUpperInvariant());
            AddAction(_content, "MAP DEFAULT", () => { streamer.TryLook(null, null, null); SetStatus("Map look: default"); });
            if (theme.devLooks == null) return;
            foreach (var o in theme.devLooks)
            {
                var opt = o;
                if (string.IsNullOrEmpty(opt.name)) continue;
                AddAction(_content, opt.name.ToUpperInvariant(), () => { streamer.TryLook(opt.post, opt.ground, opt.fluid); SetStatus("Map look: " + opt.name); }, false, 22);
            }
            AddAction(_content, "POST ON / OFF", () =>
            {
                var cam = Camera.main;
                if (cam != null && cam.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData d)) { d.renderPostProcessing = !d.renderPostProcessing; SetStatus("Post " + (d.renderPostProcessing ? "on" : "off")); }
            });
        }

        private void SetTier(GraphicsTier.Level level)
        {
            GraphicsTier.Set(level);
            SetStatus($"Tier {level}: {GraphicsTier.PointLights} point lights, bloom {(GraphicsTier.BloomIterations > 0 ? GraphicsTier.BloomIterations + " passes" : "off")}");
            ShowTab(9);
        }

        private GameObject _testLamps;

        /// Six coloured lamps spread around the hero (apart enough to tell them apart), to see the toon point lights at work.
        private void SpawnTestLamps()
        {
            ClearTestLamps();
            var hero = PlayerMovement.Instance;
            if (hero == null) { SetStatus("Start a run first"); return; }
            _testLamps = new GameObject("QA_TestLamps");
            Color[] colours = { new Color(1f, 0.6f, 0.25f), new Color(0.3f, 0.7f, 1f), new Color(1f, 0.3f, 0.6f), new Color(0.5f, 1f, 0.4f) };
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var go = new GameObject("Lamp" + i);
                go.transform.SetParent(_testLamps.transform, false);
                go.transform.position = hero.transform.position + new Vector3(Mathf.Cos(a) * 6f, 1.2f, Mathf.Sin(a) * 6f);
                var l = go.AddComponent<ToonPointLight>();
                l.colour = colours[i % colours.Length]; l.range = 3.5f; l.intensity = 2.5f; l.flicker = i % 2 == 0 ? 0.2f : 0f;
            }
            SetStatus($"6 test lamps placed; {GraphicsTier.PointLights} lit at once on this tier");
        }

        private void ClearTestLamps()
        {
            if (_testLamps != null) Destroy(_testLamps);
            _testLamps = null;
        }

        /// The numbers QA keeps checking, refreshed twice a second while the panel is open.
        private void RefreshInfo()
        {
            if (_info == null) return;
            var gun = EquippedGun();
            var ev = GachaBanners.All.Length > 0 ? GachaBanners.All[0] : null;
            _info.text = $"Coin {PlayerProfile.Coin:N0} · Gem {PlayerProfile.Gem:N0} · Tickets {PlayerProfile.Tickets:N0} · Pass XP {PlayerProfile.PassXp:N0} · Lv {PlayerProfile.AccountLevel}\n" +
                         (gun != null ? $"{gun.weaponName} ({gun.tier}) ★{PlayerProfile.GetWeaponLevel(gun.WeaponId)} · {PlayerProfile.GetWeaponShards(gun.WeaponId)} shards" : "No gun") +
                         (ev != null ? $" · Event pity {PlayerProfile.GetPity(ev.pityKey)}/{ev.hardPity}" : "") +
                         (RunState.Current != null ? $" · Run Lv {RunState.Current.Level}" : "") +
                         $" · Map {PlayingMapId()}";
        }

        private void AddSection(Transform parent, string label)
        {
            _row = null;
            var text = CreateText(parent, label.Replace(" ", "_"), label, 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, AccentColor);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
        }

        /// Buttons sit two to a row.
        private void AddAction(Transform parent, string label, Action action, bool danger = false, int fontSize = 25)
        {
            if (_row == null || _row.childCount >= 2)
            {
                _row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                _row.SetParent(parent, false);
                var h = _row.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 10f; h.childControlWidth = true; h.childControlHeight = true;
                h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                _row.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
            }
            var button = CreateButton(_row, label.Replace(" ", "_"), label, danger ? DangerColor : ButtonColor, fontSize);
            button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            button.onClick.AddListener(() => { Safe(label, action); RefreshInfo(); });
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

        // ------------------------------------------------------------------ wallet

        private void AddWallet(PlayerProfile.CurrencyKind kind, long amount)
        {
            PlayerProfile.Add(kind, amount);
            SetStatus($"{kind}: {PlayerProfile.GetBalance(kind):N0}");
        }

        private void AddTickets(long n)
        {
            PlayerProfile.AddTickets(n);
            SetStatus($"Tickets: {PlayerProfile.Tickets:N0}");
        }

        private void ZeroTickets()
        {
            PlayerProfile.TrySpendTickets(PlayerProfile.Tickets);
            SetStatus("Tickets: 0");
        }

        private void ZeroWallet()
        {
            PlayerProfile.SetBalanceForDev(PlayerProfile.CurrencyKind.Coin, 0);
            PlayerProfile.SetBalanceForDev(PlayerProfile.CurrencyKind.Gem, 0);
            SetStatus("Coin and Gem set to 0");
        }

        // ------------------------------------------------------------------ guns

        private void UnlockWeapons()
        {
            // A5: the serialized `weapons` array lives on a prefab this run may not edit, so it falls
            // back to the catalog when empty. That keeps newly onboarded weapons reachable from the
            // cheat panel without touching a prefab.
            var source = (weapons != null && weapons.Length > 0)
                ? (System.Collections.Generic.IReadOnlyList<WeaponData>)weapons
                : (WeaponCatalog.Active != null
                    ? WeaponCatalog.Active.AllData()
                    : (System.Collections.Generic.IReadOnlyList<WeaponData>)System.Array.Empty<WeaponData>());
            int count = PlayerProfile.UnlockAllWeaponsForDev(source);
            SetStatus($"Weapons unlocked: +{count} ({PlayerProfile.OwnedWeaponIds.Count} owned)");
        }

        private static WeaponData EquippedGun()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            return LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
        }

        private static EconomyConfig Economy()
        {
            var found = Resources.FindObjectsOfTypeAll<EconomyConfig>();
            return found.Length > 0 ? found[0] : null;
        }

        private void AddShards(int n)
        {
            var gun = EquippedGun() ?? throw new InvalidOperationException("No equipped gun.");
            PlayerProfile.AddWeaponShards(gun.WeaponId, n);
            SetStatus($"{gun.weaponName}: {PlayerProfile.GetWeaponShards(gun.WeaponId)} shards");
        }

        /// Pays for every star with granted shards and gold until the gun cannot go higher.
        private void MaxStarsEquipped()
        {
            var gun = EquippedGun() ?? throw new InvalidOperationException("No equipped gun.");
            var economy = Economy();
            for (int i = 0; i < 20; i++)
            {
                PlayerProfile.AddWeaponShards(gun.WeaponId, 500);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 100000);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 200000);
                if (PlayerProfile.TryUpgradeWeapon(gun, economy) != PlayerProfile.WeaponUpgradeResult.Upgraded) break;
            }
            SetStatus($"{gun.weaponName}: ★{PlayerProfile.GetWeaponLevel(gun.WeaponId)}");
        }

        private void AddGunList()
        {
            var cat = WeaponCatalog.Active;
            if (cat == null) return;
            foreach (var d in cat.DisplayData())
            {
                var gun = d;
                AddAction(_content, $"{gun.weaponName} · {gun.tier}".ToUpperInvariant(), () =>
                {
                    PlayerProfile.AddOwnedWeapon(gun.WeaponId);
                    PlayerProfile.SetEquippedWeapon(gun.WeaponId);
                    SetStatus($"Equipped {gun.weaponName}");
                }, false, 21);
            }
        }

        // ------------------------------------------------------------------ gacha / meta

        /// Puts every banner that many pulls from its guarantee (-1 resets pity to 0).
        private void SetPityEdge(int pullsLeft)
        {
            var economy = Economy();
            foreach (var b in GachaBanners.All)
            {
                var pool = GachaBanners.PoolFor(b, economy);
                int hard = b.kind == GachaBanners.Kind.Event ? b.hardPity : Mathf.Max(1, pool != null ? pool.pityThreshold : 30);
                PlayerProfile.DevSetPity(b.pityKey, pullsLeft < 0 ? 0 : Mathf.Max(0, hard - pullsLeft));
            }
            SetStatus(pullsLeft < 0 ? "Pity reset on every banner" : $"Every banner is {pullsLeft} pull(s) from its guarantee");
        }

        private void ResetDaily()
        {
            PlayerProfile.DevResetDailyClocks();
            SetStatus("New day: free pull, stamp, welcome reward and missions are fresh");
        }

        private void AddPassXp(int xp)
        {
            PlayerProfile.DevAddPassXp(xp);
            SetStatus($"Pass XP: {PlayerProfile.PassXp:N0}");
        }

        private void AddAccountXp()
        {
            PlayerProfile.AddAccountXp(5000);
            SetStatus($"Account level {PlayerProfile.AccountLevel}");
        }

        private void CompleteMissions()
        {
            int n = 0;
            foreach (var m in PassMissions.ActiveFor(DateTime.UtcNow))
            {
                PlayerProfile.AddMissionProgress(m.id, m.target);
                n++;
            }
            SetStatus($"{n} mission(s) complete: claim them on the Pass screen");
        }

        /// Rolls the event banner's rarity 10,000 times without granting anything, to compare the
        /// outcome against the published rates.
        private void SimulateRates()
        {
            var rates = GachaBanners.RatesFor(GachaBanners.All[0], Economy());
            var counts = new int[rates.Length];
            float total = 0f;
            foreach (var r in rates) total += r.percent;
            for (int i = 0; i < 10000; i++)
            {
                float roll = UnityEngine.Random.value * total, acc = 0f;
                for (int k = 0; k < rates.Length; k++) { acc += rates[k].percent; if (roll <= acc) { counts[k]++; break; } }
            }
            var sb = new System.Text.StringBuilder("10,000 rolls: ");
            for (int k = 0; k < rates.Length; k++) sb.Append($"{rates[k].tier} {counts[k] / 100f:0.#}% (pub {rates[k].percent:0.#}%)  ");
            SetStatus(sb.ToString());
        }

        private void UnlockCostumes()
        {
            int count = 0;
            for (int i = 0; i < costumeCatalogs.Length; i++)
                count += PlayerProfile.UnlockAllCostumes(costumeCatalogs[i]);
            SetStatus($"Costume entries unlocked: +{count}");
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

        // ------------------------------------------------------------------ run

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

        // Skill showcase for play-testing a power's feel without levelling up to it.
        private void GrantSkill(string id)
        {
            var skills = ZombieWar.Skills.SkillRuntime.Active ?? throw new InvalidOperationException("No active run.");
            bool ok = skills.Take(id);
            if (ok && ZombieWar.Skills.SkillCatalogDefs.ById(id)?.IsEvolution == true)
                ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();
            SetStatus(ok ? $"{id} -> rank {skills.RankOf(id)}" : $"{id}: not takeable");
        }

        private void GrantSkillMax(string id)
        {
            var def = ZombieWar.Skills.SkillCatalogDefs.ById(id) ?? throw new InvalidOperationException($"Unknown card {id}");
            var skills = ZombieWar.Skills.SkillRuntime.Active ?? throw new InvalidOperationException("No active run.");
            if (def.IsEvolution)
            {
                while (skills.Take(def.evolvesFrom)) { }
                skills.Take(def.partner);
            }
            while (skills.Take(id)) { }
            if (def.IsEvolution) ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();
            SetStatus($"{id} maxed");
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

        private static Transform PlayerOrThrow() =>
            PlayerMovement.Instance != null ? PlayerMovement.Instance.transform : throw new InvalidOperationException("No active player.");

        private void ForceLethalHit()
        {
            var health = PlayerOrThrow().GetComponent<Health>();
            if (_godMode) ToggleGodMode();
            health.TakeDamage(health.Max * 10f);
            SetStatus(health.IsHeld ? "Revive offer open" : "Player died");
        }

        private void SpawnChestHere()
        {
            var pickups = PickupManager.Instance ?? throw new InvalidOperationException("No pickup manager.");
            var p = PlayerOrThrow();
            pickups.SpawnChest(p.position + p.forward * 2f);
            SetStatus("Chest dropped");
        }

        private void SpawnItemHere(string kind)
        {
            if (!Enum.TryParse(kind, true, out PickupEffect effect)) throw new InvalidOperationException($"Unknown item {kind}");
            var pickups = PickupManager.Instance ?? throw new InvalidOperationException("No pickup manager.");
            var p = PlayerOrThrow();
            bool ok = pickups.SpawnMechanic(effect, p.position + p.forward * 2f);
            SetStatus(ok ? $"{effect} dropped" : $"{effect}: not spawned (one already out?)");
        }

        private void SpawnStationHere(string kind)
        {
            if (!Enum.TryParse(kind, true, out ZombieWar.Stations.StationKind k)) throw new InvalidOperationException($"Unknown station {kind}");
            var director = ZombieWar.Stations.StationDirector.Instance ?? throw new InvalidOperationException("No station director.");
            var p = PlayerOrThrow();
            director.SpawnDebug(k, p.position + p.forward * 9f);
            SetStatus($"{k} placed ahead");
        }

        private void SetAccountLevel(int level)
        {
            int need = AccountProgress.TotalFor(level) - PlayerProfile.AccountXp;
            if (need > 0) PlayerProfile.AddAccountXp(need);
            SetStatus($"Account LV {PlayerProfile.AccountLevel}");
        }

        private void KillAllZombies()
        {
            var zombies = FindObjectsByType<ZombieBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < zombies.Length; i++)
                zombies[i].TakeDamage(float.MaxValue);
            SetStatus($"Killed {zombies.Length} active zombie(s)");
        }

        private void RaiseThreat()
        {
            if (RunState.Current == null) throw new InvalidOperationException("No active run.");
            Threat.ThreatDirector.ReportObjectiveCompleted();
            SetStatus($"Threat tier: {Threat.ThreatDirector.Instance?.CurrentTier ?? 0} (+1 pending)");
        }

        private void EndRun()
        {
            if (RunState.Current == null) throw new InvalidOperationException("No active run.");
            Bill.Events?.Fire(new RunAbandonRequestedEvent());
            SetStatus("Run ended");
            SetPanelVisible(false);
        }

        // ------------------------------------------------------------------ map

        private static string PlayingMapId()
        {
            var streamer = World.BakedMapStreamer.Active;
            if (streamer != null && streamer.Theme != null) return streamer.Theme.id;
            return GameFlow.InGameplay ? World.MapTheme.ProceduralId : "-";
        }

        /// Every baked map in Resources/MapThemes, plus the old procedural world. Tapping one loads it
        /// at once: the run restarts on that map (from the menu, a run starts on it).
        private void AddMapList()
        {
            AddSection(_content, $"PLAYING: {PlayingMapId().ToUpperInvariant()} · NEXT RUN: {World.MapTheme.CurrentId.ToUpperInvariant()}");
            AddSection(_content, "DEBUG");
            AddAction(_content, "COLLIDERS ON / OFF", () =>
            {
                Dev.ColliderDebugView.Toggle();
                SetStatus(Dev.ColliderDebugView.On
                    ? "Colliders: blue water/lava, orange props, green player, red enemies, yellow other, cyan triggers"
                    : "Colliders off");
            });
            AddSection(_content, "TAP TO LOAD NOW (THE RUN RESTARTS)");
            var themes = Resources.LoadAll<World.MapTheme>(World.MapTheme.ResourceFolder.TrimEnd('/'));
            Array.Sort(themes, (a, b) => string.CompareOrdinal(a.id, b.id));
            foreach (var t in themes)
            {
                if (t == null || string.IsNullOrEmpty(t.id)) continue;
                string id = t.id;
                AddAction(_content, id.ToUpperInvariant(), () => LoadMap(id));
            }
            AddAction(_content, "OLD PROCEDURAL WORLD", () => LoadMap(World.MapTheme.ProceduralId));
        }

        private void LoadMap(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Map id missing.");
            id = id.Trim().ToLowerInvariant();
            if (id != World.MapTheme.ProceduralId && World.MapTheme.Load(id) == null)
                throw new ArgumentException($"No baked map '{id}'.");
            World.MapTheme.CurrentId = id;
            Time.timeScale = 1f;
            SetPanelVisible(false);
            if (GameFlow.InGameplay) GameFlow.RestartGameplay();
            else GameFlow.StartGameplay();
            Debug.Log("[ZombieWarCheats] map " + id);
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

        // ------------------------------------------------------------------ ui helpers

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
            if (visible) RefreshInfo();
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
            Stretch(text.rectTransform, 10f);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = fontSize;
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
