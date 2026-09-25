<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **Zombie-War** (55345 symbols, 71806 relationships, 522 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`; report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- MUST warn on HIGH/CRITICAL `risk` pre-edit; never use `riskSharedAxes` to waive a HIGH/CRITICAL `risk` warning. Compare File/symbol: MCP File omits axes; Graph-RAG expands File.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- **MUST use `query({search_query: "concept"})` for concepts/flows, `context({name: "symbolName"})` for a named symbol, or `impact` for blast radius, on read-only callers, dependencies, imports, or execution flow.** Graph first; text search only for empty/`UNKNOWN`/literals.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/Zombie-War/context` | Codebase overview, check index freshness |
| `gitnexus://repo/Zombie-War/clusters` | All functional areas |
| `gitnexus://repo/Zombie-War/processes` | All execution flows |
| `gitnexus://repo/Zombie-War/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->

## Project handoff

Before implementation, read in this order:

1. `Docs/CURRENT_STATE.md` — verified project state (evidence is `file:line`, not prose)
2. `Docs/FRAMEWORK.md` — BillGameCore / BillTween / asmdef rules; never DOTween
3. `Docs/MVP_SHIP_PLAN.md` — production scope, priorities and acceptance gates
4. `Docs/README.md` — index of every live doc

Anything under `Docs/Deprecated/` is history. Do not execute it as a task, and do not quote it as
current status — see `Docs/Deprecated/README.md` for what replaced what.

### Hard rules

- **UI ownership:** `Menu.unity` and every `Assets/_Project/UI/Prefabs/Screens/UI_*.prefab` belong
  to the owner. Never edit UI layout/prefab/scene, never open or save the Menu scene as a side
  effect. The only permitted UI work is tween/animation code (.cs) when explicitly requested.
  After any editor operation, check `git status` and revert unintended UI-file changes.
- Never introduce DOTween. Never rebuild the Player skeleton/Animator/WeaponRig — read
  `Docs/Reference/Technical/PlayerRigSocketIncident.md` first.
- Play-test from `Bootstrap.unity` only.
- Do not stage/commit/push unless the task explicitly asks for it.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools/notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
