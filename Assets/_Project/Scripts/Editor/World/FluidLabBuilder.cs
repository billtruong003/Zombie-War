using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Fluid lab (owner 02/10): one scene to tune the fluid materials in the editor. For every map it
    /// places the baked chunk with the most fluid in it (real banks, rocks and bridges, so the
    /// intersection foam and the bank glow show), in a row 40 m apart, each with a label and a game
    /// camera. The chunks use the maps' own materials (M_Fluid_&lt;map&gt;, M_MapGround_&lt;map&gt;):
    /// what is tuned here is what the maps get. FluidLabTicker keeps the shaders moving in Edit mode.
    /// To keep a tuning through a rebake, copy the numbers into MapLooks (Editor/World/MapLooks.cs).
    /// Scene: Scenes/Dev/FluidLab.unity.
    /// </summary>
    public static class FluidLabBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/FluidLab.unity";
        const float Spacing = 40f;
        static readonly string[] Maps = { "meadow", "forest", "swamp", "volcano", "tundra", "desert" };

        [MenuItem("HordeCall/World/Build Fluid Lab")]
        public static string Build()
        {
            var prev = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var log = new System.Text.StringBuilder();
            try
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.82f, 0.86f, 0.94f);
                RenderSettings.ambientEquatorColor = new Color(0.68f, 0.70f, 0.66f);
                RenderSettings.ambientGroundColor = new Color(0.48f, 0.44f, 0.38f);
                RenderSettings.fog = false;

                var rig = new GameObject("ToonLightRig");
                rig.transform.rotation = Quaternion.Euler(50f, 60f, 0f);   // the maps' sun
                rig.AddComponent<ToonLightRig>();

                new GameObject("FluidLabTicker").AddComponent<ZombieWar.Dev.FluidLabTicker>();

                // The game's volume (outline + toon bloom), so HDR lava glows as it does in play.
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
                if (profile != null)
                {
                    var vol = new GameObject("Global Volume").AddComponent<Volume>();
                    vol.isGlobal = true; vol.sharedProfile = profile;
                }

                Camera first = null;
                for (int i = 0; i < Maps.Length; i++)
                {
                    var theme = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>(ZombieWar.World.MapTheme.AssetFolder + "MapTheme_" + Maps[i] + ".asset");
                    if (theme == null || theme.chunks == null) continue;
                    var chunk = MostFluid(theme, out float area);
                    if (chunk == null) { log.Append(Maps[i]).Append(": no fluid; "); continue; }
                    var at = new Vector3(i * Spacing, 0f, 0f);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(chunk, scene);
                    go.name = "Lab_" + Maps[i];
                    go.transform.position = at;

                    var label = new GameObject("Label_" + Maps[i]).AddComponent<TextMesh>();
                    label.text = Maps[i].ToUpperInvariant();
                    label.fontSize = 64; label.characterSize = 0.25f; label.anchor = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.font = font;
                    label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    label.transform.SetPositionAndRotation(at + new Vector3(0f, 3f, 17f), Quaternion.Euler(60f, 0f, 0f));

                    // The game camera looks down at 60° from 12 m up and 8 m back; aimed at the fluid.
                    var focus = FluidCentre(go, at);
                    var cam = new GameObject("Cam_" + Maps[i]).AddComponent<Camera>();
                    cam.transform.SetPositionAndRotation(focus + new Vector3(0f, 12f, -8f), Quaternion.Euler(60f, 0f, 0f));
                    cam.fieldOfView = 60f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.2f, 0.25f, 0.32f);
                    var data = cam.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    data.requiresDepthOption = CameraOverrideOption.On;
                    if (first == null) { first = cam; cam.tag = "MainCamera"; }
                    else cam.enabled = false;
                    log.Append(Maps[i]).Append(": ").Append(chunk.name).Append(" (").Append(area.ToString("0")).Append(" m² of fluid); ");
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (prev.IsValid()) SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
            return log.ToString();
        }

        // The chunk with the most basin floor: ground vertices the baker marked as sunk (UV.x carries
        // the basin depth). The fluid surface itself spans whole chunks, so its size says nothing.
        static GameObject MostFluid(ZombieWar.World.MapTheme theme, out float best)
        {
            GameObject pick = null; best = 0f;
            foreach (var c in theme.chunks)
            {
                if (c == null) continue;
                float n = BasinPoints(c, out _);
                if (n > best) { best = n; pick = c; }
            }
            best *= 0.25f;   // basin vertices are 0.5 m apart: about a quarter m² each
            return pick;
        }

        static int BasinPoints(GameObject chunk, out Vector3 centre)
        {
            int n = 0; centre = Vector3.zero;
            var uv = new System.Collections.Generic.List<Vector2>();
            foreach (var r in chunk.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("M_MapGround_")) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh; mesh.GetUVs(0, uv);
                var v = mesh.vertices;
                for (int i = 0; i < uv.Count; i++)
                    if (uv[i].x > 0.15f) { n++; centre += r.transform.TransformPoint(v[i]); }
            }
            if (n > 0) centre /= n;
            return n;
        }

        static Vector3 FluidCentre(GameObject chunk, Vector3 fallback)
        {
            return BasinPoints(chunk, out var c) > 0 ? new Vector3(c.x, 0f, c.z) : fallback;
        }
    }
}
