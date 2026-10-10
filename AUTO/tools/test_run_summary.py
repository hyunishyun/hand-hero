"""Tests for run_summary.py (deep review DR-5, DR-7, DR-9 and its second pass).

Run from the project root:
    python -m unittest discover -s AUTO/tools -p "test_*.py"

Standard library only.
"""

import contextlib
import io
import os
import unittest

import run_summary as rs

HERE = os.path.dirname(os.path.abspath(__file__))
R4_DEVICE_LOG = os.path.join(HERE, "..", "device_logs", "2026-10-09-r4", "run_log.jsonl")
R5_SAMPLE = os.path.join(HERE, "fixtures", "run_log_round5_sample.jsonl")


def old_assist_run(start, holds):
    """A round 3-4 run: no build stamp, no release diagnostics."""
    return {"aim": "Assist", "start": start, "result": "Quit", "hold_s": holds,
            "hold_started": [1 if h > 0.25 else 0 for h in holds]}


def r5_assist_run(start, holds, build=None):
    """A round-5 run: release_by is always written, one entry per hold."""
    run = {"aim": "Assist", "start": start, "result": "Quit", "hold_s": holds,
           "hold_started": [1 if h > 0.25 else 0 for h in holds],
           "release_by": ["relative"] * len(holds), "meta_seen": [1] * len(holds),
           "peak_strength": [1.0] * len(holds), "min_strength": [0.78] * len(holds),
           "release_strength": [0.72] * len(holds)}
    if build:
        run["build"] = build
    return run


def assist_headline(lines):
    return next(line for line in lines if line.startswith("  Assist"))


def run_main(*args):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = rs.main(["run_summary.py", *args])
    return code, out.getvalue()


class PinchHoldsByModeTests(unittest.TestCase):
    def test_headline_counts_only_holds_with_release_diagnostics(self):
        runs = [old_assist_run("2026-10-09T18:07:26", [1.5] * 8 + [0.2] * 2),
                r5_assist_run("2026-10-10T10:00:00", [0.1] * 9 + [1.2])]
        lines = rs.pinch_holds_by_mode(runs)
        self.assertEqual("  Assist: 10 holds, median 0.10 s, 1 over 1 s (10%)", assist_headline(lines))

    def test_older_holds_are_on_their_own_line(self):
        runs = [old_assist_run("2026-10-09T18:07:26", [1.5] * 8 + [0.2] * 2),
                r5_assist_run("2026-10-10T10:00:00", [0.1] * 9 + [1.2])]
        lines = rs.pinch_holds_by_mode(runs)
        older = [line for line in lines if "older APKs" in line]
        self.assertEqual(1, len(older), lines)
        self.assertIn("10 holds", older[0])
        self.assertIn("8 over 1 s (80%)", older[0])
        self.assertIn("not in the numbers above", older[0])

    def test_logs_before_round_5_keep_their_headline(self):
        lines = rs.pinch_holds_by_mode([old_assist_run("2026-10-09T18:07:26", [1.5, 0.2, 0.2, 0.2])])
        self.assertEqual("  Assist: 4 holds, median 0.20 s, 1 over 1 s (25%)", assist_headline(lines))
        self.assertIn("    released by: not recorded (logs before round 5)", lines)
        self.assertFalse(any("older APKs" in line for line in lines))

    def test_round_5_only_has_no_older_line(self):
        lines = rs.pinch_holds_by_mode([r5_assist_run("2026-10-10T10:00:00", [0.1, 1.2])])
        self.assertFalse(any("older APKs" in line for line in lines))


class LongHoldStrengthTests(unittest.TestCase):
    """DR-9: holds over 1 s, one by one with their peak and lowest strength while held.
    The peak tells the tap type (a light tap peaks under 0.95). The lowest is NOT the
    resting thumb (DR-9 second pass): PinchTrigger ends a hold only once 2 samples reach
    the release level and counts the first as held, so it is at or under that level
    (0.75 for a light tap, 0.8 for a firm one, 0.95 by Meta's flag). The fixtures use
    values PinchTrigger can produce."""

    def long_hold_line(self, lines):
        found = [line for line in lines if "holds over 1 s, longest first" in line]
        return found[0] if found else None

    def test_long_holds_list_peak_and_lowest_longest_first(self):
        run = r5_assist_run("2026-10-10T10:00:00", [0.1] * 5 + [1.5, 2.9])
        # 2.9 s: a light tap (1.8 cm) stuck at a close resting thumb, ended by opening
        # the hand (3 cm, then 4 cm). 1.5 s: a firm charge released by Meta's flag.
        run["peak_strength"] = [1.0] * 5 + [1.0, 0.93]
        run["min_strength"] = [0.72] * 5 + [0.93, 0.67]
        run["release_by"] = ["relative"] * 5 + ["meta", "relative"]
        line = self.long_hold_line(rs.pinch_holds_by_mode([run]))
        self.assertEqual("    holds over 1 s, longest first (peak / lowest while held):"
                         " 2.9 s 0.93/0.67, 1.5 s 1.00/0.93", line)

    def test_no_line_without_long_holds(self):
        run = r5_assist_run("2026-10-10T10:00:00", [0.1, 0.2, 0.9])
        self.assertIsNone(self.long_hold_line(rs.pinch_holds_by_mode([run])))

    def test_trigger_holds_have_no_strengths_and_are_skipped(self):
        run = r5_assist_run("2026-10-10T10:00:00", [1.2, 3.0])
        run["aim"] = "Cursor"
        run["release_by"] = ["relative", "none"]
        run["peak_strength"] = [0.98, 0.0]
        run["min_strength"] = [0.77, 0.0]
        line = self.long_hold_line(rs.pinch_holds_by_mode([run]))
        self.assertEqual("    holds over 1 s, longest first (peak / lowest while held): 1.2 s 0.98/0.77", line)

    def test_at_most_eight_listed(self):
        run = r5_assist_run("2026-10-10T10:00:00", [1.0 + 0.1 * i for i in range(1, 11)])
        line = self.long_hold_line(rs.pinch_holds_by_mode([run]))
        self.assertEqual(8, line.count(" s 1.00/"), line)
        self.assertTrue(line.endswith(", +2 more"), line)


class RestingThumbTests(unittest.TestCase):
    """DR-9 second pass: report A5 picks the knob from the resting thumb strength r. The
    run log keeps the aim hand's seconds per unsmoothed strength ("pinch_strength_s",
    RunRecordJson); r is where most of that time sits between 0.50 and 0.95."""

    # A thumb resting at 0.77 (2.5 cm): stuck light taps, firm charges at 1.00 (more
    # time than the rest, but a press, not a rest) and the open hand under 0.50.
    CLOSE_REST = {"<0.50": 40, "0.76": 6, "0.77": 30, "0.78": 6, "0.93": 4, "1.00": 50}

    def resting_line(self, lines):
        found = [line for line in lines if line.startswith("    resting thumb")]
        return found[0] if found else None

    def test_resting_thumb_is_where_the_most_time_sits(self):
        run = r5_assist_run("2026-10-10T10:00:00", [0.1, 2.9])
        run["pinch_strength_s"] = dict(self.CLOSE_REST)
        self.assertEqual("    resting thumb (most aim-hand time at 0.50-0.95): r 0.77 = 2.5 cm,"
                         " 31% of 136 s tracked", self.resting_line(rs.pinch_holds_by_mode([run])))

    def test_reads_r_where_the_lowest_while_held_cannot(self):
        # DR-9 re-check: true r 0.756-0.822 read 0.66-0.75 as "lowest while held".
        run = r5_assist_run("2026-10-10T10:00:00", [2.9])
        run["peak_strength"], run["min_strength"] = [0.93], [0.67]
        run["pinch_strength_s"] = {"<0.50": 5, "0.66": 0.1, "0.81": 3, "0.82": 20, "0.83": 4, "0.93": 1}
        lines = rs.pinch_holds_by_mode([run])
        self.assertIn("r 0.82 = 2.3 cm", self.resting_line(lines))

    def test_sums_the_time_over_runs(self):
        a = r5_assist_run("2026-10-10T10:00:00", [0.1])
        b = r5_assist_run("2026-10-10T10:20:00", [0.1])
        a["pinch_strength_s"] = {"0.70": 10, "0.71": 5}
        b["pinch_strength_s"] = {"0.71": 20, "0.72": 5}
        self.assertEqual("    resting thumb (most aim-hand time at 0.50-0.95): r 0.71 = 2.8 cm,"
                         " 100% of 40 s tracked", self.resting_line(rs.pinch_holds_by_mode([a, b])))

    def test_taps_that_never_fired_still_show_r(self):
        # After a reopen, light taps at r 0.77 never fire: no holds, but the time is there.
        run = r5_assist_run("2026-10-10T10:00:00", [])
        run["pinch_strength_s"] = {"0.77": 30, "0.84": 2, "0.93": 3}
        lines = rs.pinch_holds_by_mode([run])
        self.assertIn("  Assist: 0 holds", assist_headline(lines))
        self.assertIn("r 0.77 = 2.5 cm", self.resting_line(lines))

    def test_round_5_apk_before_the_field_says_not_recorded(self):
        run = r5_assist_run("2026-10-10T10:00:00", [0.1, 2.9])
        self.assertEqual("    resting thumb: not recorded (APK before the deep-review second pass)",
                         self.resting_line(rs.pinch_holds_by_mode([run])))

    def test_cursor_runs_have_no_resting_line(self):
        run = r5_assist_run("2026-10-10T10:00:00", [0.1])
        run["aim"] = "Cursor"
        run["release_by"] = ["none"]
        run["pinch_strength_s"] = {}
        self.assertIsNone(self.resting_line(rs.pinch_holds_by_mode([run])))

    def test_resting_thumb_helper(self):
        self.assertIsNone(rs.resting_thumb({}))
        self.assertIsNone(rs.resting_thumb({"<0.50": 3, "1.00": 4}))
        r, share, total = rs.resting_thumb({"0.70": 1, "0.71": 2, "0.72": 1, "0.90": 1})
        self.assertAlmostEqual(0.71, r)
        self.assertAlmostEqual(0.8, share)
        self.assertAlmostEqual(5, total)

    def test_r_averages_within_three_bins_of_the_peak(self):
        # The peak (+-1 bin) is at 0.71; noisy tracking spreads the rest further, so r is
        # the mean of 0.68-0.74 (0.72 here), not of the three bins at the peak (0.71).
        r, share, _ = rs.resting_thumb({"0.70": 5, "0.71": 10, "0.72": 5, "0.74": 10, "0.78": 1})
        self.assertAlmostEqual(0.72, r)
        self.assertAlmostEqual(30 / 31, share)


class SelectRunsTests(unittest.TestCase):
    def test_default_skips_unstamped_runs_from_before_round_5(self):
        # The round-5 APKs in the report (0203 release, 0156 dev) were built before the
        # DR-7 stamp, so their runs have no "build" either; release_by tells them apart.
        old = [old_assist_run("2026-10-09T17:56:05", [1.5] * 3),
               old_assist_run("2026-10-09T23:02:31", [1.5] * 5)]
        new = [r5_assist_run("2026-10-10T14:00:00", [0.1] * 4)]
        chosen, header = rs.select_runs(old + new)
        self.assertEqual(new, chosen)
        self.assertTrue(any("1 of 3 runs" in line for line in header), header)

    def test_build_none_still_selects_every_unstamped_run(self):
        runs = [old_assist_run("2026-10-09T17:56:05", [1.5]),
                r5_assist_run("2026-10-10T14:00:00", [0.1]),
                r5_assist_run("2026-10-10T15:00:00", [0.1], build="9.2.0+20261010_1042_release")]
        chosen, _ = rs.select_runs(runs, build_filter="none")
        self.assertEqual(runs[:2], chosen)

    def test_build_round_5_selects_only_unstamped_round_5_runs(self):
        runs = [old_assist_run("2026-10-09T17:56:05", [1.5]),
                r5_assist_run("2026-10-10T14:00:00", [0.1]),
                r5_assist_run("2026-10-10T15:00:00", [0.1], build="9.2.0+20261010_1042_release")]
        chosen, _ = rs.select_runs(runs, build_filter="round 5")
        self.assertEqual([runs[1]], chosen)

    def test_newest_stamped_build_is_still_the_default(self):
        runs = [old_assist_run("2026-10-09T17:56:05", [1.5]),
                r5_assist_run("2026-10-10T14:00:00", [0.1]),
                r5_assist_run("2026-10-10T15:00:00", [0.1], build="9.2.0+20261010_1042_release")]
        chosen, _ = rs.select_runs(runs)
        self.assertEqual([runs[2]], chosen)


class SinceFilterTests(unittest.TestCase):
    def test_since_keeps_runs_started_at_or_after_the_time(self):
        runs = [old_assist_run("2026-10-09T23:02:31", [1.5]),
                r5_assist_run("2026-10-10T14:00:00", [0.1]),
                r5_assist_run("2026-10-10T15:30:00", [0.1])]
        self.assertEqual(runs[1:], rs.runs_since(runs, "2026-10-10"))
        self.assertEqual(runs[2:], rs.runs_since(runs, "2026-10-10 15:00"))
        self.assertEqual(runs[1:], rs.runs_since(runs, "2026-10-10T14:00:00"))

    def test_since_drops_runs_without_a_start_time(self):
        runs = [{"aim": "Assist", "hold_s": [0.1]}, r5_assist_run("2026-10-10T14:00:00", [0.1])]
        self.assertEqual(runs[1:], rs.runs_since(runs, "2026-10-10"))


class DeviceLogScenarioTests(unittest.TestCase):
    """The DR-5 scenario on real data: the round-4 device file (rounds 3-4 runs, unstamped)
    with round-5 runs from an unstamped round-5 APK appended after `adb install -r`."""

    def test_default_summary_reports_the_round_5_assist_share(self):
        code, out = run_main(R4_DEVICE_LOG, R5_SAMPLE)
        self.assertEqual(0, code)
        self.assertIn("  Assist: 10 holds, median 0.13 s, 1 over 1 s (10%)", out)
        self.assertNotIn("141 holds", out)
        self.assertIn("Runs: 2 ", out)

    def test_all_builds_headline_is_round_5_and_older_runs_are_separate(self):
        code, out = run_main("--all", R4_DEVICE_LOG, R5_SAMPLE)
        self.assertEqual(0, code)
        self.assertIn("  Assist: 10 holds, median 0.13 s, 1 over 1 s (10%)", out)
        self.assertNotIn("141 holds", out)
        self.assertIn("older APKs (rounds 3-4, not in the numbers above): 131 holds,", out)
        self.assertIn("37 over 1 s (28%)", out)

    def test_since_on_the_command_line(self):
        code, out = run_main("--all", "--since", "2026-10-10", R4_DEVICE_LOG, R5_SAMPLE)
        self.assertEqual(0, code)
        self.assertIn("Runs: 2 ", out)
        self.assertNotIn("older APKs", out)


if __name__ == "__main__":
    unittest.main()
