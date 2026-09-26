# M8 round 2 — review + plan (for owner approval)

Date: 2026-09-26. **Nothing in sections A–F runs until the owner approves.** The one thing already
built is the skill sandbox (owner asked for it as the way to review effects).

Evidence:
- UI captures: `Review/UiTour/final/` (13 screens at 1080×1920).
- Skill captures from the sandbox: `Review/M8/fx_review/` (sheets `_sheet_storm_chain_airstrike.png`, `_sheet_drone.png`).
- Code audits (file:line) summarised per section below.

## 0. Built: skill sandbox (for review, dev-only)

- Menu **ZombieWar/Dev/Play Skill Sandbox**: starts from Bootstrap as usual, then skips the menu and
  loads `Scenes/Dev/SkillSandbox.unity`, a copy of the world scene. `Map_Level1` is not touched, and
  the sandbox is not in the build.
- The sandbox stops the horde, turns on god mode, and stands **3 immortal, pinned dummies** on screen:
  health refills after each hit, so the hit flash and damage numbers still play.
- The panel can:
  - give +1 or MAX of any of the 35 cards;
  - switch between 3, 10 or 30 dummies (a crowd for chain and airstrike);
  - run time at ×1 or ×0.25;
  - reset the skills.
- Files: `Runtime/Dev/SkillSandbox.cs`, `Editor/SkillSandboxMenu.cs`, plus an editor-only entry in
  `GameFlow.StartSandbox` / `BootstrapEntry`.

## A. UI corrections (owner feedback) · ~0.5 day

| # | Problem (seen in captures) | Fix |
|---|---|---|
| A1 | Corner radii disagree. A tile inside a card is rounder than the card (Featured card, Loadout cards), and buttons, cards and panels each use their own radius. | Radius tokens in `UITheme`: panel 40, card 32, **inner tile = card − inset (20)**, button 28, chip/pill = height/2. Set through `UISliceFit.BaseMultiplier` from the token, never by hand. Zoom-check every nested pair. |
| A2 | Gun icons fill the tile edge to edge, and a pistol is as big as a rifle. | Keep the per-gun crop, add 14% padding, and size each gun by class (share of tile width): pistol 0.62, SMG 0.78, rifle/shotgun 0.92, LMG/sniper 0.95. Regenerate 54 × 2 icons. |
| A3 | Dark text on the outline font (RESUME, PLAY AGAIN, BUY, CLAIM): the black outline swallows dark letters. | Rule: **Cairo Line (black outline) text is always white/ink**; dark text only on the plain Cairo font. Apply in `M8UiPolish`, and make `UiAudit` flag any outline-font label whose colour luminance is < 0.5. |

## B. Skill effects rework · ~2.5 days (all tuned in the sandbox)

**Root bug found:** `Materials/FX/M_SkillChainArc.mat` is **Opaque with ZWrite on**. Every arc, `Pulse`
and `Converge` ring uses it, so none of them can fade: they blink off. That is why airstrike leaves
huge hard red rings on screen. Fix first: transparent additive, no ZWrite.

| # | Now (evidence) | Plan |
|---|---|---|
| B1 Airstrike | A giant red converge ring plus a magic circle per target, 5 at once, covering the screen. The "bomb" is a particle comet shown for 0.3 s from 12 m, so it reads as a streak, not a bomb falling from the sky. The explosion and smoke look fine. | **A bombing run:** a small ground marker per target (a single thin ring that fades in), a shadow that grows under the drop point, and a **real bomb mesh** (`ETFX RocketMeshMissileFire`, or a small toon bomb) falling from ~14 m over 0.55 s at a slight angle with a whistle. The 3–5 impacts land in sequence along a line. Keep the current explosion, decal and smoke. Optional: a jet shadow crossing the screen. |
| B2 Chain Lightning | Straight blue "pipes" (the shape is computed once), hard cut-off at 0.3 s, the impact placed at the feet while the arc ends at chest height. | **Real lightning:** re-jag every 0.05 s (flicker), three layers (wide cyan glow, white core, 1–2 thin forks), tapered ends, hops that **travel** (0.03 s apart), a spark and flash at chest height on each hit, a 0.25 s fade. Thunderstorm: a sky bolt (`LightningStrikeTallBlue`) on every 2nd target and a violet tint. |
| B3 Drone | No model: a looping particle sphere (`MagicSphereBlue`), a fixed orbit, instant hitscan with the player's yellow tracer. | **A real drone rig** (owner brings the model; until then a low-poly placeholder built from primitives):<br>- **Movement:** hover bob, banks toward its velocity, strafes (figure-8 around the player, leading the target), turret turns to the target.<br>- **Firing:** muzzle point, 3-round bursts, reusing the player pipeline (`FxPool` muzzle `StandardMuzzleBlue`, a `MeshTracer` variant in the drone's colour, the impact FX).<br>- **Emissive upgrade colour:** rank 1 cyan → rank 2 green → rank 3 gold/orange → **Squadron evolution** magenta at stronger intensity plus a light trail. A `MaterialPropertyBlock` on one shared material, so no material copies. |
| B4 | Other cards were not reviewed one by one. | A sandbox pass over all 35 cards against one checklist: readable within 0.5 s, never covers the player, colour matches the card's layer, shows where it hits, has a sound. Output: pass/fix per card. |

## C. Costume + Shop redesign (mockups first, owner approves, then build) · design ~1 day, build ~3 days

**Costume now:**
- 18 slots, 10 chips on HEAD alone. Eyes, brows and mouth are separate "purchases".
- Icons are T-pose full-body renders, so face and accessory icons are unreadable.
- Buying and equipping are the same tap. Prices are text inside the name label, with no currency icon, lock or affordability colour.
- Coin prices (250–7,500) and Gem prices (15–180) are mixed together.
- Sets are small cells with no "wearing" state and no saving shown.
- 448 items, up to 4 pages per slot, no filters.

**Shop now:**
- The COSTUME tab duplicates the wardrobe (441 items, 56 pages) with no preview.
- "Featured" is a hard-coded G36C.
- No bundles, offers or IAP.

**Direction:**
- **Wardrobe (Costume screen) = try and wear:**
  - A large 3D preview taking about 55% of the screen, drag to rotate.
  - 4 tabs: FACE (eyes, brows and mouth merged into presets), HEAD, BODY, LEGS, with a chip row only where a tab needs one.
  - Owned items first, then "More", locked.
  - Tapping a locked item **tries it on**, and a bottom bar shows "Buy · 🪙 1,200" (currency icon, red when you can't afford it).
  - Icons re-rendered as close crops per slot (face and head crops, torso for tops) in an idle pose instead of T-pose.
- **Shop = storefront, in sections:**
  1. Featured offer (timed banner).
  2. **Packs** (IAP): Starter, Weapon packs, **No Ads**.
  3. **Gems** (IAP bundles).
  4. Weapons (Coin).
  5. Costume **sets** only, with a saving badge and "Try on", which opens the wardrobe.
  Single costume items move to the wardrobe.
- The mockup goes on the same Design canvas as the approved M8 mockups.

## D. Economy + monetisation · model ~1 day, IAP ~3 days, ads ~1.5 days

**Facts (audit):**
- **No IAP package, no ads SDK, and no entitlement field in the save.** "Watch ad", "Restore purchases" and "Premium pass" are placeholders.
- **Gold has no source.** It is used only by the hidden gacha and upgrades.
- A run banks **25% of its coins on death and 0% on walk-away.**
- A typical 5-minute run banks ~200–275 coins. Missions pay ~1,050 a day and ~6,200 a week, which is 4–5× more than runs.
- All 20 priced guns cost 12,960 coins in total. Costume items cost 250–2,200 coins, sets 2,500–7,500 coins or 60–180 gems.

**Proposal (to tune with a small simulation, `Tools/econ_sim.py`, before any number ships):**
1. **Two player-facing currencies:** Coin (soft, from play) and Gem (hard, from IAP plus a trickle from play). Gold stays hidden until gacha returns.
2. **Payout:** a run should feel worth playing. Options for the owner:
   - (a) keep 50% on death;
   - (b) keep 100% after surviving past a timed "extraction" (for example every 3 minutes).
   Target: a new common gun after 2–3 runs, a Rare in ~1 day, an Epic in 3–5 days, a Legendary in about 2 weeks or through a pack.
3. **Price ladder by tier:**

   | Tier | Price |
   |---|---|
   | Common | 150–400 |
   | Uncommon | 600–1,000 |
   | Rare | 1,500–2,500 |
   | Epic | 4,000–6,000 |
   | Legendary | 10,000 coins, or 300 gems |

   Weapon price, costume price and mission rewards come from one table the owner signs.
4. **IAP (Unity IAP, Google Play first):**

   | Product | Type | Price |
   |---|---|---|
   | `no_ads` | non-consumable | $2.99 |
   | `starter_pack` | one-time: a Rare gun + gems | $0.99 |
   | Weapon packs, e.g. "Heavy" (LMG + shotgun + Legendary skin) | non-consumable | $4.99 |
   | Gem bundles | consumable | $0.99–$19.99 |

   - Restore purchases goes in Settings.
   - Entitlements are saved in `PlayerProfile` (schema v3: `entitlements` plus the IDs of granted transactions, so nothing is granted twice), using the existing atomic purchase pattern.
5. **Ads (needs an SDK choice: LevelPlay or AdMob):**
   - A rewarded ad for revive, once per run.
   - A rewarded ad to double the banked coins.
   - An interstitial at most every 3 runs, **removed by `no_ads`**.

**Owner must provide:** the IAP price tier, the product list, the ads SDK choice, the Play Console app with its products (and bundle id / keystore from part D).

## E. Weapon roster → fill Epic and Legendary · ~3–4 days in steps

**Facts:**
- 54 guns in the catalog, 25 playable.
  - 26 wait for grip/muzzle anchors.
  - 2 need decimation (AR_W, AR_X).
  - 1 needs a projectile fire mode (Launcher_G).
- **22 gun bodies in the packs are not imported:** AR/SMG/Pistol K–P ×15, WWII ×5, RPG7, M2 .50 cal.
- **0 Legendary guns, 2 Epic** (both rifles).
- No premium models exist (minigun, flamethrower, energy), and there are no gold skins.
- The balance rules have no Legendary band yet: the owner needs to set one.

**Plan:**
1. **E1 faster anchors:** the queue window guesses grip, left hand and muzzle from the bounds, you nudge and save. That makes the 26 pending guns playable at about 1 minute each.
2. **E2 onboard the 20 new bodies plus WWII LMG/Recon** through the existing factory (audit triangles and duplicates first).
3. **E3 premium bodies:**
   - M2 .50 cal → Legendary LMG, uses normal hitscan.
   - AR_W / AR_X → Epic/Legendary rifles, after decimation.
   - RPG7 / Launcher_G → Legendary launcher; needs a projectile fire mode, the biggest item here.
4. **E4 Legendary identity:** Legendary = Epic power budget plus **one signature trait** (for example explosive rounds, pierce 2, or chain on crit), not just bigger numbers. Plus a **gold skin** (a recoloured palette material) for 3–4 icon guns (Deagle, AK, M2, SCAR) as pack rewards.
5. **E5 table for approval:** tier, family, price and trait per gun. Target ~75 guns: roughly 18 Common / 18 Uncommon / 16 Rare / 13 Epic / 8 Legendary.

## F. Full pass, gameplay → menu: other findings

| Where | Finding | Plan |
|---|---|---|
| Boot / loading | The splash title is text until logo art exists | Owner logo |
| PLAY / Home | Loader covers the wait: PLAY ~6.5 s, Home ~5.3 s. Menu load is heavy. | Profile the Menu scene load (preview stage, 448 costume icons); target < 2 s |
| HUD / level-up / result | Skill tiles show initials | Owner's ChatGPT icons → `UI/Icons/Skills/<id>.png` → Refresh Skill Icons |
| Pass | The free-rewards row is cut at the right edge with no scroll hint | Fade edge + peek |
| All | `CHEAT` button visible | Off in release (part D, `ZW_CHEATS`) |
| Shop | Featured slot hard-coded | Driven by an offer table (C) |

## Suggested order

A (0.5 d) → B (2.5 d) → C mockup (1 d, owner approves) → D economy table + sim (1 d, owner signs) →
C build + D IAP/ads (5–6 d) → E in steps (3–4 d).

## Needs the owner

1. Approve A–F and the order.
2. The drone model (a real drone; the placeholder is used until then).
3. The Legendary band and the trait idea (E4).
4. The payout option (D2 a or b) and the IAP product list/prices, the ads SDK, and the Play Console.
5. The logo art and the skill icons.
