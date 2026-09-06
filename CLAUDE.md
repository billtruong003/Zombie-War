<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **Zombie-War** (44301 symbols, 59261 relationships, 300 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows. For regression review, compare against the default branch: `detect_changes({scope: "compare", base_ref: "main"})`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method without first running `impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit changes without running `detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/Zombie-War/context` | Codebase overview, check index freshness |
| `gitnexus://repo/Zombie-War/clusters` | All functional areas |
| `gitnexus://repo/Zombie-War/processes` | All execution flows |
| `gitnexus://repo/Zombie-War/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

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
