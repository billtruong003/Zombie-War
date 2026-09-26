using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// Play-mode tour of every screen: captures a PNG per screen and measures the LIVE rects (after
    /// layout groups, safe area and runtime sizing), which a static prefab scan cannot see.
    /// Start Play from Bootstrap.unity first, then run the menu. Output: Review/UiTour/<tag>/.
    public static class UiTour
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        struct Step { public string name; public Action act; public Func<bool> until; public float wait; }

        static List<Step> _steps;
        static int _i;
        static double _t;
        static bool _acted;
        static double _settleUntil;   // a capture lands on a later rendered frame: hold the next step
        static string _dir;
        static readonly StringBuilder Log = new StringBuilder();

        public static string LastReport => Log.ToString();
        public static bool Running => _steps != null;

        [MenuItem("ZombieWar/UI/Audit/Screen Tour (Play Mode)")]
        public static void RunMenu() => Run("latest", true);

        public static void Run(string tag, bool includeRun)
        {
            if (!Application.isPlaying) { Debug.LogError("[UiTour] Enter Play from Bootstrap.unity first."); return; }
            _dir = System.IO.Path.GetFullPath($"Review/UiTour/{tag}");
            System.IO.Directory.CreateDirectory(_dir);
            Log.Clear();
            Log.AppendLine($"[UiTour] {tag} screen {Screen.width}x{Screen.height}");
            _steps = BuildSteps(includeRun);
            _i = 0; _acted = false; _t = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static List<Step> BuildSteps(bool includeRun)
        {
            var s = new List<Step>
            {
                new Step { name = null, until = () => UIManager.Instance != null && UIManager.Instance.Top != null, wait = 5f },   // boot splash fades out first
                Shot("hub", () => UIManager.Instance.PopTo<HubScreen>()),
                Shot("loadout", () => UIManager.Instance.Push<LoadoutScreen>()),
                Shot("shop", () => { UIManager.Instance.PopTo<HubScreen>(); UIManager.Instance.Push<ShopScreen>(); }),
                Shot("costume", () => { UIManager.Instance.PopTo<HubScreen>(); UIManager.Instance.Push<CostumeScreen>(); }),
                Shot("pass", () => { UIManager.Instance.PopTo<HubScreen>(); UIManager.Instance.Push<PassScreen>(); }),
                // Display only (the save is untouched): every gun drawn as not owned, then the buy modal.
                Shot("shop_locked", () =>
                {
                    UIManager.Instance.PopTo<HubScreen>();
                    var shop = UIManager.Instance.Push<ShopScreen>();
                    var cat = shop.GetType().GetField("catalog", Any)?.GetValue(shop) as UIPrototypeCatalog;
                    foreach (var c in shop.GetComponentsInChildren<WeaponItemCardView>(true))
                    { c.BindIcon(cat, false); c.SetOwned(false, c.data != null ? c.data.price : 0); }
                }),
                Shot("buy_modal", () =>
                {
                    var shop = UIManager.Instance.Get<ShopScreen>();
                    var cat = shop.GetType().GetField("catalog", Any)?.GetValue(shop) as UIPrototypeCatalog;
                    var d = shop.GetComponentsInChildren<WeaponItemCardView>(true).First(c => c.data != null && c.data.price > 0).data;
                    Call(shop, "ShowPurchaseModal", null, false, d.weaponName, cat.GetWeaponIcon(d, true), WalletCurrency.Coin, (long)d.price);
                }),
                Shot("shop_after", () => Call(UIManager.Instance.Get<ShopScreen>(), "ClosePurchaseModal"), 0.8f),
            };
            if (!includeRun) return s;
            s.Add(Shot("loading", () => { UIManager.Instance.PopTo<HubScreen>(); GameFlow.StartGameplay(); }, 0.6f));
            s.Add(new Step { name = null, until = () => RunState.Current != null && Overlays() != null, wait = 3f });
            s.Add(new Step { name = null, act = () =>
            {
                Cheat("zw.god");
                foreach (var id in new[] { "auto.orbit", "auto.chainlightning", "auto.chainlightning", "stat.firerate", "stat.damage" }) Cheat("zw.skill " + id);
            }, wait = 2.5f });
            s.Add(Shot("hud", null));
            s.Add(Shot("levelup", () => Overlays().ShowLevelUp()));
            s.Add(Shot("pause", () => { Call(Overlays(), "PickPerk", 0); Overlays().ShowPause(); }));
            s.Add(Shot("settings", () => Call(Overlays(), "OpenSettings")));
            s.Add(Shot("result", () => { Set(Overlays(), "settingsRoot", false); Call(Overlays(), "EndRun"); }, 2.5f));
            return s;
        }

        static Step Shot(string name, Action act, float wait = 1.6f) => new Step { name = name, act = act, wait = wait };

        static RunOverlays Overlays() => UnityEngine.Object.FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
        static void Cheat(string cmd) { try { BillGameCore.Bill.Cheat?.Execute(cmd); } catch (Exception e) { Debug.LogWarning(e.Message); } }
        static void Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Any)?.Invoke(o, a);
        static void Set(object o, string f, bool on) => (o.GetType().GetField(f, Any)?.GetValue(o) as GameObject)?.SetActive(on);

        static void Tick()
        {
            if (!Application.isPlaying) { Stop("play mode ended"); return; }
            if (EditorApplication.timeSinceStartup < _settleUntil) return;
            var st = _steps[_i];
            try
            {
                if (!_acted) { st.act?.Invoke(); _acted = true; _t = EditorApplication.timeSinceStartup; }
                if (st.until != null && !st.until()) { _t = EditorApplication.timeSinceStartup; return; }
                if (EditorApplication.timeSinceStartup - _t < st.wait) return;
                if (st.name != null) { Capture(st.name); _settleUntil = EditorApplication.timeSinceStartup + 2.0; }
            }
            catch (Exception e) { Log.AppendLine($"  step {st.name ?? _i.ToString()} failed: {e.GetBaseException().Message}"); }
            _i++; _acted = false;
            if (_i >= _steps.Count) Stop("done");
        }

        static void Stop(string why)
        {
            EditorApplication.update -= Tick;
            _steps = null;
            Log.AppendLine($"[UiTour] END ({why})");
            if (_dir != null) System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "report.txt"), Log.ToString());
            Debug.Log(Log.ToString());
        }

        static void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, name + ".png"));
            var bad = new List<string>();
            foreach (var img in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (!img.isActiveAndEnabled) continue;
                if (UiAudit.MisSliced(img, out var why))
                    bad.Add($"    {UiAudit.PathOf(img.transform, null)} [{img.sprite.name}] {why}");
            }
            Log.AppendLine($"{name}: misSliced={bad.Count}");
            foreach (var l in bad.Distinct()) Log.AppendLine(l);
        }
    }
}
