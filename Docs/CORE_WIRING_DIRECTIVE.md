# Core Wiring Directive (Phase 1-3 completion + BillGameCore integration)

Captured from the user's directive so no requirement is lost across sessions. This
governs the "finish Phase 1/2/3 + wire" work, not just nice-to-have.

## Hard requirements
1. **Everything must actually USE BillGameCore systems** (pooling, events, services,
   tween, audio). If any Phase 1/2/3 code was built on raw Unity (`Instantiate`/
   `Destroy`, `FindObjectOfType`, hand-rolled singletons, `new AudioSource`), it must
   be rewritten to route through BillGameCore. Self-audit first, then rewire.
2. **Zombies are inheritance-based**, NOT one shared script for all. A base type +
   derived variants (Melee / Ranged / Speed / Boss per the GDD). Data-driven config
   (ZombieData) is fine, but behavior specialization goes through subclasses, not
   giant switch statements.
3. **Player is spawn-based + map loads additive.** Character is NOT placed in a scene;
   it's spawned at runtime, because a **modular character** task comes later
   (CharacterModularApplier / LocomotionModular over the Character Pro SuperCasual
   model). Map = additive scene load. This keeps modular swapping possible.
4. **IK-driven upper/lower body:**
   - Hands use IK to hold the gun and to make the body rotate toward the aim
     direction (Two-Bone IK on hands + Multi-Aim for aim, Animation Rigging).
   - Feet use IK to stick to the ground (foot IK).
   - Leverage the Malbers 8-direction strafe anims already imported
     (`Locomotion/Strafe/S_Strafe_Jog_{N,NE,E,SE,S,SW,W,NW}.fbx`) as the locomotion
     blend tree — this makes the aim/strafe decoupling much easier.
5. **Model:** Character Pro SuperCasual (`Layer Lab/3D Characters Pro - Fantasy...`).
   User will let the agent set up everything (rig, prefab, wiring).

## Sequencing decision to make (self-assess)
- Finish the Phase 1/2/3 CORE and wire it solidly FIRST.
- If **wave system + world streaming** turn out to be needed to actually exercise/
  playtest the core loop, build those first, then wire. Otherwise defer to Phase 4.

## Polish phase (Phase 5) — sound
- Sound package: **AI-SFX-Studio** — https://github.com/billtruong003/AI-SFX-Studio
  (an SFX + BGM generator; can generate both sfx and bgm). Not imported yet — user
  noted it here for later.
- All sound playback MUST go through BillGameCore's audio service + pooling (pooled
  AudioSources), not `new AudioSource` / `AudioSource.PlayClipAtPoint`. Leave the
  hooks now (fire events / a thin SFX facade) so wiring audio in Phase 5 is drop-in.

## Definition of "wired" for this pass
- Scenes exist and scene management works (bootstrap -> menu -> gameplay, additive map).
- UI/HUD exists and is bound to gameplay (health, ammo, wave, joystick/fire on mobile).
- Player spawns; weapons fire via pooled projectiles/impacts; zombies spawn from a
  pool via an inheritance hierarchy; damage/death route through the shared services.
- No orphan systems: if BillGameCore provides it, gameplay uses it.
