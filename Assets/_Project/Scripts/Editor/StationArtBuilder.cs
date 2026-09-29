using UnityEditor;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Phase A9: the sci-fi station bodies (Review/M8/skill_models.blend, shared palette, each with a
    /// "_Spin" part that turns), the Supply Drop crate (KayKit, with the pack's violet flare), reward
    /// icons, completion bursts and the Heal Zone field, gathered into Resources/StationArt.asset.
    /// Idempotent: re-run after re-exporting from Blender.
    /// </summary>
    public static class StationArtBuilder
    {
        const string ModelDir = "Assets/_Project/Art/Models/Stations/";
        const string PrefabDir = "Assets/_Project/Prefabs/Stations/";
        const string ArtPath = "Assets/_Project/Resources/StationArt.asset";
        const string Etfx = "Assets/ThirdParty/Epic Toon FX/Prefabs/";
        const string Icons = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/256/";
        const string Crate = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs/Containers_Crate_Small_Green.prefab";

        [MenuItem("HordeCall/Stations/Build A9 Stations")]
        public static string Build()
        {
            var pal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Skills/M_SK_Palette.mat");
            if (pal == null) return "missing M_SK_Palette (run Build Skill Models)";

            var relay = Body("ST_Relay", pal, 45f);
            var cache = Body("ST_Cache", pal, 120f);
            var beacon = Body("ST_Beacon", pal, 90f);
            var heal = Body("ST_Heal", pal, 70f);
            var drop = SupplyDrop();

            var art = AssetDatabase.LoadAssetAtPath<StationArt>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<StationArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.looks = new[]
            {
                Look(StationKind.SignalRelay, relay, "ItemIcon_Star_Gold.Png", "Blue"),
                Look(StationKind.SupplyCache, cache, "ItemIcon_Shop.Png", "Yellow"),
                Look(StationKind.BossBeacon, beacon, "ItemIcon_Skull.png", "Red"),
                Look(StationKind.SupplyDrop, drop, "ItemIcon_Gift_Green.Png", "Purple"),
                Look(StationKind.HealZone, heal, "ItemIcon_Heart_Red.Png", "Green"),
            };
            art.healFieldFx = Fx("Interactive/Healing/HealField2");
            art.healFieldNativeRadius = 4.4f;   // measured: its circle is ~9 m across at scale 1
            art.healTickFx = Fx("Interactive/Healing/HealOnce");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return "built ST_Relay, ST_Cache, ST_Beacon, ST_Heal, ST_SupplyDrop, StationArt";
        }

        static StationArt.Look Look(StationKind kind, GameObject body, string icon, string colour) => new()
        {
            kind = kind,
            body = body,
            rewardIcon = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + icon),
            completeFx = Fx("Interactive/Level Up/Cylinder/LevelupCylinder" + colour),
        };

        static GameObject Body(string name, Material pal, float spinDegrees)
        {
            string fbx = ModelDir + name + ".fbx";
            if (AssetImporter.GetAtPath(fbx) is ModelImporter mi)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                mi.SaveAndReimport();
            }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src == null) throw new System.Exception("missing " + fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = new[] { pal };
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name.EndsWith("_Spin"))
                    {
                        var spin = t.gameObject.AddComponent<ZombieWar.Skills.Powers.SpinWhileAlive>();
                        var so = new SerializedObject(spin);
                        so.FindProperty("degreesPerSecond").vector3Value = new Vector3(0f, spinDegrees, 0f);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// The Supply Drop: KayKit's military crate with a violet signal flare burning on it.
        static GameObject SupplyDrop()
        {
            var root = new GameObject("ST_SupplyDrop");
            try
            {
                var crate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Crate), root.transform);
                PrefabUtility.UnpackPrefabInstance(crate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                crate.name = "Crate";
                crate.transform.localScale = Vector3.one * 2f;   // ~1.6 m: a drop the player can spot
                crate.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                foreach (var c in crate.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                var flare = (GameObject)PrefabUtility.InstantiatePrefab(Fx("Interactive/Flares/Soft/FlareSoftPurple").gameObject, root.transform);
                PrefabUtility.UnpackPrefabInstance(flare, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                flare.name = "Flare";
                flare.transform.localPosition = new Vector3(0.45f, 1.05f, -0.2f);
                foreach (var ps in flare.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main; m.playOnAwake = true; m.loop = true;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "ST_SupplyDrop.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static ParticleSystem Fx(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Etfx + rel + ".prefab");
            if (go == null) throw new System.Exception("missing FX " + rel);
            return go.GetComponent<ParticleSystem>();
        }
    }
}
