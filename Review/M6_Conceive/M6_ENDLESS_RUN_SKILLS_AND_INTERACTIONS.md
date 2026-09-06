# M6 — Endless Run Skills & Interactions (SUPERSEDED)

> **This working draft has been consolidated. The canonical M6 system design now lives at:**
>
> ## [`Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md`](../../Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md)

Nothing in this file is authoritative. It is kept only as a pointer so existing links do not break.

Everything it contained was audited, corrected and folded into the canonical document:

- the creative skill pool was **cut** from ~50 candidates to a 15-card vertical-slice pool, with a
  reason and a destination recorded for every removal;
- three prototype weapons were **selected on rendered visual evidence**
  (`WD_Sidearm_FiveSeven`, `WD_SMG_Generic`, `WD_Shotgun_AA12`);
- every station was given a full spec under one reusable interaction grammar;
- the economy was **decided** rather than left "under review" — Gold, Weapon Shards, star upgrades and
  gacha are `DORMANT / NOT PART OF ACTIVE M6`;
- the outfit grammar recommendation is **PROMOTE H2**, at a measured 73 % PASS.

Two claims in the draft were checked against source and resolved:

| Draft claim | Verdict |
|---|---|
| "`WaveClearedEvent` auto-collects remaining pickups" | **CONFIRMED** — `PickupManager.cs:141` calls `CollectAll()`. Removal is a MUST for endless. |
| "existing run multipliers are not fully consumed" (from `SKILL_SYSTEM_DESIGN.md §1`) | **STALE** — all five `RunPerkKind` values are consumed today. |

Supporting investigation from the same phase remains valid and unchanged:
`M6_CURRENT_STATE_AUDIT.md` · `M6_SKILL_AND_LOOP_AUDIT.md` · `M6_OUTFIT_ASSET_AUDIT.md` ·
`M6_OUTFIT_GRAMMAR_PROPOSAL.md` · `M6_DECISION_PACKET.md`
