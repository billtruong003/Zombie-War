using System.Collections.Generic;
using UnityEngine;
using static ZombieWar.EditorTools.LookShot;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// The look variants each map is shot with (owner feedback 02/10): the current look, three surface
    /// options (ground / fluid), five colour grades on top of surface option 2, and on forest the
    /// outline colours. Names are file-safe; labels are what the comparison page shows.
    /// </summary>
    public static partial class LookShot
    {
        static List<Variant> Variants(string theme)
        {
            var list = new List<Variant> { new("now", "Hiện tại", () => { }) };
            var surfaces = Surfaces(theme);
            list.AddRange(surfaces);
            var pick = surfaces.Count > 1 ? surfaces[1].apply : () => { };
            int n = 1;
            foreach (var (name, label, grade) in Grades(theme))
            {
                var g = grade;
                list.Add(new Variant($"post{n++}_{name}", label, () => { pick(); ApplyGrade(g); }));
            }
            if (theme == "forest")
                foreach (var (name, label, c) in OutlineColours)
                {
                    var col = c;
                    list.Add(new Variant("line_" + name, label, () => Ctx.OutlineColor(col)));
                }
            return list;
        }

        static readonly (string, string, Color)[] OutlineColours =
        {
            ("black", "Viền gần đen (hiện tại)", new Color(0.06f, 0.05f, 0.09f)),
            ("plum", "Viền nâu tím", new Color(0.16f, 0.09f, 0.14f)),
            ("brown", "Viền nâu ấm", new Color(0.22f, 0.12f, 0.06f)),
            ("navy", "Viền xanh than", new Color(0.06f, 0.09f, 0.2f)),
            ("green", "Viền xanh rêu đậm", new Color(0.05f, 0.14f, 0.08f)),
            ("mauve", "Viền tím xám", new Color(0.3f, 0.24f, 0.32f)),
        };

        // ── surfaces ────────────────────────────────────────────────────────────────────

        static List<Variant> Surfaces(string theme)
        {
            switch (theme)
            {
                case "volcano":
                    return new List<Variant>
                    {
                        new("surf1_charcoal_glow", "Đất than đen, nứt mảnh sáng khắp nơi · lava sủi bọt", () => { Charcoal(0.55f, 0.6f, 0.2f, warm: false); Lava(0f, 0.4f, 1.1f); }),
                        new("surf2_charcoal_plates", "Đất than đen, nứt sáng mạnh gần lava · lava có mảng vỏ + bọt", () => { Charcoal(0.6f, 0.25f, 0.35f, warm: false); Lava(0.8f, 0.35f, 1.3f); }),
                        new("surf3_basalt_crust", "Đá bazan ấm, nứt rất mảnh bập bùng · lava vỏ dày, bọt to", () => { Charcoal(0.7f, 0.45f, 0.6f, warm: true); Lava(1f, 0.3f, 1.8f); }),
                    };
                case "swamp":
                    return new List<Variant>
                    {
                        new("surf1_clear_bubbles", "Nước độc trong, bọt sủi nhỏ", () => { Water(0.45f, 0.12f, 0f, false); Bubbles(0.55f, 1.4f, 0.5f); Bed(MudBedA, MudBedB, 0.8f); }),
                        new("surf2_milky_bigbubbles", "Nước độc đục sữa, bọt to vỡ vòng", () => { Water(0.22f, 0.5f, 0f, false); Bubbles(0.8f, 2.2f, 0.35f); Glow(1.6f); }),
                        new("surf3_refract_bubbles", "Nước độc khúc xạ, thấy đáy, bọt sủi", () => { Water(0.55f, 0.12f, 0.2f, true); Bubbles(0.6f, 1.6f, 0.45f); Bed(MudBedA, MudBedB, 0.8f); }),
                    };
                case "tundra":
                    return new List<Variant>
                    {
                        new("surf1_clear_ice", "Băng trong, nứt rõ", () => { Water(0.8f, 0.35f, 0f, false); Bed(IceBedA, IceBedB, 0.8f); }),
                        new("surf2_frost_caustic", "Băng mờ sương, vân sáng nhẹ dưới đáy", () => { Water(0.45f, 0.45f, 0.3f, false); Bed(IceBedA, IceBedB, 0.8f); }),
                        new("surf3_refract_ice", "Băng khúc xạ, thấy đáy rõ", () => { Water(0.9f, 0.2f, 0.25f, true); Bed(IceBedA, IceBedB, 0.8f); }),
                    };
                default:   // meadow, forest: clear water
                    return new List<Variant>
                    {
                        new("surf1_clear", "Nước trong, viền bọt mảnh, đáy cát sỏi", () => { Water(0.7f, 0.12f, 0f, false); Bed(SandBedA, SandBedB, 0.85f); }),
                        new("surf2_clear_caustic", "Nước trong + vân sáng dưới đáy cát sỏi", () => { Water(1.0f, 0.1f, 0.6f, false); Bed(SandBedA, SandBedB, 0.85f); }),
                        new("surf3_refract_caustic", "Nước khúc xạ + vân sáng (tốn hiệu năng hơn)", () => { Water(1.0f, 0.1f, 0.6f, true); Bed(SandBedA, SandBedB, 0.85f); }),
                    };
            }
        }

        /// <summary>See-through water: clarity depth, edge tint, caustics, refraction; always a thin
        /// intersection line with a soft band behind it.</summary>
        static void Water(float clarity, float minAlpha, float caustics, bool refract)
        {
            var m = Ctx.Fluid; if (m == null) return;
            m.SetFloat("_Clarity", clarity); m.SetFloat("_MinAlpha", minAlpha);
            m.SetFloat("_Caustics", caustics);
            m.SetFloat("_FoamWidth", 0.05f); m.SetFloat("_FoamSoft", 0.12f); m.SetFloat("_FoamWobble", 0.03f);
            m.SetFloat("_StreakAlpha", 0.35f);
            if (refract) { m.EnableKeyword("_REFRACT"); m.SetFloat("_RefractStrength", 0.02f); Ctx.OpaqueTexture(true); }
            else m.DisableKeyword("_REFRACT");
        }

        /// <summary>The basin floor seen through the water: two tones of pebbly bed.</summary>
        static void Bed(Color a, Color b, float strength)
        {
            var m = Ctx.Ground; if (m == null) return;
            m.SetColor("_BedColor", a); m.SetColor("_BedColor2", b); m.SetFloat("_BedStrength", strength);
        }

        static readonly Color SandBedA = new(0.66f, 0.57f, 0.4f), SandBedB = new(0.5f, 0.46f, 0.36f);
        static readonly Color MudBedA = new(0.3f, 0.32f, 0.16f), MudBedB = new(0.22f, 0.25f, 0.13f);
        static readonly Color IceBedA = new(0.42f, 0.55f, 0.66f), IceBedB = new(0.3f, 0.42f, 0.55f);

        static void Bubbles(float amount, float spacing, float rate)
        {
            var m = Ctx.Fluid; if (m == null) return;
            m.SetFloat("_BubbleAmount", amount); m.SetFloat("_BubbleScale", spacing); m.SetFloat("_BubbleRate", rate);
        }

        static void Glow(float gain)
        {
            var m = Ctx.Fluid; if (m == null) return;
            m.SetColor("_Emission", m.GetColor("_Emission") * gain);
        }

        /// <summary>Lava basins: crust plates, boiling bubbles (amount, spacing).</summary>
        static void Lava(float plates, float bubbles, float spacing)
        {
            var m = Ctx.Fluid; if (m == null) return;
            m.SetFloat("_Plates", plates); m.SetFloat("_BubbleAmount", bubbles); m.SetFloat("_BubbleScale", spacing);
            m.SetFloat("_BubbleRate", 0.45f);
        }

        /// <summary>Volcanic ground: deep charcoal, no grey; thin cracks with lava glowing through.
        /// thin = crack thinning, glowBase = glow away from lava, flicker = glow flicker.</summary>
        static void Charcoal(float thin, float glowBase, float flicker, bool warm)
        {
            var m = Ctx.Ground; if (m == null) return;
            Color a = warm ? new Color(0.095f, 0.065f, 0.055f) : new Color(0.07f, 0.06f, 0.06f);
            Color b = warm ? new Color(0.075f, 0.05f, 0.045f) : new Color(0.055f, 0.05f, 0.05f);
            m.SetColor("_DryDebugColor", a); m.SetColor("_GrassDebugColor", b);
            m.SetColor("_SandDebugColor", a * 1.15f); m.SetColor("_RockDebugColor", b * 0.9f);
            foreach (var p in new[] { "_DryTint", "_GrassTint", "_SandTint", "_RockTint" }) m.SetColor(p, new Color(0.3f, 0.27f, 0.27f));
            m.SetFloat("_TextureStrength", 0.2f);
            m.SetFloat("_MacroStrength", 0.12f);
            m.SetFloat("_BankDarken", 0.15f);
            m.SetColor("_CrackColor", new Color(0.012f, 0.008f, 0.008f));
            m.SetFloat("_CrackStrength", 1f);
            m.EnableKeyword("_CRACKS_PROC");
            m.SetFloat("_CrackCell", warm ? 4.2f : 3.4f);
            m.SetFloat("_CrackWidth", Mathf.Lerp(0.06f, 0.02f, thin));
            m.SetFloat("_CrackFine", warm ? 0.8f : 0.55f);
            m.SetFloat("_CrackGlowBase", glowBase);
            m.SetFloat("_CrackGlowReach", 0.55f);
            m.SetFloat("_CrackGlowFlicker", flicker);
            m.SetColor("_CrackGlow", new Color(1f, 0.3f, 0.04f));
            m.SetColor("_CrackGlowCore", new Color(1.25f, 0.62f, 0.12f));
        }

        // ── colour grades: five per map ─────────────────────────────────────────────────

        static Color C(float r, float g, float b) => new(r, g, b, 1f);

        static IEnumerable<(string, string, Grade)> Grades(string theme)
        {
            switch (theme)
            {
                case "meadow":
                    yield return ("sunny", "Post 1 · Nắng trong", new Grade { exposure = 0.05f, contrast = 12, saturation = 15, temperature = 5, bloomThreshold = 0.85f, bloomIntensity = 0.35f, vignette = 0.2f });
                    yield return ("pastel", "Post 2 · Pastel mềm", new Grade { exposure = 0.15f, contrast = -8, saturation = -5, temperature = 3, shadows = C(1.05f, 1.02f, 1.1f), bloomThreshold = 0.9f, bloomIntensity = 0.2f, vignette = 0.12f });
                    yield return ("vivid", "Post 3 · Rực rỡ", new Grade { exposure = 0.05f, contrast = 20, saturation = 32, bloomThreshold = 0.8f, bloomIntensity = 0.45f, vignette = 0.25f });
                    yield return ("golden", "Post 4 · Chiều vàng", new Grade { contrast = 12, saturation = 12, temperature = 22, tint = -4, splitShadow = C(0.45f, 0.4f, 0.6f), splitHigh = C(0.65f, 0.55f, 0.4f), bloomThreshold = 0.8f, bloomIntensity = 0.5f, vignette = 0.28f });
                    yield return ("cool", "Post 5 · Sáng mát", new Grade { exposure = 0.1f, contrast = 10, saturation = 10, temperature = -12, bloomThreshold = 0.85f, bloomIntensity = 0.3f, vignette = 0.15f });
                    break;
                case "forest":
                    yield return ("deep", "Post 1 · Rừng xanh đậm", new Grade { contrast = 15, saturation = 15, temperature = -4, shadows = C(0.95f, 1.02f, 1.05f), bloomThreshold = 0.85f, bloomIntensity = 0.35f, vignette = 0.28f });
                    yield return ("sunbeam", "Post 2 · Nắng xuyên lá", new Grade { exposure = 0.08f, contrast = 12, saturation = 12, temperature = 14, highlights = C(1.06f, 1.02f, 0.92f), bloomThreshold = 0.78f, bloomIntensity = 0.55f, vignette = 0.25f });
                    yield return ("teal", "Post 3 · Sương xanh ngọc", new Grade { exposure = 0.1f, contrast = -4, temperature = -10, filter = C(0.94f, 1f, 1f), bloomThreshold = 0.9f, bloomIntensity = 0.25f, vignette = 0.2f });
                    yield return ("fairytale", "Post 4 · Cổ tích", new Grade { contrast = 18, saturation = 28, splitShadow = C(0.42f, 0.42f, 0.62f), splitHigh = C(0.62f, 0.58f, 0.42f), bloomThreshold = 0.8f, bloomIntensity = 0.45f, vignette = 0.3f });
                    yield return ("fresh", "Post 5 · Tươi sáng", new Grade { exposure = 0.15f, contrast = 8, saturation = 20, temperature = 4, bloomThreshold = 0.85f, bloomIntensity = 0.3f, vignette = 0.12f });
                    break;
                case "swamp":
                    yield return ("gloomy", "Post 1 · Đầm lầy âm u", new Grade { exposure = -0.05f, contrast = 18, saturation = 8, temperature = -8, tint = 8, shadows = C(0.92f, 1f, 1.02f), bloomThreshold = 0.8f, bloomIntensity = 0.45f, vignette = 0.35f });
                    yield return ("toxic", "Post 2 · Độc xanh nõn", new Grade { contrast = 14, saturation = 22, tint = 12, filter = C(0.96f, 1.04f, 0.92f), bloomThreshold = 0.78f, bloomIntensity = 0.6f, vignette = 0.3f });
                    yield return ("dusk", "Post 3 · Tím hoàng hôn", new Grade { contrast = 14, saturation = 14, splitShadow = C(0.45f, 0.38f, 0.62f), splitHigh = C(0.6f, 0.62f, 0.42f), bloomThreshold = 0.8f, bloomIntensity = 0.45f, vignette = 0.32f });
                    yield return ("clear", "Post 4 · Sáng dễ nhìn", new Grade { exposure = 0.12f, contrast = 8, saturation = 14, temperature = -2, bloomThreshold = 0.85f, bloomIntensity = 0.3f, vignette = 0.18f });
                    yield return ("moss", "Post 5 · Rêu ấm", new Grade { contrast = 12, saturation = 10, temperature = 12, tint = 6, bloomThreshold = 0.82f, bloomIntensity = 0.35f, vignette = 0.28f });
                    break;
                case "volcano":
                    yield return ("forge", "Post 1 · Lò rèn", new Grade { contrast = 18, saturation = 18, temperature = 12, bloomThreshold = 0.72f, bloomIntensity = 0.7f, vignette = 0.32f, vignetteColor = C(0.15f, 0.03f, 0.02f) });
                    yield return ("purple", "Post 2 · Đêm tím, lava cam", new Grade { exposure = -0.05f, contrast = 20, saturation = 16, splitShadow = C(0.42f, 0.38f, 0.64f), splitHigh = C(0.66f, 0.5f, 0.36f), bloomThreshold = 0.7f, bloomIntensity = 0.8f, vignette = 0.35f });
                    yield return ("blaze", "Post 3 · Rực cháy", new Grade { contrast = 22, saturation = 26, temperature = 6, bloomThreshold = 0.75f, bloomIntensity = 0.8f, vignette = 0.3f });
                    yield return ("readable", "Post 4 · Dễ nhìn", new Grade { exposure = 0.1f, contrast = 10, saturation = 10, bloomThreshold = 0.78f, bloomIntensity = 0.5f, vignette = 0.2f });
                    yield return ("ash", "Post 5 · Tro xám điện ảnh", new Grade { contrast = 22, saturation = -10, temperature = 8, bloomThreshold = 0.7f, bloomIntensity = 0.6f, vignette = 0.38f });
                    break;
                default:   // tundra
                    yield return ("cold", "Post 1 · Sáng lạnh", new Grade { exposure = 0.05f, contrast = 12, saturation = 12, temperature = -14, bloomThreshold = 0.88f, bloomIntensity = 0.3f, vignette = 0.18f });
                    yield return ("dawn", "Post 2 · Bình minh hồng", new Grade { contrast = 10, saturation = 14, temperature = 6, tint = 10, splitShadow = C(0.42f, 0.45f, 0.62f), splitHigh = C(0.65f, 0.52f, 0.5f), bloomThreshold = 0.82f, bloomIntensity = 0.45f, vignette = 0.22f });
                    yield return ("pastel", "Post 3 · Pastel băng", new Grade { exposure = 0.15f, contrast = -6, saturation = -4, temperature = -8, bloomThreshold = 0.9f, bloomIntensity = 0.2f, vignette = 0.1f });
                    yield return ("crisp", "Post 4 · Trong vắt", new Grade { contrast = 18, saturation = 20, temperature = -6, bloomThreshold = 0.85f, bloomIntensity = 0.35f, vignette = 0.2f });
                    yield return ("sunset", "Post 5 · Hoàng hôn cam", new Grade { contrast = 12, saturation = 12, temperature = 22, splitHigh = C(0.68f, 0.55f, 0.4f), bloomThreshold = 0.8f, bloomIntensity = 0.5f, vignette = 0.28f });
                    break;
            }
        }
    }
}
