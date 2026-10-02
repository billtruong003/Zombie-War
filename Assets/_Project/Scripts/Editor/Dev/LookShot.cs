using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Look comparison shots (2026-10-02): plays each map from Bootstrap, freezes the run, hides the HUD,
    /// then applies one look variant after another (post-processing, outline colour, ground and fluid
    /// material settings) and shoots two views of each: the hero view and the nearest bridge.
    /// Everything is applied at runtime to copies (volume profile clone, material copies); no asset
    /// changes. Output: Review/M8/look/&lt;theme&gt;/&lt;view&gt;_&lt;variant&gt;.png.
    /// </summary>
    [InitializeOnLoad]
    public static partial class LookShot
    {
        const string Key = "zw.lookshot.queue", Out = "Review/M8/look/";
        static int _step, _variant, _view;
        static float _at;

        static LookShot() => EditorApplication.update += Tick;

        /// <summary>Shoots every listed theme with its variant list.</summary>
        public static string Run(params string[] themes)
        {
            if (EditorApplication.isPlaying) return "already playing";
            SessionState.SetString(Key, string.Join(",", themes));
            SessionState.SetBool(LinesKey, false);
            SessionState.SetBool(ShadowsKey, false);
            Begin();
            return "running " + string.Join(",", themes);
        }

        static List<string> Queue() => new(SessionState.GetString(Key, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        static void Begin()
        {
            var q = Queue();
            if (q.Count == 0) return;
            PlayerPrefs.SetString("zw.map.theme", q[0]); PlayerPrefs.Save();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            _step = 0; _at = -1f;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            var q = Queue();
            if (q.Count == 0) return;
            if (!EditorApplication.isPlaying)
            {
                if (_step == 9 && !EditorApplication.isPlayingOrWillChangePlaymode) { _step = 0; Begin(); }
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (_at < 0f) _at = now;
            string theme = q[0];
            var variants = Variants(theme);
            switch (_step)
            {
                case 0:   // menu up: start the run
                    if (now - _at < 8f || !BillGameCore.Bill.IsReady) return;
                    GameFlow.StartGameplay(); _step = 1; _at = now; return;
                case 1:   // god mode, then let the run fill up
                    if (now - _at < 4f) return;
                    BillGameCore.Bill.Cheat?.Execute("zw.god"); _step = 2; _at = now; return;
                case 2:
                    KeepPlaying();
                    if (now - _at < 9f) return;
                    KeepPlaying();
                    Time.timeScale = 0f;
                    foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.isRootCanvas) c.enabled = false;
                    Ctx.Capture(theme);
                    Directory.CreateDirectory(Out + theme);
                    _variant = 0; _view = 0; _step = 3; _at = now; return;
                case 3:   // set view + variant
                    Ctx.View(_view);
                    Ctx.Reset();
                    variants[_variant].apply();
                    _step = 4; _at = now; return;
                case 4:   // let it render, then shoot
                    if (now - _at < 0.35f) return;
                    File.AppendAllText(Out + "lookshot_log.txt", $"{theme} {variants[_variant].name} frame={Time.frameCount} {Ctx.Describe()}\n");
                    ScreenCapture.CaptureScreenshot($"{Out}{theme}/{(_view == 0 ? "hero" : "bridge")}_{_variant:D2}_{variants[_variant].name}.png");
                    _step = 5; _at = now; return;
                case 5:
                    if (now - _at < 0.35f) return;
                    if (++_variant >= variants.Count) { _variant = 0; _view++; }
                    if (_view < 2) { _step = 3; return; }
                    File.WriteAllText($"{Out}{theme}/variants.txt", string.Join("\n", variants.ConvertAll(v => v.name + "\t" + v.label)));
                    Ctx.Reset();
                    q.RemoveAt(0); SessionState.SetString(Key, string.Join(",", q));
                    _step = 9; EditorApplication.ExitPlaymode();
                    if (q.Count == 0) Debug.Log("[LookShot] done");
                    return;
            }
        }

        static void KeepPlaying()
        {
            if (Time.timeScale != 0f) return;
            var o = Object.FindFirstObjectByType<RunOverlays>();
            var pick = typeof(RunOverlays).GetMethod("PickPerk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            for (int i = 0; i < 3 && o != null && pick != null && Time.timeScale == 0f; i++) pick.Invoke(o, new object[] { 0 });
        }

        // ── runtime state the variants act on ─────────────────────────────────────────────

        public sealed class Variant
        {
            public string name, label;
            public Action apply;
            public Variant(string name, string label, Action apply) { this.name = name; this.label = label; this.apply = apply; }
        }

        /// <summary>The frozen frame's camera, volume clone and material copies.</summary>
        public static class Ctx
        {
            public static Camera Cam;
            public static VolumeProfile Profile;
            public static Material Ground, Fluid;
            static Material _groundSrc, _fluidSrc;
            static Vector3 _heroCam, _bridgeCam;
            static readonly HashSet<VolumeComponent> _baseComponents = new();
            static object _outline; static Color _outlineColor;

            static float _mapLightOn = -1f, _planarOn;

            public static void Capture(string theme)
            {
                _mapLightOn = Shader.GetGlobalFloat("_ZWMapLightOn");
                _planarOn = Shader.GetGlobalFloat("_ZWPlanarShadowOn");
                Cam = Camera.main;
                // The gameplay volume is the one holding the outline: the menu's volume can still be
                // loaded next to it, and editing that one changed nothing on screen.
                Volume vol = null;
                foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v.sharedProfile != null && v.sharedProfile.components.Exists(c => c != null && c.GetType().Name == "OutlineVolume")) { vol = v; break; }
                if (vol == null) vol = Object.FindFirstObjectByType<Volume>();
                Profile = vol.profile;   // the runtime clone (OutlineProfileRuntimeGuard)
                _baseComponents.Clear();
                foreach (var c in Profile.components) _baseComponents.Add(c);
                _outline = Profile.components.Find(c => c != null && c.GetType().Name == "OutlineVolume");
                if (_outline != null) _outlineColor = ((ColorParameter)_outline.GetType().GetField("outlineColor").GetValue(_outline)).value;

                Ground = Fluid = _groundSrc = _fluidSrc = null;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    var m = r.sharedMaterial;
                    if (m == null) continue;
                    if (m.name.StartsWith("M_MapGround_") || m == Ground)
                    {
                        if (Ground == null) { _groundSrc = m; Ground = new Material(m) { name = m.name + " (look)" }; }
                        r.sharedMaterial = Ground;
                    }
                    else if (m.name.StartsWith("M_Fluid_") || m == Fluid)
                    {
                        if (Fluid == null) { _fluidSrc = m; Fluid = new Material(m) { name = m.name + " (look)" }; }
                        r.sharedMaterial = Fluid;
                    }
                }
                foreach (var b in Cam.GetComponents<MonoBehaviour>()) if (b.GetType().Name.Contains("Follow")) b.enabled = false;
                _heroCam = Cam.transform.position;
                var hero = GameObject.FindWithTag("Player").transform.position;
                Transform best = null; float bd = float.MaxValue;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name == "Bridge") { float d = Vector3.Distance(t.position, hero); if (d < bd) { bd = d; best = t; } }
                var off = _heroCam - hero;
                Vector3 spot = best != null && bd < 60f ? best.position : NearestFluidPoint(hero);
                _bridgeCam = new Vector3(spot.x, 0f, spot.z) + off;
            }

            // Some maps (tundra's frozen lakes) have no bridge near the start: the closest point of a
            // fluid surface instead.
            static Vector3 NearestFluidPoint(Vector3 hero)
            {
                Vector3 best = hero; float bd = float.MaxValue;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r.sharedMaterial != Fluid) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) { var c = r.bounds.center; float dc = Vector3.Distance(c, hero); if (dc < bd) { bd = dc; best = c; } continue; }
                    var tr = r.transform;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var w = tr.TransformPoint(v);
                        float d = Vector3.Distance(w, hero);
                        if (d < bd) { bd = d; best = w; }
                    }
                }
                return best;
            }

            public static void View(int v) => Cam.transform.position = v == 0 ? _heroCam : _bridgeCam;

            /// <summary>Back to the game's own look before each variant.</summary>
            public static void Reset()
            {
                if (Profile != null)
                {
                    foreach (var c in Profile.components.ToArray())
                        if (!_baseComponents.Contains(c)) { Profile.components.Remove(c); Object.DestroyImmediate(c); }
                    Profile.isDirty = true;
                }
                ClearOutlineOverride();
                if (_mapLightOn >= 0f) { Shader.SetGlobalFloat("_ZWMapLightOn", _mapLightOn); Shader.SetGlobalFloat("_ZWPlanarShadowOn", _planarOn); }
                if (Cam != null)
                {
                    var acd = Cam.GetComponent<UniversalAdditionalCameraData>();
                    acd.renderPostProcessing = false;
                    acd.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
                }
                if (Ground != null) { Ground.CopyPropertiesFromMaterial(_groundSrc); Ground.shaderKeywords = _groundSrc.shaderKeywords; }
                if (Fluid != null) { Fluid.CopyPropertiesFromMaterial(_fluidSrc); Fluid.shaderKeywords = _fluidSrc.shaderKeywords; }
            }

            public static string Describe() => $"line={OutlineLook.Colour} tint={OutlineLook.Tint}";

            // The line colour goes straight to the outline's runtime override: edits on the runtime
            // volume profile did not reach the frozen frame (02/10, all shots came out black).
            public static void OutlineTint(float amount, float darken) => OutlineLook.Tint = new Vector2(amount, darken);

            public static void OutlineColor(Color c) => OutlineLook.Colour = c;

            public static void ClearOutlineOverride() => OutlineLook.Clear();

            public static T Add<T>() where T : VolumeComponent
            {
                if (!Profile.TryGet(out T c)) c = Profile.Add<T>();
                c.active = true;
                return c;
            }

            public static void Post(bool on) => Cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = on;
            public static void OpaqueTexture(bool on) => Cam.GetComponent<UniversalAdditionalCameraData>().requiresColorOption = on ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
        }

        // ── post preset helper ─────────────────────────────────────────────────────────────

        /// <summary>One colour grade. Values follow the URP volume components (contrast/saturation −100…100,
        /// temperature/tint −100…100, shadows/highlights as colour multipliers).</summary>
        public struct Grade
        {
            public float exposure, contrast, saturation, temperature, tint, hue;
            public Color filter, shadows, midtones, highlights, splitShadow, splitHigh;
            public float bloomThreshold, bloomIntensity, vignette;
            public Color vignetteColor;
        }

        public static void ApplyGrade(Grade g)
        {
            Ctx.Post(true);
            var ca = Ctx.Add<ColorAdjustments>();
            ca.postExposure.Override(g.exposure); ca.contrast.Override(g.contrast); ca.saturation.Override(g.saturation);
            ca.hueShift.Override(g.hue); ca.colorFilter.Override(g.filter == default ? Color.white : g.filter);
            var wb = Ctx.Add<WhiteBalance>();
            wb.temperature.Override(g.temperature); wb.tint.Override(g.tint);
            var smh = Ctx.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(ToSmh(g.shadows)); smh.midtones.Override(ToSmh(g.midtones)); smh.highlights.Override(ToSmh(g.highlights));
            if (g.splitShadow != default || g.splitHigh != default)
            {
                var st = Ctx.Add<SplitToning>();
                st.shadows.Override(g.splitShadow == default ? Color.grey : g.splitShadow);
                st.highlights.Override(g.splitHigh == default ? Color.grey : g.splitHigh);
            }
            if (g.bloomIntensity > 0f)
            {
                var bl = Ctx.Add<Bloom>();
                bl.threshold.Override(g.bloomThreshold); bl.intensity.Override(g.bloomIntensity); bl.scatter.Override(0.6f);
            }
            if (g.vignette > 0f)
            {
                var vg = Ctx.Add<Vignette>();
                vg.intensity.Override(g.vignette); vg.smoothness.Override(0.45f);
                vg.color.Override(g.vignetteColor == default ? new Color(0.1f, 0.08f, 0.2f) : g.vignetteColor);
            }
        }

        // SMH takes (r, g, b, offset); a colour of (1,1,1) means untouched.
        static Vector4 ToSmh(Color c) => c == default ? new Vector4(1f, 1f, 1f, 0f) : new Vector4(c.r, c.g, c.b, 0f);
    }
}
