using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// Renders a UI prefab to a PNG at 1080x1920 without Play Mode (M9 self-check): the prefab is
    /// dropped under a temporary screen-space-camera canvas, laid out, drawn once and read back.
    /// </summary>
    public static class UiShot
    {
        public const string OutDir = "Review/V2/shots";

        /// <summary>Device shapes the v2 screens must hold up on (owner: responsive, not one size).</summary>
        public static readonly (string tag, int w, int h)[] Devices =
        {
            ("16x9", 1080, 1920), ("19x9", 1080, 2337), ("20x9", 1080, 2400), ("tablet", 1536, 2048),
        };

        /// <summary>Renders a prefab once per <see cref="Devices"/> entry.</summary>
        public static string RenderAll(string prefabPath, string outName)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var d in Devices) list.Add(Render(prefabPath, outName + "_" + d.tag, d.w, d.h));
            return string.Join(", ", list);
        }

        public static string Render(string prefabPath, string outName = null, int w = 1080, int h = 1920)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("[UiShot] missing " + prefabPath); return null; }
            Directory.CreateDirectory(OutDir);
            var at = new Vector3(0, -9000, 0);
            var camGo = new GameObject("UiShotCam");
            var canvasGo = new GameObject("UiShotCanvas", typeof(RectTransform));
            RenderTexture rt = null;
            try
            {
                camGo.transform.position = at + new Vector3(0, 0, -100);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.106f, 0.118f, 0.153f, 1f);
                cam.orthographic = true; cam.nearClipPlane = 1; cam.farClipPlane = 1000;
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;

                canvasGo.transform.position = at;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 50;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                // Same as UIRoot in Menu.unity: Expand keeps 1080x1920 visible and grows the canvas
                // on taller phones and wider tablets.
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
                inst.SetActive(true);
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
                foreach (var cg in inst.GetComponentsInChildren<CanvasGroup>(true)) cg.alpha = cg.gameObject == inst ? 1f : cg.alpha;
                var rootCg = inst.GetComponent<CanvasGroup>(); if (rootCg != null) rootCg.alpha = 1f;

                // Two passes: content size fitters and grids settle after the first.
                for (int i = 0; i < 2; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvasGo.transform);
                    foreach (var fit in inst.GetComponentsInChildren<ZombieWar.UI.GridFit>(true)) fit.Fit();
                }
                foreach (var t in inst.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string file = Path.Combine(OutDir, (outName ?? Path.GetFileNameWithoutExtension(prefabPath)) + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                cam.targetTexture = null;
                return file;
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
    }
}
