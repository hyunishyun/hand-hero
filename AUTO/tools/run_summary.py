#!/usr/bin/env python3
"""Summarize Hand Hero run telemetry (round 3, D16).

Reads one or more run_log.jsonl files written by RunDirector (one JSON line per
run, see HandHero.Core.RunRecordJson) and prints run count, win rate, run times
against the 10-minute limit, deaths per island, slowest islands, most-picked
items, the ASSIST charge misfire rate and which RunParams knob to turn first.

Usage:
    python AUTO/tools/run_summary.py <run_log.jsonl> [more.jsonl ...]

Standard library only.
"""

import json
import statistics
import sys
from collections import Counter, defaultdict

RUN_LIMIT_S = 600.0       # judged session: a full run must fit in 10 minutes
QUICK_HOLD_S = 0.5        # a charge shot from a shorter hold was probably meant as a normal shot
BOSS_ISLAND = 9


def load_runs(paths):
    runs = []
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for number, line in enumerate(f, 1):
                line = line.strip()
                if not line:
                    continue
                try:
                    runs.append(json.loads(line))
                except json.JSONDecodeError as e:
                    print(f"warning: {path}:{number} skipped ({e})", file=sys.stderr)
    return runs


def fmt_time(seconds):
    seconds = int(round(seconds))
    return f"{seconds // 60}:{seconds % 60:02d}"


def median(values):
    return statistics.median(values) if values else 0.0


def summarize(runs):
    lines = []
    out = lines.append

    finished = [r for r in runs if r.get("result") in ("Victory", "Defeat")]
    wins = [r for r in finished if r.get("result") == "Victory"]
    quits = len(runs) - len(finished)
    win_rate = len(wins) / len(finished) if finished else 0.0

    out(f"Runs: {len(runs)} (finished {len(finished)}, quit {quits})")
    out(f"Win rate (finished runs): {win_rate:.0%} ({len(wins)}/{len(finished)})")

    win_times = [r.get("total_s", 0.0) for r in wins]
    all_times = [r.get("total_s", 0.0) for r in finished]
    if win_times:
        over = sum(1 for t in win_times if t > RUN_LIMIT_S)
        out(f"Victory run time: median {fmt_time(median(win_times))}, max {fmt_time(max(win_times))}"
            f" (limit {fmt_time(RUN_LIMIT_S)}, {over} over)")
    if all_times:
        out(f"Finished run time: median {fmt_time(median(all_times))}, max {fmt_time(max(all_times))}")

    # Deaths per island (revived or not) and where runs were lost.
    deaths = Counter()
    lost_on = Counter()
    fight_by_type = defaultdict(list)
    fight_by_island = defaultdict(list)
    for r in runs:
        for island in r.get("islands", []):
            n = island.get("n", 0)
            deaths[n] += island.get("deaths", 0)
            fight_by_type[island.get("type", "?")].append(island.get("fight_s", 0.0))
            fight_by_island[(n, island.get("type", "?"))].append(island.get("fight_s", 0.0))
        if r.get("death_island", 0) > 0:
            lost_on[r["death_island"]] += 1

    out("")
    out("Deaths per island (all deaths / runs lost there):")
    if deaths or lost_on:
        for n in sorted(set(deaths) | set(lost_on)):
            out(f"  island {n}: {deaths[n]} / {lost_on[n]}")
    else:
        out("  none")

    out("")
    out("Slowest islands (median fight time):")
    slowest = sorted(fight_by_island.items(), key=lambda kv: median(kv[1]), reverse=True)[:5]
    for (n, kind), times in slowest:
        out(f"  island {n} {kind}: {median(times):.0f} s (n={len(times)})")
    if fight_by_type:
        out("  by type: " + ", ".join(
            f"{kind} {median(times):.0f} s" for kind, times in sorted(fight_by_type.items())))

    items = Counter(item.get("id", "?") for r in runs for item in r.get("items", []))
    out("")
    out("Most-picked items:")
    for item_id, count in items.most_common(8):
        out(f"  {item_id}: {count}")
    if not items:
        out("  none")

    # Charge misfires: quick pinches (< QUICK_HOLD_S) that still fired a charge shot.
    quick = misfires = holds = 0
    for r in runs:
        for held, charged in zip(r.get("hold_s", []), r.get("hold_charged", [])):
            holds += 1
            if held < QUICK_HOLD_S:
                quick += 1
                if charged:
                    misfires += 1
    out("")
    if quick:
        out(f"Charge misfire rate: {misfires}/{quick} pinches under {QUICK_HOLD_S} s fired a charge shot"
            f" ({misfires / quick:.0%}); {holds} pinches in total")
    else:
        out(f"Charge misfire rate: no pinches under {QUICK_HOLD_S} s recorded")

    shots = sum(r.get("shots", 0) for r in runs)
    hits = sum(r.get("hits", 0) for r in runs)
    if shots:
        out(f"Hit rate: {hits / shots:.0%} ({hits}/{shots} shots)")

    out("")
    out("Suggestion: " + suggest(runs, finished, win_rate, win_times, fight_by_type, lost_on, quick, misfires))
    return "\n".join(lines)


def suggest(runs, finished, win_rate, win_times, fight_by_type, lost_on, quick, misfires):
    """Rule-based, in the order from the round-2 report:
    EnemyHealthPerIsland (0.15), BossHealthMult (6), Arena bot count."""
    if not finished:
        return "no finished runs yet; play at least one run to Victory or Defeat."

    too_long = win_times and median(win_times) > RUN_LIMIT_S
    boss_s = median(fight_by_type.get("Boss", []))
    arena_s = median(fight_by_type.get("Arena", []))

    if too_long:
        if boss_s > 0.2 * median(win_times):
            return (f"runs are over 10 min and the boss takes {boss_s:.0f} s:"
                    " lower RunParams.BossHealthMult (6) first.")
        if arena_s > 60:
            return (f"runs are over 10 min and Arena islands take {arena_s:.0f} s each:"
                    " lower the Arena bot count (RunRules.Island: 1/2/3).")
        return "runs are over 10 min: lower RunParams.EnemyHealthPerIsland (0.15) first."

    if len(finished) >= 3 and win_rate < 0.3:
        if lost_on.get(BOSS_ISLAND, 0) >= max(lost_on.values()):
            return "most runs are lost at the boss: lower RunParams.BossHealthMult (6)."
        return "few runs are won: lower RunParams.EnemyHealthPerIsland (0.15) first."

    if quick >= 10 and misfires / quick > 0.1:
        return ("over 10% of quick pinches fired a charge shot:"
                " raise ChargeParams.HoldDelay (0.25 s) on the player's PointingBeamController.")

    if len(finished) >= 3 and win_rate > 0.8 and win_times and median(win_times) < 0.6 * RUN_LIMIT_S:
        return "runs are short and almost always won: consider raising RunParams.EnemyHealthPerIsland."

    return "no change needed yet: run time and win rate are inside the targets."


def main(argv):
    if len(argv) < 2:
        print(__doc__.strip())
        return 2
    runs = load_runs(argv[1:])
    if not runs:
        print("No runs found.")
        return 1
    print(summarize(runs))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
