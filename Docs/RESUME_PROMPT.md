# Resume prompt — current project context

Use the execution prompt supplied after the 2026-07-20 handoff commit. Before acting, read:

1. `Docs/HANDOFF.md` — canonical current status and execution order.
2. `Docs/HANDOFF_UI_CODEX.md` — exact UI architecture and wiring gaps.
3. `Docs/UI_REDESIGN_SPEC.md` — visual source of truth.
4. `Docs/WeaponRosterMapping.json` — canonical 25-weapon identity/order.
5. `Docs/PlayerRigSocketIncident.md` — rig architecture and resolved failure history.
6. `AGENTS.md` / `CLAUDE.md` — repository rules.

Do not use the old assumptions that the project has two or six weapons, that Player IK is unwired,
that Pass/HUD prefabs do not exist, or that all UI must spawn at runtime. Those statements are obsolete.

Current focus: wire real ownership/equipment/economy data into the authored UI, verify persistence and
scene transitions, then design and balance the bounded Level 1 arena. Weapon setup is complete unless a
new reproducible regression is found.
