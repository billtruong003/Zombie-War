using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// The 17 "Blob" monsters of the low-poly pack (Assets/Monsters), owner's pick 2026-10-01: the only
    /// models with move, attack, hit and death clips. Each is prepared for the VAT baker:
    ///  - its skinned meshes (up to 3) merged into one, bones unioned and bind poses rebased;
    ///  - flat material colours moved onto the KayKit gradient palette (the same texture as the map
    ///    decoration), so the whole crowd shares one texture;
    ///  - the merged mesh is expressed Z-up, as the baker expects (its prefab stands it up).
    /// Then <see cref="ZombieVATBaker"/> bakes them from the prepared prefabs with their embedded clips.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        const string BlobSource = "Assets/Monsters/Models";
        const string BlobOut = "Assets/_Project/Art/Monsters/Blob/";

        sealed class BlobDef
        {
            public string model, bake, display;
            public float height = 1.5f, hp = 45f, dmg = 7f, speed = 2.5f;
            public bool elite;
            public int coin = 1, xp = 1;
        }

        static readonly BlobDef[] Blobs =
        {
            new() { model = "Chicken Blob", bake = "BlobChicken", display = "Chicken Blob", height = 1.35f, hp = 35f, speed = 2.9f },
            new() { model = "Dog Blob", bake = "BlobDog", display = "Dog Blob", height = 1.4f, hp = 40f, speed = 2.7f },
            new() { model = "Cat Blob", bake = "BlobCat", display = "Cat Blob", height = 1.4f, hp = 40f, speed = 2.8f },
            new() { model = "Pigeon Blob", bake = "BlobPigeon", display = "Pigeon Blob", height = 1.3f, hp = 30f, speed = 3.1f },
            new() { model = "Mushroom Blob", bake = "BlobMushroom", display = "Mushroom Blob", height = 1.45f, hp = 50f, speed = 2.3f },
            new() { model = "Orc Blob", bake = "BlobOrc", display = "Orc Blob", height = 1.6f, hp = 60f, dmg = 9f, speed = 2.4f, coin = 2, xp = 2 },
            new() { model = "Bird Blob", bake = "BlobBird", display = "Bird Blob", height = 1.45f, hp = 40f, speed = 3.0f },
            new() { model = "Fish Blob", bake = "BlobFish", display = "Fish Blob", height = 1.5f, hp = 45f, speed = 2.5f },
            new() { model = "Green Blob", bake = "BlobGreen", display = "Green Blob", height = 1.3f, hp = 35f, speed = 2.6f },
            new() { model = "Pink Blob", bake = "BlobPink", display = "Pink Blob", height = 1.35f, hp = 40f, speed = 2.6f },
            new() { model = "Cactoro Blob", bake = "BlobCactoro", display = "Cactoro Blob", height = 1.55f, hp = 55f, dmg = 9f, speed = 2.4f, coin = 2, xp = 2 },
            new() { model = "Alien Blob", bake = "BlobAlien", display = "Alien Blob", height = 1.6f, hp = 55f, dmg = 8f, speed = 2.7f, coin = 2, xp = 2 },
            new() { model = "Ninja Blob", bake = "BlobNinja", display = "Ninja Blob", height = 1.5f, hp = 45f, dmg = 9f, speed = 3.2f, coin = 2, xp = 2 },
            new() { model = "Yeti Blob", bake = "BlobYeti", display = "Yeti Blob", height = 1.6f, hp = 65f, dmg = 9f, speed = 2.3f, coin = 2, xp = 2 },
            new() { model = "Wizard Blob", bake = "BlobWizard", display = "Wizard Blob", height = 1.5f, hp = 45f, dmg = 8f, speed = 2.6f, coin = 2, xp = 2 },
            new() { model = "Green Spiky Blob", bake = "BlobSpiky", display = "Spiky Blob", height = 2.1f, hp = 240f, dmg = 16f, speed = 2.1f, elite = true, coin = 6, xp = 6 },
            new() { model = "Mushnub Evolved", bake = "BlobMushnub", display = "Mushnub", height = 2.2f, hp = 260f, dmg = 18f, speed = 2.0f, elite = true, coin = 6, xp = 6 },
        };

        [MenuItem("HordeCall/Monsters/Prepare + Bake Blob Monsters")]
        public static string BakeBlobs()
        {
            Directory.CreateDirectory(BlobOut);
            var map = JsonUtility.FromJson<PalMap>(File.ReadAllText(Pal + "palette_map.json"));
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(Pal + "T_EnvPalette.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BlobOut + "M_BlobPalette.mat");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, BlobOut + "M_BlobPalette.mat"); }
            mat.SetTexture("_BaseMap", palette);
            EditorUtility.SetDirty(mat);

            var log = new System.Text.StringBuilder();
            var defs = new List<ZombieWar.Editor.ZombieVATBaker.EnemyBakeDef>();
            float worstDE = 0f;
            foreach (var b in Blobs)
            {
                string prepared = PrepareBlob(b, map, mat, out float dE, out int verts);
                worstDE = Mathf.Max(worstDE, dE);
                if (prepared == null) { log.Append($"{b.model}: prepare failed; "); continue; }
                defs.Add(new ZombieWar.Editor.ZombieVATBaker.EnemyBakeDef
                {
                    enemyId = "enemy.blob." + b.bake.Substring(4).ToLowerInvariant(), displayName = b.display, bakeName = b.bake,
                    sourceDir = BlobSource, modelName = b.model, modelPath = prepared, embeddedClips = true, targetHeight = b.height,
                    idle = new ZombieWar.Editor.ZombieVATBaker.Pick("Idle", true), move = new ZombieWar.Editor.ZombieVATBaker.Pick("Walk", true),
                    attack = new ZombieWar.Editor.ZombieVATBaker.Pick("Bite_Front"), hit = new ZombieWar.Editor.ZombieVATBaker.Pick("HitRecieve"), death = new ZombieWar.Editor.ZombieVATBaker.Pick("Death"),
                    componentType = typeof(ZombieWar.ZombieWalker), archetype = b.elite ? ZombieWar.ZombieArchetype.Heavy : ZombieWar.ZombieArchetype.Walker, isElite = b.elite,
                    maxHealth = b.hp, damage = b.dmg, moveSpeed = b.speed, attackRange = b.elite ? 1.7f : 1.3f, attackCooldown = 1.2f,
                    attackWindup = 0.35f, coinReward = b.coin, xpReward = b.xp,
                });
                log.Append($"{b.model} {verts}v; ");
            }
            AssetDatabase.SaveAssets();
            log.Append($"worst colour match ΔE {worstDE:F1}. ");
            log.Append(ZombieWar.Editor.ZombieVATBaker.BakeList(defs));
            return log.ToString();
        }

        /// One prepared prefab: the FBX hierarchy (bones, Animator) with a single merged skinned mesh.
        static string PrepareBlob(BlobDef b, PalMap map, Material mat, out float worstDE, out int verts)
        {
            worstDE = 0f; verts = 0;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{BlobSource}/{b.model}.fbx");
            if (fbx == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            var root = inst.transform;
            var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // Merged mesh space: Z-up under the root (root-from-mesh = rotation -90° about X).
            var meshFromRootRot = Quaternion.Euler(-90f, 0f, 0f);
            Matrix4x4 rootFromMesh = Matrix4x4.Rotate(meshFromRootRot);
            Matrix4x4 meshFromRoot = rootFromMesh.inverse;

            var bones = new List<Transform>();
            var bindposes = new List<Matrix4x4>();
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var bw = new List<BoneWeight>();
            var tris = new List<int>();
            float minY = float.MaxValue, maxY = float.MinValue;
            // Height range (root space) for the KayKit-style vertical shading inside each cell.
            foreach (var s in smrs)
            {
                var m = s.sharedMesh; if (m == null) continue;
                var rootFromS = root.worldToLocalMatrix * s.transform.localToWorldMatrix;
                foreach (var p in m.vertices) { float y = rootFromS.MultiplyPoint3x4(p).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            }
            float spanY = Mathf.Max(0.01f, maxY - minY);

            foreach (var s in smrs)
            {
                var m = s.sharedMesh; if (m == null) continue;
                var rootFromS = root.worldToLocalMatrix * s.transform.localToWorldMatrix;
                var meshFromS = meshFromRoot * rootFromS;
                // Bones of this renderer in the union, with bind poses rebased onto the merged mesh space.
                var map2 = new int[s.bones.Length];
                for (int i = 0; i < s.bones.Length; i++)
                {
                    int k = bones.IndexOf(s.bones[i]);
                    if (k < 0) { k = bones.Count; bones.Add(s.bones[i]); bindposes.Add(m.bindposes[i] * rootFromS.inverse * rootFromMesh); }
                    map2[i] = k;
                }
                var sv = m.vertices; var sn = m.normals; var sbw = m.boneWeights;
                for (int sub = 0; sub < m.subMeshCount && sub < s.sharedMaterials.Length; sub++)
                {
                    var mm = s.sharedMaterials[sub];
                    var col = mm != null ? Tint(mm) : Color.gray;
                    var pick = BestMatch(map, col, out float dE, includeNew: true);
                    worstDE = Mathf.Max(worstDE, dE);
                    float v0 = pick.row / (float)PalRows, v1 = (pick.row + 1) / (float)PalRows;
                    var local = new Dictionary<int, int>();
                    foreach (int i in m.GetTriangles(sub))
                    {
                        if (!local.TryGetValue(i, out int k))
                        {
                            k = v.Count; local[i] = k;
                            v.Add(meshFromS.MultiplyPoint3x4(sv[i]));
                            n.Add(sn.Length > 0 ? meshFromS.MultiplyVector(sn[i]).normalized : Vector3.forward);
                            float h = (rootFromS.MultiplyPoint3x4(sv[i]).y - minY) / spanY - 0.5f;
                            float vv = Mathf.Clamp(pick.v + h * HeightShade * (v1 - v0), v0 + (v1 - v0) * 0.04f, v1 - (v1 - v0) * 0.04f);
                            uv.Add(new Vector2((pick.col + 0.5f) / PalCols, vv));
                            var w = sbw.Length > 0 ? sbw[i] : new BoneWeight { weight0 = 1f };
                            w.boneIndex0 = map2[w.boneIndex0]; w.boneIndex1 = map2[w.boneIndex1];
                            w.boneIndex2 = map2[w.boneIndex2]; w.boneIndex3 = map2[w.boneIndex3];
                            bw.Add(w);
                        }
                        tris.Add(k);
                    }
                }
            }
            var merged = new Mesh { name = b.bake + "_Merged" };
            if (v.Count > 65000) merged.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            merged.SetVertices(v); merged.SetNormals(n); merged.SetUVs(0, uv);
            merged.boneWeights = bw.ToArray();
            merged.bindposes = bindposes.ToArray();
            merged.SetTriangles(tris, 0);
            merged.RecalculateBounds();
            verts = v.Count;
            string meshPath = BlobOut + b.bake + "_Merged.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (old != null) { EditorUtility.CopySerialized(merged, old); Object.DestroyImmediate(merged); merged = old; }
            else AssetDatabase.CreateAsset(merged, meshPath);

            // Replace the vendor renderers with the merged one (components only: a mesh object can
            // parent part of the armature).
            Transform rootBone = smrs.Length > 0 && smrs[0].rootBone != null ? smrs[0].rootBone : root;
            foreach (var s in smrs) Object.DestroyImmediate(s);
            var go = new GameObject("Body");
            go.transform.SetParent(root, false);
            go.transform.localRotation = meshFromRootRot;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = merged;
            smr.bones = bones.ToArray();
            smr.rootBone = rootBone;
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;

            string path = BlobOut + b.bake + "_Prepared.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return path;
        }
    }
}
