# Player weapon-socket incident — root cause, fix, evidence

## Root cause

`PlayerRigBuilder.BuildWeaponRig` always called `FindOrCreate(chest, "WeaponSocket")` — an
unconditional, unrestricted `Transform.Find` scoped to the **chest** bone — then unconditionally
overwrote `Weapon.weaponSocket` with whatever it found/created there. The project's real weapon
socket has always lived under the **right hand** bone, several levels below chest, so
`chest.Find("WeaponSocket")` never found it, created a second "WeaponSocket" object under chest,
and silently repointed `Weapon.weaponSocket` to that wrong object. This moved the equipped weapon
from the hand to the chest.

## Correct hand socket (unchanged, preserved)

```
Player/CharacterModel/Bone/QuickRigCharacter2_Reference/QuickRigCharacter2_Hips/
QuickRigCharacter2_Spine/QuickRigCharacter2_Spine1/QuickRigCharacter2_Spine2/
QuickRigCharacter2_RightShoulder/QuickRigCharacter2_RightArm/
QuickRigCharacter2_RightForeArm/QuickRigCharacter2_RightHand/WeaponSocket
```

Local position `(0,0,0)`, local rotation identity, relative to `RightHand` — untouched throughout
this repair.

## Accidental chest socket (removed / never persisted)

A second "WeaponSocket" was created as a direct child of the bone `PlayerRigBuilder` resolves as
"chest" (`QuickRigCharacter2_Spine1` on this rig, via `Animator.GetBoneTransform(HumanBodyBones.Chest)
?? HumanBodyBones.Spine`). By the time this incident was investigated and the asset re-verified
(direct file `grep` + a fresh `PrefabUtility.LoadPrefabContents` reload, both independent of any
Unity in-memory cache), the saved `Player.prefab` on disk already had exactly one `WeaponSocket`
object, correctly under `RightHand`, and `Weapon.weaponSocket` correctly referencing it — the
accidental chest object never survived to a final saved state. This was re-verified rigorously
before any further edits (see "Verification" below).

## Files changed

- `Assets/_Project/Prefabs/Player.prefab` — `WeaponIKController` references wired (previously all
  `NULL`: `rightHandIK`, `leftHandIK`, `aimConstraint`, `rightHandTarget`, `leftHandTarget`,
  `aimTarget`, `aimOrigin`), `RigBuilder` + `WeaponRig` (`Rig`, `ChestAim` `MultiAimConstraint`,
  `RightHandIK`/`LeftHandIK` `TwoBoneIKConstraint`, targets/hints) added. `Weapon.weaponSocket`
  unchanged — still the original hand socket (no diff line for that field).
- `Assets/_Project/Scripts/Editor/PlayerRigBuilder.cs` — socket-resolution logic rewritten (see
  below). No other file touched.

## Builder fix (`PlayerRigBuilder.BuildWeaponRig`)

Added `ResolveWeaponSocket(playerRoot, weapon, rHand, summary)`, precedence order:

1. If `Weapon.weaponSocket` already references a transform inside the player hierarchy, **preserve
   it untouched** — never repoint a valid reference.
2. Otherwise, look for a `"WeaponSocket"` object anywhere under the player root:
   - Exactly one, and it's under the right-hand bone subtree → reuse it.
   - Exactly one, but **not** under the right-hand bone → log a clear error with its full path and
     abort the socket mutation (don't guess).
   - More than one → log a clear error listing every candidate's full path and abort the socket
     mutation (don't guess — this is exactly the condition that produced the original bug).
3. Only when zero `"WeaponSocket"` objects exist anywhere is a new one created — always directly
   under the right-hand bone, never under chest/spine.

Also made defensive/idempotent per the task requirements:
- `RigBuilder`, `Rig`, `MultiAimConstraint`, `TwoBoneIKConstraint`, `WeaponIKController`, and every
  rig child object are found-or-created by identity (`GetComponent`/`Transform.Find` before
  `AddComponent`/`new GameObject`) — never duplicated on re-run.
- `RigBuilder.layers` is only rewritten when it doesn't already contain exactly the expected single
  `WeaponRig` layer.
- New GameObjects/components are created via `Undo.RegisterCreatedObjectUndo` /
  `Undo.AddComponent<T>` so the operation is undo-safe when run against a live open scene.
- A concise per-run summary (`created X` / `reused X` / `REJECTED ...`) is logged in one
  `Debug.Log` at the end of `BuildWeaponRig`.

## Verification

### Prefab structure (fresh `LoadPrefabContents` reload after `AssetDatabase.ImportAsset(ForceUpdate)`)

```
WeaponSocket count = 1   (correct, under RightHand)
RigBuilder count   = 1
Rig count          = 1
TwoBoneIKConstraint count = 2  (right + left)
MultiAimConstraint count  = 1
WeaponIKController count  = 1
WeaponRig GO count        = 1
RightHandTarget count     = 1
LeftHandTarget count      = 1
Weapon.weaponSocket -> Player/.../QuickRigCharacter2_RightHand/WeaponSocket
```

### Idempotency

- Captured `git diff` hash of `Player.prefab` before re-running the fixed `BuildWeaponRig` +
  `SaveAsPrefabAsset`, then again after: **identical MD5** (`27eac19a3e80a1b7f610619205947e64`) —
  byte-for-byte no-op on an already-correct prefab.
- Ran a third time in-memory (not saved): all counts above still exactly 1 (or 2 for the two hand
  IK constraints).
- Ambiguity guard test: cleared `weaponSocket` to `null` and added a second bogus `"WeaponSocket"`
  object (in-memory, never saved) → `BuildWeaponRig` logged the multi-candidate error and left
  `weaponSocket` `null` rather than guessing. Confirms the exact failure mode that caused this
  incident can no longer silently repeat.

### Play Mode (fresh spawn via `GameFlow.StartGameplay()`, not a live-patched instance)

| Weapon | twoHanded | rightWeight | leftWeight | right target↔grip dist | left target↔grip dist |
|---|---|---|---|---|---|
| #0 Pistol | false | 1 | 0 | 0.00000 | — |
| #1 SMG | false | 1 | 0 | — | — |
| #2 Rifle (generic) | true | 1 | 1 | 0.00000 | 0.00000 |
| #15 Benelli M4 | true | 1 | 1 | 0.00000 | 0.00000 |
| #20 M4A1 | true | 1 | 1 | 0.00000 | 0.00000 |

Additional switching check (same `WeaponIKController` instance, no re-fetch): M4A1(2H) →
Beretta M9(1H, right=1/left=0) → SCAR-L(2H, right=1/left=1) → Pistol(1H, right=1/left=0). No stale
weights at any transition.

Fired `Weapon.TryFire` while on Rifle: `MuzzlePoint` position/forward identical immediately before
and after (no detachment); no new exception from the fire path itself.

### Console

**Clean:** no errors from the weaponSocket fix itself; roster/prefab structure checks all pass.

**Not clean — separate, pre-existing-class issue, discovered during this pass:** once the IK rig
is wired at all (which is what "wire it now" asked for), the Animator throws
`System.InvalidOperationException: The PropertyStreamHandle cannot be resolved.` every frame from
inside Unity's Burst-compiled `TwoBoneIKConstraintJob`, alongside `Could not resolve
'Player(Clone)/WeaponRig/ChestAim' because it is not a child Transform in the Animator hierarchy.`
This is **not** the weaponSocket-under-chest bug (it fires regardless of which socket the weapon
is attached to, and only exists because `RigBuilder`/`Rig`/constraint components didn't exist on
`Player.prefab` at all before this session). Tried and ruled out: `Animator.hasTransformHierarchy`
is `true` (Optimize Game Objects is off, so that's not it); toggling `RigBuilder.enabled` off/on to
force a rebuild did not clear it; calling `Animator.Rebind()` did not clear it either. Despite the
exception, every numeric and visual check above shows the IK **is** producing the correct hand
positions and weights — Unity/Animation Rigging appears to keep working via the constraint's
regular (non-Burst-stream) evaluation path even though the fast-path stream binding fails for this
Humanoid avatar's non-avatar rig transforms. Flagged as a remaining issue for separate
investigation (likely an Avatar/Humanoid-specific Animation Rigging binding limitation); out of
scope for this repair.

### ThirdParty

Unchanged for anything relevant to this repair or the project's weapon packs (`Low Poly Weapons
VOL.1`, `Low Poly ShotGun Weapon Pack 1`, `Low Poly Pistol Weapon Pack 1`,
`Low Poly Weapon Pack 4_MW_1` — zero diff). The 13 modified `Epic Toon FX` material/prefab files
under `Assets/ThirdParty` are pre-existing dirty state from before this session (present in the
very first `git status` of the whole working tree) and untouched by this repair.

## Evidence screenshots

`Assets/Screenshots/OnCharacter/`:
- `verify_order00_Pistol_handsocket.png` — one-handed, right grip only.
- `verify_order01_SMG_handsocket.png` — one-handed, right grip only.
- `verify_order02_Rifle_handsocket.png` — two-handed, both grips aligned.
- `verify_order15_BenelliM4_handsocket.png` — two-handed, both grips aligned.
- `verify_order20_M4A1_handsocket.png` — two-handed, both grips aligned.

## Not done in this repair (explicit non-goals)

- Did not touch weapon IDs, names, balance values, muzzle rotations, or any weapon prefab.
- Did not modify Animator/Avatar configuration to chase the `PropertyStreamHandle` warning.
- Did not stage, commit, or push.

---

# UPDATE — Final IK architecture (Option A: in-stream GunMount) + true root cause

The "PropertyStreamHandle cannot be resolved" / "not a child Transform in the Animator hierarchy"
issue flagged above as unresolved is now FIXED, and its true root cause is proven from package
source (`com.unity.animation.rigging@1.4.1`, `TransformHandle.cs:153`):

    if (!transform.IsChildOf(animator.avatarRoot)) throw new InvalidOperationException(...)

This player's Avatar is lifted from the CharacterModel FBX, so `animator.avatarRoot` is
`Player/CharacterModel` — NOT `Player`. Every rig object previously authored at `Player/WeaponRig`
was outside avatarRoot, so ReadWrite/property stream binding failed and the rig graph NEVER built
on this character. All previously observed IK "behavior" was plain animation posing.

Second proven pitfall: transforms between the rig-driven mount and the bound IK targets that are
only moved by game code (RecoilPivot) are invisible to the animation stream unless marked with a
`RigTransform` component (`RigUtils.GetSyncableRigTransforms`). Without it the hands ignored
recoil/mount displacement exactly by the displaced amount.

## Final architecture (all under avatarRoot)

    Player (Animator, RigBuilder, Weapon, WeaponIKController)
    └─ CharacterModel  (= animator.avatarRoot)
       └─ WeaponRig (Rig)
          ├─ ChestAim    MultiAim:    chest    <- AimTarget      [1]
          ├─ GunFollow   MultiParent: GunMount <- chest          [2]
          ├─ RightHandIK TwoBoneIK -> RightHandTarget            [3]
          ├─ LeftHandIK  TwoBoneIK -> LeftHandTarget             [4]
          ├─ GunMount
          │  └─ RecoilPivot (+RigTransform marker)
          │     ├─ RightHandTarget / LeftHandTarget  (persistent, local pose snapped per equip)
          │     └─ <weapon model instantiated here — not rig-referenced>
          └─ AimTarget / RightElbowHint / LeftElbowHint

No transform is ever reparented at runtime; `WeaponIKController.SnapTargetPositionToGrip` writes
each grip position into the persistent target once per equip while target rotation remains global
Player-rig data. `Weapon.weaponMount`
(FormerlySerializedAs weaponSocket) now points at GunMount; the legacy right-hand `WeaponSocket`
remains for old tooling but is unused at runtime.

## Verified

- RigBuilder graph builds: `graph.IsValid() == true` (first time ever on this character).
- Zero Animation Rigging console errors/exceptions across spawn, 6-weapon equip matrix, firing,
  and switching (Pistol/SMG/Rifle/Benelli/M4A1/SCAR-L/AK-47).
- Pistol convergence: right-hand tip -> grip distance 0.0000, rotation delta 0.0°.
- Movement proof: displacing RecoilPivot before the RigTransform fix left the hand off by exactly
  the displacement (0.1500); after the fix the hand follows the moved grip (residual 0.0305 while
  the character was live in gameplay).
- One-/two-handed weights correct through every switch permutation; no stale weights.
- Builder idempotent: identical `git diff` MD5 across repeated build+save runs.
- All 25 WeaponData: gripLocalEuler (0,90,180), gripLocalScale (1.5,1.5,1.5), positions unchanged.

## Historical tuning note (resolved by Update 2/3)

- The temporary common baseline and sky-facing pose described here were subsequently replaced by
  the measured per-weapon authoring flow below.

---

# UPDATE 2 — Combat pose authoring (sky-facing fix + 25-weapon tune)

## Proven cause of the backward-lean / sky-facing pose

Two independent, measured causes (camera contributed emphasis only — the 60° top-down production
camera was NOT modified):

1. **ChestAim axes were anatomically wrong.** Measured with the rig disabled: this QuickRig chest
   bone's anatomical forward is local **+Y** (0.926) and anatomical up is local **−X** (−0.993).
   The old `aimAxis=Z, upAxis=Y, all channels` config forced the chest's side axis at the target,
   twisting the torso (rig-ON shoulder height skew 0.137m vs 0.014m neutral; head rolled/pitched).
2. **maintainOffset captures against BIND pose at graph build**, while the AimType aim animation
   holds the spine far from bind — so even with corrected axes, any nonzero ChestAim weight
   dragged the torso sideways. Because the player ROOT already yaws fully to AimDirection
   (PlayerMovement.MoveRotation) and the aim animation authors the upper-body stance, ChestAim is
   authored but held at **weight 0** (documented in PlayerRigBuilder).
   Same reason: **GunFollow is position-only** (rotation channels off) — the mount stays
   root-aligned so the barrel is always on the firing line; rotation-follow inherited the bind-vs-
   animation delta and yawed the gun ~90°.

## Final authored pose data (Player.prefab)

- GunMount local (under CharacterModel/WeaponRig): pos **(0.03, 0.78, 0.08)**, rot identity.
- Elbow hints: R (0.307, 0.712, −0.068), L (−0.307, 0.712, −0.068).
- ChestAim: aim=Y, up=X_NEG, maintainOffset=true, limits ±60°, X channel only, **weight 0**.
- GunFollow: position XYZ w/ maintainPositionOffset, rotation OFF.

## Per-weapon tune (all 25, recorded in git diff of WD_* + WPN_* assets)

- Rotation from each model's real barrel axis: +Z families → Euler(0,0,0); −Z families → (0,180,0)
  (the temporary (0,90,180) baseline pointed every barrel sideways).
- Scale: one-handed 1.5, two-handed 1.2.
- Position solved so RightHandGrip lands at a fixed anchor (−0.02,−0.03,0) in RecoilPivot space.
- Two-handed LeftHandGrip markers pulled back along the handguard to a 0.06m span: the chibi
  arms (0.332m reach, shoulders 0.29m apart, bladed aim stance putting the left shoulder at
  z=−0.164) cannot physically reach the original foregrips at ANY reasonable scale — markers stay
  on the mesh, hands close together (compact CQB hold). This is a geometry limit, not a fake.

## Measured results (fresh spawn)

- Geometry: **25/25 pass** — right reach 0.39, left reach 0.74–0.76, muzzle-vs-root 0.0°.
- Hand bones (real bones, not targets): Pistol & M4A1 pos error **0.0000 m**, rot error **0.0°**.
- Movement proof (M4A1): RecoilPivot +0.12 m → both hand bones followed exactly (tip Y
  0.741→0.861 / 0.809→0.930), errors stayed 0.0000; restored cleanly.
- Direction sweep (F/R/B/L/45°): shoulder-height delta ≤0.015 m, hand err 0.0000, muzzle 0.0°.
- Weight matrix through pistol/AK47/fire/SMG/sniper switches: right always 1, left 1 only when
  twoHanded. Zero Animation Rigging console errors. Builder byte-idempotent (MD5 stable).
- NOTE: `head.forward`-based metrics are meaningless on this skeleton (Maya axes) — head/torso
  correctness was verified visually (front + ¾ before/after pairs, identical camera transforms).

## Evidence

`Assets/Screenshots/OnCharacter/`: poseBEFORE_rigON_34view.png, pose_neutral_rigOFF_34view.png,
poseAFTER_front_pistol.png, poseAFTER_34view_M4A1.png, poseAFTER_gameplaycam_final.png,
pose_neutral_rigOFF_gameplaycam.png.

## Remaining limitations

- Hands sit close together on long guns because of the chibi skeleton reach; this is a deliberate
  compact-CQB compromise; stocks may clip the shoulder at some angles. Revisit only with re-authored
  animations or shorter meshes, not by breaking the rig architecture.
- 8-direction test covered F/R/B/L/45° with movement frozen; ChestAim weight 0 makes the pose
  direction-invariant by construction.

---

# UPDATE 3 — Capture All and runtime restoration (setup phase complete)

- `WeaponPoseAuthoringEditor` now exposes `CAPTURE ALL -> WEAPON DATA + PLAYER IK`.
- Capture stores weapon-root local position/rotation/scale and right/left target positions expressed
  in weapon-root local space inside the equipped `WeaponData`.
- IK target rotations remain authored once in `Player.prefab`; weapon prefab axes do not own wrist rotation.
- `Weapon.EquipData` restores authored grip marker positions before invoking `OnWeaponEquipped`, so
  spawn and weapon switching reproduce the captured pose.
- Weapon prefab marker positions remain a fallback when authored grip data is disabled.
- All 25 current WeaponData assets have authored grip data enabled and have been visually verified by the user.

Current status: weapon placement, muzzle direction, firing stability and hand pose are accepted. Any
future rig change requires a reproducible regression plus new before/after visual and numeric evidence.
