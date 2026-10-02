using System.Collections.Generic;
using UnityEditor;
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
            if (SessionState.GetBool(PostKey, false))
            {
                // The map's own grade now (its post volume) against each of its five grades.
                var posts = new List<Variant> { new("post_now", "Post đang áp", () => Ctx.Post(true)) };
                int k = 1;
                foreach (var (name, label, grade) in Grades(theme))
                {
                    var g = grade;
                    posts.Add(new Variant($"post{k++}_{name}", label, () => Ctx.MapPost(g)));
                }
                return posts;
            }
            if (SessionState.GetBool(AppliedKey, false))
            {
                // The looks written into the map materials (MapLooks), plus the second lava type.
                var applied = new List<Variant> { new("applied", "Đã áp vào material map", () => { }) };
                if (theme == "volcano")
                {
                    applied.Add(new Variant("applied_crust", "Lava kiểu 2: vỏ nguội, khe sáng", () => { if (Ctx.Fluid != null) MapLooks.LavaCrust(Ctx.Fluid); }));
                    applied.Add(new Variant("applied_cracked", "Đất than nứt sáng (bản cũ)", () => { if (Ctx.Ground != null) MapLooks.Charcoal(Ctx.Ground); }));
                }
                return applied;
            }
            var list = new List<Variant> { new("now", "Hiện tại", () => { }) };
            if (SessionState.GetBool(ShadowsKey, false))
            {
                // Baked shadows + AO + planar hero shadow on ("now") against all of them off.
                list.Add(new Variant("noshadow", "Không bóng, không AO", () =>
                {
                    Shader.SetGlobalFloat("_ZWMapLightOn", 0f);
                    Shader.SetGlobalFloat("_ZWPlanarShadowOn", 0f);
                }));
                return list;
            }
            if (SessionState.GetBool(LinesKey, false))
            {
                // Outline-only run (owner 02/10: the first six colours all read as black).
                for (int i = 0; i < OutlineLook.Presets.Length; i++)
                {
                    int k = i;
                    var p = OutlineLook.Presets[i];
                    list.Add(new Variant("line_" + p.code, p.label, () => OutlineLook.Apply(k)));
                }
                return list;
            }
            var surfaces = Surfaces(theme);
            list.AddRange(surfaces);
            var pick = surfaces.Count > 1 ? surfaces[1].apply : () => { };
            int n = 1;
            foreach (var (name, label, grade) in Grades(theme))
            {
                var g = grade;
                list.Add(new Variant($"post{n++}_{name}", label, () => { pick(); ApplyGrade(g); }));
            }
            return list;
        }

        const string AppliedKey = "zw.lookshot.applied";
        const string PostKey = "zw.lookshot.post";

        /// <summary>Shoots each map's grade now and its five grade options.</summary>
        public static string RunPost(params string[] themes)
        {
            string r = Run(themes);
            SessionState.SetBool(PostKey, true);
            return r;
        }

        /// <summary>The grade a map's post profile is written with: the owner's pick, or the first
        /// option until one is picked. Null: the map has no grade yet (volcano: owner, later).</summary>
        public static Grade? PickedGrade(string theme)
        {
            string pick = theme switch
            {
                "forest" => "deep",          // owner pick P1 (02/10, replaces P3 teal)
                "swamp" => "dusk",           // owner pick P3 (02/10, replaces P1 gloomy)
                "meadow" => "sky_pastel",    // owner pick P3, 02/10
                "desert" => "warm_sun",      // owner pick P1 (02/10, replaces P2 dusty haze)
                "tundra" => "ice_blue",      // not picked yet (round 2 grades)
                _ => null,
            };
            if (pick == null) return null;
            foreach (var (name, _, grade) in Grades(theme)) if (name == pick) return grade;
            return null;
        }

        /// <summary>Shoots the looks now written into the map materials.</summary>
        public static string RunApplied(params string[] themes)
        {
            string r = Run(themes);
            SessionState.SetBool(AppliedKey, true);
            return r;
        }

        const string LinesKey = "zw.lookshot.lines";
        const string ShadowsKey = "zw.lookshot.shadows";

        /// <summary>Shoots each theme with and without the baked shadows and AO.</summary>
        public static string RunShadows(params string[] themes)
        {
            string r = Run(themes);
            SessionState.SetBool(ShadowsKey, true);
            return r;
        }

        /// <summary>Shoots only the outline looks, on the listed themes.</summary>
        public static string RunLines(params string[] themes)
        {
            string r = Run(themes);
            SessionState.SetBool(LinesKey, true);
            return r;
        }


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

        /// <summary>A map's five grades (name, label, values).</summary>
        public static IEnumerable<(string, string, Grade)> GradeOptions(string theme) => Grades(theme);

        static IEnumerable<(string, string, Grade)> Grades(string theme)
        {
            switch (theme)
            {
                case "meadow":
                    // Round 2 (owner 02/10): the cool, bright, pastel family.
                    yield return ("cool_clear", "Post 1 · Mát trong", new Grade { exposure = 0.1f, contrast = 8, saturation = 12, temperature = -14, tint = -2, vignette = 0.12f });
                    yield return ("pastel_mint", "Post 2 · Pastel bạc hà", new Grade { exposure = 0.18f, contrast = -10, saturation = -6, temperature = -8, shadows = C(1.02f, 1.06f, 1.1f), vignette = 0.1f });
                    yield return ("sky_pastel", "Post 3 · Pastel trời xanh", new Grade { exposure = 0.15f, contrast = -4, saturation = 4, temperature = -10, filter = C(0.97f, 1f, 1.03f), splitShadow = C(0.45f, 0.5f, 0.62f), splitHigh = C(0.6f, 0.6f, 0.55f), vignette = 0.1f });
                    yield return ("fresh_cool", "Post 4 · Tươi mát đậm", new Grade { exposure = 0.08f, contrast = 14, saturation = 18, temperature = -10, tint = -4, vignette = 0.15f });
                    yield return ("soft_morning", "Post 5 · Sáng sớm dịu", new Grade { exposure = 0.2f, contrast = -6, saturation = 6, temperature = -4, highlights = C(1.02f, 1.02f, 1.06f), vignette = 0.08f });
                    break;
                case "desert":
                    yield return ("warm_sun", "Post 1 · Nắng ấm", new Grade { exposure = 0.02f, contrast = 12, saturation = 10, temperature = 10, vignette = 0.18f });
                    yield return ("dusty_haze", "Post 2 · Bụi mờ", new Grade { exposure = 0.08f, contrast = -6, saturation = -8, temperature = 14, filter = C(1.02f, 0.99f, 0.95f), vignette = 0.2f });
                    yield return ("golden_hour", "Post 3 · Giờ vàng", new Grade { contrast = 14, saturation = 14, temperature = 22, splitShadow = C(0.44f, 0.42f, 0.6f), splitHigh = C(0.66f, 0.56f, 0.4f), vignette = 0.25f });
                    yield return ("clear_noon", "Post 4 · Trưa trong", new Grade { exposure = 0.05f, contrast = 16, saturation = 16, temperature = -2, vignette = 0.12f });
                    yield return ("pastel_sand", "Post 5 · Cát pastel", new Grade { exposure = 0.15f, contrast = -8, saturation = -4, temperature = 6, shadows = C(1.04f, 1.02f, 1.08f), vignette = 0.08f });
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
                    // Round 2 (owner 02/10): five new grades for the charcoal ground and MinionsArt lava.
                    yield return ("ember_glow", "Post 1 · Than hồng", new Grade { contrast = 16, saturation = 14, temperature = 10, shadows = C(1.02f, 0.96f, 1.04f), vignette = 0.28f, vignetteColor = C(0.18f, 0.04f, 0.02f) });
                    yield return ("ash_haze", "Post 2 · Tro bụi mờ", new Grade { exposure = 0.05f, contrast = -4, saturation = -18, temperature = 6, filter = C(1.02f, 0.98f, 0.96f), vignette = 0.2f });
                    yield return ("magma_night", "Post 3 · Đêm dung nham", new Grade { exposure = -0.1f, contrast = 22, saturation = 20, splitShadow = C(0.36f, 0.34f, 0.58f), splitHigh = C(0.7f, 0.5f, 0.32f), vignette = 0.35f });
                    yield return ("crimson", "Post 4 · Đỏ thẫm", new Grade { contrast = 18, saturation = 24, temperature = 4, tint = 6, vignette = 0.3f, vignetteColor = C(0.2f, 0.02f, 0.02f) });
                    yield return ("cool_contrast", "Post 5 · Đá lạnh, lava nóng", new Grade { contrast = 20, saturation = 8, temperature = -8, vignette = 0.25f });
                    break;
                default:   // tundra
                    // Round 2 (owner 02/10): five new grades.
                    yield return ("ice_blue", "Post 1 · Xanh băng", new Grade { exposure = 0.05f, contrast = 14, saturation = 18, temperature = -18, tint = -4, splitShadow = C(0.4f, 0.48f, 0.66f), vignette = 0.15f });
                    yield return ("aurora", "Post 2 · Cực quang", new Grade { contrast = 12, saturation = 22, temperature = -10, tint = 10, splitShadow = C(0.42f, 0.4f, 0.66f), splitHigh = C(0.5f, 0.66f, 0.62f), vignette = 0.2f });
                    yield return ("silver_mist", "Post 3 · Sương bạc", new Grade { exposure = 0.12f, contrast = -6, saturation = -12, temperature = -6, filter = C(0.98f, 1f, 1.03f), vignette = 0.1f });
                    yield return ("golden_snow", "Post 4 · Tuyết nắng vàng", new Grade { contrast = 12, saturation = 12, temperature = 14, splitHigh = C(0.66f, 0.56f, 0.42f), splitShadow = C(0.42f, 0.46f, 0.64f), vignette = 0.18f });
                    yield return ("night_frost", "Post 5 · Đêm giá lạnh", new Grade { exposure = -0.15f, contrast = 18, saturation = 10, temperature = -22, vignette = 0.32f, vignetteColor = C(0.05f, 0.08f, 0.2f) });
                    break;
            }
        }
    }
}
