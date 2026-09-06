# M5+.1 — H0 / H1 / H2 comparison

## Candidates

| | H0 Current | H1 MinionsGraphic | H2 MinionsSoft |
|---|---|---|---|
| toon shader | StylizedToonWorldKit/Toon/Toon Lit (vendor) | ZombieWar/Character/ToonPrototype | same |
| metal shader | StylizedToonWorldKit/Surface/Metal (vendor) | ZombieWar/Character/MetalPrototype | same |
| lighting model | main light + additional-light loop + SH/GI + broad specular + generic rim | one artist-placed band + bounded ambient + light-facing rim | same, softer band |
| passes | 3 / 3 | 3 / 3 | 3 / 3 |

## H1 vs H2 — the documented parameter delta

Only these values differ. Architecture, camera, pose, light, outfit and background are identical.

| property | H1 Graphic | H2 Soft |
|---|---:|---:|
| Toon `_LightSoftness` | 0.035 | 0.11 |
| Toon `_AmbientStrength` | 0.22 | 0.30 |
| Toon `_RimStrength` | 0.30 | 0.22 |
| Metal `_LightSoftness` | 0.03 | 0.08 |
| Metal `_BandSoftness` | 0.06 | 0.12 |
| Metal `_BandStrength` | 0.50 | 0.38 |
| Metal `_HighlightStrength` | 0.60 | 0.42 |

## Shader contract of the recommended candidate

```text
albedo (atlas)
  x  lerp(shadowTint, lightColor, smoothstep-band(N.L))     <- one division, artist threshold+softness
  +  albedo x ambientColor x ambientStrength                <- bounded floor, NOT SampleSH
  +  lightFacingRim(fresnel) x band x albedo-tinted         <- cannot halo, cannot cross shadow
  (+ tight N.H specular, default OFF for cloth/skin)
```

Metal adds, on top of the same toon base:

```text
A  smoothstep(dot(N,V))              -> view-normal band, lerped toward albedo x bandColor
B  smoothstep(dot(N, normalize(V+L)))-> offset highlight, pushed toward the light side
C  fresnel rim x band                -> restrained metal edge
```

Deliberately absent: SH reflect-vector, cubemap, environment reflection, full-surface HDR anisotropic
sheen, additional-light loop.

## Lighting-source contract

`ZW_GetCharacterLight()` prefers the `_ToonLightDirection` / `_ToonLightColor` globals pushed by
`ToonLightRig`, and falls back to `GetMainLight()` when the rig is inactive (alpha flag 0 or zero
vector). The Hub has no rig active, so these captures exercised the **fallback** path; gameplay
exercises the rig path.

## Metrics

```text
Hub frame during capture : 65 batches / 24 SetPass
shader passes            : H0 3+3 · H1/H2 3+3   (ForwardLit, ShadowCaster, DepthOnly)
runtime materials created: 0   (prototype materials are shared assets, assigned to the runtime clone)
vendor assets modified   : 0
catalog / profile / outfit modified : 0
scenes modified          : 0
```

## Status

Recommended: **H1**. Not migrated. No production ColorA/B/C/D reference was changed; the swap was
applied to the runtime clone only and reverted before exiting Play Mode.
