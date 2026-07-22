using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Scrubs the enemy shader's dissolve and hit-flash on a live instance, without entering Play Mode.
    ///
    /// Why an editor tool rather than a PlayMode test: both effects are per-instance shader values
    /// written through a MaterialPropertyBlock, so the only meaningful check is a visual one - does
    /// the burn edge read, does the mesh clear completely at 1, does the flash wash out the albedo.
    /// A slider you can drag beats a pass/fail assertion for that.
    ///
    /// It writes exactly what ZombieBase writes at runtime (_Dissolve, _HitFlash on the Visual's
    /// MeshRenderer), so what you see here is what the pooled enemy will do on death.
    ///
    /// Menu: Tools/ZombieWar/Dissolve Test.
    /// </summary>
    public class DissolveTestWindow : EditorWindow
    {
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");

        GameObject _prefab;
        GameObject _instance;
        float _dissolve;
        float _flash;

        bool _playing;
        double _startTime;
        bool _playingFlash;
        bool _showConfig = true;
        bool _configDirty;

        MaterialPropertyBlock _block;

        [MenuItem("Tools/ZombieWar/Dissolve Test")]
        public static void Open() => GetWindow<DissolveTestWindow>("Dissolve Test");

        void OnEnable()
        {
            _block ??= new MaterialPropertyBlock();
            if (_prefab == null) _prefab = FirstEnemyPrefab();
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            DestroyInstance();
        }

        static GameObject FirstEnemyPrefab() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("ENM_"))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault();

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Spawns the enemy in the open scene and drives the same shader values ZombieBase " +
                "writes at runtime. Works outside Play Mode.", MessageType.None);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _prefab = (GameObject)EditorGUILayout.ObjectField("Enemy prefab", _prefab, typeof(GameObject), false);
                if (check.changed && _instance != null) Respawn();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_instance == null ? "Spawn" : "Respawn")) Respawn();
                using (new EditorGUI.DisabledScope(_instance == null))
                    if (GUILayout.Button("Remove")) DestroyInstance();
            }

            if (_instance == null)
            {
                EditorGUILayout.HelpBox("No instance yet. Press Spawn.", MessageType.Info);
                return;
            }

            if (Renderer == null)
            {
                EditorGUILayout.HelpBox("Instance has no MeshRenderer - not a baked VAT enemy?", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual scrub", EditorStyles.boldLabel);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _dissolve = EditorGUILayout.Slider("Dissolve", _dissolve, 0f, 1f);
                _flash = EditorGUILayout.Slider("Hit flash", _flash, 0f, 1f);
                if (check.changed) { _playing = _playingFlash = false; Apply(); }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animate", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Durations come from the shared config, so what you preview is what ships.",
                EditorStyles.miniLabel);

            // Two independent timelines: a death dissolve and a per-bullet flash are tuned against
            // completely different feels, so sharing one slider made both impossible to judge.
            var cfg = VatLookApplier.LoadOrCreateConfig();
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                float dissolveDur = EditorGUILayout.Slider("Dissolve duration (s)", cfg.dissolveDuration, 0.05f, 4f);
                float flashDur = EditorGUILayout.Slider("Hit flash duration (s)", cfg.hitFlashDuration, 0.02f, 1f);
                if (check.changed)
                {
                    cfg.dissolveDuration = dissolveDur;
                    cfg.hitFlashDuration = flashDur;
                    EditorUtility.SetDirty(cfg);
                    _configDirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play dissolve 0 → 1")) StartPlay(false);
                if (GUILayout.Button("Play hit flash 1 → 0")) StartPlay(true);
            }

            if (GUILayout.Button("Reset (solid, no flash)"))
            {
                _playing = _playingFlash = false;
                _dissolve = _flash = 0f;
                Apply();
            }

            EditorGUILayout.Space();
            var mat = Renderer.sharedMaterial;
            EditorGUILayout.LabelField("Material", mat != null ? mat.shader.name : "none");
            if (mat != null && mat.shader.name != "ZombieWar/VAT/EnemyToon")
                EditorGUILayout.HelpBox(
                    "This material is not the enemy shader, so _Dissolve/_HitFlash will do nothing. " +
                    "Re-run Tools/ZombieWar/Bake Enemies (VAT).", MessageType.Warning);

            DrawSharedLook();

            if (_playing) Repaint();
        }

        /// <summary>
        /// Edits the shared <see cref="VatLookConfig"/> and pushes it to EVERY enemy material.
        ///
        /// Lives in this window on purpose: tuning the burn is a look-at-it-while-you-drag job, and
        /// the whole point of the config is that the value you settle on applies to the entire
        /// roster rather than just the enemy you happened to be testing.
        /// </summary>
        void DrawSharedLook()
        {
            EditorGUILayout.Space();
            _showConfig = EditorGUILayout.Foldout(_showConfig, "Shared look (all VAT enemies)", true);
            if (!_showConfig) return;

            var cfg = VatLookApplier.LoadOrCreateConfig();
            var so = new SerializedObject(cfg);
            so.Update();

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var name in new[]
                {
                    "specSteps", "specSize", "specIntensity",
                    "dissolveNoise", "dissolveNoiseTiling", "dissolveEdgeColor", "dissolveEdgeWidth",
                    "dissolveDuration", "hitFlashColor", "hitFlashDuration",
                })
                {
                    var prop = so.FindProperty(name);
                    if (prop != null) EditorGUILayout.PropertyField(prop);
                }

                // Live preview: push straight to the materials on the same frame the value changes,
                // WITHOUT saving. Previously this only set a dirty flag, so nothing moved until you
                // pressed Apply - which made tuning (especially swapping the noise texture) blind.
                if (so.ApplyModifiedProperties())
                {
                    VatLookApplier.Apply(cfg, save: false);
                    _configDirty = true;
                    SceneView.RepaintAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_configDirty ? "Save + apply to ALL enemies *" : "Save + apply to ALL enemies"))
                    {
                        VatLookApplier.Apply(cfg, save: true);
                        _configDirty = false;
                    }
                    if (GUILayout.Button("Select config", GUILayout.Width(100)))
                        Selection.activeObject = cfg;
                }

                EditorGUILayout.HelpBox(
                    _configDirty
                        ? "Previewing live on all enemy materials. Press Save to write it to disk " +
                          "(durations only reach the prefabs on Save)."
                        : "Saved. Materials and prefab durations match the config.",
                    _configDirty ? MessageType.Info : MessageType.None);
            }
        }

        Renderer Renderer =>
            _instance != null ? _instance.GetComponentInChildren<MeshRenderer>(true) : null;

        void StartPlay(bool flash)
        {
            _playing = true;
            _playingFlash = flash;
            _startTime = EditorApplication.timeSinceStartup;
        }

        void Tick()
        {
            if (!_playing || _instance == null) return;

            var cfg = VatLookApplier.LoadOrCreateConfig();
            float duration = Mathf.Max(0.01f,
                _playingFlash ? cfg.hitFlashDuration : cfg.dissolveDuration);

            float t = Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - _startTime) / duration);
            if (_playingFlash) _flash = 1f - t;
            else _dissolve = t;

            Apply();
            if (t >= 1f) _playing = false;
        }

        /// <summary>Read-modify-write, exactly like ZombieBase - so dissolve, flash and the
        /// VAT_Animator's own animation-time writes on this renderer never clobber each other.</summary>
        void Apply()
        {
            var renderer = Renderer;
            if (renderer == null) return;

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(DissolveId, _dissolve);
            _block.SetFloat(HitFlashId, _flash);
            renderer.SetPropertyBlock(_block);

            SceneView.RepaintAll();
        }

        void Respawn()
        {
            DestroyInstance();
            if (_prefab == null) return;

            _instance = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
            _instance.name = _prefab.name + " (dissolve test)";
            _instance.transform.position = Vector3.zero;
            _dissolve = _flash = 0f;
            _playing = _playingFlash = false;
            Apply();

            Selection.activeGameObject = _instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        void DestroyInstance()
        {
            if (_instance == null) return;
            DestroyImmediate(_instance);
            _instance = null;
        }
    }
}
