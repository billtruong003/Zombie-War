using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// 05/10 owner: the Studio outline on a picked costume piece looked torn. The PieceHighlight hull
    /// pushes vertices along their normals, and at a hard edge a vertex is split into copies with
    /// different normals, so the hull opens up there. On import, the costume meshes get one averaged
    /// normal per position in UV channel 3 (TEXCOORD3), which the outline shader reads instead.
    /// The meshes are not readable at runtime, so this has to happen at import.
    /// </summary>
    public sealed class OutlineNormalsBaker : AssetPostprocessor
    {
        const string CostumeFolder = "3D Characters Pro-Casual/FBX/Character";

        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').Contains(CostumeFolder)) return;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Bake(smr.sharedMesh);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) Bake(mf.sharedMesh);
        }

        /// <summary>Writes the area-agnostic average of every normal sharing a position into UV3.</summary>
        public static void Bake(Mesh mesh)
        {
            if (mesh == null) return;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length) return;

            var sum = new Dictionary<Vector3Int, Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Key(vertices[i]);
                sum[key] = sum.TryGetValue(key, out var n) ? n + normals[i] : normals[i];
            }
            var smooth = new List<Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = sum[Key(vertices[i])];
                smooth.Add(n.sqrMagnitude > 1e-8f ? n.normalized : normals[i]);
            }
            mesh.SetUVs(3, smooth);
        }

        static Vector3Int Key(Vector3 v) => new(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
    }
}
