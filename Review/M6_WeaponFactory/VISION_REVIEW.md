# M6.1 — Vision review of the weapon contact sheets

All six sheets were **opened and inspected**. Nothing below is inferred from filenames or from the
inventory CSV. Sheets live in `Evidence/`.

| Sheet | Contents | Opened |
|---|---|---|
| `Sheet_01_Pistols.png` | 12 sidearm bodies | ✔ |
| `Sheet_02_SMG_AR_LMG.png` | 14 SMG / AR / LMG bodies | ✔ |
| `Sheet_03_Shotgun_Marksman.png` | 8 shotgun / marksman bodies | ✔ |
| `Sheet_04_Launcher_Special.png` | 5 launcher / mounted bodies | ✔ |
| `Sheet_05_Attachments_Duplicates.png` | 24-item sample of the 374 non-weapons | ✔ |
| `Sheet_06_PlayerHeld.png` | 6 representatives held by the real player in gameplay | ✔ |

---

## Sheet 01 — Sidearms

- At **inspection scale**, roughly half the sidearms are separable, and always by **colour, not shape**:
  `FiveSeven` (tan), `USP45` (tan), `M1911` VOL1 (brown grip), `Makarov` (cream grip panel),
  `BerettaM9` (brown grip), `DesertEagle` (open revolver frame).
- `Glock19`, `P226`, `PistolA`, `Python357` are effectively one dark shape.
- `Pistol_P` (MW4) is the **most distinct sidearm silhouette in the project** — a machine-pistol with
  an extended magazine and top rail — but it renders very dark because it is URP Lit.
- **Conclusion:** most sidearms become difficult to distinguish at gameplay scale; visual art alone is
  insufficient to carry weapon identity. Colour is the cheapest available identity lever.

## Sheet 02 — SMG / AR / LMG

- **The URP-Lit finding is visible here and is decisive.** `SMG_P`, `AR_T` and `AR_U` (all MW4) render
  as near-black shapes with no toon outline, sitting flat against the dark background, while
  toon-shaded neighbours (`AK47` orange wood, `SCARL` tan, `M4_8` two-tone) read cleanly.
  Material conversion is a **hard gate**, not polish.
- `WPN_LMG_Generic` (M249) is the **strongest silhouette in the entire arsenal**: bipod plus a green
  ammo box, unmistakable at any size.
- `WPN_AssaultRifle_FAMAS` has a genuinely unique bullpup outline — the only AR distinguishable by
  shape rather than colour.
- `WPN_SMG_Generic` (Uzi) is compact with a long protruding magazine — clearly separable from both
  pistols and rifles.
- `M2_50cal` shows a tripod: it is a **mounted emplacement**, not a carried weapon. Correctly LATER.
- Poly cost is visible in the labels: MW4 bodies run 10 300–11 700 tris against 3 600–8 100 for the
  project's own rifles.

## Sheet 03 — Shotgun / Marksman

- `WPN_Shotgun_DoubleBarrel` is instantly readable — long break-action barrels with a **brown wooden
  stock**, the only shotgun with warm colour.
- `WPN_Shotgun_AA12` reads as a boxy drum-fed weapon; distinct from the pump shotguns.
- `Mossberg500` and `SPAS12` are near-identical dark pump silhouettes.
- `Recon_P` (MW4 marksman) has an excellent long-barrel sniper silhouette **and** a tan/olive body —
  visually the best marksman asset available — but again URP Lit.
- `WPN_Marksman_SniperGeneric` (M107) is long and readable but entirely grey.

## Sheet 04 — Launcher / mounted / special

- `GrenadeLauncher_A` and `_B` read as compact standalone launchers; `_C` is larger with a pistol grip
  and top rail.
- `RPG7` is the only one with strong colour (brown tube, olive warhead) and an unmistakable profile.
- `M2_50cal` is tripod-mounted.
- All five are correctly classified LATER: **no runtime `Projectile` fire-mode path is implemented**,
  and a mounted weapon has no place in a one-weapon mobile run.

## Sheet 05 — Attachments / duplicates / demo objects

- The attachment sample confirms the classification: lasers, weapon lights, muzzle brakes, flash
  hiders, suppressors and rails are all small modular parts, correctly excluded from the weapon count.
- `Distance_Measuring_Equipment` and `HeartBeat_Sensor` render as flat dark boxes — gadget props, not
  weapons.
- The `Pistol_A` … `Pistol_H` row is the vendor original of the project's sidearms, confirming the
  duplicate classification visually as well as by mesh signature.

## Sheet 06 — Representative weapons held by the real player

### What this sheet does prove

**Six distinct weapons were equipped on the real player through the real equip path.** Each frame used
its own instance — the log records `WPN_AssaultRifle_G36C(Clone)`, `WPN_Shotgun_AA12(Clone)`,
`WPN_Marksman_SniperGeneric(Clone)`, `WPN_LMG_Generic(Clone)`, `WPN_SMG_Generic(Clone)` and
`WPN_Sidearm_FiveSeven(Clone)` — and the six PNGs differ from one another. An earlier attempt in which
four of six frames were byte-identical copies of the SMG shot was discarded, not presented.

### What this sheet does NOT prove — `NOT RUN`

> **Visual grip validation: `NOT RUN — requires a fixed rig-relative camera`.**
>
> The camera solved its distance from the player's own renderer bounds (1.38 × 1.27 × 2.30 m) and ended
> up sitting almost directly above the character's backpack. **No hand is visible in any of the six
> frames** — only a partial barrel at the edge of frame.
>
> Therefore this sheet **cannot** support any claim about:
>
> - hand penetration through the weapon model,
> - grip alignment of the right or left hand,
> - muzzle placement relative to the player,
> - weapon orientation relative to the aim axis.
>
> An earlier version of this document asserted "no visible hand penetration ... and no weapon floating
> away from the grip". **That assertion was not readable from these pixels and has been withdrawn.**
> Calling it merely "poor framing" understated the problem: the check was never performed.

### What the grip contract IS evidenced by — data, and it is genuine

Independent of any screenshot, and independently confirmed:

- **25/25** production weapon prefabs carry `WeaponGripPoints` with right-hand, left-hand **and** muzzle
  transforms assigned.
- **25/25** `WeaponData` assets set `useAuthoredGripPositions = true`.

That is real evidence that the authored grip pipeline exists and is applied across the shipped arsenal.
It is **not** evidence that any individual weapon looks correct in the hand — only a rig-relative
capture can show that.

**Queued, not done:** building a fixed rig-relative grip-validation camera and re-shooting the holding
sheet is Factory **tooling** work. It is recorded in `WEAPON_FAMILIES_AND_FACTORY.md` section 5.3 and in
the **M7.0** deliverable list. It was deliberately not attempted in the correction pass, which is
documentation-only.

---

## Cross-cutting conclusions

0. **Visual grip validation has not been performed.** The holding sheet proves the equip path works
   and that six different weapons were mounted; it proves nothing about how they sit in the hand.
   That check is queued as Factory tooling (M7.0).
1. **Identity cannot come from the models.** Only about six bodies in the whole arsenal are
   recognisable by shape alone (LMG_Generic, FAMAS, DoubleBarrel, AA12, Uzi, Pistol_P). Everything
   else needs behaviour, colour or both.
2. **URP-Lit conversion is a blocking gate for MW4**, which is 263 of the 415 prefabs and the source of
   the best SMG and marksman bodies.
3. **The best unused assets are `Recon_P` (marksman) and `SMG_P` (SMG)** — both would materially
   improve two single-body families, and both need conversion.
4. **Colour is the cheapest identity lever available** and is currently under-used: the arsenal is
   overwhelmingly dark grey.
