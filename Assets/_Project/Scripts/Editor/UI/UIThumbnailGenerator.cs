using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Editor-only: sinh thumbnail PNG cho weapon prefab + costume part (skinned mesh bind pose)
    /// bằng PreviewRenderUtility, rồi populate UIPrototypeCatalog (icon + weapon entries).
    /// - Filename ổn định theo asset GUID → re-run ghi đè cùng path, giữ GUID sprite.
    /// - Costume catalog rất lớn → chỉ generate PagesToCover trang đầu mỗi tab (log rõ),
    ///   phần còn lại dùng fallback icon semantic (Layer Lab).
    /// - Fail 1 item không abort batch; cleanup bằng finally; có cancel.
    /// </summary>
    public static class UIThumbnailGenerator
    {
        const string WeaponsDir = "Assets/_Project/UI/Icons/Generated/Weapons";
        public const string CostumeDir = "Assets/_Project/UI/Icons/Generated/Costume";
        public const string CatalogAssetPath = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";
        const string CostumeCatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";
        const int Size = 256;              // legacy costume-part path (PreviewRenderUtility)

        // Icon súng: chụp profile ngang (nòng hướng phải), scene camera alpha-0 → nền trong suốt,
        // capture 2048 MSAA rồi downscale bilinear về 512 (pipeline chung với CasualIconGenerator).
        const int WeaponCaptureSize = 2048;
        const int WeaponIconSize = 512;

        [MenuItem("ZombieWar/UI/Authoring/Generate Item Thumbnails")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Thumbs] Không chạy khi Play Mode.");
                return;
            }

            Directory.CreateDirectory(WeaponsDir);
            Directory.CreateDirectory(CostumeDir);

            var catalog = EnsureCatalogAsset();
            int okW = 0, failW = 0, okC = 0, failC = 0;

            try
            {
                var weapons = LoadAllWeaponData();
                // Frame CHUNG một scale cho cả roster (theo khẩu dài nhất): súng lục nhỏ trong khung
                // đúng tỉ lệ thật so với súng trường — không auto-zoom từng khẩu làm lệch proportion.
                float rosterExtent = 0f;
                foreach (var wd in weapons) rosterExtent = Mathf.Max(rosterExtent, MeasureExtent(wd));

                for (int i = 0; i < weapons.Count; i++)
                {
                    var wd = weapons[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Generate Thumbnails",
                            $"Weapon {i + 1}/{weapons.Count}: {wd.weaponName}", i / (float)(weapons.Count + 1)))
                        { Debug.LogWarning("[Thumbs] Cancelled bởi user."); break; }

                    var sprite = RenderWeapon(wd, rosterExtent);
                    if (sprite != null) okW++; else failW++;
                    UpsertWeaponEntry(catalog, wd, sprite);
                }

                catalog.weaponFallbackIcon = UIKit.Icon("Icon_Sword02");
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[Thumbs] Weapons DONE — {okW} ok / {failW} fallback.");
            // KHÔNG chain GenerateCostumeIcons ở đây: path đó dựa trên ModularCostumeCatalog (Fantasy
            // legacy) và sẽ dọn 448 entry Pro Casual active ra khỏi mapping như "stale". Icon costume
            // active sinh bằng ZombieWar/Costume/Generate Casual Icons.
        }

        /// Sinh icon con thiếu — an toàn resume/cancel, không đụng asset đã hoàn thành.
        [MenuItem("ZombieWar/UI/Authoring/Generate Missing Costume Thumbnails")]
        public static void GenerateMissingCostume() => GenerateCostumeIcons(onlyMissing: true);

        /// Chủ đích refresh TOÀN BỘ icon costume (đè cùng path deterministic, giữ GUID sprite).
        [MenuItem("ZombieWar/UI/Authoring/Regenerate All Costume Thumbnails")]
        public static void RegenerateAllCostume() => GenerateCostumeIcons(onlyMissing: false);

        /// Slice 4.1: MỌI part hợp lệ trong catalog (978) phải có icon thật render từ đúng mesh đó.
        /// - Path deterministic C_&lt;guid8&gt;_&lt;name&gt;.png → rerun đè cùng file.
        /// - onlyMissing: bỏ qua entry đã có sprite hợp lệ trong mapping (resume-safe).
        /// - Dọn mapping stale (guid không còn trong catalog).
        /// - Fallback runtime = sprite trung tính (rounded_dashed) — KHÔNG bao giờ là helmet/đồ khác.
        public static void GenerateCostumeIcons(bool onlyMissing)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Thumbs] Không chạy khi Play Mode.");
                return;
            }
            Directory.CreateDirectory(CostumeDir);
            var catalog = EnsureCatalogAsset();
            var costumeCatalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CostumeCatalogPath);
            if (costumeCatalog == null) { Debug.LogError("[Thumbs] Không thấy ModularCostumeCatalog."); return; }

            var targets = new List<(string slot, ModularCostumeCatalog.PartEntry part)>();
            var validGuids = new HashSet<string>();
            foreach (var slot in costumeCatalog.slots)
                foreach (var p in slot.parts)
                {
                    if (string.IsNullOrEmpty(p.guid)) continue;
                    validGuids.Add(p.guid);
                    if (onlyMissing)
                    {
                        var existing = catalog.costumeIcons.FirstOrDefault(e => e.guid == p.guid);
                        if (existing != null && existing.icon != null) continue;
                    }
                    targets.Add((slot.slot, p));
                }

            // Dọn mapping stale trước khi generate.
            int stale = catalog.costumeIcons.RemoveAll(e => !validGuids.Contains(e.guid));

            int ok = 0, fail = 0;
            bool cancelled = false;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var (slot, part) = targets[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Costume Thumbnails",
                            $"{i + 1}/{targets.Count}: {slot}/{part.name}", i / (float)Mathf.Max(1, targets.Count)))
                    {
                        cancelled = true;
                        Debug.LogWarning($"[Thumbs] Cancelled tại {i}/{targets.Count} — chạy lại 'Generate Missing' để resume.");
                        break;
                    }
                    var sprite = RenderCostumePart(slot, part);
                    if (sprite != null) { ok++; UpsertCostumeIcon(catalog, part.guid, sprite); }
                    else { fail++; Debug.LogWarning($"[Thumbs] FAIL render '{slot}/{part.name}' ({part.guid})"); }
                }
                // Fallback trung tính — chỉ là lưới an toàn runtime, validator vẫn coi thiếu icon là lỗi.
                catalog.costumeFallbackIcon = UISpriteFactory.Load("rounded_dashed");
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            int mapped = catalog.costumeIcons.Count(e => e.icon != null);
            Debug.Log($"[Thumbs] Costume DONE — generated {ok}, fail {fail}, stale removed {stale}, " +
                      $"mapping {mapped}/{validGuids.Count}{(cancelled ? " (CANCELLED — resume bằng Generate Missing)" : "")}.");
        }

        // ================================================================ render

        /// Bounds profile (max của chiều dài Z / cao Y) — dùng gom scale chung cả roster.
        static float MeasureExtent(WeaponData wd)
        {
            if (wd == null || wd.weaponPrefab == null) return 0f;
            var inst = Object.Instantiate(wd.weaponPrefab, new Vector3(5000f, 5000f, 5000f), Quaternion.identity);
            try
            {
                var renderers = inst.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0) return 0f;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return Mathf.Max(b.size.z, b.size.y);
            }
            finally { Object.DestroyImmediate(inst); }
        }

        static Sprite RenderWeapon(WeaponData wd, float rosterExtent)
        {
            if (wd == null || wd.weaponPrefab == null)
            {
                if (wd != null) Debug.LogWarning($"[Thumbs] '{wd.name}' không có weaponPrefab — dùng fallback.", wd);
                return null;
            }

            // Đặt xa khỏi mọi content scene để frustum chỉ thấy khẩu súng.
            var offset = new Vector3(5000f, 5000f, 5000f);
            GameObject inst = null, camGO = null, keyGO = null, fillGO = null;
            RenderTexture rt = null;
            try
            {
                inst = Object.Instantiate(wd.weaponPrefab, offset, Quaternion.identity);
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                    ps.gameObject.SetActive(false);

                var renderers = inst.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"[Thumbs] '{wd.name}' prefab không có Renderer — fallback.", wd);
                    return null;
                }
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.extents.sqrMagnitude < 1e-8f)
                {
                    Debug.LogWarning($"[Thumbs] '{wd.name}' bounds zero — fallback.", wd);
                    return null;
                }

                keyGO = MakeLight("ThumbKey", new Vector3(35f, 140f, 0f), 1.15f);
                fillGO = MakeLight("ThumbFill", new Vector3(10f, -30f, 0f), 0.5f);

                camGO = new GameObject("ThumbCam");
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);   // nền trong suốt thật
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.001f;
                cam.farClipPlane = 100f;
                rt = new RenderTexture(WeaponCaptureSize, WeaponCaptureSize, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 8 };
                cam.targetTexture = rt;

                // Profile ngang, nòng súng hướng sang PHẢI icon (camera phía +X).
                // Scale khung = trung bình nhân giữa size khẩu này và khẩu dài nhất roster:
                // giữ thứ bậc to–nhỏ (súng lục nhỏ hơn rifle rõ rệt) nhưng không true-scale
                // đến mức súng lục bé tí không đọc được. pad 1.24 chừa mép cho outline
                // (Tools/outline_icons.py chạy SAU mỗi lần render).
                float baseExtent = Mathf.Max(b.size.z, b.size.y);
                float blended = Mathf.Sqrt(baseExtent * Mathf.Max(rosterExtent, baseExtent));
                float extent = blended * 1.24f;
                float dist = extent * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = b.center + Vector3.right * dist;
                cam.transform.LookAt(b.center);
                cam.Render();

                string file = $"{WeaponsDir}/{StableName("W", wd)}.png";
                CasualIconGenerator.WriteDownscaled(rt, file, WeaponIconSize);
                return ImportIcon(file, WeaponIconSize);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Thumbs] '{AssetDatabase.GetAssetPath(wd)}' render fail: {ex.Message} — fallback.");
                return null;
            }
            finally
            {
                if (camGO != null) { var c = camGO.GetComponent<Camera>(); if (c != null) c.targetTexture = null; }
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (camGO != null) Object.DestroyImmediate(camGO);
                if (keyGO != null) Object.DestroyImmediate(keyGO);
                if (fillGO != null) Object.DestroyImmediate(fillGO);
                if (inst != null) Object.DestroyImmediate(inst);
            }
        }

        static GameObject MakeLight(string name, Vector3 euler, float intensity)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        static Sprite ImportIcon(string path, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = maxSize;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// Hướng camera theo slot để icon đọc được: mặt/đầu = chính diện, thân/lưng = 3/4,
        /// tay/chân/giày = crop sát bounds của chính part (bounds mesh bind pose đã là part đó).
        static Vector3 SlotViewDir(string slot) => slot switch
        {
            "Chest" or "Body" => new Vector3(-0.45f, 0.2f, -1f),
            "Back" => new Vector3(0f, 0.15f, 1f),          // đồ đeo lưng nhìn từ phía sau
            "Hands" or "Legs" or "Feet" => new Vector3(-0.25f, 0.1f, -1f),
            _ => new Vector3(0f, 0.12f, -1f),              // Hair/Head/mặt: chính diện hơi cao
        };

        static Sprite RenderCostumePart(string slot, in ModularCostumeCatalog.PartEntry part)
        {
            // Skinned part render ở BIND POSE qua DrawMesh — không cần rig/animator, không đụng scene.
            if (part.skinnedMesh == null || part.materials == null || part.materials.Length == 0)
                return null;

            var pru = new PreviewRenderUtility();
            try
            {
                var mesh = part.skinnedMesh;
                var b = mesh.bounds;
                if (b.extents.sqrMagnitude < 1e-8f) return null;

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var mat = part.materials[Mathf.Min(s, part.materials.Length - 1)];
                    if (mat == null) continue;
                    pru.DrawMesh(mesh, Matrix4x4.identity, mat, s);
                }
                FrameCamera(pru, b, SlotViewDir(slot));
                return Snap(pru, CostumeDir, $"C_{ShortGuid(part.guid)}_{Sanitize(part.name)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Thumbs] costume '{part.name}' render fail: {ex.Message}");
                return null;
            }
            finally
            {
                pru.Cleanup();
            }
        }

        static void FrameCamera(PreviewRenderUtility pru, Bounds b, Vector3 dir)
        {
            var cam = pru.camera;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // URP preview không giữ alpha → nền transparent thành đen. Dùng luôn màu surface card
            // (#1B2130) để thumbnail hoà vào card thay vì ô đen.
            cam.backgroundColor = new Color(0.106f, 0.129f, 0.188f, 1f);
            float dist = b.extents.magnitude / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
            cam.transform.position = b.center + dir.normalized * Mathf.Max(dist, 0.01f);
            cam.transform.LookAt(b.center);
            pru.lights[0].intensity = 1.3f;
            pru.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            if (pru.lights.Length > 1) pru.lights[1].intensity = 0.6f;
            pru.ambientColor = new Color(0.35f, 0.35f, 0.38f);
        }

        static Sprite Snap(PreviewRenderUtility pru, string dir, string fileName)
        {
            pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
            pru.Render(true);
            var tex = pru.EndStaticPreview();
            if (tex == null) return null;

            string path = $"{dir}/{fileName}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================================================================ catalog

        public static UIPrototypeCatalog EnsureCatalogAsset()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(CatalogAssetPath);
            if (catalog != null) return catalog;
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogAssetPath)!);
            catalog = ScriptableObject.CreateInstance<UIPrototypeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            Debug.Log("[Thumbs] Tạo UIPrototypeCatalog: " + CatalogAssetPath);
            return catalog;
        }

        /// Roster order = explicit CatalogOrder (presentation order, set by WeaponRosterMigration) —
        /// KHÔNG dùng AssetDatabase.FindAssets order (không xác định). WeaponId là tie-breaker
        /// deterministic cho asset chưa migrate (CatalogOrder mặc định -1, trùng nhau).
        public static List<WeaponData> LoadAllWeaponData()
            => AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null)
                .OrderBy(w => w.CatalogOrder).ThenBy(w => w.WeaponId)
                .ToList();

        static void UpsertWeaponEntry(UIPrototypeCatalog catalog, WeaponData wd, Sprite icon)
        {
            var entry = catalog.weapons.FirstOrDefault(e => e.data == wd);
            if (entry == null)
            {
                entry = new UIPrototypeCatalog.WeaponEntry { data = wd, owned = wd.unlockCost <= 0 };
                catalog.weapons.Add(entry);
            }
            if (icon != null) entry.icon = icon;   // giữ manual override khi generate fail
        }

        static void UpsertCostumeIcon(UIPrototypeCatalog catalog, string guid, Sprite icon)
        {
            var entry = catalog.costumeIcons.FirstOrDefault(e => e.guid == guid);
            if (entry == null)
            {
                entry = new UIPrototypeCatalog.CostumeIcon { guid = guid };
                catalog.costumeIcons.Add(entry);
            }
            entry.icon = icon;
        }

        static string StableName(string prefix, Object asset)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return $"{prefix}_{ShortGuid(guid)}_{Sanitize(asset.name)}";
        }

        static string ShortGuid(string guid)
            => string.IsNullOrEmpty(guid) ? "noguid" : guid.Substring(0, Mathf.Min(8, guid.Length));

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
