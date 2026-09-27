using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// Play-mode capture of a live canvas (real data, real state) at any resolution, without
    /// touching the Game view or the desktop: the canvas is switched to Screen Space - Camera on a
    /// temporary camera that renders into a texture of the asked size, then switched back.
    /// Used for the M10 UI audit (overlaps, spacing) across device shapes.
    /// </summary>
    public static class LiveUiShot
    {
        public const string OutDir = "Review/V2/live";

        public static string Capture(Canvas canvas, string name, int w, int h, Camera sceneCamera = null)
        {
            if (canvas == null) return "no canvas";
            Directory.CreateDirectory(OutDir);
            var mode = canvas.renderMode; var cam = canvas.worldCamera; var plane = canvas.planeDistance;
            var go = new GameObject("LiveUiShotCam");
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var c = go.AddComponent<Camera>();
                c.targetTexture = rt;
                if (sceneCamera != null)
                {
                    // Draw the 3D world under the UI, from the scene camera's point of view.
                    c.CopyFrom(sceneCamera); c.targetTexture = rt;
                }
                else { if (Camera.main != null) { c.CopyFrom(Camera.main); c.targetTexture = rt; } c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.106f, 0.118f, 0.153f, 1f); c.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << canvas.gameObject.layer); }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = c; canvas.planeDistance = Mathf.Min(c.farClipPlane * 0.5f, 1f + c.nearClipPlane * 2f);
                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                    foreach (var fit in canvas.GetComponentsInChildren<ZombieWar.UI.GridFit>(false)) fit.Fit();
                }
                foreach (var t in canvas.GetComponentsInChildren<TMPro.TMP_Text>(false)) t.ForceMeshUpdate(true, true);
                c.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                var path = Path.Combine(OutDir, name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return path;
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = cam; canvas.planeDistance = plane;
                Canvas.ForceUpdateCanvases();
                Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        public static readonly (string tag, int w, int h)[] Shapes = { ("16x9", 1080, 1920), ("20x9", 1080, 2400), ("tablet", 1536, 2048) };

        public static string CaptureAll(Canvas canvas, string name, Camera sceneCamera = null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in Shapes) sb.Append(Capture(canvas, name + "_" + s.tag, s.w, s.h, sceneCamera)).Append(' ');
            return sb.ToString();
        }
    }
}
