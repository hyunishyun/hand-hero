#!/usr/bin/env python3
"""Summarize Hand Hero run telemetry (round 3, D16).

Reads one or more run_log.jsonl files written by RunDirector (one JSON line per
run, see HandHero.Core.RunRecordJson) and prints run count, win rate, run times
against the 10-minute limit, deaths per island, slowest islands, most-picked
items, the ASSIST charge misfire rate, pinch holds per aim mode (share over 1 s
and what released each pinch, round 5), where the ASSIST thumb rests (deep review
DR-9 second pass, report A5), island themes and terrain pieces (round 5) and which
RunParams knob to turn first.

The device file keeps every run across `adb install -r`, so each run names the
APK that wrote it ("build", deep review DR-7) and only the newest build is
summarized unless --all or --build is given. Runs written before the stamp have
no "build". They are split in two (deep review DR-5): "no build stamp (round 5)"
for the round-5 APKs built before the stamp (they write release_by on every run)
and "no build stamp (rounds 3-4)" for the older ones. The pinch-hold headline
per aim mode counts only runs with release diagnostics; older holds get a line
of their own, so --all cannot dilute the round-5 ASSIST share either.

Usage:
    python AUTO/tools/run_summary.py [--all | --build <part of a name>] [--since <time>] <run_log.jsonl> [more.jsonl ...]

    --all            summarize every run in the files (all builds together)
    --build TEXT     only the builds whose name contains TEXT ("none" = every run without a stamp,
                     "round 5" = the unstamped round-5 runs)
    --since TIME     only runs that started at or after TIME (headset local time,
                     e.g. 2026-10-10 or "2026-10-10 14:00"); applied before the build choice

Tests: python -m unittest discover -s AUTO/tools -p "test_*.py"

Standard library only.
"""

import argparse
import json
import re
import statistics
import sys
from collections import Counter, defaultdict

RUN_LIMIT_S = 600.0       # judged session: a full run must fit in 10 minutes
QUICK_HOLD_S = 0.5        # a charge started by a shorter hold was probably meant as a normal shot
LONG_HOLD_S = 1.0         # round 5: share of holds over this, per aim mode (ASSIST release bug)
LONG_HOLD_LIST = 8        # deep review DR-9: holds over LONG_HOLD_S listed with their strengths
# Deep review DR-9 second pass: the resting thumb r is the strength (0.01 bins of
# "pinch_strength_s") with the most aim-hand time in this range (+-1 bin). Under 0.50 is an
# open hand (3.75 cm or more); 0.96-1.00 is a press with the thumb on the index.
REST_LOW, REST_HIGH = 0.50, 0.95
REST_SPREAD = 3           # r = the time-weighted mean within 3 bins (0.03) of that peak
RELEASE_ORDER = ("meta", "absolute", "relative", "lost", "none")
BOSS_ISLAND = 9
NO_STAMP = "no build stamp"   # runs written before the deep-review build stamp (DR-7)
# DR-5: unstamped runs split on release_by, which every round-5 APK writes (RunRecordJson)
# and no round 3-4 APK did. The round-5 APKs in the report (0203, 0156) are unstamped.
NO_STAMP_R5 = NO_STAMP + " (round 5)"
NO_STAMP_OLD = NO_STAMP + " (rounds 3-4)"
SINCE_FORMAT = re.compile(r"^\d{4}-\d{2}-\d{2}([T ]\d{2}(:\d{2}(:\d{2})?)?)?$")


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


def has_release_diagnostics(run):
    """True for runs written by a round-5 (or later) APK: release_by is always there, even empty."""
    return run.get("release_by") is not None


def build_of(run):
    if run.get("build"):
        return run["build"]
    return NO_STAMP_R5 if has_release_diagnostics(run) else NO_STAMP_OLD


def runs_since(runs, since):
    """Runs that started at or after `since`. RunDirector writes "start" as headset local time
    "yyyy-MM-ddTHH:mm:ss", so a plain string comparison works; "2026-10-10 14:00" and
    "2026-10-10" are accepted too. Runs without a start time are dropped."""
    since = since.strip().replace(" ", "T")
    return [r for r in runs if r.get("start") and r["start"] >= since]


def builds_in(runs):
    """Per build, oldest first (by its newest run): run count and first / last start time."""
    groups = {}
    for index, r in enumerate(runs):
        key = (r.get("start", ""), index)
        g = groups.setdefault(build_of(r), {"runs": 0, "first": key, "last": key})
        g["runs"] += 1
        g["first"] = min(g["first"], key)
        g["last"] = max(g["last"], key)
    return sorted(groups.items(), key=lambda kv: kv[1]["last"])


def select_runs(runs, build_filter=None, all_builds=False):
    """The runs to summarize and the header lines that say which ones.
    Default: the build of the newest run (by start time, then file order)."""
    builds = builds_in(runs)
    lines = []
    if len(builds) > 1 or build_filter or all_builds:
        lines.append("Builds in the log (oldest first):")
        for name, g in builds:
            first, last = g["first"][0] or "?", g["last"][0] or "?"
            span = first if first == last else f"{first} .. {last}"
            lines.append(f"  {name}: {g['runs']} run{'s' if g['runs'] != 1 else ''}, {span}")

    if all_builds:
        chosen = runs
        lines.append(f"Summarizing every build together ({len(runs)} runs).")
    elif build_filter:
        unstamped = build_filter.lower() == "none"
        names = [name for name, _ in builds
                 if (name.startswith(NO_STAMP) if unstamped else build_filter in name)]
        chosen = [r for r in runs if build_of(r) in names]
        lines.append(f"Summarizing build {', '.join(names) if names else '(none matched)'}"
                     f" ({len(chosen)} of {len(runs)} runs).")
    else:
        newest = builds[-1][0]
        chosen = [r for r in runs if build_of(r) == newest]
        if len(builds) > 1:
            lines.append(f"Summarizing the newest build only: {newest} ({len(chosen)} of {len(runs)} runs)."
                         " --all for every run, --build <part of a name> for another build.")
        else:
            lines.append(f"Build: {newest}")
    lines.append("")
    return chosen, lines


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

    # Round 4 (S6): which bot archetypes were killed and which hurt the player.
    kills_by = Counter()
    damage_by = Counter()
    for r in runs:
        kills_by.update(r.get("kills_by", {}))
        damage_by.update(r.get("damage_by", {}))
    out("")
    out("By bot archetype (kills / damage taken):")
    if kills_by or damage_by:
        for kind in sorted(set(kills_by) | set(damage_by)):
            out(f"  {kind}: {kills_by[kind]:.0f} / {damage_by[kind]:.0f}")
    else:
        out("  none (logs before round 4)")

    # Round 5 (T3): island themes and extra terrain pieces.
    lines.extend(terrain_lines(runs))

    starts = Counter(r.get("start_relic") or "none" for r in runs if "start_relic" in r)
    if starts:
        out("")
        out("Starting relics: " + ", ".join(f"{relic} {count}" for relic, count in starts.most_common()))

    items = Counter(item.get("id", "?") for r in runs for item in r.get("items", []))
    out("")
    out("Most-picked items:")
    for item_id, count in items.most_common(8):
        out(f"  {item_id}: {count}")
    if not items:
        out("  none")

    # Charge misfires: quick pinches (< QUICK_HOLD_S) that still started a charge
    # (slowdown + orb), shot or not. Logs written before "hold_started" existed
    # only have "hold_charged" (a charge shot fired), which undercounts.
    quick = misfires = holds = 0
    for r in runs:
        started = r.get("hold_started", r.get("hold_charged", []))
        for held, charging in zip(r.get("hold_s", []), started):
            holds += 1
            if held < QUICK_HOLD_S:
                quick += 1
                if charging:
                    misfires += 1
    out("")
    if quick:
        out(f"Charge misfire rate: {misfires}/{quick} pinches under {QUICK_HOLD_S} s started a charge"
            f" ({misfires / quick:.0%}); {holds} pinches in total")
    else:
        out(f"Charge misfire rate: no pinches under {QUICK_HOLD_S} s recorded")

    shots = sum(r.get("shots", 0) for r in runs)
    hits = sum(r.get("hits", 0) for r in runs)
    if shots:
        out(f"Hit rate: {hits / shots:.0%} ({hits}/{shots} shots)")

    lines.extend(pinch_holds_by_mode(runs))

    out("")
    out("Suggestion: " + suggest(runs, finished, win_rate, win_times, fight_by_type, lost_on, quick, misfires))
    return "\n".join(lines)


def strength_cm(strength):
    """HandGestureTracker's thumb-index distance for an unsmoothed strength: (6 - cm) / 4.5."""
    return 6.0 - 4.5 * strength


def resting_thumb(seconds):
    """Deep review DR-9 second pass: where the aim-hand thumb rests, from the run log's
    seconds per strength bin ({"<0.50": s, "0.71": s, ...}, RunRecordJson).

    The bin in REST_LOW..REST_HIGH whose window (itself and its two neighbours in the
    range) holds the most time is the centre; r is the time-weighted mean of the bins
    within REST_SPREAD of it (in the range). Simulated (rest 0.71-0.82, taps at 3/s to
    0.93 or 1.00, charges up to 4 s, open hand, Gaussian noise up to 0.03): r stays
    within 0.006 of the true rest. Returns (r, share of all tracked time within
    REST_SPREAD, all tracked seconds) or None."""
    total = sum(seconds.values())
    bins = defaultdict(float)
    for label, s in seconds.items():
        try:
            value = round(float(label), 2)
        except ValueError:
            continue  # "<0.50"
        if REST_LOW <= value <= REST_HIGH:
            bins[value] += s
    if total <= 0 or not any(s > 0 for s in bins.values()):
        return None

    def window(value, bins_each_side):
        steps = range(-bins_each_side, bins_each_side + 1)
        return [w for w in (round(value + step / 100, 2) for step in steps) if w in bins]

    centre = max(sorted(bins), key=lambda v: sum(bins[w] for w in window(v, 1)))
    near = window(centre, REST_SPREAD)
    mass = sum(bins[w] for w in near)
    r = sum(w * bins[w] for w in near) / mass
    return r, mass / total, total


def pinch_holds_by_mode(runs):
    """Round 5 (D2): per aim mode, how long pinches were held and what released them.

    release_by per hold: meta (Meta's index-pinch flag), absolute (the old 0.6 reset
    would release too), relative (only the drop-from-peak rule released), lost
    (tracking / system gesture), none (CURSOR trigger holds, no pinch diagnostics).
    Strengths are the unsmoothed thumb-index values (1 = pinched, 0.71 = 2.8 cm).

    Deep review DR-5: the headline (holds, median, share over 1 s) counts only runs with
    release diagnostics (round-5 APKs). Holds from older APKs, still in the device file
    after `adb install -r`, are printed on their own line; a mode with only older holds
    keeps them in the headline and says "not recorded".

    Deep review DR-9: holds over 1 s are also listed one by one (longest first, up to
    LONG_HOLD_LIST) with their peak and lowest strength while held. The peak tells the
    tap type (under 0.95 = a light tap). The lowest is not the resting thumb (second
    pass): a hold ends only once 2 samples reach the release level and the first counts
    as held, so it is at or under that level (0.75 for a light tap).

    Deep review DR-9 second pass: the resting thumb r comes from the aim hand's time per
    strength ("pinch_strength_s", ASSIST frames of the run, held or not). It shows r for
    stuck holds and for taps that never fired (no holds at all). Report section 2, A5."""
    modes = defaultdict(lambda: {"durations": [], "older": [], "release": Counter(), "recorded": 0,
                                 "meta_seen": 0, "peak": [], "low": [], "at_release": [], "long": [],
                                 "strength_s": Counter(), "strength_runs": 0})
    for r in runs:
        m = modes[r.get("aim", "?")]
        holds = r.get("hold_s", [])
        released_by = r.get("release_by")
        if released_by is None:
            m["older"].extend(holds)
            continue
        if r.get("pinch_strength_s") is not None:
            m["strength_runs"] += 1
            m["strength_s"].update(r["pinch_strength_s"])
        m["durations"].extend(holds)
        m["recorded"] += len(released_by)
        m["release"].update(released_by)
        m["meta_seen"] += sum(r.get("meta_seen", []))
        peaks = r.get("peak_strength", [])
        lows = r.get("min_strength", [])
        at_release = r.get("release_strength", [])
        for i, kind in enumerate(released_by):
            if kind == "none":
                continue
            if i < len(peaks):
                m["peak"].append(peaks[i])
            if i < len(lows):
                m["low"].append(lows[i])
            if i < len(at_release):
                m["at_release"].append(at_release[i])
            if i < len(holds) and holds[i] > LONG_HOLD_S and i < len(peaks) and i < len(lows):
                m["long"].append((holds[i], peaks[i], lows[i]))

    def holds_line(durations):
        long_holds = sum(1 for d in durations if d > LONG_HOLD_S)
        return (f"{len(durations)} holds, median {median(durations):.2f} s,"
                f" {long_holds} over {LONG_HOLD_S:g} s ({long_holds / len(durations):.0%})")

    def resting_line(m):
        """None when there is nothing to say (CURSOR, or no ASSIST pinch at all)."""
        rest = resting_thumb(m["strength_s"])
        if rest:
            r, share, total = rest
            return (f"    resting thumb (most aim-hand time at {REST_LOW:.2f}-{REST_HIGH:.2f}):"
                    f" r {r:.2f} = {strength_cm(r):.1f} cm, {share:.0%} of {total:.0f} s tracked")
        if m["peak"] and not m["strength_runs"]:
            return "    resting thumb: not recorded (APK before the deep-review second pass)"
        return None

    out = ["", f"Pinch holds by aim mode (over {LONG_HOLD_S:g} s / what released them):"]
    if not any(m["durations"] or m["older"] or resting_line(m) for m in modes.values()):
        out.append("  none recorded")
        return out
    for mode in sorted(modes):
        m = modes[mode]
        durations, older = m["durations"], m["older"]
        if not durations:
            if older:
                out.append(f"  {mode}: {holds_line(older)}")
                out.append("    released by: not recorded (logs before round 5)")
            elif resting_line(m):
                # DR-9 second pass: taps that never fired (after a reopen) leave no hold.
                out.append(f"  {mode}: 0 holds")
                out.append(resting_line(m))
            continue
        out.append(f"  {mode}: {holds_line(durations)}")
        total = m["recorded"]
        if total:
            kinds = [k for k in RELEASE_ORDER if m["release"][k]] + sorted(
                k for k in m["release"] if k not in RELEASE_ORDER)
            mix = ", ".join(f"{k} {m['release'][k]} ({m['release'][k] / total:.0%})" for k in kinds)
            out.append(f"    released by: {mix}; Meta flag seen in {m['meta_seen']}/{total} holds")
        else:
            out.append("    released by: not recorded")
        if m["peak"]:
            out.append(f"    strength (median, unsmoothed): peak {median(m['peak']):.2f},"
                       f" lowest while held {median(m['low']):.2f}, at release {median(m['at_release']):.2f}")
        if m["long"]:
            longest = sorted(m["long"], key=lambda h: -h[0])
            listed = ", ".join(f"{d:.1f} s {peak:.2f}/{low:.2f}" for d, peak, low in longest[:LONG_HOLD_LIST])
            more = len(longest) - LONG_HOLD_LIST
            out.append(f"    holds over {LONG_HOLD_S:g} s, longest first (peak / lowest while held): {listed}"
                       + (f", +{more} more" if more > 0 else ""))
        if resting_line(m):
            out.append(resting_line(m))
        if older:
            out.append(f"    older APKs (rounds 3-4, not in the numbers above): {holds_line(older)}")
    return out


def terrain_lines(runs):
    """Round 5 (T3): theme mix, extra pieces per island and fallback layouts.
    An island with a theme but no layout_seed fell back to today's two pillars."""
    lines = ["", "Terrain per island (round 5: theme / extra pieces):"]
    islands = [i for r in runs for i in r.get("islands", []) if "theme" in i]
    if not islands:
        lines.append("  none (logs before round 5)")
        return lines

    by_theme = defaultdict(list)
    for island in islands:
        by_theme[island.get("theme") or "default"].append(island)
    for theme, group in sorted(by_theme.items()):
        fights = [i.get("fight_s", 0.0) for i in group]
        damage = [i.get("damage", 0.0) for i in group]
        lines.append(f"  {theme}: {len(group)} islands, median fight {median(fights):.0f} s,"
                     f" median damage taken {median(damage):.0f}")

    def mean(key):
        return sum(i.get(key, 0) for i in islands) / len(islands)

    fallbacks = sum(1 for i in islands if "layout_seed" not in i)
    lines.append(f"  extra pieces per island (mean): low walls {mean('low_walls'):.1f},"
                 f" platforms {mean('platforms'):.1f}, thin pillars {mean('thin_pillars'):.1f}")
    lines.append(f"  fallback layouts (today's two pillars): {fallbacks}/{len(islands)}")
    return lines


def suggest(runs, finished, win_rate, win_times, fight_by_type, lost_on, quick, misfires):
    """Rule-based, in the order from the round-2 report:
    EnemyHealthPerIsland (0.15), BossHealthMult (5), Arena bot count."""
    if not finished:
        return "no finished runs yet; play at least one run to Victory or Defeat."

    too_long = win_times and median(win_times) > RUN_LIMIT_S
    boss_s = median(fight_by_type.get("Boss", []))
    arena_s = median(fight_by_type.get("Arena", []))

    if too_long:
        if boss_s > 0.2 * median(win_times):
            return (f"runs are over 10 min and the boss takes {boss_s:.0f} s:"
                    " lower RunParams.BossHealthMult (5) first.")
        if arena_s > 60:
            return (f"runs are over 10 min and Arena islands take {arena_s:.0f} s each:"
                    " lower the Arena bot count (RunRules.Island: 1/2/3).")
        return "runs are over 10 min: lower RunParams.EnemyHealthPerIsland (0.15) first."

    if len(finished) >= 3 and win_rate < 0.3:
        if lost_on.get(BOSS_ISLAND, 0) >= max(lost_on.values()):
            return "most runs are lost at the boss: lower RunParams.BossHealthMult (5)."
        return "few runs are won: lower RunParams.EnemyHealthPerIsland (0.15) first."

    if quick >= 10 and misfires / quick > 0.1:
        return ("over 10% of quick pinches started a charge:"
                " raise ChargeParams.HoldDelay (0.25 s) on the player's PointingBeamController.")

    if len(finished) >= 3 and win_rate > 0.8 and win_times and median(win_times) < 0.6 * RUN_LIMIT_S:
        return "runs are short and almost always won: consider raising RunParams.EnemyHealthPerIsland."

    return "no change needed yet: run time and win rate are inside the targets."


def main(argv):
    if len(argv) < 2:
        print(__doc__.strip())
        return 2
    parser = argparse.ArgumentParser(description="Summarize Hand Hero run_log.jsonl files.")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--all", action="store_true", help="summarize every build together")
    group.add_argument("--build", metavar="TEXT",
                       help='only builds whose name contains TEXT ("none" = every run without a build stamp,'
                            ' "round 5" = the unstamped round-5 runs)')
    parser.add_argument("--since", metavar="TIME",
                        help='only runs that started at or after TIME (headset local time, e.g. 2026-10-10'
                             ' or "2026-10-10 14:00")')
    parser.add_argument("files", nargs="+", help="run_log.jsonl files")
    args = parser.parse_args(argv[1:])
    if args.since is not None and not SINCE_FORMAT.match(args.since.strip()):
        parser.error(f"--since: expected yyyy-MM-dd[ HH[:mm[:ss]]], got {args.since!r}")

    runs = load_runs(args.files)
    if not runs:
        print("No runs found.")
        return 1
    since = []
    if args.since is not None:
        kept = runs_since(runs, args.since)
        since.append(f"Runs started at or after {args.since.strip()}: {len(kept)} of {len(runs)}.")
        runs = kept
        if not runs:
            print(since[0])
            print("No runs found.")
            return 1
    chosen, header = select_runs(runs, args.build, args.all)
    if not chosen:
        print("\n".join(since + header))
        print("No runs from that build.")
        return 1
    print("\n".join(since + header + [summarize(chosen)]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
