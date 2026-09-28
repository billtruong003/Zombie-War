using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools.Weapons
{
    /// <summary>
    /// Finishes the vendor guns that were onboarded as data only (owner 2026-09-28: "the remaining
    /// guns, add them in full"). For each one it places the grip, support and muzzle markers,
    /// sets the hold offset, copies FX from a shipped gun of the same class, and gives it a name,
    /// a tier, stats in that tier's DPS band and a price from the signed-off table.
    ///
    /// Markers are measured, not guessed: the owner's 25 authored guns put the right wrist about
    /// 14 cm behind and 6 cm below the front of the trigger, the left wrist about 25 cm ahead of
    /// the right under the handguard, and the right wrist at the same spot in the rig for every
    /// long gun (and another spot for every pistol). This tool applies those rules to each
    /// model's own Trigger/Barrel parts. The owner can still fine-tune any gun with the
    /// Authoring Queue / WeaponPoseAuthoring workflow.
    /// </summary>
    public static class WeaponAutoGrip
    {
        // Measured from the authored guns (world metres, in the rig).
        const float WristBehindTrigger = 0.14f;
        const float WristBelowTrigger = 0.06f;
        const float WristSide = 0.033f;
        const float SupportAhead = 0.25f;
        static readonly Vector3 LongGunWrist = new(0.18f, -0.17f, -0.02f);
        static readonly Vector3 PistolWrist = new(0.068f, 0.095f, 0.31f);

        public class Spec
        {
            public string id, name; public WeaponTier tier; public bool twoHanded = true;
            public WeaponClass cls; public float dmg, rof, range, spread; public int pellets = 1;
            public int pierce; public float pierceFalloff = 1f, knockback; public int price;
            public float splash;
        }

        static Spec S(string id, string name, WeaponClass cls, WeaponTier tier, float dmg, float rof, float range,
                      float spread, int price, int pellets = 1, int pierce = 0, float pierceFalloff = 1f,
                      bool twoHanded = true, float knockback = 0f, float splash = 0f) =>
            new() { id = id, name = name, cls = cls, tier = tier, dmg = dmg, rof = rof, range = range, spread = spread,
                    price = price, pellets = pellets, pierce = pierce, pierceFalloff = pierceFalloff,
                    twoHanded = twoHanded, knockback = knockback, splash = splash };

        // DPS bands (1 star): Common 50-90, Uncommon 110-145, Rare 165-200, Epic 215-245,
        // Legendary 270-300. Prices: Common 400-1,200, Uncommon 1,800, Rare 2,500-3,500,
        // Epic 5,000-6,000, Legendary 12,000.
        public static readonly Spec[] Specs =
        {
            S("weapon.smg.vintage_b", "Tommy Gun", WeaponClass.SMG, WeaponTier.Uncommon, 9, 13, 6f, 2.5f, 1800),
            S("weapon.smg.pack_j", "MP5", WeaponClass.SMG, WeaponTier.Uncommon, 10, 13, 6f, 2f, 1800),
            S("weapon.smg.modern_p", "MP9", WeaponClass.SMG, WeaponTier.Uncommon, 9, 15, 6f, 2.5f, 1800),
            S("weapon.smg.pack_g", "MPX", WeaponClass.SMG, WeaponTier.Rare, 12, 14, 6f, 2f, 2600),
            S("weapon.smg.pack_i", "UMP-45", WeaponClass.SMG, WeaponTier.Rare, 15, 12, 6.2f, 2f, 3000),
            S("weapon.smg.pack_h", "MP7", WeaponClass.SMG, WeaponTier.Epic, 13, 17, 6.2f, 2f, 5200),
            S("weapon.smg.pack_f", "Vector", WeaponClass.SMG, WeaponTier.Legendary, 14, 20, 6.5f, 1.8f, 12000),

            S("weapon.sidearm.m1911_vol1", "Officer 1911", WeaponClass.Sidearm, WeaponTier.Common, 17, 4.5f, 7.5f, 0f, 700, twoHanded: false),
            S("weapon.sidearm.modern_r", "Rhino .357", WeaponClass.Sidearm, WeaponTier.Uncommon, 30, 4f, 7.5f, 0f, 1800, twoHanded: false),
            S("weapon.sidearm.machine_pistol_p", "Micro Uzi", WeaponClass.Sidearm, WeaponTier.Uncommon, 8, 16, 6f, 3f, 1800, twoHanded: false),

            S("weapon.marksman.vintage_recon_b", "Kar98k", WeaponClass.Marksman, WeaponTier.Rare, 200, 0.8f, 16f, 0f, 3000, pierce: 3, pierceFalloff: 0.8f),
            S("weapon.marksman.recon_p", "AWM", WeaponClass.Marksman, WeaponTier.Legendary, 340, 0.8f, 18f, 0f, 12000, pierce: 5, pierceFalloff: 0.85f),
            S("weapon.lmg.vintage_b", "BAR M1918", WeaponClass.LMG, WeaponTier.Rare, 20, 9, 9.5f, 2.5f, 3200),

            S("weapon.shotgun.pack_f", "Remington 870", WeaponClass.Shotgun, WeaponTier.Common, 8, 1.4f, 5.5f, 12f, 1100, pellets: 8),
            S("weapon.shotgun.legacy_d", "MKA 1919", WeaponClass.Shotgun, WeaponTier.Common, 10, 1.1f, 5.5f, 12f, 1000, pellets: 8),
            S("weapon.shotgun.modern_p", "M590", WeaponClass.Shotgun, WeaponTier.Uncommon, 9, 1.7f, 5.5f, 12f, 1800, pellets: 8),
            S("weapon.shotgun.pack_j", "Saiga-12", WeaponClass.Shotgun, WeaponTier.Rare, 8, 2.8f, 5.5f, 12f, 2800, pellets: 8),
            S("weapon.shotgun.pack_h", "USAS-12", WeaponClass.Shotgun, WeaponTier.Rare, 8, 3.1f, 5.5f, 12f, 3400, pellets: 8),
            S("weapon.shotgun.pack_i", "Vepr-12", WeaponClass.Shotgun, WeaponTier.Epic, 9, 3.1f, 5.5f, 12f, 5200, pellets: 8),
            S("weapon.shotgun.pack_g", "Origin-12", WeaponClass.Shotgun, WeaponTier.Epic, 10, 3f, 5.5f, 12f, 6000, pellets: 8),

            S("weapon.assault_rifle.vintage_a", "M1 Carbine", WeaponClass.AssaultRifle, WeaponTier.Uncommon, 15, 8.5f, 9f, 1.5f, 1800),
            S("weapon.assault_rifle.vintage_b", "M1 Garand", WeaponClass.AssaultRifle, WeaponTier.Rare, 34, 5f, 9.5f, 1f, 2500),
            S("weapon.assault_rifle.legacy_a2", "M16A2", WeaponClass.AssaultRifle, WeaponTier.Rare, 19, 9.5f, 9f, 1.5f, 2800),
            S("weapon.assault_rifle.modern_t", "G3A3", WeaponClass.AssaultRifle, WeaponTier.Rare, 26, 7.5f, 9.5f, 1.5f, 3400),
            S("weapon.assault_rifle.legacy_m4", "Mk18", WeaponClass.AssaultRifle, WeaponTier.Epic, 21, 10.5f, 9f, 1.5f, 5000),
            S("weapon.assault_rifle.modern_u", "SG 552", WeaponClass.AssaultRifle, WeaponTier.Epic, 22, 10.5f, 9f, 1.5f, 5500),
            S("weapon.assault_rifle.modern_x", "AK-12", WeaponClass.AssaultRifle, WeaponTier.Epic, 24, 10f, 9f, 1.5f, 6000),
            S("weapon.assault_rifle.modern_w", "HK416", WeaponClass.AssaultRifle, WeaponTier.Legendary, 26, 11f, 9.5f, 1.2f, 12000),

            // Every other enemy within 2.8 m of the landing point takes 60 % of the hit.
            S("weapon.launcher.modern_g", "Thumper GL", WeaponClass.Rocket, WeaponTier.Legendary, 120, 1.2f, 10f, 0f, 12000, splash: 2.8f),
        };

        const string BlastFx = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/Explosions/GrenadeExplosion/GrenadeExplosionFire.prefab";

        static readonly Dictionary<WeaponClass, string> Reference = new()
        {
            { WeaponClass.Sidearm, "weapon.sidearm.glock_19" },
            { WeaponClass.SMG, "weapon.assault_rifle.m4a1" },
            { WeaponClass.AssaultRifle, "weapon.assault_rifle.m4a1" },
            { WeaponClass.Shotgun, "weapon.shotgun.mossberg_500" },
            { WeaponClass.Marksman, "weapon.marksman.sniper_generic" },
            { WeaponClass.LMG, "weapon.lmg.generic" },
            { WeaponClass.Rocket, "weapon.shotgun.mossberg_500" },
        };

        // ------------------------------------------------------------------ geometry

        class Part { public string name; public List<Vector3> v = new(); public Bounds b; }

        /// Mesh parts in root-local space, mirrored so the barrel points along +Z (sign = -1 when the
        /// model was built facing -Z).
        static List<Part> Parts(GameObject root, out int sign)
        {
            var parts = new List<Part>();
            var w2l = root.transform.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var p = new Part { name = mf.name.ToLowerInvariant() };
                var m = w2l * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) p.v.Add(m.MultiplyPoint3x4(v));
                parts.Add(p);
            }
            var trig = parts.FirstOrDefault(p => p.name.Contains("trigger"));
            var barrel = parts.FirstOrDefault(p => p.name.Contains("barrel") && !p.name.Contains("guard"))
                         ?? parts.FirstOrDefault(p => p.name.Contains("slide"));
            float tz = trig != null ? trig.v.Average(v => v.z) : 0f;
            float bz;
            if (barrel != null) bz = barrel.v.Average(v => v.z);
            else
            {
                var all = parts.SelectMany(p => p.v).ToList();
                float mn = all.Min(v => v.z), mx = all.Max(v => v.z);
                bz = (mx - tz) >= (tz - mn) ? mx : mn;
            }
            sign = bz >= tz ? 1 : -1;
            foreach (var p in parts)
            {
                for (int i = 0; i < p.v.Count; i++) p.v[i] = new Vector3(sign * p.v[i].x, p.v[i].y, sign * p.v[i].z);
                var b = new Bounds(p.v[0], Vector3.zero);
                foreach (var v in p.v) b.Encapsulate(v);
                p.b = b;
            }
            return parts;
        }

        static bool IsBody(Part p) =>
            !(p.name.Contains("mag") || p.name.Contains("grip") || p.name.Contains("stock") || p.name.Contains("bipod")
              || p.name.Contains("trigger") || p.name.Contains("sight") || p.name.Contains("optic") || p.name.Contains("carry"));

        public struct Fit
        {
            public int sign; public float scale, length;
            public Vector3 right, left, muzzle;       // canonical (+Z = muzzle), root-local units
        }

        public static Fit Measure(GameObject root, Spec spec)
        {
            var parts = Parts(root, out int sign);
            var all = parts.SelectMany(p => p.v).ToList();
            float minZ = all.Min(v => v.z), maxZ = all.Max(v => v.z), len = maxZ - minZ;

            float scale = spec.cls switch
            {
                WeaponClass.Sidearm => Mathf.Clamp(0.45f / len, 1.2f, 1.8f),
                WeaponClass.SMG => Mathf.Clamp(1.2f, 0.8f / len, 1.0f / len),
                WeaponClass.Marksman => Mathf.Clamp(1.2f, 1.35f / len, 1.65f / len),
                WeaponClass.LMG => Mathf.Clamp(1.2f, 1.3f / len, 1.6f / len),
                WeaponClass.Shotgun => Mathf.Clamp(1.2f, 1.1f / len, 1.55f / len),
                _ => Mathf.Clamp(1.2f, 1.1f / len, 1.5f / len),
            };

            var trig = parts.FirstOrDefault(p => p.name.Contains("trigger"));
            var tb = trig != null ? trig.b : new Bounds(new Vector3(0, 0, minZ + len * 0.35f), Vector3.one * 0.02f);
            var right = new Vector3(WristSide / scale, tb.min.y - WristBelowTrigger / scale, tb.min.z - WristBehindTrigger / scale);

            // Support wrist: under the handguard/receiver (not the magazine), a hand-length ahead.
            float lz = right.z + SupportAhead / scale;
            float slice = 0.02f / scale, bottom = float.MaxValue;
            foreach (var p in parts.Where(IsBody))
                foreach (var v in p.v)
                    if (Mathf.Abs(v.z - lz) < slice && v.y < bottom) bottom = v.y;
            if (bottom == float.MaxValue) bottom = right.y;
            float ly = Mathf.Clamp(bottom - 0.035f / scale, right.y - 0.03f / scale, right.y + 0.08f / scale);
            var left = new Vector3(0f, ly, lz);

            // Muzzle: the front of the barrel (or of the gun), centred on the bore.
            var front = parts.Where(p => p.name.Contains("barrel") || p.name.Contains("muzzle") || p.name.Contains("slide")).SelectMany(p => p.v).ToList();
            if (front.Count == 0) front = all;
            float fz = front.Max(v => v.z);
            var tip = front.Where(v => v.z > fz - 0.015f).ToList();
            var muzzle = new Vector3(0f, (tip.Min(v => v.y) + tip.Max(v => v.y)) * 0.5f, fz);

            return new Fit { sign = sign, scale = scale, length = len, right = right, left = left, muzzle = muzzle };
        }

        /// Side-view points (z, y, part) and the fitted markers of every gun, for a quick plot review.
        public static string DumpSideViews()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var spec in Specs)
            {
                var d = WeaponCatalog.Active.DataById(spec.id);
                if (d == null || d.weaponPrefab == null) continue;
                var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(d.weaponPrefab));
                try
                {
                    root.transform.localScale = Vector3.one;
                    var fit = Measure(root, spec);
                    var parts = Parts(root, out _);
                    sb.Append($"G|{spec.id}|{spec.name}|{fit.scale:F3}|{fit.right.z:F4},{fit.right.y:F4}|{fit.left.z:F4},{fit.left.y:F4}|{fit.muzzle.z:F4},{fit.muzzle.y:F4}\n");
                    foreach (var p in parts)
                    {
                        sb.Append("P|" + p.name + "|");
                        for (int i = 0; i < p.v.Count; i += 3) sb.Append($"{p.v[i].z:F3},{p.v[i].y:F3};");
                        sb.Append('\n');
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ review (Play Mode)

        /// Equips each finished gun on the live player (Skill Sandbox) and saves a side and a
        /// front-three-quarter shot per gun, with two authored guns first for comparison.
        [MenuItem("ZombieWar/Weapons/Factory/Review Finished Guns (Play Mode)")]
        public static void Review()
        {
            if (!Application.isPlaying) { Debug.LogError("[AutoGrip] Play the Skill Sandbox first."); return; }
            var weapon = Object.FindFirstObjectByType<Weapon>();
            if (weapon == null) { Debug.LogError("[AutoGrip] No Weapon in the scene."); return; }
            var ids = new List<string> { "weapon.assault_rifle.m4a1", "weapon.sidearm.glock_19" };
            ids.AddRange(Specs.Select(s => s.id));
            var host = new GameObject("~AutoGripReview") { hideFlags = HideFlags.HideAndDontSave };
            host.AddComponent<ReviewRunner>().Begin(weapon, ids);
        }

        public static bool ReviewDone;

        class ReviewRunner : MonoBehaviour
        {
            public void Begin(Weapon w, List<string> ids) { ReviewDone = false; StartCoroutine(Run(w, ids)); }

            System.Collections.IEnumerator Run(Weapon weapon, List<string> ids)
            {
                string dir = System.IO.Path.GetFullPath("Review/M8/guns");
                System.IO.Directory.CreateDirectory(dir);
                var cam = new GameObject("~AutoGripCam") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
                var rt = new RenderTexture(400, 400, 24) { antiAliasing = 4 };
                var tex = new Texture2D(400, 400, TextureFormat.RGB24, false);
                var body = weapon.GetComponentInParent<Animator>()?.transform ?? weapon.transform.root;
                foreach (var id in ids)
                {
                    var d = WeaponCatalog.Active.DataById(id);
                    if (d == null || !weapon.Equip(d)) { Debug.LogWarning("[AutoGrip] could not equip " + id); continue; }
                    for (int i = 0; i < 6; i++) { yield return null; yield return new WaitForEndOfFrame(); }
                    var grip = weapon.CurrentGrips != null && weapon.CurrentGrips.RightHandGrip != null ? weapon.CurrentGrips.RightHandGrip.position : body.position + Vector3.up * 0.6f;
                    var target = grip + body.forward * 0.15f;
                    var views = new[] { body.right * 1.5f + Vector3.up * 0.1f, body.forward * 1.2f + body.right * 0.8f + Vector3.up * 0.3f };
                    for (int v = 0; v < views.Length; v++)
                    {
                        cam.transform.position = target + views[v];
                        cam.transform.LookAt(target);
                        cam.targetTexture = rt; cam.Render();
                        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 400, 400), 0, 0); tex.Apply();
                        RenderTexture.active = null; cam.targetTexture = null;
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{id}_{v}.png"), tex.EncodeToPNG());
                    }
                }
                Destroy(cam.gameObject); rt.Release();
                ReviewDone = true;
                Destroy(gameObject);
            }
        }

        static Vector3 Local(Vector3 canonical, int sign) => new(sign * canonical.x, canonical.y, sign * canonical.z);

        // ------------------------------------------------------------------ apply

        [MenuItem("ZombieWar/Weapons/Factory/Finish Remaining Guns (auto grip)")]
        public static void FinishAll()
        {
            var catalog = WeaponCatalog.Active;
            var log = new System.Text.StringBuilder();
            foreach (var spec in Specs) log.AppendLine(Finish(catalog, spec));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[AutoGrip]\n" + log);
        }

        public static string Finish(WeaponCatalog catalog, Spec spec)
        {
            var entry = catalog.ById(spec.id);
            var d = entry?.data;
            if (d == null || d.weaponPrefab == null) return $"{spec.id}: MISSING";
            var refData = catalog.DataById(Reference[spec.cls]);

            string path = AssetDatabase.GetAssetPath(d.weaponPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.localScale = Vector3.one;
                var fit = Measure(root, spec);
                var grips = root.GetComponent<WeaponGripPoints>() ?? root.AddComponent<WeaponGripPoints>();
                Transform Marker(string n, Vector3 canonical, Quaternion rot)
                {
                    var t = root.transform.Find(n) ?? new GameObject(n).transform;
                    t.SetParent(root.transform, false);
                    t.localPosition = Local(canonical, fit.sign);
                    t.localRotation = rot;
                    return t;
                }
                var face = fit.sign > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                var r = Marker("RightHandGrip", fit.right, Quaternion.identity);
                var l = Marker("LeftHandGrip", fit.left, Quaternion.identity);
                var m = Marker("MuzzlePoint", fit.muzzle, face);
                var so = new SerializedObject(grips);
                so.FindProperty("rightHandGrip").objectReferenceValue = r;
                so.FindProperty("leftHandGrip").objectReferenceValue = l;
                so.FindProperty("muzzlePoint").objectReferenceValue = m;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);

                Undo.RecordObject(d, "Finish gun");
                bool longHold = spec.twoHanded;
                var wrist = longHold ? LongGunWrist : PistolWrist;
                d.gripLocalScale = Vector3.one * fit.scale;
                d.gripLocalEuler = fit.sign > 0 ? Vector3.zero : new Vector3(0f, 180f, 0f);
                d.gripLocalPosition = wrist - fit.scale * fit.right;
                d.useAuthoredGripPositions = true;
                d.rightHandGripRootPosition = Local(fit.right, fit.sign);
                d.leftHandGripRootPosition = Local(fit.left, fit.sign);
                d.twoHanded = spec.twoHanded;

                d.weaponName = spec.name; d.weaponClass = spec.cls; d.tier = spec.tier; d.price = spec.price; d.unlockCost = 0;
                d.fireMode = spec.pierce != 0 ? FireMode.PiercingLine : spec.pellets > 1 ? FireMode.MultiPelletHitscan : FireMode.SingleHitscan;
                d.damage = spec.dmg; d.fireRate = spec.rof; d.range = spec.range; d.spreadAngle = spec.spread;
                d.pelletCount = spec.pellets; d.pierceCount = spec.pierce; d.pierceDamageFalloff = spec.pierceFalloff;
                d.knockback = spec.knockback > 0f ? spec.knockback : refData != null ? refData.knockback : 0f;
                d.splashRadius = spec.splash;
                d.splashFx = spec.splash > 0f ? AssetDatabase.LoadAssetAtPath<ParticleSystem>(BlastFx) : null;
                if (refData != null)
                {
                    d.muzzleFlashPrefab = refData.muzzleFlashPrefab; d.smokeTrailPrefab = refData.smokeTrailPrefab;
                    d.impactPrefab = refData.impactPrefab; d.tracerPrefab = refData.tracerPrefab;
                    d.fireSfxKey = refData.fireSfxKey; d.reloadSfxKey = refData.reloadSfxKey;
                    d.recoilKickDistance = refData.recoilKickDistance; d.recoilKickDuration = refData.recoilKickDuration;
                    d.recoilReturnDuration = refData.recoilReturnDuration; d.recoilAimKickAngle = refData.recoilAimKickAngle;
                    d.recoilSideKickAngle = refData.recoilSideKickAngle;
                    d.damageFalloffCurve = refData.damageFalloffCurve != null ? new AnimationCurve(refData.damageFalloffCurve.keys) : null;
                }
                var dso = new SerializedObject(d);
                dso.FindProperty("authoringStatus").intValue = (int)WeaponData.AuthoringStatus.Ready;
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(d);

                entry.tier = spec.tier;
                entry.family = spec.cls.ToString();
                entry.unlockMethod = WeaponCatalog.UnlockMethod.Purchase;
                return $"{spec.id} -> {spec.name} {spec.tier} scale {fit.scale:F2} sign {fit.sign} len {fit.length:F2} " +
                       $"R {fit.right:F3} L {fit.left:F3} M {fit.muzzle:F3} dps {CombatPower.EffectiveDps(d, 1):F0}";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
