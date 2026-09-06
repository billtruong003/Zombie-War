# M7.0 Task 1 — G5 grip validation: tool, method, and first-ever results

**Status:** the G5 check has now been RUN. Before this milestone it had never been executed on any
weapon, including the 25 already shipping.
**Tool:** `Assets/_Project/Scripts/Editor/Weapons/WeaponGripValidationCapture.cs`
(menu `ZombieWar/Weapons/Factory/G5 Grip Validation Capture (Play Mode)`)
**Evidence:** `Evidence/Grip/*.png` (50 captures) · `Evidence/Sheet_G5_GripValidation.png`
· `Evidence/g5_grip_results.csv` (50 rows, full metrics)

---

## 1. What the tool does differently

The M6.1 attempt solved camera distance from the **character's renderer bounds**
(1.38 x 1.27 x 2.30 m), ended up above the backpack, and produced six frames in which **no hand was
visible at all**. That check was correctly recorded as `NOT RUN`.

This tool never reads character bounds. Framing is solved from the equipped weapon's
`WeaponGripPoints` plus the player's up axis:

```text
target       = midpoint(authored grip, actual hand bone)
frameRadius  = clamp(max(barrelLength * 0.42, handGripSeparation * 1.35 + 0.06), 0.11 m, 0.52 m)
viewDir      = perpendicular to the barrel, from the side facing AWAY from the torso
               (so the body can never occlude the hand), lifted 0.34 and rolled for the 2nd view
distance     = frameRadius / tan(fov/2)
```

Framing on the **midpoint** rather than the grip is deliberate: centring on the grip alone cropped the
hand out of frame in exactly the cases where hand and grip are far apart — which is the defect G5
exists to reveal.

### The tool asserts its own success

Every capture projects the animator's hand bone into viewport space. **If the hand is not inside the
frame, the capture is marked failed and the tool reports a TOOL FAILURE** — not a verdict on the
weapon. Each PNG is also SHA-hashed, so two captures can never silently be the same image.

| Self-check | Result |
|---|---|
| Hand bone inside frame | **50 / 50** |
| Distinct images (no duplicate frames) | **50 / 50** |
| Equip verified (`Current == asset` and `CurrentSlot == requested`) | **50 / 50** |
| Slot mismatches (the M6.1 two-handed bug) | **0** |

Two defects in **this tool** were found and fixed by its own checks before any weapon was judged:

1. **39/50 distinct images on the first run.** For one-handed weapons the "second view" solved to a
   byte-identical camera, so it was not a second view. Fixed by rolling the eye 68 degrees around the
   barrel axis. Without the SHA column this would have shipped looking like 50 captures.
2. **`handToGripCm` measured every row against the RIGHT grip**, so left-grip rows reported the
   weapon's own grip spacing rather than a hand error. Fixed to measure each view's own hand and grip.

## 2. Results — 11 PASS / 14 FAIL

Pass condition: each required hand sits within **6 cm** of its authored grip. One-handed weapons are
judged on the right hand only; two-handed weapons on both.

| Weapon | Hands | right hand → grip (cm) | support hand → grip (cm) | G5 | Defect |
|---|---|---:|---:|---|---|
| `WD_AssaultRifle_AK47` | 2H | 2.8 | 19.6 | **FAIL** | support hand 19.6 cm off |
| `WD_AssaultRifle_FAMAS` | 2H | 20.3 | 44.9 | **FAIL** | right hand 20.3 cm off; support hand 44.9 cm off |
| `WD_AssaultRifle_G36C` | 2H | 5.7 | 42.8 | **FAIL** | support hand 42.8 cm off |
| `WD_AssaultRifle_Generic` | 2H | 0.0 | 13.6 | **FAIL** | support hand 13.6 cm off |
| `WD_AssaultRifle_M4A1` | 2H | 4.7 | 18.1 | **FAIL** | support hand 18.1 cm off |
| `WD_AssaultRifle_SCARL` | 2H | 1.3 | 14.4 | **FAIL** | support hand 14.4 cm off |
| `WD_LMG_Generic` | 2H | 0.0 | 21.5 | **FAIL** | support hand 21.5 cm off |
| `WD_Marksman_SniperGeneric` | 2H | 0.0 | 25.3 | **FAIL** | support hand 25.3 cm off |
| `WD_SMG_Generic` | 1H | 0.0 | — | **PASS** | — |
| `WD_Shotgun_AA12` | 2H | 0.0 | 16.5 | **FAIL** | support hand 16.5 cm off |
| `WD_Shotgun_BenelliM4` | 2H | 0.0 | 7.0 | **FAIL** | support hand 7.0 cm off |
| `WD_Shotgun_DoubleBarrel` | 2H | 0.0 | 7.6 | **FAIL** | support hand 7.6 cm off |
| `WD_Shotgun_Generic` | 2H | 0.0 | 14.9 | **FAIL** | support hand 14.9 cm off |
| `WD_Shotgun_Mossberg500` | 2H | 2.2 | 13.6 | **FAIL** | support hand 13.6 cm off |
| `WD_Shotgun_SPAS12` | 2H | 0.0 | 15.8 | **FAIL** | support hand 15.8 cm off |
| `WD_Sidearm_BerettaM9` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_DesertEagle` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_FiveSeven` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_Glock19` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_M1911` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_Makarov` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_P226` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_PistolA` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_Python357` | 1H | 0.0 | — | **PASS** | — |
| `WD_Sidearm_USP45` | 1H | 0.0 | — | **PASS** | — |

```text
one-handed  11 / 11 PASS
two-handed   0 / 14 PASS
```

## 3. Root cause — measured, not guessed

The failure is **systematic and it is not an IK bug**. Measured live on `WD_AssaultRifle_FAMAS`:

```text
arm chain (shoulder -> elbow -> hand)      33.2 cm
shoulder -> authored leftHandGrip          77.3 cm
IK target -> authored grip                  0.0 cm   <- the controller is doing its job exactly
hand bone -> authored grip                 44.2 cm   =  77.3 - 33.2
```

`WeaponIKController.HandleWeaponEquipped` snaps `leftHandTarget` onto `leftHandGrip` and sets the
constraint weight to **1.00** (verified live for both hands). The IK then solves perfectly: the arm
extends fully and still falls short by exactly the amount by which the grip exceeds arm reach.

> **The authored grip points on two-handed weapons are placed beyond the character's arm reach.**
> The weapons are authored at realistic human proportions; this character's arm is 33.2 cm. Every
> two-handed weapon puts its support grip 40–78 cm from the shoulder.

`outOfReach` (shoulder→grip > arm reach) is true for **20 of 50** captures — all 14 support grips and
6 primary grips.

This distinction matters for M7.1: a screenshot cannot tell "grip authored out of reach" from "IK
failed to solve", and the two have completely different fixes. The CSV now carries `armReachCm`,
`shoulderToGripCm` and `outOfReach` so the distinction is machine-readable for every future weapon.

## 4. Manual-fix queue handed to M7.1

**No weapon pose was changed in this milestone** — M7.0 records defects, it does not fix them.

| # | Item | Scope | Note |
|---|---|---|---|
| 1 | Support grip out of reach on **all 14 two-handed weapons** | rig or weapon authoring | The single largest visual defect in the shipped arsenal. Fix is a decision, not a nudge: either re-author `leftHandGrip` inward to within ~33 cm of the shoulder, or hold two-handed weapons closer to the body, or accept a stylised one-hand carry for long guns. **This is an owner call, not a data edit.** |
| 2 | `WD_AssaultRifle_FAMAS` primary grip 20.3 cm off | weapon authoring | The only weapon whose **right** hand also misses badly. Bullpup layout puts the grip far forward. |
| 3 | `WD_AssaultRifle_G36C` primary grip 5.7 cm | weapon authoring | Borderline; passes the 6 cm bar but `outOfReach` is true. |
| 4 | Weapon body intersects the torso on several two-handed captures | consequence of #1 | Visible in the FAMAS / G36C / LMG captures. Expected to resolve with #1; re-shoot after. |
| 5 | Re-run this tool after any fix | tooling | It is idempotent and takes about 90 seconds for the full 25. |

## 5. Honest limits of this result

- The 6 cm pass bar is **mine**, not an owner decision. It is a starting threshold, not a locked gate.
- `muzzleInsideModel` returned false for all 50; that check uses a shrunk bounds test and is weak
  evidence, not proof that no muzzle is buried.
- Finger-level interpenetration is not measured. The captures make it **visible** for a human, which
  is what a MANUAL gate means; nothing here automates that judgement.
- Captures were taken in one animation state (idle/aim on `Map_Level1`). A firing or reload pose could
  differ.
