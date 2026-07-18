using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace ZombieWar.EditorTools
{
    // Renders every weapon prefab across the ThirdParty packs into a single
    // labelled contact-sheet PNG so we can eyeball models when mapping WeaponData.
    // Reusable for HUD icon / shop thumbnail generation later.
    public static class WeaponCatalog
    {
        static readonly string[] Folders =
        {
            "Assets/ThirdParty/Low Poly Weapons VOL.1/Prefabs",
            "Assets/ThirdParty/Low Poly Pistol Weapon Pack 1/Prefabs/Weapons",
            "Assets/ThirdParty/Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons",
            "Assets/ThirdParty/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons",
        };

        [MenuItem("Tools/ZombieWar/Weapon Catalog Sheet")]
        public static void Generate()
        {
            var paths = new List<string>();
            foreach (var f in Folders)
            {
                if (!AssetDatabase.IsValidFolder(f)) continue;
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { f }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.EndsWith(".prefab")) paths.Add(p);
                }
            }
            paths = paths.Distinct().OrderBy(x => x).ToList();

            GameObject root = null; Camera cam = null; RenderTexture rt = null; Texture2D outTex = null;
            var order = new StringBuilder();
            const float Y = 5000f;
            const int cols = 7;
            const float spacing = 2.3f;
            const float target = 1.0f;
            int rows = Mathf.CeilToInt(paths.Count / (float)cols);

            try
            {
                root = new GameObject("___WSHEET___");
                for (int i = 0; i < paths.Count; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                    if (prefab == null) continue;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    int col = i % cols, row = i / cols;
                    var cellPos = new Vector3(col * spacing, Y, -row * spacing);

                    var b = Encap(inst);
                    float ext = Mathf.Max(0.001f, Mathf.Max(b.extents.x, b.extents.y, b.extents.z));
                    inst.transform.localScale = Vector3.one * (target / ext);
                    b = Encap(inst);
                    inst.transform.position += cellPos - b.center;

                    var lab = new GameObject("lbl");
                    lab.transform.SetParent(root.transform);
                    lab.transform.position = cellPos + new Vector3(0, 0, 1.05f);
                    var tmp = lab.AddComponent<TMPro.TextMeshPro>();
                    tmp.text = Path.GetFileNameWithoutExtension(paths[i]);
                    tmp.fontSize = 5f;
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.rectTransform.sizeDelta = new Vector2(spacing, 0.6f);
                    tmp.enableWordWrapping = false;
                    order.AppendLine($"{i} [{col},{row}] {tmp.text}");
                }

                MakeLight(root, Quaternion.Euler(90, 0, 0), 1.25f);
                MakeLight(root, Quaternion.Euler(45, 180, 0), 0.5f);

                var cg = new GameObject("cam"); cg.transform.SetParent(root.transform);
                cam = cg.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.14f, 0.17f);
                cam.orthographic = true;
                var gridCenter = new Vector3((cols - 1) * spacing / 2f, Y, -(rows - 1) * spacing / 2f);
                cam.transform.position = gridCenter + new Vector3(0, 120, 0);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                cam.orthographicSize = rows * spacing / 2f * 1.06f;
                cam.nearClipPlane = 0.01f; cam.farClipPlane = 10000f;

                foreach (var t in root.GetComponentsInChildren<TMPro.TextMeshPro>())
                    t.transform.rotation = cam.transform.rotation;

                int W = cols * 300, H = rows * 300;
                rt = new RenderTexture(W, H, 24);
                cam.targetTexture = rt;
                var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();

                RenderTexture.active = rt;
                outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
                outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                outTex.Apply();
                RenderTexture.active = null;

                Directory.CreateDirectory("Assets/Screenshots");
                File.WriteAllBytes("Assets/Screenshots/weapon_catalog.png", outTex.EncodeToPNG());
                Debug.Log($"WCATALOG count={paths.Count} -> Assets/Screenshots/weapon_catalog.png\n{order}");
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (outTex != null) Object.DestroyImmediate(outTex);
                if (root != null) Object.DestroyImmediate(root);
            }
            AssetDatabase.Refresh();
        }

        static Bounds Encap(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void MakeLight(GameObject parent, Quaternion rot, float intensity)
        {
            var lg = new GameObject("lgt"); lg.transform.SetParent(parent.transform);
            var lt = lg.AddComponent<Light>();
            lt.type = LightType.Directional; lt.intensity = intensity;
            lg.transform.rotation = rot;
        }
    }
}
