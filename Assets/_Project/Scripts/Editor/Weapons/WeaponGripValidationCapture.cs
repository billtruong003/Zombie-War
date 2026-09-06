using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools.Weapons
{
    /// <summary>
    /// M7.0 Task 1 — the rig-relative grip validation camera that gate <b>G5</b> depends on.
    ///
    /// G5 ("the grip looks correct in the hand") is a BLOCKING onboarding check, and until this tool
    /// existed it had never been run on any weapon — including the 25 already shipping.
    ///
    /// The M6.1 attempt solved camera distance from the CHARACTER'S renderer bounds
    /// (1.38 x 1.27 x 2.30 m) and ended up above the backpack with no hand in any frame. This tool
    /// never reads character bounds. Framing is solved entirely from the equipped weapon's
    /// <see cref="WeaponGripPoints"/> plus the player's up axis, at hand scale (centimetres), so the
    /// grip fills the frame regardless of how large the character is.
    ///
    /// The tool asserts its own success: every capture projects the animator's RightHand bone into
    /// viewport space and FAILS THE CAPTURE if the hand is not inside the frame. A capture without a
    /// visible hand is a tool failure, not a weapon verdict.
    /// </summary>
    public static class WeaponGripValidationCapture
    {
        const int CaptureSize = 512;
        const float Fov = 32f;

        // Hand-scale framing. These are metres of visible radius around the grip point, chosen so a
        // human hand (~0.18 m across) fills roughly a third of the frame. They are deliberately NOT
        // derived from any character measurement.
        const float MinFrameRadius = 0.11f;
        const float MaxFrameRadius = 0.52f;

        // Viewport margin the hand bone must sit inside for a capture to count as framed.
        const float SafeMargin = 0.04f;

        static string EvidenceDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Review", "M7_0_Factory", "Evidence", "Grip"));

        public class Shot
        {
            public string weaponId;
            public string weaponName;
            public bool twoHanded;
            public int slotRequested;
            public int slotActual;
            public string view;              // "RightGrip" / "LeftGrip" / "Front"
            public bool equipVerified;       // the real equip path put THIS weapon in the hand
            public int instanceId;           // proves each frame used its own instance
            public bool handInFrame;
            public Vector3 handViewport;
            public float handToGripCm;       // distance hand bone -> authored grip, in centimetres
            public float armReachCm;         // shoulder -> hand chain length for THIS arm
            public float shoulderToGripCm;   // shoulder -> authored grip
            public bool outOfReach;          // shoulderToGrip > armReach: the IK physically cannot arrive
            public bool muzzleInsideModel;
            public string pngPath;
            public string pngSha;            // proves no two frames are byte-identical
            public string note = "";
        }

        [MenuItem("ZombieWar/Weapons/Factory/G5 Grip Validation Capture (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[G5] Enter Play Mode from Bootstrap.unity first — this tool captures the " +
                               "REAL player through the REAL equip path. It refuses to fake it from prefabs.");
                return;
            }

            var weapon = Object.FindFirstObjectByType<ZombieWar.Weapon>();
            if (weapon == null) { Debug.LogError("[G5] No Weapon component in the loaded scene."); return; }

            var host = new GameObject("~G5CaptureHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<CaptureRunner>().Begin(weapon);
        }

        // ------------------------------------------------------------------ runner

        class CaptureRunner : MonoBehaviour
        {
            ZombieWar.Weapon _weapon;

            public void Begin(ZombieWar.Weapon w) { _weapon = w; StartCoroutine(Capture()); }

            IEnumerator Capture()
            {
                Directory.CreateDirectory(EvidenceDir);

                var animator = _weapon.GetComponentInParent<Animator>() ??
                               _weapon.GetComponentInChildren<Animator>();
                Transform handBone = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                    : null;
                Transform leftHandBone = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
                    : null;

                if (handBone == null)
                    Debug.LogWarning("[G5] No humanoid RightHand bone — falling back to the authored grip " +
                                     "transform for the in-frame assertion. Stated, not hidden.");

                var roster = _weapon.Weapons?.Where(x => x != null).OrderBy(x => x.CatalogOrder).ToList()
                             ?? new List<ZombieWar.WeaponData>();
                if (roster.Count == 0) { Debug.LogError("[G5] Player arsenal is empty."); Cleanup(); yield break; }

                var cam = BuildCamera();
                var rt = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 8 };
                var shots = new List<Shot>();

                foreach (var data in roster)
                {
                    int slot = data.twoHanded ? 1 : 0;

                    // The M6.1 defect: two-handed weapons equip to slot 1 while the capture followed
                    // slot 0, so four of six frames were byte-identical. Equip AND select explicitly,
                    // then verify - never assume.
                    _weapon.EquipToSlot(slot, data);
                    _weapon.EquipSlot(slot);

                    // Let the animator + Animation Rigging graph evaluate: rig constraints run in
                    // LateUpdate, so the grip transforms are only correct at end of frame.
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    yield return null;
                    yield return new WaitForEndOfFrame();

                    bool equipOk = _weapon.Current == data && _weapon.CurrentSlot == slot;
                    var grips = _weapon.CurrentGrips;

                    if (!equipOk || grips == null || grips.RightHandGrip == null)
                    {
                        shots.Add(new Shot
                        {
                            weaponId = data.WeaponId, weaponName = data.name, twoHanded = data.twoHanded,
                            slotRequested = slot, slotActual = _weapon.CurrentSlot, view = "RightGrip",
                            equipVerified = equipOk, handInFrame = false,
                            note = grips == null ? "no WeaponGripPoints on the equipped instance"
                                 : grips.RightHandGrip == null ? "rightHandGrip transform not assigned"
                                 : $"equip mismatch: asked slot {slot}, got slot {_weapon.CurrentSlot}"
                        });
                        continue;
                    }

                    shots.Add(Shoot(cam, rt, data, grips, grips.RightHandGrip, handBone, "RightGrip", slot, equipOk, 0f,
                                    ArmReach(animator, false)));

                    // Second angle. Two-handed weapons get the support hand (that is where fore-end
                    // penetration shows); one-handed weapons get the same grip rolled around the
                    // barrel axis toward the muzzle, which is the angle that reveals fingers passing
                    // through the frame.
                    //
                    // The roll is not cosmetic. The first run of this tool produced 39 distinct
                    // images from 50 captures: for one-handed weapons the "second" view solved to a
                    // byte-identical camera, so it was not a second view at all. The per-image SHA in
                    // the CSV is what caught it, and the roll is what fixes it.
                    if (data.twoHanded && grips.LeftHandGrip != null)
                        shots.Add(Shoot(cam, rt, data, grips, grips.LeftHandGrip, leftHandBone, "LeftGrip", slot, equipOk, 0f,
                                        ArmReach(animator, true)));
                    else
                        shots.Add(Shoot(cam, rt, data, grips, grips.RightHandGrip, handBone, "Front", slot, equipOk, 68f,
                                        ArmReach(animator, false)));
                }

                RenderTexture.active = null;
                cam.targetTexture = null;
                DestroyImmediate(cam.gameObject);
                rt.Release(); DestroyImmediate(rt);

                WriteReport(shots);
                BuildSheet(shots);
                Cleanup();
            }

            void Cleanup() { DestroyImmediate(gameObject); }

            /// <summary>
            /// Shoulder -> elbow -> hand chain length, and the shoulder transform, for one arm.
            /// A TwoBoneIK constraint cannot place the hand further from the shoulder than this, so
            /// comparing it against shoulder->grip separates "authoring is out of reach" (a real
            /// weapon defect) from "the IK failed to solve" (a rig defect). They look identical in a
            /// screenshot and have completely different fixes.
            /// </summary>
            static (Transform shoulder, float reach) ArmReach(Animator anim, bool left)
            {
                if (anim == null || !anim.isHuman) return (null, -1f);
                var upper = anim.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var lower = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                if (upper == null || lower == null || hand == null) return (null, -1f);
                return (upper, Vector3.Distance(upper.position, lower.position) +
                               Vector3.Distance(lower.position, hand.position));
            }

            Camera BuildCamera()
            {
                var go = new GameObject("~G5Camera") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.14f, 0.16f, 1f);
                cam.fieldOfView = Fov;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 20f;
                cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
                cam.enabled = false;   // rendered manually
                cam.depth = -100;
                return cam;
            }

            /// <summary>
            /// Solves the camera FROM THE GRIP, never from character bounds, and captures one frame.
            /// </summary>
            Shot Shoot(Camera cam, RenderTexture rt, ZombieWar.WeaponData data, WeaponGripPoints grips,
                       Transform gripTarget, Transform handBone, string view, int slot, bool equipOk,
                       float rollAroundBarrelDeg, (Transform shoulder, float reach) arm)
            {
                Transform playerRoot = _weapon.transform.root;
                Vector3 up = playerRoot.up;

                // Frame the HAND AND THE GRIP TOGETHER, centred on their midpoint. Centring on the
                // grip alone pushed the hand out of frame whenever the two were far apart — which is
                // precisely the defect G5 exists to reveal, so the framing must widen to show it
                // rather than crop it away.
                Vector3 handPos = handBone != null ? handBone.position : gripTarget.position;
                float handGripSep = Vector3.Distance(handPos, gripTarget.position);
                Vector3 target = (gripTarget.position + handPos) * 0.5f;

                // Barrel axis defines the weapon's own frame; scale follows grip->muzzle length, so a
                // pistol frames tighter than an LMG without either reading character size.
                Vector3 barrel = grips.MuzzlePoint != null
                    ? grips.MuzzlePoint.position - grips.RightHandGrip.position
                    : gripTarget.forward * 0.3f;
                float barrelLen = Mathf.Max(barrel.magnitude, 0.05f);
                Vector3 barrelDir = barrel.sqrMagnitude > 1e-6f ? barrel.normalized : gripTarget.forward;

                float frameRadius = Mathf.Clamp(
                    Mathf.Max(barrelLen * 0.42f, handGripSep * 1.35f + 0.06f),
                    MinFrameRadius, MaxFrameRadius);

                // Look perpendicular to the barrel, from the side facing AWAY from the body, so the
                // torso never occludes the hand.
                Vector3 side = Vector3.Cross(up, barrelDir).normalized;
                if (side.sqrMagnitude < 1e-4f) side = playerRoot.right;
                Vector3 chest = playerRoot.position + up * 1.2f;
                if (Vector3.Dot(side, target - chest) < 0f) side = -side;

                Vector3 viewDir = (side * 0.86f + up * 0.34f + barrelDir * 0.18f).normalized;

                // Roll the eye around the barrel axis for the second view so it is a genuinely
                // different angle rather than the same solve twice.
                if (Mathf.Abs(rollAroundBarrelDeg) > 0.01f)
                    viewDir = Quaternion.AngleAxis(rollAroundBarrelDeg, barrelDir) * viewDir;

                float dist = frameRadius / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);

                cam.transform.position = target + viewDir * dist;
                cam.transform.rotation = Quaternion.LookRotation(-viewDir, up);

                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                // ---- the tool's own acceptance condition ----
                Transform handRef = handBone != null ? handBone : gripTarget;
                Vector3 vp = cam.WorldToViewportPoint(handRef.position);
                bool inFrame = vp.z > 0f &&
                               vp.x > SafeMargin && vp.x < 1f - SafeMargin &&
                               vp.y > SafeMargin && vp.y < 1f - SafeMargin;

                // Distance from THIS view's hand bone to THIS view's grip. Measuring every row
                // against the right grip made left-grip rows report the weapon's own grip spacing,
                // which is not a defect and not what G5 asks.
                float handToGripCm = handBone != null ? handGripSep * 100f : -1f;

                float shoulderToGripCm = arm.shoulder != null
                    ? Vector3.Distance(arm.shoulder.position, gripTarget.position) * 100f : -1f;
                float armReachCm = arm.reach >= 0f ? arm.reach * 100f : -1f;
                bool outOfReach = shoulderToGripCm > 0f && armReachCm > 0f && shoulderToGripCm > armReachCm;

                // Muzzle buried inside the weapon body is a real authoring defect and is cheap to
                // detect: the muzzle should sit at or beyond the far end of the barrel axis.
                bool muzzleInside = false;
                if (grips.MuzzlePoint != null)
                {
                    var rends = _weapon.CurrentGrips.GetComponentsInChildren<Renderer>(false);
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        // shrink slightly so touching the shell is not counted as buried
                        var inner = new Bounds(b.center, b.size * 0.72f);
                        muzzleInside = inner.Contains(grips.MuzzlePoint.position);
                    }
                }

                string safeView = view;
                string file = $"{data.name}__{safeView}.png";
                string full = Path.Combine(EvidenceDir, file);
                var png = tex.EncodeToPNG();
                File.WriteAllBytes(full, png);
                DestroyImmediate(tex);

                return new Shot
                {
                    weaponId = data.WeaponId,
                    weaponName = data.name,
                    twoHanded = data.twoHanded,
                    slotRequested = slot,
                    slotActual = _weapon.CurrentSlot,
                    view = safeView,
                    equipVerified = equipOk,
                    instanceId = grips.GetInstanceID(),
                    handInFrame = inFrame,
                    handViewport = vp,
                    handToGripCm = handToGripCm,
                    armReachCm = armReachCm,
                    shoulderToGripCm = shoulderToGripCm,
                    outOfReach = outOfReach,
                    muzzleInsideModel = muzzleInside,
                    pngPath = "Review/M7_0_Factory/Evidence/Grip/" + file,
                    pngSha = Sha(png)
                };
            }

            static string Sha(byte[] bytes)
            {
                using (var sha = System.Security.Cryptography.SHA1.Create())
                    return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 12);
            }

            void WriteReport(List<Shot> shots)
            {
                var sb = new StringBuilder();
                sb.AppendLine("weaponId,weaponName,twoHanded,view,slotRequested,slotActual,equipVerified," +
                              "instanceId,handInFrame,vpX,vpY,vpZ,handToGripCm,armReachCm,shoulderToGripCm," +
                              "outOfReach,muzzleInsideModel,pngSha,pngPath,note");
                foreach (var s in shots)
                    sb.AppendLine($"{s.weaponId},{s.weaponName},{s.twoHanded},{s.view},{s.slotRequested}," +
                                  $"{s.slotActual},{s.equipVerified},{s.instanceId},{s.handInFrame}," +
                                  $"{s.handViewport.x:F3},{s.handViewport.y:F3},{s.handViewport.z:F3}," +
                                  $"{s.handToGripCm:F1},{s.armReachCm:F1},{s.shoulderToGripCm:F1}," +
                                  $"{s.outOfReach},{s.muzzleInsideModel},{s.pngSha},{s.pngPath},\"{s.note}\"");

                string csv = Path.Combine(EvidenceDir, "..", "g5_grip_results.csv");
                File.WriteAllText(Path.GetFullPath(csv), sb.ToString());

                int framed = shots.Count(s => s.handInFrame);
                int distinct = shots.Where(s => s.pngSha != null).Select(s => s.pngSha).Distinct().Count();
                int captured = shots.Count(s => s.pngSha != null);

                Debug.Log($"[G5] {shots.Count} captures over {shots.Select(s => s.weaponId).Distinct().Count()} weapons. " +
                          $"hand-in-frame {framed}/{shots.Count}; distinct images {distinct}/{captured}. " +
                          $"CSV -> Review/M7_0_Factory/g5_grip_results.csv");

                if (framed < shots.Count)
                    Debug.LogError($"[G5] TOOL FAILURE on {shots.Count - framed} capture(s): the grip hand is not " +
                                   "inside the frame. That is a defect in this tool, not a verdict on the weapon.");
            }

            void BuildSheet(List<Shot> shots)
            {
                var primary = shots.Where(s => s.view == "RightGrip" && s.pngPath != null).ToList();
                if (primary.Count == 0) return;

                const int cell = 256, cols = 5;
                int rows = Mathf.CeilToInt(primary.Count / (float)cols);
                var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGB24, false);
                var fill = new Color32(20, 21, 24, 255);
                var px = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < px.Length; i++) px[i] = fill;
                sheet.SetPixels32(px);

                for (int i = 0; i < primary.Count; i++)
                {
                    string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", primary[i].pngPath));
                    if (!File.Exists(full)) continue;
                    var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    t.LoadImage(File.ReadAllBytes(full));
                    int cx = (i % cols) * cell;
                    int cy = sheet.height - ((i / cols) + 1) * cell;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                            sheet.SetPixel(cx + x, cy + y, t.GetPixelBilinear(x / (float)cell, y / (float)cell));
                    DestroyImmediate(t);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(EvidenceDir, "..", "Sheet_G5_GripValidation.png"), sheet.EncodeToPNG());
                DestroyImmediate(sheet);
            }
        }
    }
}
