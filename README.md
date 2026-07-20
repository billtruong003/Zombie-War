# Zombie War

Top-down mobile zombie survivor built with Unity 6000.3.10f1 and URP.

Current project status: [`Docs/HANDOFF.md`](Docs/HANDOFF.md).

Active work: [`Docs/TASK_BREAKDOWN.md`](Docs/TASK_BREAKDOWN.md).

Gameplay design: [`Docs/GAMEPLAY_DESIGN.md`](Docs/GAMEPLAY_DESIGN.md).

## Current foundation

- Auto-aim/auto-fire combat, virtual joystick, bomb and weapon switching.
- Pooled VAT zombies, NavMesh chase, waves, health/death and run overlays.
- Canonical 25-weapon roster with stable IDs, prefabs, icons, muzzle/grips and authored IK poses.
- Editable prefab-first UI for Hub, Loadout, Costume, Shop, Pass and HUD.
- 978-part modular costume catalog and RenderTexture menu preview.

## Main paths

```text
Assets/_Project/
  Data/Weapons/                 25 WeaponData assets
  Prefabs/Weapons/              25 WPN_* prefabs
  Prefabs/Player.prefab         player, animation and weapon rig
  Scenes/                       Bootstrap, Menu, Map_Level1
  Scripts/Runtime/Gameplay/     player, weapon, waves, zombies
  Scripts/Runtime/UI/           screen/view/runtime UI logic
  Scripts/Editor/UI/            UI authoring and validation tools
  UI/Prefabs/                   editable screen/preview prefabs
  UI/Icons/Generated/           generated item thumbnails
Docs/                           design, handoff and architecture records
```

## Packages and rules

- Animation Rigging 1.4.1 for Player weapon IK.
- AI Navigation 2.0.10 for zombie pathing.
- Input System 1.18.0.
- MCP for Unity for Editor automation.
- BillGameCore services and BillTween; do not add DOTween.
- Repeated runtime objects use pooling.
- Run GitNexus impact analysis before symbol edits and `detect_changes` before commits.

## Opening and testing

Open `Assets/_Project/Scenes/Bootstrap.unity` and enter Play Mode. Bootstrap registers Bill services
before loading Menu/Gameplay. Use the UI authoring validation menu before changing generated scene contracts.

Binary assets use Git LFS. Run `git lfs install` once per machine and obtain all LFS objects before opening Unity.
