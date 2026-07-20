# ZombieWar — Current Editor Checklist

> Updated 2026-07-20. The old manual Phase 1/2 assembly instructions are obsolete; Player, weapon rig,
> weapon assets, UI prefabs and scenes are already authored. Do not recreate them by hand.

## Open/test

1. Open `Assets/_Project/Scenes/Bootstrap.unity`.
2. Confirm Unity 6000.3.10f1 has finished import and compile.
3. Run `ZombieWar/UI/Authoring/Validate All UI References`.
4. Enter Play Mode from Bootstrap and verify Menu → Map_Level1.
5. Check Console for C# errors, missing references and repeated runtime exceptions.

## Weapon pose authoring

1. Enter Play Mode and equip the weapon to tune.
2. Select spawned Player → `WeaponPoseAuthoring`.
3. Use Manual Mode; place weapon and right/left targets.
4. Click `CAPTURE ALL -> WEAPON DATA + PLAYER IK`.
5. Switch away and back to verify init restore.

Grip positions are per WeaponData. Target rotations are global Player-rig data. Never move hand bones
directly and never re-parent GunMount/targets without reading `PlayerRigSocketIncident.md`.

## UI authoring

- Edit existing prefabs under `Assets/_Project/UI/Prefabs` for ordinary visual tuning.
- Use contract/validation menu commands for reference repair.
- Use `Generate Item Thumbnails` after catalog/icon changes.
- Use destructive rebuild commands only intentionally and review every prefab/scene diff.
- Capture ScreenSpaceOverlay evidence with `ScreenCapture.CaptureScreenshot`, not camera RenderTexture.

## Map authoring

- Current Map_Level1 is a functional test arena.
- Modify geometry/spawn layout in the scene, then re-bake its `NavMeshSurface`.
- Validate camera bounds, spawn distances and all enemy paths before balance testing.

## Never do

- Do not create old `WP_*`/generic `WD_*` assets; use canonical `WPN_*` and current `WD_<Family>_<Model>` assets.
- Do not restore a right-hand WeaponSocket ownership model; the weapon is mounted under WeaponRig/GunMount.
- Do not introduce DOTween.
- Do not migrate to Addressables/Resources during UI wiring/map blockout.
