"""Bot lab report (backlog #28): summarise bot runs against the genre-rule targets.

    python Tools/botlab_report.py <label> [<label> ...]

Reads Review/QA/botlab/summary.jsonl (one line per run, written by Dev/BotLab.cs) and the per-run
CSVs next to it. Prints, per label: survival time, level and cards by 2:00 / 5:00, the first card,
peak elites and ranged share, low-HP time, XP pickup rate and coin per minute, then which targets
pass.

Targets (GAME_DESIGN §4 + owner 05/10): new player lives 6-8 min; first card 25-45 s; a strong
build still dies; ranged <= 15 % of the crowd; elites never a pack (<= 6 alive).
"""
import csv
import glob
import json
import os
import statistics as st
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Review", "QA", "botlab")


def runs_for(label):
    out = []
    with open(os.path.join(DIR, "summary.jsonl"), encoding="utf-8") as f:
        for line in f:
            d = json.loads(line)
            if d["label"] == label:
                out.append(d)
    return out


def csv_rows(label, index):
    files = sorted(glob.glob(os.path.join(DIR, f"*_{label}_{index}.csv")))
    if not files:
        return []
    with open(files[-1], encoding="utf-8") as f:
        return list(csv.DictReader(f))


def level_at(rows, seconds):
    lv = 1
    for r in rows:
        if int(r["t"]) <= seconds:
            lv = int(r["level"])
    return lv


def fmt(sec):
    return f"{int(sec) // 60}:{int(sec) % 60:02d}"


def report(label):
    rs = runs_for(label)
    if not rs:
        print(f"{label}: no runs")
        return
    secs = [r["seconds"] for r in rs]
    lv2, lv5, peak_el = [], [], []
    for r in rs:
        rows = csv_rows(label, r["run"])
        lv2.append(level_at(rows, 120))
        lv5.append(level_at(rows, 300))
        peak_el.append(max((int(x["elites"]) for x in rows), default=0))
    pick = [r["xp_gained"] / max(1, r["xp_dropped"]) for r in rs]
    coin_min = [r["coin"] / max(1, r["seconds"] / 60) for r in rs]
    print(f"\n== {label}: {len(rs)} runs, {sum(r['died'] for r in rs)} died")
    print(f"survival   median {fmt(st.median(secs))}  min {fmt(min(secs))}  max {fmt(max(secs))}")
    print(f"first card median {st.median(r['first_card'] for r in rs):.0f} s   cards by 2:00 median {st.median(r['cards_by_2min'] for r in rs):.0f}")
    print(f"level @2:00 median {st.median(lv2):.0f}   @5:00 median {st.median(lv5):.0f}   final median {st.median(r['level'] for r in rs):.0f}")
    print(f"peak elites alive median {st.median(peak_el):.0f} (max {max(peak_el)})   peak ranged share max {max(r['peak_ranged_share'] for r in rs):.2f}")
    print(f"low-HP seconds median {st.median(r['low_hp_seconds'] for r in rs):.0f}   XP pickup {st.median(pick):.0%}   coin/min median {st.median(coin_min):.0f}")
    print(f"kills median {st.median(r['kills'] for r in rs):.0f}   chests median {st.median(r['chests'] for r in rs):.0f}   peak tier median {st.median(r['peak_tier'] for r in rs):.0f}")


if __name__ == "__main__":
    for lab in sys.argv[1:]:
        report(lab)
