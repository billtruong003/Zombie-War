# Localization plan (vi / en / ja / ko) — draft for owner approval (06/10)

Status: **plan only, nothing built.** Round 8 item 62. Needs the owner's call on the font question
(§3), because fonts change how every screen looks.

## 1. What has to be translated (measured 06/10)
| Source | Size | Where |
|---|---|---|
| Fixed texts in UI prefabs | ~440 unique strings in 12 V2 screen prefabs | `Assets/_Project/UI/Prefabs/Screens/UI_*.prefab` |
| Texts set by code | ~950 string literals (titles, toasts, result lines, cards) | `Scripts/Runtime/UI/**`, `Systems/**` |
| Radio voice subtitles | 268 lines | `Audio/VO/Resources/VO/vo_subtitles.json` |
| Data | skill names and descriptions, guns, missions, achievements, costume names | ScriptableObjects and static tables |
| Store and legal | Play listing, privacy policy summary | outside the build |

Voice audio stays English (the radio cast is international by design); only subtitles are translated.

## 2. How (recommendation)
- **One table per language**, `Resources/Loc/<lang>.json` (key → text), English as the source and
  fallback. Lookup `Loc.T("result.died")`, with `{0}` placeholders for numbers.
- **Prefab texts** get a small `LocText` component holding the key; it sets the text on enable and
  when the language changes. Adding that component touches the UI prefabs → it is a mechanical change
  (no layout change), done by an editor tool in one pass, shown to the owner before commit.
- **Code texts** move to keys screen by screen (Home, Result, Shop, Gacha, Arsenal, Pass, Settings,
  HUD, FTUE radio), each step tested with the UI screenshot tool at 16:9 / 20:9 / tablet.
- **Language choice**: device language on first launch (vi / ja / ko, everything else en), then the
  Settings row (mockup S1).
- **Translation**: first pass by Claude for vi / ja / ko with a glossary (gun names, HordeCall terms,
  radio call signs stay English), then a native check for ja and ko before launch in those markets.
- **Text length**: vi and ko run ~20–30 % longer than en; screens get auto-size limits and the
  screenshot pass catches overflow.

## 3. Fonts — owner decision needed
The UI uses Cairo (Latin + Arabic only) and Quicksand. Vietnamese needs extra Latin accents;
Japanese and Korean need CJK glyphs. Options:

| Option | Look | Size cost | Notes |
|---|---|---|---|
| **A. Keep Cairo for Latin, add fallback fonts** (Noto Sans JP / KR, dynamic SDF) | Latin unchanged; ja/ko in Noto | ~+8–12 MB (fonts as dynamic atlases, filled at run time) | Recommended. Vietnamese: check Cairo's accent coverage, fall back to Noto Sans for missing glyphs |
| B. One family for all (e.g. Noto Sans / M PLUS Rounded) | Every language looks the same | similar | Changes the current look in every screen |
| C. Static pre-baked CJK atlases | Crisp | +30–60 MB | Not worth it with the build-size work going on |

## 4. Order
1. Approve this plan + font option.
2. `Loc` + tables + `LocText` tool (no visible change in English).
3. Move screens one by one; Claude's vi / ja / ko first pass.
4. Screenshot pass per language, fix overflows.
5. Native review for ja / ko; Play listing per language.

Estimate: 3–4 working days for 1–4 (most of it moving ~1,400 strings to keys and checking screens).
