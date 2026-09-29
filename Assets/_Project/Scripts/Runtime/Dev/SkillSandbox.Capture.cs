using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Threat;

namespace ZombieWar.Dev
{
    // Review capture: single shots, frame bursts and contact sheets written under Review/M8/sandbox
    // (the project folder in the Editor, persistentDataPath on a device).
    public sealed partial class SkillSandbox
    {
        public const int ShotWidth = 540, ShotHeight = 960;
        public bool Capturing { get; private set; }
        public string LastCaptureFolder { get; private set; }

        public static string CaptureRoot =>
            Application.isEditor
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Review", "M8", "sandbox"))
                : System.IO.Path.Combine(Application.persistentDataPath, "sandbox");

        static string NewFolder(string label)
        {
            string safe = string.IsNullOrEmpty(label) ? "shot" : label.Replace(' ', '_').Replace('/', '_').Replace(':', '_');
            string folder = System.IO.Path.Combine(CaptureRoot, System.DateTime.Now.ToString("MMdd_HHmmss") + "_" + safe);
            System.IO.Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>One screenshot of what the game camera shows now.</summary>
        public IEnumerator Snap(string label)
        {
            Capturing = true;
            string folder = NewFolder(label);
            yield return CaptureFrame(System.IO.Path.Combine(folder, "shot.png"), null);
            LastCaptureFolder = folder;
            Capturing = false;
        }

        /// <summary>
        /// <paramref name="frames"/> screenshots, <paramref name="gap"/> real seconds apart, at
        /// <paramref name="timeScale"/>, then a contact sheet of all of them (sheet.png) for review.
        /// </summary>
        public IEnumerator Burst(string label, int frames, float gap, float timeScale)
        {
            Capturing = true;
            bool panel = _panelOpen;
            _panelOpen = false;
            string folder = NewFolder(label);
            float before = Time.timeScale;
            Time.timeScale = timeScale;
            var shots = new List<Texture2D>(frames);
            for (int i = 0; i < frames; i++)
            {
                yield return CaptureFrame(System.IO.Path.Combine(folder, $"f{i:00}.png"), shots);
                yield return new WaitForSecondsRealtime(gap);
            }
            Time.timeScale = _paused ? 0f : before;
            WriteSheet(shots, System.IO.Path.Combine(folder, "sheet.png"));
            foreach (var t in shots) Destroy(t);
            _panelOpen = panel;
            LastCaptureFolder = folder;
            Capturing = false;
            Debug.Log("[SandboxCapture] " + folder);
        }

        /// <summary>Grid of frames (4 across, half size) with a thin gutter, for one-glance review.</summary>
        static void WriteSheet(List<Texture2D> shots, string file)
        {
            if (shots.Count == 0) return;
            const int cols = 4, gutter = 4;
            int w = ShotWidth / 2, h = ShotHeight / 2;
            int rows = Mathf.CeilToInt(shots.Count / (float)cols);
            var sheet = new Texture2D(cols * w + (cols + 1) * gutter, rows * h + (rows + 1) * gutter, TextureFormat.RGB24, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 22, 28, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < shots.Count; i++)
            {
                int c = i % cols, r = rows - 1 - i / cols;          // first frame top-left
                var rt = RenderTexture.GetTemporary(w, h);
                Graphics.Blit(shots[i], rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, w, h), gutter + c * (w + gutter), gutter + r * (h + gutter));
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(file, sheet.EncodeToPNG());
            Destroy(sheet);
        }

        /// <param name="keep">When set, the frame texture is kept (for a sheet) instead of destroyed.</param>
        private static IEnumerator CaptureFrame(string file, List<Texture2D> keep)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var rt = RenderTexture.GetTemporary(ShotWidth, ShotHeight);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var sm = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
            sm.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0); sm.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(file, sm.EncodeToPNG());
            Destroy(tex);
            if (keep != null) keep.Add(sm); else Destroy(sm);
        }

        // ------------------------------------------------------------------ scripted review runs

        /// <summary>
        /// Captures every card in <paramref name="specs"/> to PNGs for review. Spec format:
        /// "cardId|family|dummies|mortal|walk|waitSeconds" (family: - for the default SMG).
        /// Frames are taken at <paramref name="timeScale"/> every <paramref name="gap"/> real seconds.
        /// Emergency Detonation turns god mode off for its own capture (it needs the player hurt).
        /// </summary>
        public IEnumerator Capture(string[] specs, string folder, int frames, float gap, float timeScale)
        {
            _panelOpen = false;
            System.IO.Directory.CreateDirectory(folder);
            while (ThreatDirector.Instance == null || ThreatDirector.Instance.enabled) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var spec in specs)
            {
                var f = spec.Split('|');
                string id = f[0];
                ResetSkills(); StopWalk(); Mortal = false; Time.timeScale = 1f;
                EquipFamily(f.Length > 1 && f[1] != "-" ? (WeaponClass)System.Enum.Parse(typeof(WeaponClass), f[1]) : WeaponClass.SMG);
                SetDummies(f.Length > 2 ? int.Parse(f[2]) : 10);
                yield return new WaitForSecondsRealtime(1.2f);
                Mortal = f.Length > 3 && f[3] == "1";
                Walk = f.Length > 4 && f[4] == "1";
                Max(id);
                Health hp = null;
                if (id == SkillCatalogDefs.AutoEmergency)
                {
                    SetGod(false);
                    hp = PlayerMovement.Instance.GetComponent<Health>();
                    hp.TakeDamage(hp.Max * 0.85f);
                }
                yield return new WaitForSecondsRealtime(f.Length > 5 ? float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture) : 0.5f);
                Time.timeScale = timeScale;
                for (int i = 0; i < frames; i++)
                {
                    yield return CaptureFrame(System.IO.Path.Combine(folder, $"{id}_{i:00}.png"), null);
                    yield return new WaitForSecondsRealtime(gap);
                }
                Time.timeScale = 1f;
                if (hp != null) { SetGod(true); hp.ResetHealth(); }
                Debug.Log("[SandboxCapture] " + id);
            }
            StopWalk(); Mortal = false;
            Debug.Log("[SandboxCapture] END");
        }

        /// <summary>
        /// Fires a power right now (bypassing its cooldown) and captures the frames after it, so a
        /// review never misses the moment. Spec: "cardToGrant|powerId|targets|radius" (radius: a
        /// number, or "value" for the card's own value, or "frost" for the Frost Nova radius).
        /// </summary>
        public IEnumerator ForceCapture(string[] specs, string folder, int frames, float gap, float timeScale)
        {
            _panelOpen = false;
            System.IO.Directory.CreateDirectory(folder);
            while (ThreatDirector.Instance == null || ThreatDirector.Instance.enabled) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var apply = typeof(SkillCombatDriver).GetMethod("Apply", F);
            var value = typeof(SkillRuntime).GetMethod("Value", F);
            foreach (var spec in specs)
            {
                var f = spec.Split('|');
                ResetSkills(); Time.timeScale = 1f; SetDummies(10);
                yield return new WaitForSecondsRealtime(1f);
                Max(f[0]);
                yield return new WaitForSecondsRealtime(0.2f);
                var run = SkillRuntime.Active;
                float radius = f[3] == "value" ? (float)value.Invoke(run, new object[] { f[1] })
                             : f[3] == "frost" ? run.FrostRadius
                             : float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                var proc = new SkillRuntime.PowerProc { skillId = f[1], targets = int.Parse(f[2]), radius = radius };
                Time.timeScale = timeScale;
                apply.Invoke(SkillCombatDriver.Instance, new object[] { run, proc, PlayerMovement.Instance.transform.position });
                for (int i = 0; i < frames; i++)
                {
                    yield return CaptureFrame(System.IO.Path.Combine(folder, $"{f[0]}_{i:00}.png"), null);
                    yield return new WaitForSecondsRealtime(gap);
                }
                Time.timeScale = 1f;
            }
            Debug.Log("[SandboxCapture] END");
        }
    }
}
