using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Inventory of the low-poly monster pack in Assets/Monsters (2026-10-01), before any of it is
    /// converted: per model, triangles, height, materials, and which clips can serve as move, attack,
    /// hit and death (by name). Writes Review/M8/monsters/inventory.csv and a picture sheet.
    /// </summary>
    public static class MonsterInventory
    {
        const string Models = "Assets/Monsters/Models";
        const string Out = "Review/M8/monsters/";

        static readonly (string role, string[] words)[] Roles =
        {
            ("move", new[] { "walk", "run", "fly", "flying", "hop", "jump", "swim", "crawl", "roll" }),
            ("attack", new[] { "attack", "punch", "bite", "headbutt", "kick", "slash", "sword", "spit", "shoot", "spin" }),
            ("hit", new[] { "hit", "recieve", "receive", "damage" }),
            ("death", new[] { "death", "die", "dead" }),
        };

        [MenuItem("HordeCall/Monsters/Inventory")]
        public static string Run()
        {
            Directory.CreateDirectory(Out);
            var csv = new StringBuilder("model,triangles,height_m,materials,clips,move,attack,hit,death,missing\n");
            var rows = new List<(string name, GameObject go)>();
            int complete = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { Models }).OrderBy(x => AssetDatabase.GUIDToAssetPath(x)))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int tris = 0, mats = 0;
                var b = new Bounds(); bool first = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    tris += mesh.triangles.Length / 3;
                    mats += r.sharedMaterials.Length;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview")).Select(c => c.name.Contains("|") ? c.name.Substring(c.name.IndexOf('|') + 1) : c.name).Distinct().ToList();
                var found = new Dictionary<string, string>();
                foreach (var (role, words) in Roles)
                {
                    var hit = clips.FirstOrDefault(c => words.Any(w => c.ToLowerInvariant().Contains(w)));
                    found[role] = hit ?? "";
                }
                var missing = Roles.Where(r => found[r.role] == "").Select(r => r.role).ToList();
                if (missing.Count == 0) complete++;
                string name = Path.GetFileNameWithoutExtension(path);
                csv.Append($"{name},{tris},{b.size.y:F2},{mats},{clips.Count},{found["move"]},{found["attack"]},{found["hit"]},{found["death"]},{string.Join(" ", missing)}\n");
                rows.Add((name, go));
            }
            File.WriteAllText(Out + "inventory.csv", csv.ToString());
            int sheets = RenderSheet(rows);
            return $"{rows.Count} models, {complete} with move/attack/hit/death clips by name; {sheets} sheet(s) in {Out}";
        }

        static int RenderSheet(List<(string name, GameObject go)> rows)
        {
            const int T = 160, Cols = 10;
            int n = rows.Count, lines = (n + Cols - 1) / Cols;
            var util = new PreviewRenderUtility();
            util.camera.fieldOfView = 30f;
            util.camera.clearFlags = CameraClearFlags.SolidColor;
            util.camera.backgroundColor = new Color(0.82f, 0.85f, 0.8f);
            util.camera.farClipPlane = 200f;
            util.lights[0].intensity = 1.2f; util.lights[0].transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            util.lights[1].intensity = 0.6f;
            util.ambientColor = new Color(0.5f, 0.52f, 0.55f);
            var sheet = new Texture2D(T * Cols, T * lines, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    util.BeginPreview(new Rect(0, 0, T, T), GUIStyle.none);
                    var go = util.InstantiatePrefabInScene(rows[i].go);
                    go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var b = new Bounds(go.transform.position, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    float radius = Mathf.Max(0.2f, b.extents.magnitude);
                    util.camera.transform.position = b.center + Quaternion.Euler(15f, 0f, 0f) * new Vector3(0f, 0f, -1.1f * radius / Mathf.Sin(15f * Mathf.Deg2Rad));
                    util.camera.transform.LookAt(b.center);
                    util.camera.Render();
                    var rt = (RenderTexture)util.EndPreview();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(T, T, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, T, T), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    Object.DestroyImmediate(go);
                    int cx = i % Cols, cy = lines - 1 - i / Cols;
                    sheet.SetPixels(cx * T, cy * T, T, T, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                }
                sheet.Apply();
                File.WriteAllBytes(Out + "monsters_sheet.png", sheet.EncodeToPNG());
            }
            finally { util.Cleanup(); Object.DestroyImmediate(sheet); }
            return 1;
        }
    }
}
