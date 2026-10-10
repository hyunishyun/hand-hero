"""Tests for run_summary.py (deep review DR-5, DR-7).

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
           "peak_strength": [1.0] * len(holds), "min_strength": [0.95] * len(holds),
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
