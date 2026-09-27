using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: puts the v2 screens under UIRoot in Menu.unity (owner approved the Meta v2 mockups, so
    /// this scene edit is intended), makes Home the first screen and wires every navigation link.
    /// A link prefers the v2 screen and falls back to the M8 screen until the v2 one exists, so
    /// re-running this after each phase moves the links over. Old M8 screens are left in place
    /// until the clean-up phase.
    /// </summary>
    public static class V2MenuInstaller
    {
        const string MenuScene = "Assets/_Project/Scenes/Menu.unity";
        public const string ToastPath = V2KitSample.Dir + "UI_V2_Toast.prefab";

        /// v2 prefabs to install, in hierarchy order (later = drawn on top).
        static readonly string[] Prefabs =
        {
            V2HomeBuilder.Path,
            V2KitSample.Dir + "UI_V2_Profile.prefab",
            V2KitSample.Dir + "UI_V2_Settings.prefab",
            V2KitSample.Dir + "UI_V2_Daily.prefab",
            V2KitSample.Dir + "UI_V2_Pass.prefab",
            V2KitSample.Dir + "UI_V2_Arsenal.prefab",
            V2KitSample.Dir + "UI_V2_Shop.prefab",
            V2KitSample.Dir + "UI_V2_Gacha.prefab",
            V2KitSample.Dir + "UI_V2_Studio.prefab",
            ToastPath,
        };

        /// For each link: v2 screen type name first, M8 fallback second.
        static readonly Dictionary<string, string[]> Targets = new()
        {
            ["profileScreen"] = new[] { "ProfileScreen" },
            ["settingsScreen"] = new[] { "SettingsScreen" },
            ["dailyScreen"] = new[] { "DailyScreen" },
            ["passScreen"] = new[] { "PassScreenV2", "PassScreen" },
            ["arsenalScreen"] = new[] { "ArsenalScreen", "LoadoutScreen" },
            ["shopScreen"] = new[] { "ShopScreenV2", "ShopScreen" },
            ["gachaScreen"] = new[] { "GachaScreen" },
            ["studioScreen"] = new[] { "StudioScreen", "CostumeScreen" },
        };

        [MenuItem("HordeCall/UI v2/Build Toast")]
        public static string BuildToast()
        {
            var r = ScreenRoot("Toast");
            try
            {
                var cg = r.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false;
                var toast = r.gameObject.AddComponent<Toast>();
                var pill = Box(Node(r, "Pill"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 170, 300, 40);
                Surface(pill, Hex("12151c"), 12f).color = new Color(0.07f, 0.08f, 0.11f, 0.94f);
                var t = Body(Fill(Node(pill, "Text"), 12, 0, 12, 0), "Unlocks at level 3", 14f, Ink, TextAlignmentOptions.Center);
                Wire(toast, "label", t);
                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, ToastPath);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { UnityEngine.Object.DestroyImmediate(r.gameObject); }
        }

        /// <summary>Rebuilds every v2 prefab that has a builder and renders each to Review/V2/shots.</summary>
        [MenuItem("HordeCall/UI v2/Build All + Shots")]
        public static string BuildAll()
        {
            var log = new List<string>();
            void Step(string name, Func<string> build)
            {
                try { var path = build(); log.Add($"{name}: {path} -> {UiShot.RenderAll(path, name)}"); }
                catch (Exception e) { log.Add($"{name}: FAILED {e.Message}"); Debug.LogException(e); }
            }
            Step("Home", V2HomeBuilder.Build);
            Step("Profile", V2ProfileBuilder.Build);
            Step("Settings", V2SettingsBuilder.Build);
            Step("Daily", V2DailyBuilder.Build);
            Step("Pass", V2PassBuilder.Build);
            Step("Arsenal", V2ArsenalBuilder.Build);
            Step("Shop", V2ShopBuilder.Build);
            try { log.Add("Toast: " + BuildToast()); } catch (Exception e) { log.Add("Toast: FAILED " + e.Message); }
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        [MenuItem("HordeCall/UI v2/Install Into Menu")]
        public static string Install()
        {
            if (EditorApplication.isPlaying) return "not in play mode";
            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            var log = new List<string>();
            try
            {
                var uiRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<UIManager>() != null);
                if (uiRoot == null) return "UIRoot with UIManager not found";

                foreach (var path in Prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var old = uiRoot.transform.Find(prefab.name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, uiRoot.transform);
                    var rt = (RectTransform)inst.transform;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                    // Screens start hidden (UIManager shows them); the toast stays on as an overlay.
                    inst.SetActive(inst.GetComponent<UIScreen>() == null);
                    log.Add("installed " + prefab.name);
                }

                var screens = uiRoot.GetComponentsInChildren<UIScreen>(true).ToList();
                UIScreen Find(string[] names) => names.Select(n => screens.FirstOrDefault(s => s.GetType().Name == n)).FirstOrDefault(s => s != null);

                var home = screens.OfType<HomeScreen>().FirstOrDefault();
                if (home != null)
                {
                    foreach (var kv in Targets)
                    {
                        var target = Find(kv.Value);
                        Wire(home, kv.Key, target);
                        log.Add($"{kv.Key} -> {(target != null ? target.GetType().Name : "none")}");
                    }
                    Wire(uiRoot.GetComponent<UIManager>(), "initialScreen", home);
                    log.Add("initialScreen -> HomeScreen");
                }

                // Every v2 tab bar in every screen gets the same five targets.
                var navTargets = new UIScreen[] { home, Find(Targets["arsenalScreen"]), Find(Targets["shopScreen"]), Find(Targets["gachaScreen"]), Find(Targets["passScreen"]) };
                foreach (var nav in uiRoot.GetComponentsInChildren<NavBarV2>(true)) WireArray(nav, "targets", navTargets);

                // Back-links and cross links on the other v2 screens use the same lookup.
                foreach (var s in screens)
                {
                    var so = new SerializedObject(s);
                    foreach (var kv in Targets)
                    {
                        var p = so.FindProperty(kv.Key);
                        if (p != null && p.propertyType == SerializedPropertyType.ObjectReference && s != home)
                            p.objectReferenceValue = Find(kv.Value);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // The old Hub is no longer the way in; keep it hidden until clean-up.
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            return string.Join("\n", log);
        }
    }
}
