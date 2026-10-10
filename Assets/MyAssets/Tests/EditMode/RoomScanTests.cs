using System.Collections.Generic;
using System.Text;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Round 4 (S8, D9): the MR room-scan spike. The flow decides when the AR
    // plane / bounding box managers run and when the scene permission is asked;
    // the timer measures time to first result; the summary turns trackables
    // into one perf log line (counts, labels, sizes only).
    public class RoomScanTests
    {
        // ---- RoomScanFlow ----

        [Test]
        public void Flow_VrArena_NeverAsksNorRuns()
        {
            var flow = new RoomScanFlow();
            for (int i = 0; i < 3; i++)
            {
                RoomScanCommand c = flow.Step(tabletop: false, available: true, granted: false);
                Assert.IsFalse(c.RunManagers);
                Assert.IsFalse(c.RequestPermission);
                Assert.AreEqual(RoomScanEvent.None, c.Event);
            }
        }

        [Test]
        public void Flow_ProbeOff_NeverAsksNorRuns()
        {
            var flow = new RoomScanFlow { Enabled = false };
            RoomScanCommand c = flow.Step(tabletop: true, available: true, granted: true);
            Assert.IsFalse(c.RunManagers);
            Assert.IsFalse(c.RequestPermission);
            Assert.AreEqual(RoomScanEvent.None, c.Event);
        }

        [Test]
        public void Flow_NoSubsystem_ReportsUnavailableOnce_AndNeverAsks()
        {
            var flow = new RoomScanFlow();
            RoomScanCommand first = flow.Step(true, available: false, granted: false);
            RoomScanCommand second = flow.Step(true, available: false, granted: false);

            Assert.AreEqual(RoomScanEvent.Unavailable, first.Event);
            Assert.AreEqual(RoomScanEvent.None, second.Event);
            Assert.IsFalse(first.RequestPermission || second.RequestPermission);
            Assert.IsFalse(first.RunManagers || second.RunManagers);
        }

        [Test]
        public void Flow_NotGranted_AsksOnce_WhileWaiting()
        {
            var flow = new RoomScanFlow();
            RoomScanCommand ask = flow.Step(true, true, granted: false);
            RoomScanCommand wait = flow.Step(true, true, granted: false);

            Assert.IsTrue(ask.RequestPermission);
            Assert.AreEqual(RoomScanEvent.PermissionAsked, ask.Event);
            Assert.IsFalse(ask.RunManagers);
            Assert.IsFalse(wait.RequestPermission, "one system dialog per session");
            Assert.IsFalse(wait.RunManagers);
        }

        [Test]
        public void Flow_Denied_StaysOff_AndNeverAsksAgain()
        {
            var flow = new RoomScanFlow();
            flow.Step(true, true, false);
            Assert.AreEqual(RoomScanEvent.PermissionDenied, flow.OnPermissionResult(false));

            RoomScanCommand c = flow.Step(true, true, false);
            Assert.IsFalse(c.RunManagers);
            Assert.IsFalse(c.RequestPermission);

            flow.Step(false, true, false); // back to the VR arena
            c = flow.Step(true, true, false); // MR TABLE again
            Assert.IsFalse(c.RunManagers);
            Assert.IsFalse(c.RequestPermission);
            Assert.AreEqual(RoomScanEvent.None, c.Event);
        }

        [Test]
        public void Flow_Granted_RunsManagers()
        {
            var flow = new RoomScanFlow();
            flow.Step(true, true, false);
            Assert.AreEqual(RoomScanEvent.PermissionGranted, flow.OnPermissionResult(true));

            RoomScanCommand c = flow.Step(true, true, granted: true);
            Assert.IsTrue(c.RunManagers);
            Assert.AreEqual(RoomScanEvent.Started, c.Event);
            Assert.AreEqual(RoomScanEvent.None, flow.Step(true, true, true).Event);
        }

        [Test]
        public void Flow_GrantedAnswer_TrustsTheCallbackEvenIfTheQueryLags()
        {
            // The Android callback can arrive a frame before HasUserAuthorizedPermission reports it.
            var flow = new RoomScanFlow();
            flow.Step(true, true, false);
            flow.OnPermissionResult(true);

            Assert.IsTrue(flow.Step(true, true, granted: false).RunManagers);
        }

        [Test]
        public void Flow_AlreadyGranted_RunsWithoutAsking()
        {
            var flow = new RoomScanFlow();
            RoomScanCommand c = flow.Step(true, true, granted: true);
            Assert.IsTrue(c.RunManagers);
            Assert.IsFalse(c.RequestPermission);
            Assert.AreEqual(RoomScanEvent.Started, c.Event);
        }

        [Test]
        public void Flow_LeavingTabletop_Stops_ReenteringRestartsWithoutAsking()
        {
            var flow = new RoomScanFlow();
            flow.Step(true, true, true);

            RoomScanCommand off = flow.Step(false, true, true);
            Assert.IsFalse(off.RunManagers);
            Assert.AreEqual(RoomScanEvent.Stopped, off.Event);
            Assert.AreEqual(RoomScanEvent.None, flow.Step(false, true, true).Event);

            RoomScanCommand on = flow.Step(true, true, true);
            Assert.IsTrue(on.RunManagers);
            Assert.IsFalse(on.RequestPermission);
            Assert.AreEqual(RoomScanEvent.Started, on.Event);
        }

        [Test]
        public void Flow_AnswerAfterLeavingTabletop_WaitsForTheNextTabletop()
        {
            var flow = new RoomScanFlow();
            flow.Step(true, true, false);
            flow.Step(false, true, false);
            flow.OnPermissionResult(true);

            Assert.IsFalse(flow.Step(false, true, true).RunManagers);
            Assert.IsTrue(flow.Step(true, true, true).RunManagers);
        }

        // ---- round 5: dismissed dialog, failures, deferred stop, permission polling ----

        [Test]
        public void Flow_DismissedDialog_IsADenialForTheSession_WithItsOwnEvent()
        {
            // S8-1-2: closing the dialog without an answer used to leave the flow in Asked.
            var flow = new RoomScanFlow();
            flow.Step(true, true, false);
            Assert.AreEqual(RoomScanEvent.PermissionDismissed, flow.OnPermissionDismissed());

            RoomScanCommand c = flow.Step(true, true, false);
            Assert.IsFalse(c.RunManagers);
            Assert.IsFalse(c.RequestPermission);

            flow.Step(false, true, false);
            c = flow.Step(true, true, granted: true); // granted later in the settings: next session
            Assert.IsFalse(c.RunManagers);
            Assert.IsFalse(c.RequestPermission, "never asked again this session");
            Assert.AreEqual(RoomScanEvent.None, c.Event);
        }

        [Test]
        public void EventName_PermissionDismissed()
        {
            Assert.AreEqual("PERMISSION_DISMISSED", RoomScanSummary.EventName(RoomScanEvent.PermissionDismissed));
        }

        [Test]
        public void Flow_FailedOnTheStartFrame_ReportsUnavailableOnce_AndNothingElse()
        {
            // S8-1-3: the probe gets Started, switching the managers throws, it calls OnFailed.
            var flow = new RoomScanFlow();
            Assert.AreEqual(RoomScanEvent.Started, flow.Step(true, true, true).Event);

            Assert.AreEqual(RoomScanEvent.Unavailable, flow.OnFailed());
            Assert.AreEqual(RoomScanEvent.None, flow.OnFailed(), "logged once");
            Assert.IsTrue(flow.Failed);
            Assert.IsFalse(flow.Running);

            bool[] tabletop = { true, false, true, false };
            foreach (bool t in tabletop)
            {
                RoomScanCommand c = flow.Step(t, true, true);
                Assert.IsFalse(c.RunManagers);
                Assert.IsFalse(c.RequestPermission);
                Assert.AreEqual(RoomScanEvent.None, c.Event, "no STARTED / STOPPED after a failure");
            }
        }

        [Test]
        public void Flow_FailedAfterUnavailableWasReported_LogsNothingMore()
        {
            var flow = new RoomScanFlow();
            Assert.AreEqual(RoomScanEvent.Unavailable, flow.Step(true, available: false, granted: false).Event);
            Assert.AreEqual(RoomScanEvent.None, flow.OnFailed());
        }

        [Test]
        public void Flow_StopDelay_KeepsTheManagersOnForThoseFrames_ThenStops()
        {
            // F2-2: the summary and the shutdown move off the match-start frame.
            var flow = new RoomScanFlow { StopDelayFrames = 2 };
            flow.Step(true, true, true);

            for (int frame = 0; frame < 2; frame++)
            {
                RoomScanCommand wait = flow.Step(false, true, true);
                Assert.IsTrue(wait.RunManagers, $"frame {frame}");
                Assert.AreEqual(RoomScanEvent.None, wait.Event, $"frame {frame}");
            }
            RoomScanCommand stop = flow.Step(false, true, true);
            Assert.IsFalse(stop.RunManagers);
            Assert.AreEqual(RoomScanEvent.Stopped, stop.Event);
            Assert.AreEqual(RoomScanEvent.None, flow.Step(false, true, true).Event);
            Assert.IsFalse(flow.Running);
        }

        [Test]
        public void Flow_StopDelay_BackInTheMenuBeforeTheStop_KeepsRunning_WithoutANewStart()
        {
            var flow = new RoomScanFlow { StopDelayFrames = 2 };
            flow.Step(true, true, true);
            flow.Step(false, true, true);

            RoomScanCommand back = flow.Step(true, true, true);
            Assert.IsTrue(back.RunManagers);
            Assert.AreEqual(RoomScanEvent.None, back.Event);

            // A later exit waits the full delay again.
            Assert.IsTrue(flow.Step(false, true, true).RunManagers);
            Assert.IsTrue(flow.Step(false, true, true).RunManagers);
            Assert.AreEqual(RoomScanEvent.Stopped, flow.Step(false, true, true).Event);
        }

        [Test]
        public void Flow_StopDelay_NotRunning_NothingToWaitFor()
        {
            var flow = new RoomScanFlow { StopDelayFrames = 3 };
            RoomScanCommand c = flow.Step(false, true, true);
            Assert.IsFalse(c.RunManagers);
            Assert.AreEqual(RoomScanEvent.None, c.Event);
        }

        [Test]
        public void Flow_NeedsPermissionQuery_OnlyUntilTheAnswerIsKnown()
        {
            var flow = new RoomScanFlow();
            Assert.IsTrue(flow.NeedsPermissionQuery, "before the first MR TABLE frame");
            flow.Step(true, true, false);
            Assert.IsTrue(flow.NeedsPermissionQuery, "dialog open");
            flow.OnPermissionResult(true);
            Assert.IsFalse(flow.NeedsPermissionQuery, "granted");

            var denied = new RoomScanFlow();
            denied.Step(true, true, false);
            denied.OnPermissionResult(false);
            Assert.IsFalse(denied.NeedsPermissionQuery, "denied");

            var dismissed = new RoomScanFlow();
            dismissed.Step(true, true, false);
            dismissed.OnPermissionDismissed();
            Assert.IsFalse(dismissed.NeedsPermissionQuery, "dismissed");

            var alreadyGranted = new RoomScanFlow();
            alreadyGranted.Step(true, true, true);
            Assert.IsFalse(alreadyGranted.NeedsPermissionQuery, "granted before the app started");

            var unavailable = new RoomScanFlow();
            unavailable.Step(true, false, false);
            Assert.IsFalse(unavailable.NeedsPermissionQuery, "no subsystem");

            var failed = new RoomScanFlow();
            failed.OnFailed();
            Assert.IsFalse(failed.NeedsPermissionQuery, "failed");

            Assert.IsFalse(new RoomScanFlow { Enabled = false }.NeedsPermissionQuery, "probe off");
        }

        [Test]
        public void PermissionPoll_FirstQueryAtOnce_ThenAtMostOncePerInterval()
        {
            var poll = new RoomScanPermissionPoll { Interval = 0.5 };
            Assert.IsTrue(poll.Due(true, 10.0));
            Assert.IsFalse(poll.Due(true, 10.1));
            Assert.IsFalse(poll.Due(true, 10.49));
            Assert.IsTrue(poll.Due(true, 10.5));
            Assert.IsFalse(poll.Due(true, 10.6));
        }

        [Test]
        public void PermissionPoll_NeverWhenNotNeeded_AndKeepsTheLastAnswer()
        {
            var poll = new RoomScanPermissionPoll();
            Assert.IsFalse(poll.Due(false, 0.0));
            Assert.IsFalse(poll.Due(false, 100.0));
            Assert.IsFalse(poll.LastAnswer);

            Assert.IsTrue(poll.Due(true, 100.0));
            poll.Answer(true);
            Assert.IsTrue(poll.LastAnswer);
            Assert.IsFalse(poll.Due(false, 200.0));
            Assert.IsTrue(poll.LastAnswer);
        }

        [Test]
        public void Probe_InTheMrTableMenu_QueriesTwicePerSecondWhileAsking_AndNeverOnceGranted()
        {
            // The probe's Update, one frame per step at 72 Hz.
            var flow = new RoomScanFlow();
            var poll = new RoomScanPermissionPoll { Interval = 0.5 };
            int queries = 0;
            int frame = 0;

            void Frame(bool systemAnswer)
            {
                double now = frame++ / 72.0;
                if (poll.Due(flow.NeedsPermissionQuery, now))
                {
                    queries++;
                    poll.Answer(systemAnswer);
                }
                flow.Step(true, true, poll.LastAnswer);
            }

            for (int i = 0; i < 72; i++) Frame(false); // the dialog is open for a second
            Assert.AreEqual(2, queries, "at 0 s and 0.5 s, not 72 times");

            flow.OnPermissionResult(true);
            queries = 0;
            for (int i = 0; i < 720; i++) Frame(true); // ten seconds in the menu
            Assert.AreEqual(0, queries, "decided: no more JNI calls");
            Assert.IsTrue(flow.Running);
        }

        // ---- RoomScanTimer ----

        [Test]
        public void Timer_BeforeStart_ReportsNothing()
        {
            var timer = new RoomScanTimer();
            Assert.IsFalse(timer.Observe(RoomItemKind.Plane, 3, 5.0));
            Assert.AreEqual(-1f, timer.FirstSeconds(RoomItemKind.Plane));
        }

        [Test]
        public void Timer_RecordsOnlyTheFirstNonEmptyResultPerKind()
        {
            var timer = new RoomScanTimer();
            timer.Start(10.0);

            Assert.IsFalse(timer.Observe(RoomItemKind.Plane, 0, 10.2), "an empty update is not a result");
            Assert.IsTrue(timer.Observe(RoomItemKind.Plane, 4, 10.5));
            Assert.IsFalse(timer.Observe(RoomItemKind.Plane, 6, 11.0));
            Assert.IsTrue(timer.Observe(RoomItemKind.Box, 2, 12.25));

            Assert.AreEqual(0.5f, timer.FirstSeconds(RoomItemKind.Plane), 1e-4f);
            Assert.AreEqual(2.25f, timer.FirstSeconds(RoomItemKind.Box), 1e-4f);
        }

        [Test]
        public void Timer_Restart_ForgetsTheLastSession()
        {
            var timer = new RoomScanTimer();
            timer.Start(0.0);
            timer.Observe(RoomItemKind.Plane, 1, 1.0);
            timer.Start(20.0);

            Assert.AreEqual(-1f, timer.FirstSeconds(RoomItemKind.Plane));
            Assert.IsTrue(timer.Observe(RoomItemKind.Plane, 1, 20.3));
            Assert.AreEqual(0.3f, timer.FirstSeconds(RoomItemKind.Plane), 1e-4f);
        }

        // ---- RoomScanSummary ----

        private static RoomItem Plane(string label, bool up, Vector3 center, float w, float d)
        {
            return new RoomItem { Kind = RoomItemKind.Plane, Label = label, HorizontalUp = up, Center = center, Size = new Vector3(w, 0f, d) };
        }

        private static RoomItem Box(string label, Vector3 center, Vector3 size)
        {
            return new RoomItem { Kind = RoomItemKind.Box, Label = label, Center = center, Size = size };
        }

        private static readonly Vector3 Table = new Vector3(0f, -0.5f, 0.7f);

        [Test]
        public void Largest_PicksTheBiggestUpFacingSurfaceNearTheTable()
        {
            var items = new List<RoomItem>
            {
                Plane("Table", true, new Vector3(0.1f, -0.48f, 0.8f), 1.2f, 0.6f),  // 0.72 m2
                Plane("Other", true, new Vector3(-0.4f, -0.55f, 0.5f), 0.5f, 0.5f), // 0.25 m2
            };

            RoomSurface s = RoomScanSummary.LargestHorizontalNear(items, Table, 1f, 0.5f);

            Assert.IsTrue(s.Found);
            Assert.AreEqual("Table", s.Label);
            Assert.AreEqual(RoomItemKind.Plane, s.Kind);
            Assert.AreEqual(0.72f, s.Area, 1e-4f);
            Assert.AreEqual(0.02f, s.HeightAboveTable, 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(0.01f + 0.01f), s.Distance, 1e-4f);
        }

        [Test]
        public void Largest_IgnoresWallsCeilingsFloorsAndFarSurfaces()
        {
            var items = new List<RoomItem>
            {
                Plane("WallFace", false, new Vector3(0f, 0f, 1.2f), 4f, 2.5f),
                Plane("Ceiling", false, new Vector3(0f, 1.3f, 0f), 4f, 4f),     // faces down
                Plane("Floor", true, new Vector3(0f, -1.2f, 0f), 4f, 4f),       // 0.7 m below the table point
                Plane("Couch", true, new Vector3(3f, -0.6f, 0f), 2f, 0.9f),     // 3.1 m away
            };

            Assert.IsFalse(RoomScanSummary.LargestHorizontalNear(items, Table, 1f, 0.5f).Found);
        }

        [Test]
        public void Largest_UsesTheTopFaceOfABox()
        {
            var items = new List<RoomItem>
            {
                // A 0.75 m tall table box whose center is 0.375 m above the floor: top at -0.5.
                Box("Table", new Vector3(0f, -0.875f, 0.7f), new Vector3(1.5f, 0.75f, 0.8f)),
            };

            RoomSurface s = RoomScanSummary.LargestHorizontalNear(items, Table, 1f, 0.5f);

            Assert.IsTrue(s.Found);
            Assert.AreEqual(RoomItemKind.Box, s.Kind);
            Assert.AreEqual(1.2f, s.Area, 1e-4f);
            Assert.AreEqual(1.5f, s.Width, 1e-4f);
            Assert.AreEqual(0.8f, s.Depth, 1e-4f);
            Assert.AreEqual(0f, s.HeightAboveTable, 1e-4f);
        }

        [Test]
        public void Labels_AreCountedAndSortedPerKind()
        {
            var items = new List<RoomItem>
            {
                Plane("WallFace", false, Vector3.zero, 1f, 1f),
                Plane("Table", true, Vector3.zero, 1f, 1f),
                Plane("WallFace", false, Vector3.zero, 1f, 1f),
                Box("Couch", Vector3.zero, Vector3.one),
            };
            var sb = new StringBuilder();

            RoomScanSummary.AppendLabelCounts(sb, items, RoomItemKind.Plane);
            sb.Append(' ');
            RoomScanSummary.AppendLabelCounts(sb, items, RoomItemKind.Box);
            sb.Append(' ');
            RoomScanSummary.AppendLabelCounts(sb, new List<RoomItem>(), RoomItemKind.Box);

            Assert.AreEqual("[Table:1,WallFace:2] [Couch:1] [-]", sb.ToString());
        }

        [Test]
        public void CleanLabel_JoinsFlagsAndNamesEmptyOnes()
        {
            Assert.AreEqual("Table+Couch", RoomScanSummary.CleanLabel("Table, Couch"));
            Assert.AreEqual("None", RoomScanSummary.CleanLabel(""));
            Assert.AreEqual("None", RoomScanSummary.CleanLabel(null));
            Assert.AreEqual("Floor", RoomScanSummary.CleanLabel("Floor"));
        }

        [Test]
        public void Format_WritesCountsLabelsTimesAndTheTableSurface()
        {
            var items = new List<RoomItem>
            {
                Plane("Floor", true, new Vector3(0f, -1.2f, 0f), 3f, 4f),
                Plane("Table", true, new Vector3(0f, -0.47f, 0.7f), 1.2f, 0.6f),
                Box("Table", new Vector3(0f, -0.845f, 0.7f), new Vector3(1.2f, 0.75f, 0.6f)),
            };
            var timer = new RoomScanTimer();
            timer.Start(0.0);
            timer.Observe(RoomItemKind.Plane, 2, 0.42);
            timer.Observe(RoomItemKind.Box, 1, 0.5);

            string line = RoomScanSummary.Format(items, timer, Table, 1f, 0.5f);

            Assert.AreEqual(
                "planes=2 [Floor:1,Table:1] boxes=1 [Table:1] first_plane=0.42s first_box=0.50s " +
                "near_table=Table(plane) 1.20x0.60m area=0.72 dy=0.03 d=0.00",
                line);
        }

        [Test]
        public void Format_SaysNoneWhenNothingWasFound()
        {
            var timer = new RoomScanTimer();
            timer.Start(0.0);

            Assert.AreEqual(
                "planes=0 [-] boxes=0 [-] first_plane=-1.00s first_box=-1.00s near_table=none",
                RoomScanSummary.Format(new List<RoomItem>(), timer, Table, 1f, 0.5f));
        }

        // ---- perf log ----

        [Test]
        public void PerfLog_RoomScanRecord_PrintsItsEventAndNote()
        {
            var sb = new StringBuilder();
            var s = new PerfSample
            {
                Kind = PerfRecordKind.RoomScan,
                Detail = (int)RoomScanEvent.Summary,
                Note = "planes=2 [Floor:1,Table:1]",
            };

            PerfLogFormatter.AppendLine(sb, s, 0.0);

            string line = sb.ToString();
            StringAssert.Contains(" ROOM_SCAN scan=SUMMARY ", line);
            StringAssert.EndsWith(" head=0 | planes=2 [Floor:1,Table:1]\n", line);
        }

        [Test]
        public void PerfLog_OtherRecords_HaveNoNote()
        {
            var sb = new StringBuilder();
            PerfLogFormatter.AppendLine(sb, new PerfSample { Kind = PerfRecordKind.Spike }, 0.0);
            StringAssert.EndsWith(" head=0\n", sb.ToString());
            StringAssert.DoesNotContain("scan=", sb.ToString());
        }
    }
}
