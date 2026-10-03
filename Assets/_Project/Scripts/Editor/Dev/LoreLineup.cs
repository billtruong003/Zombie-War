using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieWar.EditorTools
{
    /// Lore lineup (2026-10-03): the story cast stood on a meadow chunk in their own dev scene,
    /// dressed from the game's costume catalog and enemy prefabs, then shot one by one for the
    /// character sheets. Nothing here touches the game scenes, the menu or the Player prefab.
    public static class LoreLineup
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/LoreLineup.unity";
        const string OutDir = "Review/Lore/characters";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string IdleController = "Assets/_Project/Animation/MenuIdle.controller";
        const string Catalog = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string Economy = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string Enemies = "Assets/_Project/Prefabs/Enemies/";

        sealed class Cast
        {
            public string id, set, prefab;
            public (string slot, string item)[] extra = new (string, string)[0];
            public float scale = 1f, x, z;
            public bool crown;
        }

        static readonly Cast[] Roster =
        {
            new Cast { id = "finn", set = "casual.pro.set.011", x = -1.6f },
            new Cast { id = "hana", set = "casual.pro.set.017", extra = new[] { ("Head", "casual.pro.headgear.060") }, scale = 1.08f, x = 0f },
            new Cast { id = "granny_bea", set = "casual.pro.set.016", extra = new[] { ("Head", "casual.pro.headgear.007") }, scale = 0.94f, x = 1.6f },
            new Cast { id = "rex", set = "casual.pro.set.025", extra = new[] { ("Head", "casual.pro.headgear.033") }, x = -3.2f },
            new Cast { id = "kiki", set = "casual.pro.set.008", scale = 0.96f, x = 3.2f },
            new Cast { id = "gloop", prefab = "ENM_BlobPink_VAT", x = -5.2f },
            new Cast { id = "king_blobert", prefab = "ENM_BlobOrc_VAT", scale = 1.7f, crown = true, x = 6.0f },
            new Cast { id = "sir_prickles", prefab = "ENM_CactusBoss_VAT", x = -4.5f, z = 5.5f },
            new Cast { id = "duke_diggs", prefab = "ENM_MoleRatKing_VAT", x = 0f, z = 6.5f },
            new Cast { id = "mc_bones", prefab = "ENM_SkeletonGiant_VAT", x = 4.8f, z = 5.5f },
        };

        [MenuItem("HordeCall/Dev/Lore Lineup/Build Scene")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Lore] exit Play first"); return; }
            // Additive, so whatever scene is open (saved or not) is left exactly as it was.
            var old = SceneManager.GetSceneByPath(ScenePath);
            if (old.IsValid() && old.isLoaded) EditorSceneManager.CloseScene(old, true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var chunk = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/EnvSandbox/Maps/meadow/Chunk_meadow_2_2.prefab");
            if (chunk != null) PrefabUtility.InstantiatePrefab(chunk, scene);

            var sun = new GameObject("Sun").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(sun.gameObject, scene);
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46f, 28f, 0f);   // from behind the camera's left shoulder
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.72f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.42f, 0.32f);

            var root = new GameObject("Cast").transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            foreach (var c in Roster)
            {
                var go = c.prefab == null ? Hero(c) : Enemy(c);
                if (go == null) { Debug.LogWarning($"[Lore] could not build {c.id}"); continue; }
                go.name = c.id;
                go.transform.SetParent(root, true);
                go.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.z), Quaternion.Euler(0f, 180f, 0f));
                go.transform.localScale = Vector3.one * c.scale;
                if (c.crown) AddCrown(go);
            }
            Pose(root);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Lore] lineup scene built: " + ScenePath);
        }

        static GameObject Hero(Cast c)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, SceneManager.GetActiveScene());
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Only the look is needed: gameplay components would run their editor hooks for nothing.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is CharacterModularApplier)) Object.DestroyImmediate(mb);
            var applier = go.GetComponentInChildren<CharacterModularApplier>(true);
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Catalog);
            var econ = AssetDatabase.LoadAssetAtPath<EconomyConfig>(Economy);
            if (applier == null || catalog == null || econ == null) return go;
            applier.SetCatalog(catalog);
            applier.EnsureBoneMap(true);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!smr.name.StartsWith("Costume_")) smr.gameObject.SetActive(false);
            foreach (var def in catalog.slotDefinitions) applier.Clear(def.id);
            foreach (var def in catalog.slotDefinitions)
                if (def.required && catalog.TryFindByItemId(def.defaultItemId, out var ds, out var de)) applier.Apply(ds, de);
            var items = new List<string>();
            foreach (var s in econ.costumeSets) if (s.setId == c.set) items.AddRange(s.itemIds);
            bool gloves = false, shoes = false;
            foreach (var id in items)
                if (catalog.TryFindByItemId(id, out var slot, out var entry)) { applier.Apply(slot, entry); gloves |= slot == "Hands"; shoes |= slot == "Feet"; }
            foreach (var (slotName, item) in c.extra)
                if (catalog.TryFindByItemId(item, out var slot, out var entry)) applier.Apply(slot, entry);
            applier.ApplyCasualTechnicalBase(gloves, shoes);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleController);
            return go;
        }

        static GameObject Enemy(Cast c)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + c.prefab + ".prefab");
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, SceneManager.GetActiveScene());
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // VAT_Animator stays: it runs in the editor and feeds the vertex animation to the shader.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is VAT_Animator)) Object.DestroyImmediate(mb);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            return go;
        }

        /// The costume crown, as a plain mesh sat on top of the blob.
        static void AddCrown(GameObject blob)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Catalog);
            if (catalog == null || !catalog.TryFindByItemId("casual.pro.headgear.046", out _, out var entry) || entry.skinnedMesh == null) return;
            // The VAT renderer's bounds cover every animation frame; the baked mesh is the body at rest.
            var mf = blob.GetComponentInChildren<MeshFilter>();
            var top = mf != null && mf.sharedMesh != null
                ? new Bounds(mf.transform.TransformPoint(mf.sharedMesh.bounds.center), Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale))
                : Bounds(blob);
            var crown = new GameObject("Crown");
            SceneManager.MoveGameObjectToScene(crown, blob.scene);
            crown.AddComponent<MeshFilter>().sharedMesh = entry.skinnedMesh;
            crown.AddComponent<MeshRenderer>().sharedMaterials = entry.materials;
            var mb = entry.skinnedMesh.bounds;
            float s = top.size.x * 0.42f / Mathf.Max(0.01f, mb.size.x);
            crown.transform.localScale = Vector3.one * s;
            crown.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(0f, 0f, -8f);
            crown.transform.position = Vector3.zero;
            var cb = crown.GetComponent<MeshRenderer>().bounds;
            var seat = new Vector3(top.center.x, top.max.y - top.size.y * 0.34f, top.center.z);
            crown.transform.position = seat - new Vector3(cb.center.x, cb.min.y, cb.center.z);
            crown.transform.SetParent(blob.transform, true);
        }

        static void Pose(Transform root)
        {
            foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            {
                if (anim.runtimeAnimatorController == null) continue;
                anim.Rebind();
                anim.Update(0.35f);
            }
            var tick = typeof(VAT_Animator).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var vat in root.GetComponentsInChildren<VAT_Animator>(true))
                tick?.Invoke(vat, new object[] { 0.35f });
        }

        /// Hides the grass, flowers and props standing between the camera (on -Z) and a box, so
        /// nothing covers the subject; the trees and ground behind stay. Returns what it hid.
        static List<Renderer> ClearView(Bounds subject, Transform cast)
        {
            // The grass comes in batched patches several metres wide, so test overlap, not centres.
            var front = new Bounds();
            front.SetMinMax(new Vector3(subject.min.x - 1.2f, -5f, subject.min.z - 9f),
                            new Vector3(subject.max.x + 1.2f, 50f, subject.min.z + 0.4f));
            var hidden = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.IsChildOf(cast) || r.gameObject.scene != cast.gameObject.scene) continue;
                if (r.name == "Ground" || r.name.StartsWith("Fluid")) continue;
                if (!r.bounds.Intersects(front)) continue;
                r.enabled = false;
                hidden.Add(r);
            }
            return hidden;
        }

        static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var rb = r.bounds;
                // A VAT renderer's bounds cover every frame of every clip: frame the body at rest.
                if (r.GetComponent<VAT_Animator>() != null && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    rb = new Bounds(mf.transform.TransformPoint(mf.sharedMesh.bounds.center), Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale));
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return b;
        }

        [MenuItem("HordeCall/Dev/Lore Lineup/Capture")]
        public static void Capture()
        {
            var root = GameObject.Find("Cast");
            if (root == null) { Debug.LogWarning("[Lore] open " + ScenePath + " first"); return; }
            Directory.CreateDirectory(OutDir);
            Pose(root.transform);
            var cast = new List<GameObject>();
            foreach (Transform t in root.transform) cast.Add(t.gameObject);

            var camGo = new GameObject("LoreCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.64f, 0.82f, 0.95f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            try
            {
                foreach (var who in cast)
                {
                    foreach (var o in cast) o.SetActive(o == who);
                    var b = Bounds(who);
                    var hidden = ClearView(b, root.transform);
                    cam.fieldOfView = 24f;
                    float h = Mathf.Max(b.size.y, b.size.x * 0.8f) * 1.12f;
                    float dist = h * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    var look = b.center + Vector3.up * b.size.y * 0.02f;
                    camGo.transform.position = look + new Vector3(0.16f * dist, 0.2f * dist, -dist);
                    camGo.transform.LookAt(look);
                    Shoot(cam, 900, 1200, Path.Combine(OutDir, who.name + ".png"));
                    foreach (var r in hidden) r.enabled = true;
                }
                foreach (var o in cast) o.SetActive(true);
                var all = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (var o in cast) { var ob = Bounds(o); if (first) { all = ob; first = false; } else all.Encapsulate(ob); }
                var cleared = ClearView(all, root.transform);
                cam.fieldOfView = 30f;
                float d = all.size.x * 0.6f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                camGo.transform.position = all.center + new Vector3(0f, 0.3f * d, -d);
                camGo.transform.LookAt(all.center + Vector3.down * 0.3f);
                Shoot(cam, 2400, 1200, Path.Combine(OutDir, "_lineup.png"));
                foreach (var r in cleared) r.enabled = true;
            }
            finally
            {
                foreach (var o in cast) o.SetActive(true);
                Object.DestroyImmediate(camGo);
            }
            Debug.Log("[Lore] captured " + cast.Count + " characters to " + OutDir);
        }

        static void Shoot(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
