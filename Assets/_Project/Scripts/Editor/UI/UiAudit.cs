using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.EditorTools
{
    /// Read-only checks over every UI prefab and the Menu scene. Nothing here saves an asset.
    /// - Sliced images whose scaled border does not fit the rect (the "pointy bar" bug).
    /// - Which TMP fonts each screen uses.
    /// - Prefab-instance overrides in Menu.unity (they silently cancel prefab edits).
    public static class UiAudit
    {
        public const string ScreensDir = "Assets/_Project/UI/Prefabs/Screens/";
        public const string MenuScene = "Assets/_Project/Scenes/Menu.unity";

        public static IEnumerable<string> ScreenPrefabs() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { ScreensDir.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath);

        /// Border (in rect units) the image draws on each axis, before Unity squashes it to fit.
        public static Vector2 DrawnBorder(Image img)
        {
            var b = img.sprite.border; // x=left y=bottom z=right w=top, sprite pixels
            float ppu = img.sprite.pixelsPerUnit * img.pixelsPerUnitMultiplier / 100f;
            if (ppu <= 0f) ppu = 1f;
            return new Vector2((b.x + b.z) / ppu, (b.y + b.w) / ppu);
        }

        /// True when a sliced image's border is larger than its rect on either axis.
        public static bool MisSliced(Image img, out string why)
        {
            why = null;
            if (img == null || img.sprite == null || img.type != Image.Type.Sliced || !img.hasBorder) return false;
            var size = ((RectTransform)img.transform).rect.size;
            var drawn = DrawnBorder(img);
            const float tol = 0.5f;
            if (drawn.x > size.x + tol || drawn.y > size.y + tol)
            {
                why = $"rect {size.x:0}x{size.y:0} border {drawn.x:0}x{drawn.y:0} ppum {img.pixelsPerUnitMultiplier:0.##}";
                return true;
            }
            return false;
        }

        public static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// Prefab contents have no parent canvas: give the root the reference size and run layout so
        /// rect sizes match what the phone shows.
        public static void PrepareForMeasure(GameObject root)
        {
            var rt = root.transform as RectTransform;
            if (rt != null && rt.rect.width < 1f) rt.sizeDelta = new Vector2(1080, 1920);
            if (rt != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
        }

        /// Compact text tree of a prefab branch: rects, images, text and components (read-only).
        public static string DumpTree(string prefabPath, string branch, int maxDepth)
        {
            var sb = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var start = string.IsNullOrEmpty(branch) ? root.transform : root.transform.Find(branch);
                if (start == null) return "branch not found: " + branch;
                DumpNode(sb, start, 0, maxDepth);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return sb.ToString();
        }

        static void DumpNode(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            var rt = t as RectTransform;
            var line = new StringBuilder(new string(' ', depth * 2)).Append(t.name);
            if (!t.gameObject.activeSelf) line.Append(" [off]");
            if (rt != null)
                line.Append($" a({rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##})-({rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}) p({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0}) s({rt.sizeDelta.x:0},{rt.sizeDelta.y:0})");
            var img = t.GetComponent<Image>();
            if (img != null) line.Append($" IMG[{(img.sprite != null ? img.sprite.name : "none")} #{ColorUtility.ToHtmlStringRGBA(img.color)}]");
            var tx = t.GetComponent<TMP_Text>();
            if (tx != null)
            {
                string txt = tx.text.Replace('\n', ' ');
                line.Append($" TXT[{(tx.font != null ? tx.font.name : "none")} {tx.fontSize} #{ColorUtility.ToHtmlStringRGB(tx.color)} '{txt.Substring(0, Mathf.Min(32, txt.Length))}']");
            }
            var comps = t.GetComponents<Component>()
                .Where(c => c != null && !(c is RectTransform) && !(c is CanvasRenderer) && !(c is Image) && !(c is TMP_Text) && !(c is ZombieWar.UI.UISliceFit))
                .Select(c => c.GetType().Name).ToList();
            if (comps.Count > 0) line.Append(" {").Append(string.Join(",", comps)).Append("}");
            sb.AppendLine(line.ToString());
            if (depth < maxDepth) foreach (Transform c in t) DumpNode(sb, c, depth + 1, maxDepth);
        }

        [MenuItem("ZombieWar/UI/Audit/Report (read-only)")]
        public static void Report() => Debug.Log(BuildReport());

        public static string BuildReport()
        {
            var sb = new StringBuilder("[UiAudit]\n");
            foreach (var path in ScreenPrefabs())
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PrepareForMeasure(root);
                    var bad = new List<string>();
                    foreach (var img in root.GetComponentsInChildren<Image>(true))
                        if (MisSliced(img, out var why)) bad.Add($"    {PathOf(img.transform, root.transform)} [{img.sprite.name}] {why}");
                    var fonts = root.GetComponentsInChildren<TMP_Text>(true)
                        .GroupBy(t => t.font != null ? t.font.name : "<none>")
                        .Select(g => $"{g.Key}:{g.Count()}");
                    int missing = root.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    sb.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: misSliced={bad.Count} missingScripts={missing} fonts={string.Join(", ", fonts)}");
                    foreach (var l in bad) sb.AppendLine(l);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            sb.Append(MenuOverrideReport(false));
            return sb.ToString();
        }

        /// Counts overrides per UI prefab instance in Menu.unity by reading the scene without opening it.
        public static string MenuOverrideReport(bool listAll)
        {
            var sb = new StringBuilder();
            var text = System.IO.File.ReadAllText(MenuScene);
            var guidToName = new Dictionary<string, string>();
            foreach (var p in ScreenPrefabs()) guidToName[AssetDatabase.AssetPathToGUID(p)] = System.IO.Path.GetFileNameWithoutExtension(p);
            // Each PrefabInstance block: m_Modifications ... m_SourcePrefab: {fileID: 100100000, guid: X, type: 3}
            var blocks = text.Split(new[] { "--- !u!" }, System.StringSplitOptions.None);
            foreach (var b in blocks)
            {
                if (!b.StartsWith("1001 ")) continue;
                var m = System.Text.RegularExpressions.Regex.Match(b, @"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)");
                if (!m.Success || !guidToName.TryGetValue(m.Groups[1].Value, out var name)) continue;
                var props = System.Text.RegularExpressions.Regex.Matches(b, @"propertyPath: (\S+)").Cast<System.Text.RegularExpressions.Match>()
                    .Select(x => x.Groups[1].Value).ToList();
                var kinds = props.GroupBy(p => p.Split('.')[0]).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key}:{g.Count()}");
                bool removed = b.Contains("m_RemovedComponents: [") == false || b.Contains("m_RemovedGameObjects: [") == false;
                sb.AppendLine($"Menu.unity override {name}: {props.Count} ({string.Join(", ", kinds)}){(removed ? " +removed items" : "")}");
            }
            return sb.ToString();
        }
    }
}
