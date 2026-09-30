using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Environment theme concepts (2026-09-30): renders every environment prefab of the vendor packs
    /// to a 256² thumbnail at the same three-quarter angle, and logs its triangle count and size in
    /// metres, so theme kits are picked from what the assets really look like. Renders in an isolated
    /// preview scene; no project scene is opened or touched. Output: Review/M8/env_kit/.
    /// </summary>
    public static class EnvKitThumbnails
    {
        static readonly (string pack, string folder, string[] include)[] Packs =
        {
            ("KayKit", "Assets/KayKit/Packs/Bits", null),
            ("TinyTeacup", "Assets/Tiny Teacup Studio", null),
            ("Playground", "Assets/Playground Low Poly", null),
            ("Lux", "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios", null),
            ("SyntyGeneric", "Assets/Synty/PolygonGeneric", new[] { "SM_Gen_Env_", "SM_Gen_Prop_" }),
            ("SyntyDarkFantasy", "Assets/Synty/PolygonDarkFantasy", new[] { "SM_Env_" }),
            ("SyntyKaiju", "Assets/Synty/PolygonKaiju", new[] { "SM_Env_", "SM_Prop_" }),
            ("MegaCityNature", "Assets/JC_LP_MegaCity/Prefabs/Nature", null),
            ("MegaCityFloorProps", "Assets/JC_LP_MegaCity/Prefabs/FloorProps", null),
            ("MegaCityIndustrial", "Assets/JC_LP_MegaCity/Prefabs/IndustrialProps", null),
            ("MegaCityProps", "Assets/JC_LP_MegaCity/Prefabs/Props", null),
        };

        [MenuItem("HordeCall/World/Render Env Kit Thumbnails")]
        public static string Render()
        {
            const string outRoot = "Review/M8/env_kit/";
            var pru = new PreviewRenderUtility();
            var csv = new StringBuilder("pack,name,tris,sizeX,sizeY,sizeZ,path\n");
            int count = 0;
            try
            {
                pru.camera.fieldOfView = 26f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0.78f, 0.8f, 0.83f);
                pru.camera.nearClipPlane = 0.05f;
                pru.camera.farClipPlane = 2000f;
                pru.lights[0].intensity = 1.25f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                pru.lights[1].intensity = 0.5f;
                pru.ambientColor = new Color(0.45f, 0.47f, 0.52f);

                foreach (var (pack, folder, include) in Packs)
                {
                    Directory.CreateDirectory(outRoot + pack);
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        string name = Path.GetFileNameWithoutExtension(path);
                        if (include != null && !Matches(name, include)) continue;
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null) continue;
                        var go = pru.InstantiatePrefabInScene(prefab);
                        try
                        {
                            var rs = go.GetComponentsInChildren<Renderer>();
                            if (rs.Length == 0) continue;
                            var b = rs[0].bounds;
                            int tris = 0;
                            foreach (var r in rs)
                            {
                                b.Encapsulate(r.bounds);
                                var mf = r.GetComponent<MeshFilter>();
                                var mesh = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                                if (mesh != null) tris += (int)(mesh.GetIndexCount(0) / 3);
                                if (mesh != null) for (int s = 1; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
                            }
                            float radius = Mathf.Max(0.2f, b.extents.magnitude);
                            var dir = Quaternion.Euler(28f, -35f, 0f) * Vector3.back;
                            float dist = radius / Mathf.Sin(pru.camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                            pru.camera.transform.position = b.center - dir * -dist;
                            pru.camera.transform.position = b.center + dir * dist;
                            pru.camera.transform.LookAt(b.center);
                            pru.BeginStaticPreview(new Rect(0, 0, 256, 256));
                            pru.camera.Render();
                            var tex = pru.EndStaticPreview();
                            File.WriteAllBytes(outRoot + pack + "/" + name + ".png", tex.EncodeToPNG());
                            Object.DestroyImmediate(tex);
                            csv.Append(pack).Append(',').Append(name).Append(',').Append(tris).Append(',')
                               .Append(b.size.x.ToString("0.00")).Append(',').Append(b.size.y.ToString("0.00")).Append(',')
                               .Append(b.size.z.ToString("0.00")).Append(',').Append(path).Append('\n');
                            count++;
                        }
                        finally { Object.DestroyImmediate(go); }
                    }
                }
            }
            finally { pru.Cleanup(); }
            File.WriteAllText(outRoot + "kit.csv", csv.ToString());
            return count + " thumbnails";
        }

        static bool Matches(string name, string[] include)
        {
            foreach (var s in include) if (name.StartsWith(s)) return true;
            return false;
        }
    }
}
