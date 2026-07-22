using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Renders every prefab in a folder to a labelled contact sheet, so a whole vendor pack can be
    /// judged visually in one image instead of clicking 132 assets.
    ///
    /// Each cell frames its prefab from its real bounds, so wildly different-sized props (a coin vs
    /// a large crate) are all readable at the same cell size - the sheet is for identifying WHAT
    /// each asset is, not for comparing scale.
    ///
    /// Menu: Tools/ZombieWar/Prefab Contact Sheet.
    /// </summary>
    public class PrefabContactSheet : EditorWindow
    {
        [SerializeField] string folder = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs";
        [SerializeField] string outputPath = "Assets/Screenshots/EnemyCampaign/SourceAudit/KayKit_ContactSheet.png";
        [SerializeField] int cell = 200;
        [SerializeField] int columns = 11;
        [SerializeField] string filter = "";

        [MenuItem("Tools/ZombieWar/Prefab Contact Sheet")]
        public static void Open() => GetWindow<PrefabContactSheet>("Contact Sheet");

        void OnGUI()
        {
            folder = EditorGUILayout.TextField("Folder", folder);
            filter = EditorGUILayout.TextField("Name filter", filter);
            outputPath = EditorGUILayout.TextField("Output PNG", outputPath);
            cell = EditorGUILayout.IntSlider("Cell px", cell, 96, 384);
            columns = EditorGUILayout.IntSlider("Columns", columns, 4, 16);

            if (GUILayout.Button("Build sheet", GUILayout.Height(28)))
                Build(folder, outputPath, cell, columns, filter);
        }

        public static string Build(string folder, string outputPath, int cell, int columns, string filter = "")
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.IsNullOrEmpty(filter) ||
                            System.IO.Path.GetFileNameWithoutExtension(p)
                                .IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p)
                .ToList();

            if (paths.Count == 0) { Debug.LogWarning($"[ContactSheet] No prefabs in {folder}"); return null; }

            // Swap to the empty render scene FIRST: opening a scene unloads unused assets, which
            // would destroy any Texture2D allocated before this point.
            var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            int rows = Mathf.CeilToInt(paths.Count / (float)columns);
            int labelH = Mathf.Max(16, cell / 8);
            var sheet = new Texture2D(columns * cell, rows * (cell + labelH), TextureFormat.RGB24, false);

            var bg = new Color(0.17f, 0.18f, 0.21f);
            var fill = Enumerable.Repeat(bg, sheet.width * sheet.height).ToArray();
            sheet.SetPixels(fill);

            // One camera + one light reused for every cell - creating them per prefab is what makes
            // naive versions of this tool take minutes.
            var rig = new GameObject("SheetRig");
            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(rig.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.orthographic = true;
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(rig.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            var rt = new RenderTexture(cell, cell, 24);
            var shot = new Texture2D(cell, cell, TextureFormat.RGB24, false);
            var labels = new List<(string name, int col, int row)>();

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                    if (src == null) continue;

                    EditorUtility.DisplayProgressBar("Contact sheet",
                        System.IO.Path.GetFileNameWithoutExtension(paths[i]), i / (float)paths.Count);

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    inst.transform.position = Vector3.zero;
                    inst.transform.rotation = Quaternion.Euler(0f, 35f, 0f);   // three-quarter view

                    var bounds = Encapsulate(inst);
                    // Frame from real bounds so tiny and huge props both fill their cell.
                    float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                    if (extent <= 0.0001f) extent = 0.5f;
                    cam.orthographicSize = extent * 1.45f;
                    cam.transform.position = bounds.center + new Vector3(0f, extent * 0.85f, -extent * 3f);
                    cam.transform.LookAt(bounds.center);

                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                    shot.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;

                    int col = i % columns, row = i / columns;
                    // Texture2D origin is bottom-left; write rows top-down so the sheet reads in order.
                    int x = col * cell;
                    int y = sheet.height - (row + 1) * (cell + labelH) + labelH;
                    sheet.SetPixels(x, y, cell, cell, shot.GetPixels());

                    labels.Add((System.IO.Path.GetFileNameWithoutExtension(paths[i]), col, row));
                    Object.DestroyImmediate(inst);
                }

                // Names are drawn as a pixel font: GUI text cannot be composited into a Texture2D,
                // and an unlabelled sheet of 132 similar props is useless for deciding anything.
                foreach (var (name, col, row) in labels)
                {
                    int x = col * cell + 4;
                    int y = sheet.height - (row + 1) * (cell + labelH) + 4;
                    TinyFont.Draw(sheet, name.Replace("_", " "), x, y, Color.white, Mathf.Max(1, cell / 130));
                }

                sheet.Apply();
                var dir = System.IO.Path.GetDirectoryName(outputPath).Replace('\\', '/');
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
                AssetDatabase.Refresh();

                Debug.Log($"[ContactSheet] {paths.Count} prefabs -> {outputPath} ({sheet.width}x{sheet.height})");
                return outputPath;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(sheet);
                rt.Release();
                Object.DestroyImmediate(rt);

                // Put the editor back where it was rather than leaving an empty untitled scene open.
                if (!string.IsNullOrEmpty(previousScene))
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        previousScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
        }

        static Bounds Encapsulate(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }

    /// <summary>Minimal 3x5 pixel font. Exists only so contact sheets can be labelled - Unity has no
    /// way to rasterise GUI text into a Texture2D from an editor script.</summary>
    static class TinyFont
    {
        // Each glyph is 5 rows of 3 bits, top row first.
        static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            ['A'] = new byte[] { 0b010, 0b101, 0b111, 0b101, 0b101 },
            ['B'] = new byte[] { 0b110, 0b101, 0b110, 0b101, 0b110 },
            ['C'] = new byte[] { 0b011, 0b100, 0b100, 0b100, 0b011 },
            ['D'] = new byte[] { 0b110, 0b101, 0b101, 0b101, 0b110 },
            ['E'] = new byte[] { 0b111, 0b100, 0b110, 0b100, 0b111 },
            ['F'] = new byte[] { 0b111, 0b100, 0b110, 0b100, 0b100 },
            ['G'] = new byte[] { 0b011, 0b100, 0b101, 0b101, 0b011 },
            ['H'] = new byte[] { 0b101, 0b101, 0b111, 0b101, 0b101 },
            ['I'] = new byte[] { 0b111, 0b010, 0b010, 0b010, 0b111 },
            ['J'] = new byte[] { 0b001, 0b001, 0b001, 0b101, 0b010 },
            ['K'] = new byte[] { 0b101, 0b101, 0b110, 0b101, 0b101 },
            ['L'] = new byte[] { 0b100, 0b100, 0b100, 0b100, 0b111 },
            ['M'] = new byte[] { 0b101, 0b111, 0b111, 0b101, 0b101 },
            ['N'] = new byte[] { 0b101, 0b111, 0b111, 0b111, 0b101 },
            ['O'] = new byte[] { 0b010, 0b101, 0b101, 0b101, 0b010 },
            ['P'] = new byte[] { 0b110, 0b101, 0b110, 0b100, 0b100 },
            ['Q'] = new byte[] { 0b010, 0b101, 0b101, 0b111, 0b011 },
            ['R'] = new byte[] { 0b110, 0b101, 0b110, 0b101, 0b101 },
            ['S'] = new byte[] { 0b011, 0b100, 0b010, 0b001, 0b110 },
            ['T'] = new byte[] { 0b111, 0b010, 0b010, 0b010, 0b010 },
            ['U'] = new byte[] { 0b101, 0b101, 0b101, 0b101, 0b011 },
            ['V'] = new byte[] { 0b101, 0b101, 0b101, 0b101, 0b010 },
            ['W'] = new byte[] { 0b101, 0b101, 0b111, 0b111, 0b101 },
            ['X'] = new byte[] { 0b101, 0b101, 0b010, 0b101, 0b101 },
            ['Y'] = new byte[] { 0b101, 0b101, 0b010, 0b010, 0b010 },
            ['Z'] = new byte[] { 0b111, 0b001, 0b010, 0b100, 0b111 },
            ['0'] = new byte[] { 0b111, 0b101, 0b101, 0b101, 0b111 },
            ['1'] = new byte[] { 0b010, 0b110, 0b010, 0b010, 0b111 },
            ['2'] = new byte[] { 0b110, 0b001, 0b010, 0b100, 0b111 },
            ['3'] = new byte[] { 0b110, 0b001, 0b010, 0b001, 0b110 },
            ['4'] = new byte[] { 0b101, 0b101, 0b111, 0b001, 0b001 },
            ['5'] = new byte[] { 0b111, 0b100, 0b110, 0b001, 0b110 },
            ['6'] = new byte[] { 0b011, 0b100, 0b110, 0b101, 0b010 },
            ['7'] = new byte[] { 0b111, 0b001, 0b010, 0b010, 0b010 },
            ['8'] = new byte[] { 0b010, 0b101, 0b010, 0b101, 0b010 },
            ['9'] = new byte[] { 0b010, 0b101, 0b011, 0b001, 0b110 },
            [' '] = new byte[] { 0, 0, 0, 0, 0 },
            ['-'] = new byte[] { 0, 0, 0b111, 0, 0 },
        };

        public static void Draw(Texture2D tex, string text, int x, int y, Color color, int scale)
        {
            text = text.ToUpperInvariant();
            int cursor = x;
            foreach (var ch in text)
            {
                if (!Glyphs.TryGetValue(ch, out var glyph)) { cursor += 4 * scale; continue; }
                for (int row = 0; row < 5; row++)
                for (int bit = 0; bit < 3; bit++)
                {
                    if ((glyph[row] & (1 << (2 - bit))) == 0) continue;
                    // glyph rows are top-down, texture is bottom-up
                    int px = cursor + bit * scale;
                    int py = y + (4 - row) * scale;
                    for (int sx = 0; sx < scale; sx++)
                    for (int sy = 0; sy < scale; sy++)
                    {
                        int fx = px + sx, fy = py + sy;
                        if (fx >= 0 && fx < tex.width && fy >= 0 && fy < tex.height)
                            tex.SetPixel(fx, fy, color);
                    }
                }
                cursor += 4 * scale;
            }
        }
    }
}
