#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Unity Build Size Optimizer v2
/// ▸ Fix All — batch-fix every auto-fixable issue in one click
/// ▸ Click any asset to Ping / Select it in the Project window
/// ▸ Detailed per-folder, per-file breakdown with sizes
///
/// Place in any Editor/ folder. Open via: Tools ▸ Build Size Optimizer
/// Shortcut: Ctrl+Shift+B
/// </summary>
public class BuildSizeOptimizer : EditorWindow
{
    // ─── Types ───────────────────────────────────────────────────
    private enum Severity { Pass, Warning, Error }

    private class CheckResult
    {
        public Severity severity;
        public string category;
        public string message;
        public string fix;
        public Action autoFix;
        public List<AssetEntry> assets;
        public bool foldout;
    }

    private struct AssetEntry
    {
        public string path;
        public string label;
        public long bytes;
    }

    // ─── State ───────────────────────────────────────────────────
    private readonly List<CheckResult> _results = new();
    private Vector2 _scroll;
    private bool _scanned;
    private int _passCount, _warnCount, _errorCount, _fixableCount;
    private string _filterCategory = "All";
    private readonly List<string> _categories = new() { "All" };

    private GUIStyle _headerStyle, _foldoutBoldStyle;
    private bool _stylesReady;

    // ─── Window ──────────────────────────────────────────────────
    [MenuItem("Tools/Build Size Optimizer %#b")]
    public static void Open()
    {
        var w = GetWindow<BuildSizeOptimizer>("Build Size Optimizer");
        w.minSize = new Vector2(520, 400);
    }

    private void InitStyles()
    {
        if (_stylesReady) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        _foldoutBoldStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        _stylesReady = true;
    }

    private void OnGUI()
    {
        InitStyles();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🔍 Build Size Optimizer v2", _headerStyle);
        EditorGUILayout.Space(4);

        // ── Action buttons ──
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("▶  Run Full Scan", GUILayout.Height(30)))
            RunScan();

        GUI.enabled = _scanned && _fixableCount > 0;
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
        if (GUILayout.Button($"⚡ Fix All ({_fixableCount})", GUILayout.Height(30), GUILayout.Width(130)))
        {
            if (EditorUtility.DisplayDialog("Fix All",
                $"Auto-fix {_fixableCount} issue(s)?\n\nThis will modify Player Settings and asset import settings.\nMake sure your project is version-controlled!",
                "Fix All", "Cancel"))
            {
                FixAll();
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        if (!_scanned)
        {
            EditorGUILayout.HelpBox(
                "Click 'Run Full Scan' to analyze your project for build-size issues.", MessageType.Info);
            return;
        }

        // ── Summary bar ──
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal("box");
        ColorLabel($"✅ {_passCount} Pass", Color.green, 90);
        ColorLabel($"⚠ {_warnCount} Warn", new Color(1f, 0.8f, 0f), 90);
        ColorLabel($"❌ {_errorCount} Error", new Color(1f, 0.35f, 0.35f), 90);
        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("Filter:", GUILayout.Width(38));
        int idx = _categories.IndexOf(_filterCategory);
        int newIdx = EditorGUILayout.Popup(idx, _categories.ToArray(), GUILayout.Width(120));
        if (newIdx != idx) _filterCategory = _categories[newIdx];

        EditorGUILayout.EndHorizontal();

        // ── Results ──
        EditorGUILayout.Space(4);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string lastCat = null;
        foreach (var r in _results)
        {
            if (_filterCategory != "All" && r.category != _filterCategory) continue;

            if (r.category != lastCat)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField($"━━  {r.category}  ━━", EditorStyles.boldLabel);
                lastCat = r.category;
            }

            DrawResult(r);
        }

        EditorGUILayout.EndScrollView();
    }

    // ─── Draw single result row ──────────────────────────────────
    private void DrawResult(CheckResult r)
    {
        var bg = r.severity switch
        {
            Severity.Error => new Color(1f, 0.2f, 0.2f, 0.08f),
            Severity.Warning => new Color(1f, 0.85f, 0f, 0.06f),
            _ => new Color(0.2f, 1f, 0.2f, 0.05f),
        };

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = bg;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        // ── Main row ──
        EditorGUILayout.BeginHorizontal();

        string icon = r.severity switch
        {
            Severity.Pass => "✅",
            Severity.Warning => "⚠️",
            _ => "❌"
        };
        EditorGUILayout.LabelField($"{icon}  {r.message}", EditorStyles.wordWrappedLabel);

        if (r.autoFix != null && r.severity != Severity.Pass)
        {
            GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
            if (GUILayout.Button("Fix", GUILayout.Width(50)))
            {
                r.autoFix();
                RunScan();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();

        // ── Fix hint ──
        if (r.severity != Severity.Pass && !string.IsNullOrEmpty(r.fix))
            EditorGUILayout.HelpBox("💡 " + r.fix, MessageType.None);

        // ── Asset list foldout ──
        if (r.assets != null && r.assets.Count > 0)
        {
            r.foldout = EditorGUILayout.Foldout(r.foldout,
                $"📂 {r.assets.Count} item(s) — click to locate", true, _foldoutBoldStyle);

            if (r.foldout)
            {
                EditorGUI.indentLevel++;
                int shown = 0;
                foreach (var a in r.assets.OrderByDescending(x => x.bytes))
                {
                    EditorGUILayout.BeginHorizontal();
                    string sizeStr = a.bytes > 0 ? $"  [{FormatBytes(a.bytes)}]" : "";

                    // Clickable link
                    if (GUILayout.Button($"▸ {a.label}{sizeStr}", EditorStyles.linkLabel))
                        PingAsset(a.path);

                    // Select button
                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                        SelectAsset(a.path);

                    EditorGUILayout.EndHorizontal();

                    if (++shown >= 50)
                    {
                        EditorGUILayout.LabelField($"  ... and {r.assets.Count - 50} more");
                        break;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndVertical();
    }

    // ─── Fix All ─────────────────────────────────────────────────
    private void FixAll()
    {
        int fixCount = 0;
        foreach (var r in _results)
        {
            if (r.autoFix != null && r.severity != Severity.Pass)
            {
                try { r.autoFix(); fixCount++; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BuildOptimizer] Auto-fix failed: {e.Message}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildOptimizer] Applied {fixCount} auto-fix(es). Re-scanning...");
        RunScan();
    }

    // ═══════════════════════════════════════════════════════════════
    //  SCANNER
    // ═══════════════════════════════════════════════════════════════
    private void RunScan()
    {
        _results.Clear();
        _categories.Clear();
        _categories.Add("All");
        _passCount = _warnCount = _errorCount = _fixableCount = 0;

        EditorUtility.DisplayProgressBar("Build Size Optimizer", "Scanning...", 0f);
        try
        {
            Step(0.05f, "Code settings", CheckScriptingBackend);
            Step(0.08f, "Code settings", CheckStrippingLevel);
            Step(0.10f, "Code settings", CheckStripEngineCode);
            Step(0.12f, "Code settings", CheckIncrementalGC);
            Step(0.20f, "Textures", CheckTextures);
            Step(0.40f, "Audio", CheckAudio);
            Step(0.55f, "Meshes", CheckMeshes);
            Step(0.65f, "Resources", CheckResourcesFolders);
            Step(0.72f, "Duplicates", CheckDuplicateAssets);
            Step(0.80f, "Shaders", CheckShaderVariants);
            Step(0.85f, "Lighting", CheckLightmaps);
            Step(0.90f, "Video", CheckVideoClips);
            Step(0.93f, "Fonts", CheckFonts);
            Step(0.96f, "Plugins", CheckPlugins);
        }
        finally { EditorUtility.ClearProgressBar(); }

        foreach (var r in _results)
        {
            switch (r.severity)
            {
                case Severity.Pass: _passCount++; break;
                case Severity.Warning: _warnCount++; break;
                case Severity.Error: _errorCount++; break;
            }
            if (r.autoFix != null && r.severity != Severity.Pass) _fixableCount++;
            if (!_categories.Contains(r.category)) _categories.Add(r.category);
        }

        _scanned = true;
        Repaint();
    }

    private static void Step(float progress, string label, Action action)
    {
        EditorUtility.DisplayProgressBar("Build Size Optimizer", label, progress);
        action();
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. SCRIPTING BACKEND
    // ═══════════════════════════════════════════════════════════════
    private void CheckScriptingBackend()
    {
        var g = CurrentBuildTargetGroup();
        var b = PlayerSettings.GetScriptingBackend(g);

        if (b == ScriptingImplementation.IL2CPP)
            Add(Severity.Pass, "Code", "Scripting Backend = IL2CPP");
        else
            Add(Severity.Error, "Code",
                "Scripting Backend is Mono — IL2CPP produces smaller & faster builds.",
                "Player Settings ▸ Scripting Backend ▸ IL2CPP",
                () => PlayerSettings.SetScriptingBackend(g, ScriptingImplementation.IL2CPP));
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. MANAGED STRIPPING LEVEL
    // ═══════════════════════════════════════════════════════════════
    private void CheckStrippingLevel()
    {
        var g = CurrentBuildTargetGroup();
        var l = PlayerSettings.GetManagedStrippingLevel(g);

        if (l >= ManagedStrippingLevel.High)
            Add(Severity.Pass, "Code", $"Managed Stripping Level = {l}");
        else
            Add(Severity.Warning, "Code",
                $"Managed Stripping Level = {l}. Set to High for smaller builds.",
                "Player Settings ▸ Other ▸ Managed Stripping Level ▸ High\n⚠ Test thoroughly — may strip code used via reflection.",
                () => PlayerSettings.SetManagedStrippingLevel(g, ManagedStrippingLevel.High));
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. STRIP ENGINE CODE
    // ═══════════════════════════════════════════════════════════════
    private void CheckStripEngineCode()
    {
#if UNITY_2021_1_OR_NEWER
        if (PlayerSettings.stripEngineCode)
            Add(Severity.Pass, "Code", "Strip Engine Code = ON");
        else
            Add(Severity.Warning, "Code",
                "Strip Engine Code is OFF — enabling removes unused engine modules.",
                "Player Settings ▸ Other ▸ Strip Engine Code",
                () => PlayerSettings.stripEngineCode = true);
#endif
    }

    // ═══════════════════════════════════════════════════════════════
    //  4. INCREMENTAL GC
    // ═══════════════════════════════════════════════════════════════
    private void CheckIncrementalGC()
    {
        if (PlayerSettings.gcIncremental)
            Add(Severity.Pass, "Code", "Incremental GC = ON");
        else
            Add(Severity.Warning, "Code",
                "Incremental GC is OFF.",
                "Player Settings ▸ Other ▸ Use Incremental GC",
                () => PlayerSettings.gcIncremental = true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  5. TEXTURES — per-asset breakdown + batch fix
    // ═══════════════════════════════════════════════════════════════
    private void CheckTextures()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D");
        var rwList = new List<AssetEntry>();
        var bigList = new List<AssetEntry>();
        var rawList = new List<AssetEntry>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            long bytes = SafeFileSize(path);

            if (imp.isReadable)
                rwList.Add(new AssetEntry { path = path, label = path, bytes = bytes });

            if (imp.maxTextureSize > 2048)
                bigList.Add(new AssetEntry
                { path = path, label = $"{path}  (max {imp.maxTextureSize}px)", bytes = bytes });

            var s = imp.GetDefaultPlatformTextureSettings();
            if (s.textureCompression == TextureImporterCompression.Uncompressed)
                rawList.Add(new AssetEntry { path = path, label = path, bytes = bytes });
        }

        // Read/Write
        if (rwList.Count == 0)
            Add(Severity.Pass, "Textures", "No textures with Read/Write enabled");
        else
            Add(Severity.Error, "Textures",
                $"{rwList.Count} texture(s) have Read/Write ON — doubles VRAM per texture.",
                "Disable unless you call GetPixel/SetPixel at runtime.\nAuto-fix disables Read/Write on all listed textures.",
                () => BatchFixTextures(rwList, rw: true), rwList);

        // Oversized
        if (bigList.Count == 0)
            Add(Severity.Pass, "Textures", "No textures > 2048px");
        else
            Add(Severity.Warning, "Textures",
                $"{bigList.Count} texture(s) exceed 2048px.",
                "Auto-fix caps at 2048. Only hero/splash art should be larger.",
                () => BatchFixTextures(bigList, maxSize: true), bigList);

        // Uncompressed
        if (rawList.Count == 0)
            Add(Severity.Pass, "Textures", "All textures are compressed");
        else
            Add(Severity.Error, "Textures",
                $"{rawList.Count} texture(s) are Uncompressed!",
                "Auto-fix sets Compressed. For mobile also set ASTC per-platform override.",
                () => BatchFixTextures(rawList, compress: true), rawList);
    }

    private void BatchFixTextures(List<AssetEntry> list,
        bool rw = false, bool maxSize = false, bool compress = false)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var imp = AssetImporter.GetAtPath(list[i].path) as TextureImporter;
            if (imp == null) continue;
            if (rw) imp.isReadable = false;
            if (maxSize) imp.maxTextureSize = 2048;
            if (compress) imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
            if (i % 20 == 0)
                EditorUtility.DisplayProgressBar("Fixing textures...", list[i].path, (float)i / list.Count);
        }
        EditorUtility.ClearProgressBar();
    }

    // ═══════════════════════════════════════════════════════════════
    //  6. AUDIO — per-asset breakdown + batch fix
    // ═══════════════════════════════════════════════════════════════
    private void CheckAudio()
    {
        var guids = AssetDatabase.FindAssets("t:AudioClip");
        var pcmList = new List<AssetEntry>();
        var stereoList = new List<AssetEntry>();
        var hiSrList = new List<AssetEntry>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;

            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            long bytes = SafeFileSize(path);

            if (imp.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM)
                pcmList.Add(new AssetEntry { path = path, label = path, bytes = bytes });

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                if (clip.channels > 1 && clip.length < 5f)
                    stereoList.Add(new AssetEntry
                    { path = path, label = $"{path}  ({clip.length:F1}s, {clip.channels}ch)", bytes = bytes });

                if (clip.frequency > 44100)
                    hiSrList.Add(new AssetEntry
                    { path = path, label = $"{path}  ({clip.frequency} Hz)", bytes = bytes });
            }
        }

        if (pcmList.Count == 0)
            Add(Severity.Pass, "Audio", "No uncompressed PCM audio");
        else
            Add(Severity.Warning, "Audio",
                $"{pcmList.Count} clip(s) use PCM (uncompressed).",
                "Auto-fix → Vorbis quality 70%. Use ADPCM for very short SFX manually.",
                () => BatchFixAudio(pcmList, fixPCM: true), pcmList);

        if (stereoList.Count == 0)
            Add(Severity.Pass, "Audio", "No short stereo SFX");
        else
            Add(Severity.Warning, "Audio",
                $"{stereoList.Count} short clip(s) are stereo — mono saves ~50%.",
                "Auto-fix → Force To Mono.",
                () => BatchFixAudio(stereoList, fixStereo: true), stereoList);

        if (hiSrList.Count == 0)
            Add(Severity.Pass, "Audio", "No audio above 44100 Hz");
        else
            Add(Severity.Warning, "Audio",
                $"{hiSrList.Count} clip(s) exceed 44100 Hz.",
                "Override to 22050 Hz for SFX, 44100 for music. (Manual fix recommended)",
                null, hiSrList);
    }

    private void BatchFixAudio(List<AssetEntry> list, bool fixPCM = false, bool fixStereo = false)
    {
        foreach (var a in list)
        {
            var imp = AssetImporter.GetAtPath(a.path) as AudioImporter;
            if (imp == null) continue;
            if (fixPCM)
            {
                var s = imp.defaultSampleSettings;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                imp.defaultSampleSettings = s;
            }
            if (fixStereo) imp.forceToMono = true;
            imp.SaveAndReimport();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  7. MESHES — per-asset breakdown + batch fix
    // ═══════════════════════════════════════════════════════════════
    private void CheckMeshes()
    {
        var guids = AssetDatabase.FindAssets("t:Model");
        var rwList = new List<AssetEntry>();
        var compList = new List<AssetEntry>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;

            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            long bytes = SafeFileSize(path);

            if (imp.isReadable)
                rwList.Add(new AssetEntry { path = path, label = path, bytes = bytes });
            if (imp.meshCompression == ModelImporterMeshCompression.Off)
                compList.Add(new AssetEntry { path = path, label = path, bytes = bytes });
        }

        if (rwList.Count == 0)
            Add(Severity.Pass, "Meshes", "No meshes with Read/Write enabled");
        else
            Add(Severity.Warning, "Meshes",
                $"{rwList.Count} mesh(es) have Read/Write ON.",
                "Auto-fix disables Read/Write. Keep ON only for runtime mesh access.",
                () => BatchFixMeshes(rwList, fixRW: true), rwList);

        if (compList.Count == 0)
            Add(Severity.Pass, "Meshes", "All meshes have compression");
        else
            Add(Severity.Warning, "Meshes",
                $"{compList.Count} mesh(es) have no compression.",
                "Auto-fix → Medium compression.",
                () => BatchFixMeshes(compList, fixComp: true), compList);
    }

    private void BatchFixMeshes(List<AssetEntry> list, bool fixRW = false, bool fixComp = false)
    {
        foreach (var a in list)
        {
            var imp = AssetImporter.GetAtPath(a.path) as ModelImporter;
            if (imp == null) continue;
            if (fixRW) imp.isReadable = false;
            if (fixComp) imp.meshCompression = ModelImporterMeshCompression.Medium;
            imp.SaveAndReimport();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  8. RESOURCES/ — per-folder + heavy files breakdown
    // ═══════════════════════════════════════════════════════════════
    private void CheckResourcesFolders()
    {
        if (!Directory.Exists("Assets")) return;
        var dirs = Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories);

        if (dirs.Length == 0)
        {
            Add(Severity.Pass, "Resources", "No Resources/ folders — great!");
            return;
        }

        var entries = new List<AssetEntry>();
        long grandTotal = 0;

        foreach (var dir in dirs)
        {
            string relDir = dir.Replace("\\", "/");
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).ToList();
            long folderBytes = files.Sum(f => new FileInfo(f).Length);
            grandTotal += folderBytes;

            // Add folder header
            entries.Add(new AssetEntry
            {
                path = relDir,
                label = $"📁 {relDir}/  ({files.Count} files, {FormatBytes(folderBytes)})",
                bytes = folderBytes
            });

            // Add top heavy files inside this folder (> 100 KB)
            foreach (var f in files.OrderByDescending(f => new FileInfo(f).Length).Take(20))
            {
                var fi = new FileInfo(f);
                if (fi.Length > 100 * 1024)
                {
                    string relPath = f.Replace("\\", "/");
                    entries.Add(new AssetEntry
                    {
                        path = relPath,
                        label = $"    ▸ {relPath}",
                        bytes = fi.Length
                    });
                }
            }
        }

        float mb = grandTotal / (1024f * 1024f);
        var sev = mb > 20 ? Severity.Error : mb > 5 ? Severity.Warning : Severity.Pass;

        Add(sev, "Resources",
            $"{dirs.Length} Resources/ folder(s), total {FormatBytes(grandTotal)}. ALL included in build!",
            "Migrate heavy assets to Addressables or AssetBundles.\nResources/ should only hold tiny essentials (< 1 MB ideally).",
            null, entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  9. DUPLICATE TEXTURES
    // ═══════════════════════════════════════════════════════════════
    private void CheckDuplicateAssets()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D");
        var map = new Dictionary<string, List<string>>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!map.ContainsKey(name)) map[name] = new();
            map[name].Add(path);
        }

        var dupes = map.Where(kv => kv.Value.Count > 1).ToList();

        if (dupes.Count == 0)
        {
            Add(Severity.Pass, "Assets", "No duplicate texture names detected");
            return;
        }

        var entries = new List<AssetEntry>();
        foreach (var kv in dupes.OrderByDescending(kv => kv.Value.Count))
        {
            foreach (var p in kv.Value)
            {
                entries.Add(new AssetEntry
                {
                    path = p,
                    label = $"[{kv.Key}]  {p}",
                    bytes = SafeFileSize(p)
                });
            }
        }

        Add(Severity.Warning, "Assets",
            $"{dupes.Count} texture name(s) found in multiple locations (possible duplicates).",
            "Review and consolidate. Same-name textures in different folders = wasted space.",
            null, entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  10. SHADER VARIANTS
    // ═══════════════════════════════════════════════════════════════
    private void CheckShaderVariants()
    {
        var guids = AssetDatabase.FindAssets("t:ShaderVariantCollection");

        if (guids.Length > 0)
        {
            var entries = guids.Select(g =>
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                return new AssetEntry { path = p, label = p, bytes = 0 };
            }).ToList();
            Add(Severity.Pass, "Shaders", $"Found {guids.Length} ShaderVariantCollection(s)", assets: entries);
        }
        else
        {
            Add(Severity.Warning, "Shaders",
                "No ShaderVariantCollection found — Unity may include ALL shader variants.",
                "Create: Assets ▸ Create ▸ Shader Variant Collection.\n" +
                "Also review: Project Settings ▸ Graphics ▸ Shader Stripping.");
        }

        Add(Severity.Warning, "Shaders",
            "Review 'Always Included Shaders' in Graphics Settings.",
            "Project Settings ▸ Graphics ▸ remove any shaders not needed on every scene.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  11. LIGHTMAPS
    // ═══════════════════════════════════════════════════════════════
    private void CheckLightmaps()
    {
        var guids = AssetDatabase.FindAssets("Lightmap- t:Texture2D");

        if (guids.Length == 0) { Add(Severity.Pass, "Lighting", "No baked lightmaps"); return; }

        var entries = new List<AssetEntry>();
        long total = 0;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            long b = SafeFileSize(p);
            total += b;
            entries.Add(new AssetEntry { path = p, label = p, bytes = b });
        }

        float mb = total / (1024f * 1024f);
        Add(mb > 50 ? Severity.Warning : Severity.Pass, "Lighting",
            $"{guids.Length} lightmap texture(s) ({FormatBytes(total)}).",
            mb > 50 ? "Reduce Lightmap Resolution or use Light Probes + real-time GI." : "",
            null, entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  12. VIDEO CLIPS
    // ═══════════════════════════════════════════════════════════════
    private void CheckVideoClips()
    {
        var guids = AssetDatabase.FindAssets("t:VideoClip");
        if (guids.Length == 0) { Add(Severity.Pass, "Video", "No video clips"); return; }

        var entries = new List<AssetEntry>();
        long total = 0;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            long b = SafeFileSize(p);
            total += b;
            entries.Add(new AssetEntry { path = p, label = p, bytes = b });
        }

        Add(Severity.Warning, "Video",
            $"{guids.Length} video clip(s) ({FormatBytes(total)}).",
            "Stream from CDN or use Addressables. Compress externally with H.264/H.265 before import.",
            null, entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  13. FONTS
    // ═══════════════════════════════════════════════════════════════
    private void CheckFonts()
    {
        var guids = AssetDatabase.FindAssets("t:Font");
        var entries = new List<AssetEntry>();
        long total = 0;

        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.StartsWith("Packages/")) continue;
            long b = SafeFileSize(p);
            total += b;
            entries.Add(new AssetEntry { path = p, label = p, bytes = b });
        }

        float mb = total / (1024f * 1024f);
        if (mb < 2)
            Add(Severity.Pass, "Fonts", $"{entries.Count} font(s) ({FormatBytes(total)})", assets: entries);
        else
            Add(Severity.Warning, "Fonts",
                $"{entries.Count} font(s) totaling {FormatBytes(total)}.",
                "Use TMP Font Asset Creator with only needed Unicode ranges.\nRemove unused font weights (Bold, Light, etc.).",
                null, entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  14. PLUGINS
    // ═══════════════════════════════════════════════════════════════
    private void CheckPlugins()
    {
        var guids = AssetDatabase.FindAssets("t:PluginImporter");
        var badPlugins = new List<AssetEntry>();

        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.StartsWith("Packages/")) continue;

            var imp = AssetImporter.GetAtPath(p) as PluginImporter;
            if (imp == null) continue;

            if (imp.GetCompatibleWithAnyPlatform())
            {
                badPlugins.Add(new AssetEntry { path = p, label = p, bytes = SafeFileSize(p) });
            }
        }

        if (badPlugins.Count == 0)
            Add(Severity.Pass, "Plugins", "No plugins set to 'Any Platform'");
        else
            Add(Severity.Warning, "Plugins",
                $"{badPlugins.Count} plugin(s) enabled for 'Any Platform'.",
                "Set specific platform targets per plugin to avoid bundling unnecessary native libs.",
                null, badPlugins);
    }

    // ─── Utilities ───────────────────────────────────────────────
    private void Add(Severity s, string cat, string msg, string fix = "",
        Action autoFix = null, List<AssetEntry> assets = null)
    {
        _results.Add(new CheckResult
        {
            severity = s,
            category = cat,
            message = msg,
            fix = fix,
            autoFix = autoFix,
            assets = assets ?? new(),
            foldout = false
        });
    }

    private static BuildTargetGroup CurrentBuildTargetGroup()
        => BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

    private static void PingAsset(string path)
    {
        var obj = AssetDatabase.LoadMainAssetAtPath(path);
        if (obj != null) EditorGUIUtility.PingObject(obj);
        if (Directory.Exists(path))
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (folder != null) EditorGUIUtility.PingObject(folder);
        }
    }

    private static void SelectAsset(string path)
    {
        var obj = AssetDatabase.LoadMainAssetAtPath(path);
        if (obj != null)
        {
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }

    private static long SafeFileSize(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
        return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
    }

    private static void ColorLabel(string text, Color c, float width)
    {
        var prev = GUI.color;
        GUI.color = c;
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel, GUILayout.Width(width));
        GUI.color = prev;
    }
}
#endif