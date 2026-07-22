using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>Bakes full-body icons for the curated Pro Casual commerce sets.</summary>
    public static class CostumeSetIconGenerator
    {
        const string CatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string EconomyPath = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        const string OutputDir = "Assets/_Project/UI/Icons/Generated/CasualSets";
        const string AuditPath = "Assets/Screenshots/CasualMigration/costume_sets_audit.txt";
        const int Size = 512;

        [MenuItem("ZombieWar/Costume/Generate Pro Casual Set Icons")]
        public static void Generate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (catalog == null || economy == null || player == null || economy.costumeSets.Count == 0)
            { Debug.LogError("[CostumeSetIcons] Missing catalog/economy/player or no authored sets."); return; }
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(AuditPath));

            GameObject inst = null, camGo = null, keyGo = null, fillGo = null;
            RenderTexture rt = null;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(player);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                var applier = inst.GetComponentInChildren<CharacterModularApplier>(true);
                applier.SetCatalog(catalog); applier.EnsureBoneMap(true);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    if (!r.gameObject.name.StartsWith("Costume_", StringComparison.Ordinal)) r.enabled = false;

                keyGo = Light("SetIconKey", new Vector3(28, 145, 0), 1.15f);
                fillGo = Light("SetIconFill", new Vector3(10, -25, 0), .5f);
                camGo = new GameObject("SetIconCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.fieldOfView = 25f;
                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                var audit = new StringBuilder();
                audit.AppendLine("PRO CASUAL CURATED OUTFIT SETS");
                audit.AppendLine($"count={economy.costumeSets.Count}; catalogItems={catalog.TotalParts}");
                foreach (var set in economy.costumeSets)
                {
                    foreach (var def in catalog.slotDefinitions) applier.Clear(def.id);
                    foreach (var def in catalog.slotDefinitions)
                        if (def.required && catalog.TryFindByItemId(def.defaultItemId, out var ds, out var de)) applier.Apply(ds, de);
                    foreach (var id in set.itemIds)
                        if (catalog.TryFindByItemId(id, out var slot, out var entry)) applier.Apply(slot, entry);
                    bool gloves = set.itemIds.Any(id => catalog.TryFindByItemId(id, out var slot, out _) && slot == "Hands");
                    bool shoes = set.itemIds.Any(id => catalog.TryFindByItemId(id, out var slot, out _) && slot == "Feet");
                    applier.ApplyCasualTechnicalBase(gloves, shoes);

                    var renderers = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(x => x.enabled && x.gameObject.activeInHierarchy
                            && x.gameObject.name.StartsWith("Costume_", StringComparison.Ordinal)).ToArray();
                    if (renderers.Length == 0) continue;
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    Vector3 target = b.center + Vector3.up * b.size.y * .03f;
                    float extent = Mathf.Max(b.size.y, b.size.x * 1.35f) * 1.13f;
                    float dist = extent * .5f / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
                    Vector3 dir = Quaternion.Euler(-2, 10, 0) * Vector3.forward;
                    cam.transform.position = target + dir * dist;
                    cam.transform.LookAt(target);
                    string path = $"{OutputDir}/{set.setId}.png";
                    Render(cam, path);
                    audit.AppendLine($"{set.setId}\t{set.displayName}\t{set.rarity}\t{set.gemPrice} Gem\t{set.sourcePreset}\t{string.Join(",", set.itemIds)}");
                }
                File.WriteAllText(AuditPath, audit.ToString(), Encoding.UTF8);
            }
            finally
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (keyGo != null) UnityEngine.Object.DestroyImmediate(keyGo);
                if (fillGo != null) UnityEngine.Object.DestroyImmediate(fillGo);
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }

            AssetDatabase.Refresh();
            foreach (var set in economy.costumeSets)
            {
                string path = $"{OutputDir}/{set.setId}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
                set.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            economy.RebuildLookups(); EditorUtility.SetDirty(economy); AssetDatabase.SaveAssets();
            Debug.Log($"[CostumeSetIcons] Rendered and bound {economy.costumeSets.Count} labelled set icons. Audit: {AuditPath}");
        }

        static GameObject Light(string name, Vector3 rotation, float intensity)
        {
            var go = new GameObject(name); var l = go.AddComponent<UnityEngine.Light>();
            l.type = LightType.Directional; l.intensity = intensity; go.transform.rotation = Quaternion.Euler(rotation); return go;
        }

        static void Render(Camera cam, string path)
        {
            cam.Render(); var old = RenderTexture.active; RenderTexture.active = cam.targetTexture;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply(); RenderTexture.active = old;
            File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
