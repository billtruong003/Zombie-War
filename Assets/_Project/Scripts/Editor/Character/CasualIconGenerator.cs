using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Editor-only deterministic icon generator for the Casual costume catalog. For every player-facing
    /// item it dresses a real Player instance in the authored starter outfit, swaps only the target
    /// slot to that item, frames it with a slot-specific camera and renders a transparent PNG. The PNG
    /// is imported as a Sprite and bound to the item's <see cref="ModularCostumeCatalog.PartEntry.icon"/>
    /// (identity = stable itemId). Idempotent: stable output paths, re-run rebinds without churn.
    ///
    /// Not a runtime system — icons are baked assets. Never modifies the vendor pack.
    /// </summary>
    public static class CasualIconGenerator
    {
        const string Catalog = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string OutDir = "Assets/_Project/UI/Icons/Generated/CasualCostume";
        const int IconSize = 256;

        // Deterministic pose time sampled from the idle clip so arms rest at the sides (not T-pose).
        const float PoseTime = 0.6f;

        // Framing computed from the target renderer's world bounds (auto-fits any item, pose-safe).
        // pad = how much bigger than the item to frame (context); biasY = shift target up by a
        // fraction of bounds height (e.g. face decal is low on the head → bias up to include it).
        struct Framing { public float fov, yaw, pitch, pad, biasY; public bool behind; }

        static readonly Dictionary<string, Framing> Frames = new()
        {
            { "Hair",    new Framing { fov = 28f, yaw = 16f, pitch = -6f, pad = 1.35f, biasY = -0.05f } },
            { "Face",    new Framing { fov = 28f, yaw = 10f, pitch = -4f, pad = 2.7f,  biasY = 0.25f } },
            { "Eye",     new Framing { fov = 28f, yaw = 8f,  pitch = -4f, pad = 3.4f,  biasY = 0.3f } },
            { "Brow",    new Framing { fov = 28f, yaw = 8f,  pitch = -4f, pad = 3.4f,  biasY = 0.2f } },
            { "Mouth",   new Framing { fov = 28f, yaw = 8f,  pitch = -4f, pad = 3.4f,  biasY = 0.45f } },
            { "Beard",   new Framing { fov = 28f, yaw = 12f, pitch = -4f, pad = 2.3f,  biasY = 0.15f } },
            { "Mask",    new Framing { fov = 28f, yaw = 12f, pitch = -4f, pad = 2.0f,  biasY = 0.15f } },
            { "HairAccessory", new Framing { fov = 28f, yaw = 18f, pitch = -6f, pad = 1.7f, biasY = 0f } },
            { "Head",    new Framing { fov = 28f, yaw = 20f, pitch = -6f, pad = 1.7f,  biasY = 0.0f } },
            { "Eyewear", new Framing { fov = 28f, yaw = 10f, pitch = -6f, pad = 3.0f,  biasY = 0.4f } },
            { "Earring", new Framing { fov = 28f, yaw = 28f, pitch = -4f, pad = 2.5f,  biasY = 0.1f } },
            { "Chest",   new Framing { fov = 32f, yaw = 15f, pitch = -4f, pad = 1.25f, biasY = 0.05f } },
            { "Hands",   new Framing { fov = 30f, yaw = 22f, pitch = -2f, pad = 1.7f,  biasY = 0.0f } },
            { "Bracelet", new Framing { fov = 30f, yaw = 25f, pitch = -2f, pad = 2.0f, biasY = 0f } },
            { "HandAccessory", new Framing { fov = 30f, yaw = 25f, pitch = -2f, pad = 1.8f, biasY = 0f } },
            { "Watch",   new Framing { fov = 30f, yaw = 25f, pitch = -2f, pad = 2.1f,  biasY = 0f } },
            { "Back",    new Framing { fov = 32f, yaw = 8f,  pitch = -4f, pad = 1.4f,  biasY = 0.0f, behind = true } },
            { "Body",    new Framing { fov = 30f, yaw = 12f, pitch = -3f, pad = 1.12f, biasY = 0.0f } },
            { "Legs",    new Framing { fov = 32f, yaw = 12f, pitch = -3f, pad = 1.2f,  biasY = 0.0f } },
            { "Feet",    new Framing { fov = 30f, yaw = 18f, pitch = 2f,  pad = 1.7f,  biasY = 0.1f } },
        };

        // Bare base context (itemIds): ONLY body + face + hair — no clothing. Every icon is shot on
        // this naked-bodied character so the target item is never layered over other garments (a Top
        // icon shows the top on a bare lower body, a Body icon shows the actual body, etc.).
        static readonly (string slot, string itemId)[] Starter =
        {
            ("Eye", "casual.pro.eye.001"), ("Brow", "casual.pro.eyebrow.001"),
            ("Mouth", "casual.pro.lips.001"), ("Hair", "casual.pro.hair.001"),
        };

        [MenuItem("ZombieWar/Costume/Generate Casual Icons (ALL)")]
        public static void GenerateAll() => Run();

        [MenuItem("ZombieWar/Costume/Generate Casual Icons (Sample per slot)")]
        public static void GenerateSample() => Run(sampleOnePerSlot: true);

        static void Run(bool sampleOnePerSlot = false, HashSet<string> onlySlots = null)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Catalog);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (catalog == null || prefab == null) { Debug.LogError("[CasualIcon] catalog/player missing"); return; }
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

            GameObject inst = null; GameObject camGO = null, keyGO = null, fillGO = null; RenderTexture rt = null;
            var writtenByItem = new Dictionary<string, string>();
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
                PoseIdle(inst);

                var applier = inst.GetComponentInChildren<CharacterModularApplier>(true);
                applier.SetCatalog(catalog);
                applier.EnsureBoneMap(true);
                DisableBaked(inst, applier);
                ApplyStarter(applier, catalog);
                applier.ApplyCasualTechnicalBase(false, false);
                var attachRoot = inst.transform;

                // Lighting (deterministic).
                keyGO = MakeLight("Key", new Vector3(30, 150, 0), 1.1f);
                fillGO = MakeLight("Fill", new Vector3(15, -20, 0), 0.45f);
                camGO = new GameObject("IconCam");
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0); // transparent cut-out
                rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                int done = 0;
                foreach (var slot in catalog.slots)
                {
                    if (catalog.IsTechnicalCasualSlot(slot.slot)) continue;
                    if (onlySlots != null && !onlySlots.Contains(slot.slot)) continue;
                    if (!Frames.TryGetValue(slot.slot, out var frame))
                    { Debug.LogWarning($"[CasualIcon] no framing for slot {slot.slot}"); continue; }

                    var parts = slot.parts;
                    int step = sampleOnePerSlot ? Math.Max(1, parts.Count) : 1;
                    for (int i = 0; i < parts.Count; i += step)
                    {
                        var entry = parts[i];
                        applier.ApplyCasualTechnicalBase(slot.slot == "Hands", slot.slot == "Feet");
                        applier.Apply(slot.slot, entry);            // swap only this slot to the target
                        var rend = FindRenderer(attachRoot, slot.slot);
                        string file = Path.Combine(OutDir, entry.itemId + ".png");
                        if (rend != null) RenderTo(cam, frame, rend, file);
                        else Debug.LogWarning($"[CasualIcon] no renderer for {entry.itemId}");
                        writtenByItem[entry.itemId] = file;
                        RestoreSlot(applier, catalog, slot.slot);    // put the starter item back for this slot
                        applier.ApplyCasualTechnicalBase(false, false);
                        done++;
                    }
                }
                Debug.Log($"[CasualIcon] rendered {done} icon(s) -> {OutDir}");
            }
            catch (Exception e) { Debug.LogError($"[CasualIcon] {e.Message}\n{e.StackTrace}"); }
            finally
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (camGO) UnityEngine.Object.DestroyImmediate(camGO);
                if (keyGO) UnityEngine.Object.DestroyImmediate(keyGO);
                if (fillGO) UnityEngine.Object.DestroyImmediate(fillGO);
                if (inst) UnityEngine.Object.DestroyImmediate(inst);
            }

            AssetDatabase.Refresh();
            BindIcons(catalog, writtenByItem);
        }

        // -------- pose / dressing --------

        static void PoseIdle(GameObject inst)
        {
            // Bind/T-pose: deterministic, arms held horizontal (clear of head close-ups), exact bone
            // anchors. Idle-clip sampling was rejected — it raised an arm across the face/torso frames.
            foreach (var animator in inst.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        }

        static void DisableBaked(GameObject inst, CharacterModularApplier applier)
        {
            // Hide everything that isn't a costume renderer: baked Fantasy body, blob shadow, held
            // weapon, effects — so icons show only the character + target item on a clean background.
            var attach = applier.transform.Find("Costume");
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (attach == null || !r.transform.IsChildOf(attach))
                    r.enabled = false;
        }

        static void ApplyStarter(CharacterModularApplier applier, ModularCostumeCatalog catalog)
        {
            foreach (var (slot, itemId) in Starter)
                if (catalog.TryFindByItemId(itemId, out var s, out var e)) applier.Apply(s, e);
        }

        static void RestoreSlot(CharacterModularApplier applier, ModularCostumeCatalog catalog, string slot)
        {
            foreach (var (s, itemId) in Starter)
                if (s == slot && catalog.TryFindByItemId(itemId, out var rs, out var e)) { applier.Apply(rs, e); return; }
            applier.Clear(slot); // slot not in starter (optional) -> leave empty context
        }

        // -------- render --------

        static SkinnedMeshRenderer FindRenderer(Transform attachRoot, string slot)
        {
            if (attachRoot == null) return null;
            string name = "Costume_" + slot;
            foreach (var r in attachRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.gameObject.name == name) return r;
            return null;
        }

        static void RenderTo(Camera cam, Framing f, SkinnedMeshRenderer renderer, string path)
        {
            renderer.updateWhenOffscreen = true; // force a correct world-bounds evaluation in edit mode
            var b = renderer.bounds;
            Vector3 target = b.center + Vector3.up * (f.biasY * b.size.y);
            float extent = Mathf.Max(b.size.x, b.size.y) * f.pad;
            float dist = extent * 0.5f / Mathf.Tan(f.fov * 0.5f * Mathf.Deg2Rad);
            // Character faces +Z. Camera sits on the +Z (front) side; behind flips to the -Z side.
            var dir = Quaternion.Euler(f.pitch, (f.behind ? 180f : 0f) + f.yaw, 0f) * Vector3.forward;
            cam.transform.position = target + dir * dist;
            cam.transform.LookAt(target);
            cam.fieldOfView = f.fov;
            cam.Render();

            var prev = RenderTexture.active; RenderTexture.active = cam.targetTexture;
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static GameObject MakeLight(string name, Vector3 euler, float intensity)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = intensity;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        // -------- import + bind --------

        static void BindIcons(ModularCostumeCatalog catalog, Dictionary<string, string> writtenByItem)
        {
            foreach (var kv in writtenByItem)
            {
                var importer = AssetImporter.GetAtPath(kv.Value) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh();

            int bound = 0;
            for (int s = 0; s < catalog.slots.Count; s++)
            {
                var parts = catalog.slots[s].parts;
                for (int p = 0; p < parts.Count; p++)
                {
                    if (!writtenByItem.TryGetValue(parts[p].itemId, out var path)) continue;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) continue;
                    var e = parts[p]; e.icon = sprite; parts[p] = e; bound++;
                }
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CasualIcon] bound {bound} icon(s) onto catalog itemIds.");
        }
    }
}
