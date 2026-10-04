using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// Brings every UI screen prefab up to the slice rules (memory: ui-sliced-bars):
    /// 1. Bar fills become clips (UIBarClip + RectMask2D) around a full-size graphic, so a low value
    ///    is a clean cut of a round pill instead of a squashed sliver. Screen code keeps driving the
    ///    same RectTransform through anchorMax.x; Image references move to the new graphic.
    /// 2. Every sliced image with a border gets UISliceFit, so its corners fit at any size.
    /// Idempotent: converted bars and fitted images are skipped on a second run.
    public static class UiKitApply
    {
        [MenuItem("ZombieWar/UI/Kit/Apply Slice Fit + Bar Clips (all screens)")]
        public static void ApplyAll()
        {
            foreach (var path in UiAudit.ScreenPrefabs())
                Debug.Log($"[UiKit] {System.IO.Path.GetFileName(path)}: {Apply(path)}");
        }

        public static string Apply(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int bars = 0;
                foreach (var fill in FindBarFills(root))
                    if (ConvertBar(root, fill)) bars++;
                int fits = AddSliceFit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"bars converted {bars}, slice-fit added {fits}";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int AddSliceFit(GameObject root)
        {
            int n = 0;
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null || img.type != Image.Type.Sliced || !img.hasBorder) continue;
                if (img.GetComponent<UISliceFit>() != null) continue;
                var fit = img.gameObject.AddComponent<UISliceFit>();
                fit.BaseMultiplier = img.pixelsPerUnitMultiplier;
                n++;
            }
            return n;
        }

        /// RectTransforms that screen code or a Slider scales through anchorMax.x.
        static IEnumerable<GameObject> FindBarFills(GameObject root)
        {
            var set = new HashSet<GameObject>();
            foreach (var s in root.GetComponentsInChildren<Slider>(true))
                if (s.fillRect != null) set.Add(s.fillRect.gameObject);
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Only screens that drive a bar value; tabs and chips also name their tint image "fill".
                if (mb == null || !(mb is HudController)) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    string p = it.propertyPath.ToLowerInvariant();
                    bool named = (p.Contains("fill") && !p.Contains("cooldown")) || p.StartsWith("statbars");
                    if (!named) continue;
                    var go = (it.objectReferenceValue as Component)?.gameObject;
                    if (go == null || !go.transform.IsChildOf(root.transform)) continue;
                    // Already converted: the reference now points at the graphic inside a clip.
                    if (go.transform.parent != null && go.transform.parent.GetComponent<UIBarClip>() != null) continue;
                    var img = go.GetComponent<Image>();
                    if (img != null && img.type == Image.Type.Filled) continue;   // radial/filled bars use fillAmount
                    if (img == null && go.GetComponent<UIBarClip>() == null) continue;
                    set.Add(go);
                }
            }
            return set;
        }

        public static bool ConvertBar(GameObject root, GameObject fill)
        {
            if (fill.GetComponent<UIBarClip>() != null) return false;
            var img = fill.GetComponent<Image>();
            if (img == null) return false;

            var g = new GameObject("Graphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.layer = fill.layer;
            var grt = (RectTransform)g.transform;
            grt.SetParent(fill.transform, false);
            grt.SetAsFirstSibling();
            var gi = g.GetComponent<Image>();
            gi.sprite = img.sprite;
            gi.color = img.color;
            gi.material = img.material == img.defaultMaterial ? null : img.material;
            gi.type = img.type;
            gi.fillCenter = img.fillCenter;
            gi.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
            gi.preserveAspect = img.preserveAspect;
            gi.raycastTarget = false;

            // Anything that pointed at the old image (e.g. the HUD's red-under-30% tint) follows it.
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == img)
                    { it.objectReferenceValue = gi; changed = true; }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var s in root.GetComponentsInChildren<Selectable>(true))
                if (s.targetGraphic == img) s.targetGraphic = gi;

            Object.DestroyImmediate(img, true);
            fill.AddComponent<RectMask2D>();
            var clip = fill.AddComponent<UIBarClip>();
            clip.Graphic = grt;
            return true;
        }
    }
}
