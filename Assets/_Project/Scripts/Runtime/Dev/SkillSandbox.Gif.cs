using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Dev
{
    /// Short clips of each skill for review (VFX audit, 2026-09-30): the card is maxed on a crowd,
    /// its power is fired at once (no waiting on a cooldown), and a square around the player is
    /// recorded as JPG frames at a steady game-time step. Tools/make_gifs.py turns each folder of
    /// frames into a GIF.
    public sealed partial class SkillSandbox
    {
        public IEnumerator RecordClips(string label, IEnumerable<string> ids, float seconds = 3f, float step = 0.08f,
                                       float timeScale = 0.5f, int size = 480, int crowd = 16)
        {
            Stressing = true;
            _panelOpen = false;
            bool immortal = ImmortalHorde;
            ImmortalHorde = true;
            SetMode(EnemyMode.Horde);
            SetDummies(crowd);
            string root = NewFolder(label);
            yield return new WaitForSeconds(2.5f);

            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var apply = typeof(SkillCombatDriver).GetMethod("Apply", F);
            var value = typeof(SkillRuntime).GetMethod("Value", F | System.Reflection.BindingFlags.Public);

            foreach (var id in ids)
            {
                Time.timeScale = 1f;
                // A gun card needs its gun in hand.
                string family = id.Substring(0, Mathf.Max(0, id.IndexOf('.')));
                EquipFamily(family switch
                {
                    "sidearm" => WeaponClass.Sidearm, "smg" => WeaponClass.SMG, "shotgun" => WeaponClass.Shotgun,
                    "lmg" => WeaponClass.LMG, "marksman" => WeaponClass.Marksman, "rocket" => WeaponClass.Rocket,
                    _ => WeaponClass.AssaultRifle,
                });
                ApplyBuild(new Dictionary<string, int>());
                Max(id);
                bool walk = id == SkillCatalogDefs.AutoFireTrail || id == "sidearm.rungun";
                if (walk) Walk = true; else StopWalk();
                yield return new WaitForSeconds(1.6f);   // the crowd closes in first, so area powers find it

                // Fire the power now: an evolution fires the power it grew from.
                var def = SkillCatalogDefs.ById(id);
                string power = def != null && def.IsEvolution ? def.evolvesFrom : id;
                var run = SkillRuntime.Active;
                var powerDef = SkillCatalogDefs.ById(power);
                if (apply != null && run != null && powerDef != null && powerDef.layer == SkillLayer.Autonomous && PlayerMovement.Instance != null)
                {
                    float radius = power == SkillCatalogDefs.AutoFrostNova ? run.FrostRadius
                                 : value != null ? (float)value.Invoke(run, new object[] { power }) : 3f;
                    var proc = new SkillRuntime.PowerProc { skillId = power, targets = 3, radius = radius };
                    apply.Invoke(SkillCombatDriver.Instance, new object[] { run, proc, PlayerMovement.Instance.transform.position });
                }

                if (id == SkillCatalogDefs.UniGuardian) SkillArsenal.Instance?.OnGuardianAngel();   // only fires on a saved death

                string folder = System.IO.Path.Combine(root, id);
                System.IO.Directory.CreateDirectory(folder);
                Time.timeScale = timeScale;
                int frames = Mathf.CeilToInt(seconds / step);
                for (int i = 0; i < frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    SaveSquare(System.IO.Path.Combine(folder, $"{i:000}.jpg"), size);
                    float until = Time.time + step;
                    while (Time.time < until) yield return null;
                }
            }
            Time.timeScale = 1f;
            StopWalk();
            ApplyBuild(new Dictionary<string, int>());
            ImmortalHorde = immortal;
            SetMode(EnemyMode.Dummies);
            SetDummies(8);
            LastCaptureFolder = root;
            Stressing = false;
            Debug.Log("[Clips] " + root);
        }

        /// The square of the screen centred on the player, downscaled, as a JPG.
        static void SaveSquare(string path, int size)
        {
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var cam = Camera.main;
                Vector2 c = new Vector2(shot.width * 0.5f, shot.height * 0.5f);
                if (cam != null && PlayerMovement.Instance != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(PlayerMovement.Instance.transform.position);
                    c = new Vector2(sp.x * shot.width / Screen.width, sp.y * shot.height / Screen.height);
                }
                int side = Mathf.RoundToInt(Mathf.Min(shot.width, shot.height) * 0.72f);
                int x0 = Mathf.Clamp(Mathf.RoundToInt(c.x - side * 0.5f), 0, shot.width - side);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(c.y - side * 0.5f), 0, shot.height - side);
                var rt = RenderTexture.GetTemporary(size, size, 0);
                Graphics.Blit(shot, rt, new Vector2(side / (float)shot.width, side / (float)shot.height),
                              new Vector2(x0 / (float)shot.width, y0 / (float)shot.height));
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                System.IO.File.WriteAllBytes(path, tex.EncodeToJPG(85));
                Destroy(tex);
            }
            finally { Destroy(shot); }
        }
    }
}
