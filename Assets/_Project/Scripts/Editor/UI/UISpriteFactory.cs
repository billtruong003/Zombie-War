using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Sinh structural sprites cho UI (spec §2.4) bằng SDF — chạy 1 lần, idempotent.
    /// Đây là asset THẬT (rounded/glow/pill/dashed...), không phải placeholder:
    /// thiếu chúng thì mọi card/button đều là hình vuông flat, không thể match design.
    /// Artwork (icon súng, costume, gacha art) KHÔNG thuộc file này — vẫn là placeholder.
    /// </summary>
    public static class UISpriteFactory
    {
        public const string Dir = "Assets/_Project/UI/Sprites";

        [MenuItem("ZombieWar/UI/Generate UI Sprites")]
        public static void Generate()
        {
            EnsureAll(force: true);
            Debug.Log("[UISpriteFactory] Sprites generated vào " + Dir);
        }

        /// <summary>Sinh mọi sprite nếu thiếu (force = đè lại). Gọi từ installer trước khi build màn.</summary>
        public static void EnsureAll(bool force = false)
        {
            Directory.CreateDirectory(Dir);

            RoundedRect("rounded_24", 96, 96, 24, border: 32, force);
            RoundedRect("rounded_32", 112, 112, 32, border: 40, force);
            RoundedFrame("frame_24", 96, 96, 24, thickness: 3, border: 32, force);
            RoundedFrame("frame_32", 112, 112, 32, thickness: 4, border: 40, force);
            DashedFrame("rounded_dashed", 160, 160, 24, thickness: 2.5f, border: 40, force);
            Capsule("pill", 128, 64, border: 30, force);
            CircleSprite("circle", 128, force);
            Ring("ring_thin", 256, thickness: 8, force);
            GlowSoft("glow_soft", 160, 160, 32, falloff: 44, border: 56, force);
            GradGoldV("grad_gold_v", force);
            RoundedGrad("grad_gold_rounded", 96, 96, 24, border: 32, force);
            RedVignette("grad_red_vignette", 256, force);
            DiagonalTile("bg_diagonal", 128, force);

            AssetDatabase.Refresh();
        }

        public static Sprite Load(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        // ============================================================ generators

        static void RoundedRect(string name, int w, int h, float r, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                float d = SdRoundRect(x, y, w, h, r);
                return new Color(1, 1, 1, AA(d));
            });
        }

        static void RoundedFrame(string name, int w, int h, float r, float thickness, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                float d = SdRoundRect(x, y, w, h, r);
                float a = AA(Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f);
                return new Color(1, 1, 1, a);
            });
        }

        static void DashedFrame(string name, int w, int h, float r, float thickness, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                float d = SdRoundRect(x, y, w, h, r);
                float band = AA(Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f);
                if (band <= 0f) return Color.clear;
                // dash 12/8 dọc theo cạnh gần nhất (xấp xỉ — đủ cho cảm giác dashed)
                float px = x - w * 0.5f, py = y - h * 0.5f;
                float ex = Mathf.Abs(px) - (w * 0.5f - r), ey = Mathf.Abs(py) - (h * 0.5f - r);
                float u = ex > 0 && ey > 0 ? Mathf.Atan2(ey, ex) * r
                        : ex > ey ? py : px;
                bool on = Mathf.Abs(u) % 20f < 12f;
                return new Color(1, 1, 1, on ? band : 0f);
            });
        }

        static void Capsule(string name, int w, int h, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                float d = SdRoundRect(x, y, w, h, h * 0.5f);
                return new Color(1, 1, 1, AA(d));
            });
        }

        static void CircleSprite(string name, int size, bool force)
        {
            Bake(name, size, size, 0, force, (x, y) =>
            {
                float d = Dist(x, y, size) - (size * 0.5f - 1.5f);
                return new Color(1, 1, 1, AA(d));
            });
        }

        static void Ring(string name, int size, float thickness, bool force)
        {
            Bake(name, size, size, 0, force, (x, y) =>
            {
                float d = Mathf.Abs(Dist(x, y, size) - (size * 0.5f - thickness - 1.5f)) - thickness * 0.5f;
                return new Color(1, 1, 1, AA(d));
            });
        }

        static void GlowSoft(string name, int w, int h, float r, float falloff, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                float inset = falloff + 2f;
                float d = SdRoundRectInset(x, y, w, h, r, inset);
                float a = d <= 0 ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / falloff), 2.2f);
                return new Color(1, 1, 1, a);
            });
        }

        static readonly Color GoldHi = new Color(1f, 0.812f, 0.4f);      // #FFCF66
        static readonly Color GoldLo = new Color(0.91f, 0.604f, 0.169f); // #E89A2B

        static void GradGoldV(string name, bool force)
        {
            Bake(name, 8, 128, 0, force, (x, y) => Color.Lerp(GoldLo, GoldHi, y / 127f));
        }

        static void RoundedGrad(string name, int w, int h, float r, int border, bool force)
        {
            Bake(name, w, h, border, force, (x, y) =>
            {
                var c = Color.Lerp(GoldLo, GoldHi, y / (float)(h - 1));
                c.a = AA(SdRoundRect(x, y, w, h, r));
                return c;
            });
        }

        static void RedVignette(string name, int size, bool force)
        {
            var red = new Color(0.898f, 0.282f, 0.302f); // #E5484D
            Bake(name, size, size, 0, force, (x, y) =>
            {
                float r01 = Dist(x, y, size) / (size * 0.5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r01 - 0.45f) / 0.55f));
                return new Color(red.r, red.g, red.b, a);
            });
        }

        static void DiagonalTile(string name, int size, bool force)
        {
            Bake(name, size, size, 0, force, (x, y) =>
            {
                bool stripe = (x + y) % 64 < 20;
                return new Color(1, 1, 1, stripe ? 0.045f : 0f);
            }, wrapRepeat: true);
        }

        // ============================================================ core

        static float SdRoundRect(float x, float y, float w, float h, float r)
            => SdRoundRectInset(x, y, w, h, r, 1.5f);

        static float SdRoundRectInset(float x, float y, float w, float h, float r, float inset)
        {
            float px = x - w * 0.5f + 0.5f, py = y - h * 0.5f + 0.5f;
            float bx = w * 0.5f - r - inset, by = h * 0.5f - r - inset;
            float qx = Mathf.Max(Mathf.Abs(px) - bx, 0f), qy = Mathf.Max(Mathf.Abs(py) - by, 0f);
            return Mathf.Sqrt(qx * qx + qy * qy) - r;
        }

        static float Dist(float x, float y, int size)
        {
            float px = x - size * 0.5f + 0.5f, py = y - size * 0.5f + 0.5f;
            return Mathf.Sqrt(px * px + py * py);
        }

        static float AA(float d) => Mathf.Clamp01(0.5f - d / 1.5f);

        static void Bake(string name, int w, int h, int border, bool force,
            System.Func<int, int, Color> pixel, bool wrapRepeat = false)
        {
            string path = $"{Dir}/{name}.png";
            if (!force && File.Exists(path)) return;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }
}
