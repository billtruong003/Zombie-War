using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// One look per map theme (2026-10-01). Every decoration piece keeps its UVs into the shared
    /// gradient palette (T_EnvPalette) and foliage atlas (T_EnvFoliage); a theme only swaps those two
    /// textures for recoloured copies, so the same rock is warm grey on the meadow, moss grey in the
    /// forest, dark basalt with a red cast by the volcano and cold blue grey on the tundra.
    /// Colours are moved by class, with soft weights so a gradient never breaks:
    ///  - stone (low saturation) takes the theme's stone hue and brightness;
    ///  - wood (dark warm hues) is dried, soaked or charred;
    ///  - greens take the theme's leaf hue (and the teal Lux grass is pulled back to grass green);
    ///  - warm foliage (orange autumn canopies) is turned toward the theme;
    ///  - everything else (crates, flowers, fuel) is only toned.
    /// The map baker swaps M_EnvSolid / M_EnvFoliage for the theme's copies in every chunk.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        sealed class Look
        {
            public float stoneHue, stoneSat, stoneV = 1f;
            public float woodHue = -1f, woodS = 1f, woodV = 1f;
            public float leafHue = -1f, leafS = 1f, leafV = 1f;
            public float warmHue = -1f, warmS = 1f, warmV = 1f;
            public float otherS = 1f, otherV = 1f;
            public bool tealFix = true;
            // Desert (02/10): blue-grey stones up to this saturation still count as stone, and bright
            // warm solids (Tiny Teacup's orange cliffs) follow the warm hue instead of staying as shipped.
            public float greySatMax = 0.28f;
            public bool warmSolids;
        }

        static readonly Dictionary<string, Look> Looks = new()
        {
            // Sunny farm: warm sandstone greys, fresh grass, everything else as authored.
            ["meadow"] = new Look { stoneHue = 35f, stoneSat = 0.10f, stoneV = 1.02f },
            // Deep forest: moss on the stones, darker and bluer leaves, autumn canopies turned olive.
            ["forest"] = new Look { stoneHue = 95f, stoneSat = 0.13f, stoneV = 0.9f, woodS = 1.1f, woodV = 0.9f,
                                    leafHue = 112f, leafS = 0.95f, leafV = 0.82f, warmHue = 96f, warmS = 0.62f, warmV = 0.6f },
            // Toxic swamp: everything damp and sickly yellow-green.
            ["swamp"] = new Look { stoneHue = 85f, stoneSat = 0.16f, stoneV = 0.78f, woodHue = 32f, woodS = 0.8f, woodV = 0.7f,
                                   leafHue = 70f, leafS = 0.85f, leafV = 0.78f, warmHue = 58f, warmS = 0.6f, warmV = 0.72f, otherS = 0.8f, otherV = 0.85f },
            // Volcano: basalt with a red cast, charred wood, scorched leaves, embers for warm foliage.
            ["volcano"] = new Look { stoneHue = 8f, stoneSat = 0.2f, stoneV = 0.62f, woodS = 0.5f, woodV = 0.45f,
                                     leafHue = 45f, leafS = 0.35f, leafV = 0.55f, warmHue = 18f, warmS = 0.8f, warmV = 0.62f, otherV = 0.85f },
            // Tundra: cold blue-grey stone, weathered wood, frosted pine needles.
            ["tundra"] = new Look { stoneHue = 212f, stoneSat = 0.12f, stoneV = 0.95f, woodS = 0.55f, woodV = 0.9f,
                                    leafHue = 160f, leafS = 0.42f, leafV = 0.78f, warmHue = 30f, warmS = 0.4f, warmV = 0.8f, otherS = 0.85f, tealFix = false },
            // Desert (concept 02/10): sandstone, sun-bleached wood, dry olive leaves.
            ["desert"] = new Look { stoneHue = 34f, stoneSat = 0.32f, stoneV = 1.4f, woodS = 0.7f, woodV = 1.05f,
                                    leafHue = 62f, leafS = 0.6f, leafV = 0.88f, warmHue = 30f, warmS = 0.48f, warmV = 0.95f,
                                    greySatMax = 0.45f, warmSolids = true },
            // City (concept 02/10): cool concrete greys, everything else as authored.
            ["city"] = new Look { stoneHue = 220f, stoneSat = 0.05f, stoneV = 0.96f },
        };

        static float Band(float h, float lo, float hi, float soft) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo - soft, lo, h)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hi, hi + soft, h)));

        static float TowardHue(float h, float target, float k) => h + Mathf.DeltaAngle(h, target) * k;

        static Color HsvDeg(float h, float s, float v)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(h, 360f) / 360f, Mathf.Clamp01(s), Mathf.Clamp01(v));
            return c;
        }

        /// One pixel of the palette (foliage = false) or of the foliage atlas (foliage = true).
        static Color Restyle(Color c, Look k, bool foliage)
        {
            Color.RGBToHSV(c, out float h01, out float s, out float v);
            float h = h01 * 360f;
            float grey = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, k.greySatMax, s));
            float green = Band(h, 65f, 200f, 15f) * (1f - grey);
            float warm = Band(h, 8f, 48f, 8f) * (1f - grey);
            // Wood is the dark end of the warm hues on solids; in the atlas the saturated warm hues
            // are leaves (autumn canopies), the rest of the warm hues stay wood-like bark and twigs.
            float wood = foliage ? warm * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, s)))
                                 : warm * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.92f, v)));
            float warmLeaf = foliage || k.warmSolids ? warm - wood : 0f;
            float other = Mathf.Max(0f, 1f - grey - green - wood - warmLeaf);

            var stone = HsvDeg(k.stoneHue, k.stoneSat * (0.6f + s * 2.5f), v * k.stoneV);

            // Lux teal grass back to grass green (foliage only: teal crates and fuel cans are fine), and
            // calmer, since the teal is far more saturated than the rest of the greens.
            float teal = k.tealFix && foliage ? Band(h, 140f, 200f, 12f) : 0f;
            float gh = TowardHue(h, 100f, teal * 0.85f);
            if (k.leafHue >= 0f) gh = TowardHue(gh, k.leafHue, 0.75f);
            var leaf = HsvDeg(gh, s * k.leafS * Mathf.Lerp(1f, 0.62f, teal), v * k.leafV * Mathf.Lerp(1f, 0.82f, teal));

            var wd = HsvDeg(k.woodHue >= 0f ? TowardHue(h, k.woodHue, 0.7f) : h, s * k.woodS, v * k.woodV);
            var wl = HsvDeg(k.warmHue >= 0f ? TowardHue(h, k.warmHue, 0.9f) : h, s * k.warmS, v * k.warmV);
            var ot = HsvDeg(h, s * k.otherS, v * k.otherV);

            float sum = grey + green + wood + warmLeaf + other;
            var o = (stone * grey + leaf * green + wd * wood + wl * warmLeaf + ot * other) / Mathf.Max(0.0001f, sum);
            o.a = c.a;
            return o;
        }

        static readonly Dictionary<string, (Material solid, Material foliage)> ThemeMats = new();

        /// The theme's copies of the two decoration materials, built from the current base textures.
        static (Material solid, Material foliage) ThemeMaterials(string id)
        {
            if (ThemeMats.TryGetValue(id, out var ms)) return ms;
            if (!Looks.TryGetValue(id, out var look)) return (null, null);
            var baseSolid = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvSolid.mat");
            var baseFoliage = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvFoliage.mat");
            Directory.CreateDirectory(Pal + "Themes");
            ms = (ThemeCopy(baseSolid, Pal + "T_EnvPalette.png", id, look, false),
                  ThemeCopy(baseFoliage, Pal + "T_EnvFoliage.png", id, look, true));
            ThemeMats[id] = ms;
            return ms;
        }

        static Material ThemeCopy(Material src, string texPath, string id, Look look, bool foliage)
        {
            var srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var srcImp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            int size = srcTex.width;
            var px = ReadTexture(srcTex, size, out _);
            for (int i = 0; i < px.Length; i++) px[i] = Restyle(px[i], look, foliage);
            if (foliage) EnvSandboxBuilder.BleedIntoTransparent(px, size, size);
            string outTex = Pal + "Themes/" + Path.GetFileNameWithoutExtension(texPath) + "_" + id + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(px); tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            // Unity keeps imported files mapped; release them before overwriting (IO error 1224), and
            // leave an unchanged texture alone.
            if (!File.Exists(outTex) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(outTex), png))
            {
                AssetDatabase.ReleaseCachedFileHandles();
                File.WriteAllBytes(outTex, png);
            }
            AssetDatabase.ImportAsset(outTex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outTex);
            imp.sRGBTexture = true;
            imp.filterMode = srcImp.filterMode;
            imp.mipmapEnabled = srcImp.mipmapEnabled;
            imp.alphaIsTransparency = srcImp.alphaIsTransparency;
            imp.mipMapsPreserveCoverage = srcImp.mipMapsPreserveCoverage;
            imp.alphaTestReferenceValue = srcImp.alphaTestReferenceValue;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = srcImp.maxTextureSize;
            imp.SaveAndReimport();

            string matPath = Pal + "Themes/" + src.name + "_" + id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(outTex);
            m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null) m.SetTexture("_EmissionMap", t);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// Swaps the shared decoration materials in a chunk for the theme's copies.
        static void ApplyThemeLook(GameObject chunk, string id)
        {
            var (solid, foliage) = ThemeMaterials(id);
            if (solid == null) return;
            var baseSolid = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvSolid.mat");
            var baseFoliage = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvFoliage.mat");
            foreach (var r in chunk.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == baseSolid) { mats[i] = solid; changed = true; }
                    else if (mats[i] == baseFoliage) { mats[i] = foliage; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// Contact sheet: every theme's palette and foliage atlas side by side, for review.
        [MenuItem("HordeCall/World/Palette/Render Theme Looks")]
        public static string RenderThemeLooks()
        {
            ThemeMats.Clear();
            Directory.CreateDirectory("Review/M8/palette");
            const int T = 256;
            var sheet = new Texture2D(T * (Looks.Count + 1), T * 2, TextureFormat.RGB24, false);
            void Put(int col, int row, string path)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var px = ReadTexture(t, T, out _);
                for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(new Color(0.15f, 0.16f, 0.18f), px[i], px[i].a);
                sheet.SetPixels(col * T, row * T, T, T, px);
            }
            Put(0, 1, Pal + "T_EnvPalette.png"); Put(0, 0, Pal + "T_EnvFoliage.png");
            int c = 1;
            foreach (var id in Looks.Keys)
            {
                ThemeMaterials(id);
                Put(c, 1, Pal + "Themes/T_EnvPalette_" + id + ".png");
                Put(c, 0, Pal + "Themes/T_EnvFoliage_" + id + ".png");
                c++;
            }
            sheet.Apply();
            File.WriteAllBytes("Review/M8/palette/theme_looks.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return "base, " + string.Join(", ", Looks.Keys);
        }
    }
}
