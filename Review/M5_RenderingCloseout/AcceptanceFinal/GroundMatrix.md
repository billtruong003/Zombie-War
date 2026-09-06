# Gate 6 — ground correctness matrix (48 cells)

Occlusion Culling OFF, frustum culling active, player-centred 5x5 ring, radius unchanged.

| camera | traversal | steps | result |
|---|---|---:|---|
| portrait perspective | +X | 14 | PASS |
| portrait perspective | -X | 14 | PASS |
| portrait perspective | +Z | 14 | PASS |
| portrait perspective | -Z | 14 | PASS |
| portrait perspective | diagonal | 14 | PASS |
| portrait perspective | zero crossing | 16 | PASS |
| portrait perspective | negative coords | 12 | PASS |
| portrait perspective | teleport | 2 | PASS |
| portrait orthographic | +X | 14 | PASS |
| portrait orthographic | -X | 14 | PASS |
| portrait orthographic | +Z | 14 | PASS |
| portrait orthographic | -Z | 14 | PASS |
| portrait orthographic | diagonal | 14 | PASS |
| portrait orthographic | zero crossing | 16 | PASS |
| portrait orthographic | negative coords | 12 | PASS |
| portrait orthographic | teleport | 2 | PASS |
| tall portrait perspective | +X | 14 | PASS |
| tall portrait perspective | -X | 14 | PASS |
| tall portrait perspective | +Z | 14 | PASS |
| tall portrait perspective | -Z | 14 | PASS |
| tall portrait perspective | diagonal | 14 | PASS |
| tall portrait perspective | zero crossing | 16 | PASS |
| tall portrait perspective | negative coords | 12 | PASS |
| tall portrait perspective | teleport | 2 | PASS |
| tall portrait orthographic | +X | 14 | PASS |
| tall portrait orthographic | -X | 14 | PASS |
| tall portrait orthographic | +Z | 14 | PASS |
| tall portrait orthographic | -Z | 14 | PASS |
| tall portrait orthographic | diagonal | 14 | PASS |
| tall portrait orthographic | zero crossing | 16 | PASS |
| tall portrait orthographic | negative coords | 12 | PASS |
| tall portrait orthographic | teleport | 2 | PASS |
| landscape perspective | +X | 14 | PASS |
| landscape perspective | -X | 14 | PASS |
| landscape perspective | +Z | 14 | PASS |
| landscape perspective | -Z | 14 | PASS |
| landscape perspective | diagonal | 14 | PASS |
| landscape perspective | zero crossing | 16 | PASS |
| landscape perspective | negative coords | 12 | PASS |
| landscape perspective | teleport | 2 | PASS |
| landscape orthographic | +X | 14 | PASS |
| landscape orthographic | -X | 14 | PASS |
| landscape orthographic | +Z | 14 | PASS |
| landscape orthographic | -Z | 14 | PASS |
| landscape orthographic | diagonal | 14 | PASS |
| landscape orthographic | zero crossing | 16 | PASS |
| landscape orthographic | negative coords | 12 | PASS |
| landscape orthographic | teleport | 2 | PASS |

**48 PASS / 0 FAIL out of 48 cells.**
