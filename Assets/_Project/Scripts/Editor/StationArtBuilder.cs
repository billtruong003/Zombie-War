using UnityEditor;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Phase A9b (owner-picked concepts, 2026-09-30): the station bodies from
    /// Review/M8/station_detail.blend — the hex pad (Relay, Cache and Beacon, told apart by the colour
    /// of its lines), the crystal plinth (Heal Zone) and the drop pod (Supply Drop). Each model's
    /// "*_Glow" / "*_Spin" children get the energy-line material; the body keeps the shared toon
    /// palette. A StationVisual on the prefab drives colour, pulse, fill and the dissolve.
    /// Gathered into Resources/StationArt.asset. Idempotent: re-run after re-exporting from Blender.
    /// </summary>
    public static class StationArtBuilder
    {
        const string ModelDir = "Assets/_Project/Art/Models/Stations/";
        const string PrefabDir = "Assets/_Project/Prefabs/Stations/";
        const string MatDir = "Assets/_Project/Materials/Stations/";
        const string ArtPath = "Assets/_Project/Resources/StationArt.asset";
        const string Etfx = "Assets/ThirdParty/Epic Toon FX/Prefabs/";
        const string Noise = "Assets/_Project/Art/Textures/FX/tex_fx_toon_pack.png";
        const string Palette = "Assets/_Project/Art/Textures/T_HC_Palette.png";

        [MenuItem("HordeCall/Stations/Build Stations")]
        public static string Build()
        {
            var pal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Skills/M_SK_Palette.mat");
            if (pal == null) return "missing M_SK_Palette (run Build Skill Models)";
            System.IO.Directory.CreateDirectory(MatDir);
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(Noise);

            var energy = LoadOrCreate(MatDir + "M_ST_Energy.mat", "HordeCall/Station/Energy");
            energy.SetTexture("_NoiseTex", noise);
            energy.SetFloat("_Intensity", 3.2f);   // measured in play: thinner or dimmer lines vanish on the lit metal
            energy.SetFloat("_IdleLevel", 0.55f);
            EditorUtility.SetDirty(energy);
            var dissolve = LoadOrCreate(MatDir + "M_ST_Dissolve.mat", "HordeCall/Station/Dissolve");
            dissolve.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Palette));
            dissolve.SetTexture("_NoiseTex", noise);
            EditorUtility.SetDirty(dissolve);

            var pad = Body("ST_HexPad", pal, energy);
            var plinth = Body("ST_Plinth", pal, energy);
            var pod = Body("ST_DropPod", pal, energy);

            var art = AssetDatabase.LoadAssetAtPath<StationArt>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<StationArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.looks = new[]
            {
                Look(StationKind.SignalRelay, pad, "Blue"),
                Look(StationKind.SupplyCache, pad, "Yellow"),
                Look(StationKind.BossBeacon, pad, "Red"),
                Look(StationKind.SupplyDrop, pod, "Purple"),
                Look(StationKind.HealZone, plinth, "Green"),
            };
            art.dissolveMaterial = dissolve;
            art.healFieldFx = Fx("Interactive/Healing/HealField2");
            art.healFieldNativeRadius = 4.4f;   // measured: its circle is ~9 m across at scale 1
            art.healTickFx = Fx("Interactive/Healing/HealOnce");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return "built ST_HexPad, ST_Plinth, ST_DropPod, M_ST_Energy, M_ST_Dissolve, StationArt";
        }

        static Material LoadOrCreate(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new System.Exception("shader not found: " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            return m;
        }

        static StationArt.Look Look(StationKind kind, GameObject body, string colour) => new()
        {
            kind = kind,
            body = body,
            rewardIcon = null,   // A9b: no floating icon; the model and its colour say what it is
            completeFx = Fx("Interactive/Level Up/Cylinder/LevelupCylinder" + colour),
        };

        static GameObject Body(string name, Material pal, Material energy)
        {
            string fbx = ModelDir + name + ".fbx";
            if (AssetImporter.GetAtPath(fbx) is ModelImporter mi)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                mi.SaveAndReimport();
            }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new System.Exception("missing " + fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    bool glow = r.name.EndsWith("_Glow") || r.name.EndsWith("_Spin");
                    int subs = r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
                    var mats = new Material[Mathf.Max(1, subs)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = glow ? energy : pal;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = glow ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = !glow;
                }
                if (go.GetComponent<StationVisual>() == null) go.AddComponent<StationVisual>();
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static ParticleSystem Fx(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Etfx + rel + ".prefab");
            if (go == null) throw new System.Exception("missing FX " + rel);
            return go.GetComponent<ParticleSystem>();
        }
    }
}
