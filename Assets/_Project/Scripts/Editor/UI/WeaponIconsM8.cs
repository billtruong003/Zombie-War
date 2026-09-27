using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// M8 weapon icons (owner pick, 2026-09-26): two icons per gun, both drawn on a solid rarity tile
    /// by the UI.
    /// - Owned ("A"): the gun's own materials under a bright three-light rig, dark toon outline.
    /// - Locked ("B"): a flat unlit silhouette in a pale rarity tint, same outline, so a gun you do
    ///   not have reads as "not yet" at a glance.
    /// Every gun is cropped to its own bounds and fitted to the icon, so a pistol fills its tile as
    /// much as a rifle does. The owned icon keeps the old file path (sprite GUID and references
    /// survive); the locked icon sits beside it with a "_locked" suffix.
    /// </summary>
    public static class WeaponIconsM8
    {
        const string WeaponsDir = "Assets/_Project/UI/Icons/Generated/Weapons";
        const int Capture = 1024;
        const int IconSize = 512;
        const int Outline = 9;
        static readonly Color32 OutlineColor = new Color32(0x1F, 0x23, 0x30, 0xFF);

        [MenuItem("ZombieWar/UI/Authoring/Generate Weapon Icons (M8 owned + locked)")]
        public static void GenerateAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Icons M8] Not in Play Mode."); return; }
            Directory.CreateDirectory(WeaponsDir);
            var catalog = UIThumbnailGenerator.EnsureCatalogAsset();
            var weapons = UIThumbnailGenerator.LoadAllWeaponData();
            int ok = 0, fail = 0;
            try
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    var wd = weapons[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Weapon icons (M8)", $"{i + 1}/{weapons.Count}: {wd.weaponName}", i / (float)weapons.Count))
                        break;
                    if (Generate(catalog, wd)) ok++; else fail++;
                }
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[Icons M8] {ok} guns ok, {fail} failed.");
        }

        public static bool Generate(UIPrototypeCatalog catalog, WeaponData wd)
        {
            if (wd == null || wd.weaponPrefab == null) return false;
            string baseName = $"{WeaponsDir}/{UIThumbnailGenerator.StableName("W", wd)}";
            var owned = Render(wd, false);
            var locked = Render(wd, true);
            if (owned == null || locked == null) { Debug.LogWarning($"[Icons M8] '{wd.name}' render failed.", wd); return false; }
            try
            {
                var pale = Color.Lerp(wd.TierColor, Color.white, 0.72f);
                float width = WidthShare(wd.weaponClass);
                WriteIcon(owned, baseName + ".png", null, width);
                WriteIcon(locked, baseName + "_locked.png", pale, width);
            }
            finally { Object.DestroyImmediate(owned); Object.DestroyImmediate(locked); }

            var ownedSprite = UIThumbnailGenerator.ImportIcon(baseName + ".png", IconSize);
            var lockedSprite = UIThumbnailGenerator.ImportIcon(baseName + "_locked.png", IconSize);
            var entry = catalog.weapons.FirstOrDefault(e => e.data == wd);
            if (entry == null)
            {
                entry = new UIPrototypeCatalog.WeaponEntry { data = wd, owned = wd.unlockCost <= 0 };
                catalog.weapons.Add(entry);
            }
            entry.icon = ownedSprite;
            entry.lockedIcon = lockedSprite;
            return true;
        }

        /// Renders the gun in profile (muzzle to the right) on a transparent ground.
        static Texture2D Render(WeaponData wd, bool silhouette)
        {
            var ambient = RenderSettings.ambientLight;
            var ambientMode = RenderSettings.ambientMode;
            GameObject inst = null, cam = null;
            var lights = new System.Collections.Generic.List<GameObject>();
            RenderTexture rt = null;
            Material flat = null;
            try
            {
                inst = Object.Instantiate(wd.weaponPrefab, new Vector3(5000f, 5000f, 5000f), Quaternion.identity);
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                var rs = inst.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) return null;
                if (silhouette)
                {
                    flat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
                    flat.SetColor("_BaseColor", Color.white);
                    foreach (var r in rs) r.sharedMaterials = Enumerable.Repeat(flat, r.sharedMaterials.Length).ToArray();
                }
                else
                {
                    lights.Add(Light(new Vector3(35f, 140f, 0f), 1.7f));
                    lights.Add(Light(new Vector3(10f, -30f, 0f), 0.9f));
                    lights.Add(Light(new Vector3(15f, 90f, 0f), 1.3f));
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
                }
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);

                cam = new GameObject("IconCam");
                var c = cam.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0, 0, 0, 0);
                c.fieldOfView = 30f; c.nearClipPlane = 0.001f; c.farClipPlane = 100f;
                rt = new RenderTexture(Capture, Capture, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                c.targetTexture = rt;
                float ext = Mathf.Max(b.size.z, b.size.y) * 1.15f;
                c.transform.position = b.center + Vector3.right * (ext * 0.5f / Mathf.Tan(15f * Mathf.Deg2Rad));
                c.transform.LookAt(b.center);
                c.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Capture, Capture, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Capture, Capture), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                c.targetTexture = null;
                return tex;
            }
            finally
            {
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (cam != null) Object.DestroyImmediate(cam);
                foreach (var l in lights) Object.DestroyImmediate(l);
                if (inst != null) Object.DestroyImmediate(inst);
                if (flat != null) Object.DestroyImmediate(flat);
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientMode = ambientMode;
            }
        }

        static GameObject Light(Vector3 euler, float intensity)
        {
            var go = new GameObject("IconLight");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        /// Crops to the gun, fits it into the icon (2x2 supersampled), optionally recolours it to one
        /// flat tint, and writes the PNG; the outline is drawn after.
        /// <summary>
        /// M8-A (owner: icons should scale sensibly): each gun is cropped to itself, then drawn at a
        /// share of the icon width by class, so a pistol reads smaller than a rifle but never tiny.
        /// </summary>
        public static float WidthShare(WeaponClass c) => c switch
        {
            WeaponClass.Sidearm => 0.8f,
            WeaponClass.SMG => 0.9f,
            WeaponClass.LMG or WeaponClass.Marksman or WeaponClass.Railgun or WeaponClass.Rocket => 1f,
            _ => 0.96f,
        };

        /// Padding around the drawn area (share of the icon), leaving room for the outline.
        const float Padding = 0.08f;

        static void WriteIcon(Texture2D src, string path, Color? tint, float widthShare)
        {
            var px = src.GetPixels32();
            int w = src.width, h = src.height, minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { minX = minY = 0; maxX = w - 1; maxY = h - 1; }
            float cw = maxX - minX + 1, ch = maxY - minY + 1;
            float inner = IconSize * (1f - 2f * Padding);
            float scale = Mathf.Min(inner * widthShare / cw, inner / ch);
            float dw = cw * scale, dh = ch * scale;
            float ox = (IconSize - dw) * 0.5f, oy = (IconSize - dh) * 0.5f;

            var outPx = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float fx = (x + 0.25f + sx * 0.5f - ox) / scale + minX;
                            float fy = (y + 0.25f + sy * 0.5f - oy) / scale + minY;
                            if (fx < minX - 1 || fy < minY - 1 || fx > maxX + 1 || fy > maxY + 1) continue;
                            var c = src.GetPixelBilinear(fx / w, fy / h);
                            acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);   // premultiplied average
                        }
                    acc *= 0.25f;
                    if (acc.a > 0.0001f) { acc.r /= acc.a; acc.g /= acc.a; acc.b /= acc.a; }
                    if (tint.HasValue && acc.a > 0f) { var t = tint.Value; acc = new Color(t.r, t.g, t.b, acc.a); }
                    outPx[y * IconSize + x] = acc;
                }
            var dst = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            dst.SetPixels(outPx);
            dst.Apply();
            File.WriteAllBytes(path, dst.EncodeToPNG());
            Object.DestroyImmediate(dst);
            UIThumbnailGenerator.OutlinePng(path, Outline, OutlineColor);
        }
    }
}
