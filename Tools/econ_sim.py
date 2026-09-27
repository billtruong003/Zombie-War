"""Zombie War economy simulation (M8 step D).

Answers one question for the owner: with payout option B (extraction points bank 100% of the
coins carried so far), how many runs / days does each price tier cost a free player?

Inputs are measured, not guessed, where the game can tell us:
  KILLS_CUM  cumulative kills per minute, from the 2026-09-27 editor probe (invulnerable player,
             first card picked at every level-up, 3x time scale, Map_Level1). It is a ceiling:
             a real player dies, moves and misses shots.
  COIN_PER_KILL  estimated from ZombieData coinReward (1-4, bosses 30-40); the probe could not
                 measure it because a standing player leaves most drops on the ground.
Everything else is an assumption, marked ASSUME, for the owner to change.

Run:  python Tools/econ_sim.py            (prints the table)
      python Tools/econ_sim.py --csv out  (also writes out.csv)
"""
import argparse
import random

# Measured: cumulative kills at the end of minute 1..15.
KILLS_CUM = [126, 300, 407, 542, 703, 847, 1062, 1325, 1643, 1987, 2380, 2795, 3253, 3772, 4305]
COIN_PER_KILL = 1.6            # ESTIMATE from ZombieData coinReward (1-4, bosses 30-40); verify with telemetry

# ASSUME: how a real player compares with the probe ceiling.
KILL_EFFICIENCY = 0.7          # moves, dodges, misses
PICKUP_RATE = 0.75             # share of dropped coin actually collected
# ASSUME: survival time of a free player, minutes (triangular: low, mode, high) by player stage.
RUN_LENGTH = {"day1": (2.0, 4.0, 8.0), "week1": (3.0, 6.5, 12.0), "month1": (5.0, 9.0, 15.0)}
RUNS_PER_DAY = {"day1": 6, "week1": 5, "month1": 4}

# Option B payout: surviving past an extraction point banks everything carried so far.
EXTRACTION_MIN = [3, 6, 9, 12, 15]
DEATH_KEEP = 0.25              # current RunClosure.DiedCoinFraction for the unbanked part
AD_DOUBLE_RATE = 0.35          # ASSUME share of results where the player watches "2x coins"

PRICE_TIERS = {                 # proposed coin prices (step D table)
    "Common gun": 600,
    "Rare gun": 2500,
    "Epic gun": 6000,
    "Legendary gun": 18000,
    "Common outfit part": 800,
    "Rare outfit part": 2400,
    "Outfit set (coin)": 6000,
}


def coins_at(minute: float) -> float:
    """Coins carried at a given minute of a run (interpolated from the probe)."""
    if minute <= 0:
        return 0.0
    whole = int(minute)
    prev = KILLS_CUM[whole - 1] if whole >= 1 else 0
    nxt = KILLS_CUM[min(whole, len(KILLS_CUM) - 1)]
    kills = prev + (nxt - prev) * (minute - whole)
    return kills * KILL_EFFICIENCY * COIN_PER_KILL * PICKUP_RATE


def run_payout(length: float) -> float:
    banked_at = max([m for m in EXTRACTION_MIN if m <= length], default=0)
    banked = coins_at(banked_at)
    unbanked = coins_at(length) - banked
    return banked + unbanked * DEATH_KEEP


def simulate(stage: str, runs: int = 20000, seed: int = 7) -> float:
    rng = random.Random(seed)
    low, mode, high = RUN_LENGTH[stage]
    total = 0.0
    for _ in range(runs):
        pay = run_payout(rng.triangular(low, high, mode))
        if rng.random() < AD_DOUBLE_RATE:
            pay *= 2
        total += pay
    return total / runs


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv")
    args = ap.parse_args()
    rows = []
    print(f"{'stage':8} {'coin/run':>9} {'coin/day':>9}  " + "  ".join(f"{k[:14]:>14}" for k in PRICE_TIERS))
    for stage in RUN_LENGTH:
        per_run = simulate(stage)
        per_day = per_run * RUNS_PER_DAY[stage]
        cells = [f"{p / per_run:5.1f}r {p / per_day:4.1f}d" for p in PRICE_TIERS.values()]
        print(f"{stage:8} {per_run:9.0f} {per_day:9.0f}  " + "  ".join(f"{c:>14}" for c in cells))
        rows.append([stage, round(per_run), round(per_day)] + [round(p / per_run, 1) for p in PRICE_TIERS.values()])
    print("\ncells: runs needed (r) and days needed (d) for one item of that tier")
    if args.csv:
        with open(args.csv + ".csv", "w", encoding="utf-8") as f:
            f.write("stage,coin_per_run,coin_per_day," + ",".join(PRICE_TIERS) + "\n")
            for r in rows:
                f.write(",".join(map(str, r)) + "\n")


if __name__ == "__main__":
    main()
