# Working files — provenance, not deliverables

These are the raw intermediates from the M6.1 arsenal audit. They are kept because they are the
provenance behind the numbers in the deliverables, but they are **not** deliverables themselves.

| File | What it is | Previously at |
|---|---|---|
| `_raw_prefab_scan.tsv` | The Unity sweep output: 415 rows + header, one per prefab (renderers, materials, shaders, triangles, bounds, child names). Every count in the audit derives from this. | `Inventory/_raw_prefab_scan.tsv` |
| `_bucket_table.md` | Intermediate bucket tally used while drafting the summary. | repository root of this folder |
| `_render_queue.tsv` | Render list for the 38 weapon bodies. | `Inventory/` |
| `_render_queue_misc.tsv` | Render list for the 24 attachment/duplicate/demo samples. | `Inventory/` |
| `_render_queue.json` | Superseded JSON form of the body render queue (its key layout broke the parser; the TSV replaced it). | `Inventory/` |
| `bodies_progress.txt` | Completion marker from the body-render pass. | `Evidence/bodies/_progress.txt` |
| `held_camera_log.txt` | Camera-solve log from the player-held capture. Records the bounds (1.38 × 1.27 × 2.30 m) that produced the unusable framing described in `VISION_REVIEW.md`. | `Evidence/held/_log.txt` |

## Disclosure — one file was lost in the move

`Evidence/_progress.txt` and `Evidence/bodies/_progress.txt` shared a filename. Moving both into this
folder overwrote the first with the second, so only the `bodies` marker survives (`bodies_progress.txt`,
contents `DONE bodies=0 skipped=0` — a no-op re-run after the renders were already complete). The lost
file was an equivalent one-line completion marker for the combined body+misc render pass. **No audit
number depended on either file**; every count comes from `_raw_prefab_scan.tsv` and
`Inventory/weapon_inventory.csv`, both intact.

## Note on `weapon_inventory.csv`

Its `evidenceReference` column still reads `Evidence/ + _raw_prefab_scan.tsv`. That column was left
untouched on purpose: rewriting 415 verified rows to change a path string would modify a file the
reviewer has already audited. The scan file is now at `_work/_raw_prefab_scan.tsv`.
