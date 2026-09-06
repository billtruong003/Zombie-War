# M5+.1 — vision review of H0 / H1 / H2

All three images were opened and inspected directly. Nothing below is inferred from shader source,
property values or filenames.

## Controlled conditions

Identical across all three captures — only the material candidate changed:

```text
flow        : Bootstrap -> Menu/Hub (real flow)
camera      : PreviewCamera -> RenderTexture MenuCharacterPreview 512x900
pose        : animator held (Play at clip start, Update, then speed = 0)
              (speed = 0, NOT animator.enabled = false — disabling it drops skinning to T-pose,
               which invalidated an earlier capture pair)
light       : PreviewKeyLight, Directional, (1, 0.97, 0.90), intensity 1.1, rot (40, 323, 0)
fake rig    : _ToonLightDirection = (0,0,0,0)  -> the Hub exercises the URP main-light FALLBACK path
outfit      : unchanged authored costume
roles       : 14 renderers toon, 2 renderers metal (Costume_Hands, Costume_Earring)
```

Evidence:
`H0_Current/H0_controlled-1.png` · `H1_MinionsGraphic/H1_controlled.png` ·
`H2_MinionsSoft/H2_controlled.png`

## What H0 actually looks like

- **White hair is a single flat mass.** There is a faint grey wash at the silhouette edges, but no
  interior plane change at all. The hair volume does not read.
- **Legs/feet read as near-white** with no internal form.
- **Gauntlets read as grey putty or stone.** A soft gradient, no deliberate band, no metal cue.
- **Teal shirt** has a long soft airbrushed ramp rather than a division.
- **Face is pale and low-contrast against the hair**, so the focal point is weak.
- Shadow is a neutral grey — no stylized hue.

This matches the owner's rejection exactly: generic, washed, plastic.

## Criterion table

| Criterion | H0 | H1 Graphic | H2 Soft | Winner |
|---|---|---|---|---|
| White hair plane readability | none — flat mass | **clear**: warm cream top vs cool lavender under-planes | present, gentler | **H1** |
| Face readability / focal read | weak, pale | strong, warm skin separates from hair | strong | H1 ≈ H2 |
| Skin vs hair separation | poor | clear | clear | H1 ≈ H2 |
| Shadow shape quality | soft ramp, no shape | one crisp division | one soft-but-real division | **H1** |
| Cloth read (teal shirt) | plasticky gradient | fabric-like, clean division | fabric-like, softer | H1 |
| Dark cloth (brown shorts) | flat | retains internal form | retains internal form | tie |
| Metal highlight shape | none — putty | **deliberate band + warm edge** | band present, gentler | **H1** |
| Resembles chrome? | no (putty instead) | no | no | tie (all pass) |
| Small jewellery (earring) reads as metal | no | yes | marginal | **H1** |
| Rim behaviour | n/a | narrow, light-facing only, no halo | narrower still | tie (both pass) |
| Shadow colour | neutral grey | stylized cool violet | stylized cool violet | tie |
| Visibly different from H0 | — | **strongly** | clearly | H1 |

## Classification

- **H0 — REJECT.** It is the rejected baseline; the flat white hair and putty gauntlets are the exact
  defects this phase exists to fix.
- **H1 MinionsGraphic — ACCEPT.** Solves both headline defects and passes every listed acceptance gate.
- **H2 MinionsSoft — ACCEPT (second).** Same architecture, clearly better than H0, but the metal band
  and hair planes are weaker; on the earring it is close to not reading at all. Given the brief
  explicitly forbids drifting back toward the washed look, H2's softness is a step in that direction.

## An observation worth Codex's attention

In H0 the legs/feet read as near-**white**; in H1 and H2 they read as warm **skin**. This is not a hue
the prototype invented — it is the authored atlas colour becoming visible again. H0's SH/GI ambient
was lifting those surfaces toward white regardless of normal direction, which is the same mechanism
that flattened the hair. So the leg colour change is evidence of the wash being removed, not of a new
tint being added.

If the owner intends those to be white boots rather than bare skin, that is an **atlas/costume-part
question**, not a shader one, and should be resolved before production migration.

## Recommendation

**H1 — `ZombieWar/Character/ToonPrototype` + `ZombieWar/Character/MetalPrototype` at the H1 parameter
set.** It is the only candidate that makes white surfaces hold their planes and makes small metal parts
read as metal, which were the two rejected defects.
