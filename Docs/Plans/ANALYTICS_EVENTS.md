# Analytics events (06/10)

Status: **approved 06/10 (owner: "pick the most reasonable, store-safe option") and wired.**
Verified in the editor with a bot run (BotLab): run_start, skill_pick, level_up, station_complete,
achievement_unlock, earn_virtual_currency, run_end and new_best all fired with the parameters below.

Decisions taken for the owner's three questions:
1. The list stays as drafted, minus `boss_*` until bosses ship in runs, `perf_low_fps` and
   `cloud_save` (later, when there is something to act on), and `purchase_*` (Round 8 IAP).
2. `skill_pick` sends only the card taken (skill, rank, evolution), not the three offered.
3. The WebGL build sends nothing (`GameAnalytics.SendWebEventsToServer = false`): a web page would
   need its own consent banner first.
4. FTUE uses `ftue_step` with the step id; the standard `tutorial_begin/complete` names are not used
   because the FTUE is a set of independent steps, not one line.

Where each event is logged: `Online/AnalyticsHooks.cs` listens to existing signals (run start/end,
FTUE steps, stations, level ups, achievements, daily chest, missions, wallet and gun changes);
`skill_pick` in `SkillRuntime.Take`, `run_revive` in `ReviveRules`, `gacha_pull` in
`GachaBanners.Pull`, ad events in `AdService` and `RewardedAds`. Currency and gun events are found
by comparing the profile before and after a change; inside a run they are not logged (run_end
carries the run's earnings), and a cloud restore is muted.

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

## Store safety: what must be true before release
The privacy policy is live at https://billtruong003.github.io/billthedev-legal/hordecall.html
(repo billthedev-legal). It promises these, so each must exist in the release build:
- [ ] HTTPS for the server (domain + certificate); release builds refuse plain HTTP anyway.
- [ ] Settings → **Privacy options** button, shown when `AdService.PrivacyOptionsRequired` (UMP rule).
- [ ] Settings → **Delete account** (calls `DELETE /v1/account`, then wipes the local profile).
- [ ] Settings → **Privacy policy** link.
- [ ] Gun crate **drop rates** visible before opening (Google Play rule for paid random items).
- [ ] Player ID visible on the profile screen (the policy's email deletion route uses it; the
      server finds it inside the cloud save).
- [ ] Firebase Analytics data retention set to 14 months or less (default 2 months is fine).

Google Play Data safety form (draft answers, check against Google's current SDK guidance):
| Data type | Collected | Shared | Why | Optional |
|---|---|---|---|---|
| Device or other IDs (hashed Android ID, Firebase app instance ID, advertising ID) | Yes | Advertising ID with Google AdMob | App functionality, analytics, advertising | No |
| App interactions (gameplay events) | Yes | No | Analytics | No |
| Crash logs, diagnostics | Yes | No | App functionality (crash fixing) | No |
| Approximate location (from IP, by Google SDKs) | Yes | With Google AdMob | Advertising, analytics | No |
| Other user-generated content (leaderboard name) | Yes, once leaderboards ship | No | App functionality | Yes |
| Purchase history | Yes, once IAP ships | No | App functionality | No |
Encrypted in transit: yes (HTTPS). Users can request deletion: yes (in app + email, URL above).
Target audience 13+, not designed for children; ads present; in-app purchases; random items (crates).
