# Analytics events — draft for owner approval (06/10)

Status: **DRAFT, not wired yet.** Only `ad_rewarded_show` and `ad_interstitial_show` fire today (from
`AdService`). The rest is wired after the owner approves this list.

Where they go: Firebase Analytics on Android/iOS (projects `hordecall-dev` for development builds,
`hordecall-prod` for release), our own server (`POST /v1/events`) on WebGL, and nowhere in the editor.
Code: `GameAnalytics.Log("name", ("key", value), ...)` in `Assets/_Project/Scripts/Runtime/Online/`.

Rules: snake_case, at most 40 characters, at most 25 parameters per event. No personal data (no names,
no emails, no free text). Firebase already records `first_open`, `session_start`, `app_update`,
`screen_view` and device and country, so they are not repeated here.

## Why these events
Four questions the first months must answer:
1. **Does the FTUE lose people?** Funnel by step.
2. **Is a run fun and fair?** Run length, cause of death, chosen skills, difficulty.
3. **Does the economy hold?** Sources and sinks of each currency, gacha use, upgrades.
4. **Do ads earn without hurting?** Rewarded use per placement, interstitials per day, retention next to them.

## 1. FTUE (funnel)
| Event | Parameters | When |
|---|---|---|
| `tutorial_begin` | – | First radio call of the FTUE (Firebase standard name) |
| `ftue_step` | `step` (id), `index` (int) | Each FTUE step completed |
| `tutorial_complete` | `seconds` (int) | FTUE finished (Firebase standard name) |
| `ftue_skip` | `step` | Player skipped a step / call |

## 2. Run
| Event | Parameters | When |
|---|---|---|
| `run_start` | `map`, `weapon`, `weapon_level`, `run_index` (lifetime count) | Run begins |
| `run_end` | `map`, `weapon`, `outcome` (died/left), `seconds`, `kills`, `level`, `threat_tier`, `score`, `coins`, `gems`, `revives` | Result screen |
| `run_revive` | `method` (ad / gems / free), `revive_index`, `seconds` | Player revives |
| `level_up` | `level`, `seconds` | In-run level up (Firebase standard name) |
| `skill_pick` | `skill`, `rank`, `offered` (comma list), `level` | Player picks a skill card |
| `skill_evolve` | `skill`, `seconds` | A skill evolves |
| `boss_spawn` / `boss_kill` | `boss`, `seconds` | Boss arrives / dies (once bosses ship) |
| `station_complete` | `kind`, `seconds` | A standing zone finishes |
| `new_best` | `kind` (score/time), `value` | Record broken |

## 3. Economy
| Event | Parameters | When |
|---|---|---|
| `earn_virtual_currency` | `virtual_currency_name` (coin/gem/ticket/shard), `value`, `source` | Any grant (Firebase standard name) |
| `spend_virtual_currency` | `virtual_currency_name`, `value`, `item_name`, `sink` | Any spend (Firebase standard name) |
| `gacha_pull` | `crate` (coin/gem), `count` (1/10), `pity`, `best_rarity` | Crate opened |
| `gun_unlock` | `weapon`, `rarity`, `source` (gacha/shards/pack) | Gun owned |
| `gun_upgrade` | `weapon`, `level` | Upgrade bought |
| `gun_evolve` | `weapon` | Evolution |
| `shop_view` | `tab` | Shop opened |
| `daily_claim` | `day`, `streak` | Daily chest |
| `mission_claim` | `mission`, `scope` (daily/pass) | Mission reward |
| `pass_tier` | `tier`, `premium` (bool) | Pass tier reached |
| `achievement_unlock` | `achievement` | Achievement |

## 4. Ads and purchases
| Event | Parameters | When |
|---|---|---|
| `ad_rewarded_show` ✅ | `placement` | Rewarded ad shown (wired) |
| `ad_rewarded_reward` | `placement` | Reward granted |
| `ad_rewarded_unavailable` | `placement` | Button pressed, no ad loaded |
| `ad_interstitial_show` ✅ | – | Interstitial shown (wired) |
| `purchase_start` | `product` | Store purchase started (Round 8 IAP) |
| `purchase` | Firebase standard (`value`, `currency`, `items`) | Purchase verified by the server |

## 5. Health
| Event | Parameters | When |
|---|---|---|
| `perf_low_fps` | `map`, `tier`, `fps` (avg over 10 s) | Average below 25 fps for 10 s, at most once per run |
| `cloud_save` | `result` (ok/conflict/fail) | Upload attempt (sampled 1 in 10) |

## User properties
`graphics_tier`, `ftue_done` (bool), `vip_level`, `runs_bucket` (0, 1-5, 6-20, 21+), `best_score_bucket`.

## Questions for the owner
1. Approve the list as is, or add/remove events?
2. `skill_pick` sends the three offered cards: fine (helps balance), or only the picked one?
3. Should the WebGL (web) build send events to our server, or stay silent?
