# Claude execution prompt — UI/state wiring phase

Work in `D:\Project\ZombieWar`. Execute this phase end-to-end; do not only audit or return a plan.

Use the `expert-developer` skill for all implementation decisions. Also use the project GitNexus
skills for exploration/impact/detect-changes, `billgamecore` for service/event conventions, Unity MCP
skills for prefab/scene inspection and verification, and `simplifier-vi` only for the final summary.

## Read first

Read these files completely before editing:

1. `AGENTS.md` and `CLAUDE.md`.
2. `Docs/HANDOFF.md`.
3. `Docs/HANDOFF_UI_CODEX.md`.
4. `Docs/UI_REDESIGN_SPEC.md`.
5. `Docs/WeaponRosterMapping.json`.
6. `Docs/ECONOMY_DESIGN.md` — proposed values only; reconcile with source rather than assuming it is implemented.
7. `Docs/PlayerRigSocketIncident.md` — do not disturb the accepted weapon rig.

Then inspect the current source and authored assets, especially:

- `LoadoutState`, `PlayerSpawner`, `Weapon`, `WeaponData` and all 25 WeaponData assets.
- `UIPrototypeCatalog`, `CurrencyClusterWidget`, `LoadoutScreen`, `ShopScreen`, `CostumeScreen`, card/slot views.
- `MenuCharacterStage`, `CharacterModularApplier`, modular costume catalog and Player costume application.
- Menu/Map scene contracts and all six UI prefabs.

Use GitNexus query/context to trace current save → menu → scene transition → Player spawn/equip flows.
Run upstream impact analysis before every symbol edit. If risk is HIGH/CRITICAL, report it before editing.

## Objective

Wire the existing authored UI to real, persistent ownership/equipment/currency state without rebuilding
the accepted visual design. The result must support a complete loop:

`Bootstrap → Menu → inspect/buy/equip weapon or costume → Play → spawned Player uses saved loadout/costume → GameOver/return → Menu state remains correct`.

This is not the map-design phase, weapon-pose phase, or Addressables phase.

## Non-negotiable constraints

- Preserve all accepted weapon prefabs, muzzle/grip data, WeaponData authored poses and Player rig hierarchy.
- Do not modify ThirdParty assets to solve project logic.
- Do not replace editable UI prefabs with runtime-generated screens.
- Do not run destructive UI rebuild commands unless a prefab is truly missing; ordinary work must edit/bind existing prefabs and scene contracts.
- Keep `UIPrototypeCatalog` for icon/featured authoring metadata only. It must not remain the production ownership/save authority.
- Runtime code must not mutate ScriptableObject assets.
- Use stable `WeaponData.WeaponId`, never catalog index, display name or asset filename as persistent identity.
- Preserve legacy alias migration in `LoadoutState`.
- Use BillGameCore service/event patterns already present; do not add a new global singleton.
- Use BillTween/UITransition, never DOTween.
- Keep the 978-part costume UI pooled/paged; never instantiate one cell per catalog entry.
- Do not introduce Addressables or Resources migration.
- Do not stage, commit or push.

## Required implementation

### 1. Authoritative profile/wallet state

Audit existing save facilities first, then implement the smallest production-clean authority that fits
the codebase. It must persist and version at least:

- Coin/Gold/Gem balances using `long`-safe serialization.
- Owned weapon stable IDs.
- Three equipped weapon slots.
- Per-weapon upgrade level if the current Upgrades UI exposes levels; otherwise keep a forward-compatible field but do not invent upgrade effects.
- Owned costume part GUIDs and equipped costume GUID per logical slot.

Requirements:

- Migrate current `zw.loadout` data and the existing `wallet_coin/wallet_gold/wallet_gem` PlayerPrefs values without losing them.
- Resolve old weapon asset names through `LegacyAliases`, then save canonical IDs.
- Corrupt/partial JSON must fail safely with a warning and recover defaults without an exception loop.
- Currency cannot underflow or overflow silently.
- Expose change events so every visible widget refreshes without polling.
- Provide an explicit Editor/dev reset or seed path; do not hide magic test currency in runtime code.

### 2. Ownership/default rules

- Determine current default Player weapon slots from the prefab/source before choosing starter ownership.
- Any weapon required by the default loadout must be owned on a fresh profile.
- Existing `WeaponData.price` is the current Coin purchase price. `price == 0` means starter/free for this phase.
- `unlockCost` is not authoritative because current assets are zero; do not charge it accidentally.
- Duplicate purchase is idempotent and charges nothing.
- Insufficient funds changes no state and gives visible feedback.
- Keep `cheatUnlockAll` as an Editor/development display override only; it must never write ownership or bypass transaction rules in a player build.

### 3. Loadout wiring

- Show all 25 weapons in canonical catalog order with real icon/name/tier/class/stats.
- Locked weapons cannot be equipped.
- Slot 0 accepts one-handed sidearm/default-compatible weapons according to the existing slot contract.
- Slots 1–2 accept the current long-weapon contract; do not silently place incompatible weapons.
- Selecting a slot changes the picker target; selecting an owned compatible card equips it and persists.
- Empty long slots remain supported if the runtime supports them.
- Reopening Loadout, restarting Play Mode and entering gameplay must reproduce the same three slots.
- `PlayerSpawner`/`LoadoutState.ApplyTo` remains the single gameplay application path.

### 4. Shop weapon wiring

- Bind card owned/locked/affordable/selected/equipped states from the authoritative profile.
- Buy uses an atomic transaction: validate → deduct Coin → add ownership → persist → emit events → refresh.
- Owned cards offer Equip or direct navigation to Loadout; never show a buy price again.
- Missing/null WeaponData, empty/duplicate stable IDs and negative prices must be caught by Editor validation and handled safely at runtime.
- Gacha and upgrade tabs must not fake success. Wire only what has a real backend in this phase; otherwise disable the action with honest text/logging and document it.

### 5. Costume wiring

- Browsing and preview behavior remains pooled/paged.
- Selecting an owned part applies it to the preview and persists the equipped GUID for its logical slot.
- Define a minimal safe fresh-profile ownership rule from currently equipped/default parts; do not mark all 978 parts owned in production merely because prototype cheat mode does.
- If prices/rarities are absent for costume parts, do not invent a large economy table in code. Keep locked/purchase behavior disabled or use clearly authored metadata with validation.
- Menu preview and spawned gameplay Player must apply the same saved equipped parts.
- Missing GUIDs after asset changes are ignored safely and reported once, not every frame.

### 6. Currency and run-result bindings

- Replace `PlayerPrefsCurrencyProvider` as the production default with the authoritative provider while preserving its interface if useful.
- All currency clusters refresh immediately after transactions and scene changes.
- Do not fabricate GameOver rewards. If a real run result/reward source does not exist, leave payout visibly disabled/zero and document the precise missing backend.
- Pass claim, rewarded revive and Gacha must follow the same rule: no fake state mutation.

### 7. Editor validation and authored references

Extend the existing UI validation tooling only where needed. It should report:

- Duplicate/empty weapon stable IDs or catalog orders.
- Missing weapon icons/prefabs/card data.
- Missing UI profile/provider references.
- Missing costume GUID/icon fallback and broken preview/gameplay applier references.
- Missing screen prefab or scene contract references.

Use Unity Undo/SetDirty/PrefabUtility correctly for Editor mutations. Re-running authoring/validation must be idempotent.

## Verification

Use Unity MCP to inspect actual prefab/scene hierarchy, serialized fields, runtime state and Console.
Do not declare success from code inspection alone.

Run at minimum:

1. Script compilation/validation with zero new C# errors.
2. Relevant EditMode tests for migration, corrupt save recovery, purchases, insufficient funds, duplicate purchase, slot compatibility and formatting of large currency.
3. Play Mode from `Bootstrap.unity` on a fresh profile:
   - defaults are valid and owned;
   - purchase one weapon;
   - equip it to a compatible slot;
   - equip at least one costume part;
   - enter Map and inspect spawned Player weapon/costume;
   - return/restart and confirm persistence.
4. Negative-path runtime tests: locked equip, incompatible slot, insufficient funds, duplicate buy, missing GUID fallback.
5. Visual screenshots of Hub, Loadout, Costume, Shop Weapons and gameplay HUD using `ScreenCapture.CaptureScreenshot`; inspect the images, not only hierarchy.
6. Console audit after the full flow. Separate pre-existing MCP transport noise from project errors; report both honestly.
7. Confirm all 25 weapon poses/muzzles/IK data and all ThirdParty weapon packs have no unintended changes.
8. Run `detect_changes(scope="all")` and review that only expected state/UI/test/docs symbols and authored references changed.

## Documentation and final report

Update `Docs/HANDOFF.md`, `Docs/HANDOFF_UI_CODEX.md`, `Docs/TASK_BREAKDOWN.md` and any affected
architecture/economy documentation so no completed or deferred item is mislabeled. Report:

- data authority and migration path;
- exact files/assets changed;
- tests and screenshots;
- UI flows proven end-to-end;
- intentionally disabled backends and why;
- any real blocker or remaining risk.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools/notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
