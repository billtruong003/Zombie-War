using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar.Skins;

namespace ZombieWar.Editor.Skins
{
    /// <summary>
    /// Renders the season 1 skin sets for owner review, without Play Mode: every set on three guns
    /// (pistol, rifle, shotgun) from three angles, one sheet per set, into Review/M8/skins_s1.
    /// </summary>
    public static class WeaponSkinPreview
    {
        const string OutDir = "Review/M8/skins_s1";
        const int Cell = 640;
        static readonly string[] Guns = { "WD_Sidearm_DesertEagle", "WD_AssaultRifle_AK47", "WD_Shotgun_SPAS12" };
        static readonly Vector3[] Angles = { new(0f, 0f, 0f), new(-14f, 38f, 0f), new(10f, -142f, 0f) };

        [MenuItem("ZombieWar/Dev/Render Skin Sets (review)")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(OutDir);
            var data = Guns.Select(g => AssetDatabase.FindAssets($"{g} t:WeaponData").Select(AssetDatabase.GUIDToAssetPath)
                                        .Select(AssetDatabase.LoadAssetAtPath<WeaponData>).FirstOrDefault(d => d != null && d.name == g))
                           .Where(d => d != null && d.weaponPrefab != null).ToList();
            int n = 0;
            foreach (var set in WeaponSkins.Season1)
            {
                var sheet = new Texture2D(Cell * Angles.Length, Cell * data.Count, TextureFormat.RGBA32, false);
                for (int g = 0; g < data.Count; g++)
                    for (int a = 0; a < Angles.Length; a++)
                    {
                        var shot = Render(data[g], set, Angles[a]);
                        if (shot == null) continue;
                        sheet.SetPixels(a * Cell, (data.Count - 1 - g) * Cell, Cell, Cell, shot.GetPixels());
                        Object.DestroyImmediate(shot);
                    }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(OutDir, $"{set.id}.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                n++;
            }
            Debug.Log($"[Skins] {n} sheets in {OutDir} ({data.Count} guns x {Angles.Length} angles)");
        }

        static Texture2D Render(WeaponData wd, WeaponSkins.Set set, Vector3 euler)
        {
            var ambient = RenderSettings.ambientLight;
            var ambientMode = RenderSettings.ambientMode;
            GameObject inst = null, cam = null, key = null, rim = null;
            RenderTexture rt = null;
            try
            {
                var at = new Vector3(6000f, 6000f, 6000f);
                inst = Object.Instantiate(wd.weaponPrefab, at, Quaternion.identity);
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                inst.AddComponent<WeaponSkinApplier>().Apply(set);
                inst.transform.rotation = Quaternion.Euler(euler);

                var rs = inst.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) return null;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                inst.GetComponent<WeaponSkinApplier>().Apply(set);   // re-push the root matrix after the rotation

                key = Light(new Vector3(30f, 150f, 0f), 1.5f, new Color(1f, 0.96f, 0.9f));
                rim = Light(new Vector3(20f, -40f, 0f), 0.7f, new Color(0.75f, 0.85f, 1f));
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.38f);

                cam = new GameObject("SkinCam");
                var c = cam.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.1f, 0.11f, 0.14f, 1f);
                c.fieldOfView = 26f; c.nearClipPlane = 0.001f; c.farClipPlane = 100f;
                rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                c.targetTexture = rt;
                float ext = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * 1.25f;
                c.transform.position = b.center + Vector3.right * (ext * 0.5f / Mathf.Tan(13f * Mathf.Deg2Rad));
                c.transform.LookAt(b.center);
                c.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                c.targetTexture = null;
                return tex;
            }
            finally
            {
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (cam != null) Object.DestroyImmediate(cam);
                if (key != null) Object.DestroyImmediate(key);
                if (rim != null) Object.DestroyImmediate(rim);
                if (inst != null) Object.DestroyImmediate(inst);
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientMode = ambientMode;
            }
        }

        static GameObject Light(Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("SkinLight");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }
    }
}
