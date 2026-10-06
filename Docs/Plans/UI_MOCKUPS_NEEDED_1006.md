# UI that needs an owner-approved mockup (collected 2026-10-06)

Owner rule: UI prefabs and Menu change only after the mockup is approved. The logic behind each item
below is built (or will be) without touching layout; each screen waits for its mockup.

| # | Screen | What changes | Logic status |
|---|---|---|---|
| #36 | Level-up overlay (UI_Hud) | **REROLL** (2 free per run) and **BANISH** (1 per run: the card never shows again this run) buttons under the three cards | to build with the mockup |
| 3b | Gacha | two groups: **SKINS** (Neon Nights, Street) and **GUNS** (Gun Crate · coin, Elite Crate · gem); a crate result shows shards landing on a gun with its X / N bar, a full-gun hit is the big moment | data and pull logic first, screen after approval |
| 3b | Arsenal | an unowned gun shows **shards X / N** and **UNLOCK** when full (no coin price); a link to the crate that drops it; mastery → star upgrade shortcut | after approval |
| 3b | Home | "NEXT BUY" card becomes **NEXT GUN**: the gun closest to unlocking by shards | after approval |
| 3c | Shop GUNS tab | crate shortcuts, **Starter Pack $4.99** (Vector), **VIP Pack $19.99**, VIP rank strip | after approval and SDK (Đợt 8) |
| 3c | Profile / Shop | VIP rank badge and perks list | after approval |
| #24 | Arsenal + result | **TRY 5 MIN** on unowned guns; trial result card (+3 shards) | after MODE_CONTENT_PLAN approval |
| #46 | Daily Ops | DAILY CHALLENGE card: today's map + modifier, best score | after MODE_CONTENT_PLAN approval |
| U1 | HUD | milestone bar (chest 2:00 / 4:00, Horde Call, Titan) — already approved 05/10, built with Titan in Đợt 5 | Đợt 5 |
| S1 | Settings | **Privacy options** (only when `AdService.PrivacyOptionsRequired`), **Privacy policy** link, **Delete account** with a confirm step, **Player ID** with copy; later **Link account** (Google Play Games / Facebook) | logic ready: `AdService.ShowPrivacyOptions`, `DELETE /v1/account`; store-safety items in ANALYTICS_EVENTS.md |
| S2 | Gacha / crate | **Drop rates** sheet before opening any crate (Google Play rule for paid random items): rarity %, pity counter, shard targets | rates already in GunCrates / GachaBanners data |
| S3 | Boot | **Update required** (button → store) and **Maintenance** (message) screens over the menu | logic ready: `RemoteConfig.UpdateRequired / Maintenance / StoreUrl`; shows as a toast until then |
