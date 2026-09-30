using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Dev
{
    /// Key art staging (2026-09-30): real game models, lighting and animation posed into a shot and
    /// rendered by a dedicated camera at high resolution, with no HUD. The frames are references
    /// for painted key art: proportion, silhouette and composition come from the game itself.
    public sealed partial class SkillSandbox
    {
        public string LastShot { get; private set; }

        /// Dresses the player in an outfit set for the shot only; the saved profile is untouched.
        public bool DressForShot(string setId)
        {
            var player = PlayerMovement.Instance;
            var applier = player != null ? player.GetComponentInChildren<CharacterModularApplier>(true) : null;
#if UNITY_EDITOR
            // Editor-only staging: the shop data and the costume catalog are not loaded in the sandbox.
            var econ = UnityEditor.AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/Data/Economy/EconomyConfig.asset");
            var catalog = applier != null && applier.Catalog != null ? applier.Catalog
                : UnityEditor.AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>("Assets/_Project/Data/Character/CasualCostumeCatalog.asset");
            if (applier != null && applier.Catalog == null && catalog != null) { applier.SetCatalog(catalog); applier.EnsureBoneMap(true); }
#else
            EconomyConfig econ = null; ModularCostumeCatalog catalog = null;
#endif
            EconomyConfig.CostumeSetEntry set = null;
            if (econ != null) foreach (var s in econ.costumeSets) if (s.setId == setId) set = s;
            if (applier == null || catalog == null || set == null) return false;
            foreach (var def in catalog.slotDefinitions) applier.Clear(def.id);
            foreach (var def in catalog.slotDefinitions)
                if (def.required && catalog.TryFindByItemId(def.defaultItemId, out var ds, out var de)) applier.Apply(ds, de);
            bool gloves = false, shoes = false;
            foreach (var id in set.itemIds)
                if (catalog.TryFindByItemId(id, out var slot, out var entry))
                {
                    applier.Apply(slot, entry);
                    gloves |= slot == "Hands"; shoes |= slot == "Feet";
                }
            applier.ApplyCasualTechnicalBase(gloves, shoes);
            return true;
        }

        /// Puts one enemy of a type at a point around the player, facing a point, held in place.
        public ZombieBase Stage(string enemyName, Vector3 offset, Vector3 lookOffset, bool pinned = true)
        {
            var player = PlayerMovement.Instance;
            if (player == null || _spawner == null) return null;
            ZombieData data = null;
            foreach (var d in _roster) if (d != null && d.name == enemyName) data = d;
            if (data == null) return null;
            _spawner.EnsureRegistered(data, 8);
            var z = _spawner.Spawn(data);
            if (z == null) return null;
            Vector3 p = player.transform.position;
            if (pinned) Place(z, p + offset, p + lookOffset);
            else (z.gameObject.GetComponent<SandboxDummy>() ?? z.gameObject.AddComponent<SandboxDummy>()).Hold();
            if (!pinned) z.transform.position = p + offset;
            return z;
        }

        /// Scatters a crowd over an arc around the player (degrees from +Z, metres), all facing them.
        public int StageArc(string[] pool, int count, float r0, float r1, float a0, float a1, int seed = 1)
        {
            var rng = new System.Random(seed);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(a0, a1, count == 1 ? 0.5f : i / (float)(count - 1)) + (float)(rng.NextDouble() - 0.5) * 8f;
                float r = Mathf.Lerp(r0, r1, (float)rng.NextDouble());
                var off = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, r);
                if (Stage(pool[i % pool.Length], off, Vector3.zero) != null) n++;
            }
            return n;
        }

        /// Stops the gun (no new hits, splats or damage numbers) and hides the station compass, so a
        /// held pose reads clean. On again restores both.
        public void CombatForShot(bool on)
        {
            foreach (var w in FindObjectsByType<Weapon>(FindObjectsSortMode.None)) w.enabled = on;
            foreach (var c in FindObjectsByType<ZombieWar.Stations.StationCompass>(FindObjectsSortMode.None)) c.enabled = on;
            foreach (var lr in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                if (lr.GetComponentInParent<ZombieWar.Stations.StationCompass>() != null) lr.enabled = on;
        }

        /// Turns the player to face a yaw (degrees), for turnarounds.
        public void FacePlayer(float yaw)
        {
            var p = PlayerMovement.Instance;
            if (p == null) return;
            var q = Quaternion.Euler(0f, yaw, 0f);
            p.transform.rotation = q;
            var rb = p.GetComponent<Rigidbody>(); if (rb != null) rb.rotation = q;
            // The body follows the aim vector every physics step, so the aim has to turn too.
            AimField?.SetValue(p, q * Vector3.forward);
        }

        static readonly System.Reflection.FieldInfo AimField = typeof(PlayerMovement).GetField("<AimDirection>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// Removes every station and pickup, so one shot's props never show up in the next.
        public void ClearProps()
        {
            var st = ZombieWar.Stations.StationDirector.Instance;
            st?.GetType().GetMethod("ReleaseAllStations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(st, null);
            foreach (var s in FindObjectsByType<ZombieWar.Stations.Station>(FindObjectsSortMode.None)) Destroy(s.gameObject);
            foreach (var pk in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (BillGameCore.Bill.Pool != null) BillGameCore.Bill.Pool.Return(pk.gameObject); else pk.gameObject.SetActive(false);
        }

        /// Stages a row of enemies left to right, spaced by their real widths, all facing the camera (-Z).
        public float StageRow(string[] names, float x0, float z, float gap)
        {
            float x = x0;
            foreach (var n in names)
            {
                var e = Stage(n, new Vector3(x, 0f, z), new Vector3(x, 0f, z - 10f));
                if (e == null) { Debug.LogWarning("[KeyArt] no enemy " + n); continue; }
                float w = 1f;
                var rs = e.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds); w = Mathf.Clamp(b.size.x, 0.6f, 6f); }
                var pos = e.transform.position; pos.x += w * 0.5f;
                Place(e, pos, pos + new Vector3(0f, 0f, -10f));
                x += w + gap;
            }
            return x;
        }

        public bool Shooting { get; private set; }

        /// Front / three-quarter / side / back of the player, same framing.
        public IEnumerator Turnaround(string prefix, float[] yaws, Vector3 cam, Vector3 look, float fov, int w, int h)
        {
            Shooting = true;
            for (int i = 0; i < yaws.Length; i++)
            {
                FacePlayer(yaws[i]);
                yield return null; yield return null;
                FacePlayer(yaws[i]);
                yield return Shot($"{prefix}_{i}", cam, look, fov, w, h);
            }
            Shooting = false;
        }

        /// The same shot once per outfit set (and optionally per gun family), for lineups.
        public IEnumerator Lineup(string prefix, string[] sets, WeaponClass[] guns, float yaw, Vector3 cam, Vector3 look, float fov, int w, int h)
        {
            Shooting = true;
            int n = Mathf.Max(sets.Length, guns != null ? guns.Length : 0);
            for (int i = 0; i < n; i++)
            {
                DressForShot(sets[Mathf.Min(i, sets.Length - 1)]);
                if (guns != null && guns.Length > 0) EquipFamily(guns[Mathf.Min(i, guns.Length - 1)]);
                CombatForShot(false);
                for (int k = 0; k < 6; k++) { FacePlayer(yaw); yield return null; }
                yield return Shot($"{prefix}_{i}", cam, look, fov, w, h);
            }
            Shooting = false;
        }

        /// Renders the world (no UI) from a camera placed relative to the player, into a PNG.
        public IEnumerator Shot(string name, Vector3 camOffset, Vector3 lookOffset, float fov, int width, int height, bool freeze = true)
        {
            var player = PlayerMovement.Instance;
            var main = Camera.main;
            if (player == null || main == null) yield break;
            float before = Time.timeScale;
            if (freeze) Time.timeScale = 0f;
            yield return new WaitForEndOfFrame();

            var go = Instantiate(main.gameObject);
            foreach (var mb in go.GetComponents<MonoBehaviour>())
                if (!(mb is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)) Destroy(mb);
            var al = go.GetComponent<AudioListener>(); if (al != null) Destroy(al);
            var cam = go.GetComponent<Camera>();
            Vector3 p = player.transform.position;
            go.transform.position = p + camOffset;
            go.transform.rotation = Quaternion.LookRotation((p + lookOffset) - go.transform.position, Vector3.up);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Review", "KeyArt", "concepts_v2");
            System.IO.Directory.CreateDirectory(dir);
            LastShot = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, name + ".png"));
            System.IO.File.WriteAllBytes(LastShot, tex.EncodeToPNG());
            cam.targetTexture = null;
            Destroy(tex); rt.Release(); Destroy(rt); Destroy(go);
            if (freeze) Time.timeScale = before;
        }
    }
}
