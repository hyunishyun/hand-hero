namespace HandHero.Core
{
    public struct PinchState
    {
        // True for exactly one step per pinch (fire a normal shot).
        public bool FireTriggered;
        // Pinch is closed (charge shot hold).
        public bool Held;
        // Set on the step a held pinch ends (round 5, D2): what ended it and the strengths.
        public PinchRelease Release;
    }

    // What ended a pinch (round 5, D1/D2), for the run log. When several rules
    // fire on the same step the first in this order names it.
    public enum PinchReleaseBy : byte
    {
        None,     // still held, or nothing was held
        Meta,     // Meta's index-pinch flag went off (after it was on during this press)
        Absolute, // strength fell to ResetThreshold (the rounds 1-4 rule would release too)
        Relative, // strength fell RelativeRelease below this press's peak, or to the floor under FireThreshold (only the new rule releases)
        Lost,     // tracking lost, or the system gesture took the pinch
    }

    // One finished pinch (round 5, D2). Strengths are the unsmoothed 0..1 values.
    public struct PinchRelease
    {
        public PinchReleaseBy By;
        // Highest strength during the press (the closest thumb-index distance).
        public float PeakStrength;
        // Lowest strength while the pinch still counted as held.
        public float MinStrength;
        // Strength on the release step (the last tracked value for Lost).
        public float ReleaseStrength;
        // Meta's index-pinch flag was on at some point during the press.
        public bool MetaSeen;

        public bool Ended => By != PinchReleaseBy.None;
    }

    // One aim-hand sample (round 5).
    public struct PinchSample
    {
        public bool Tracked;
        // Unsmoothed thumb-index pinch strength: 0 = apart, 1 = pinched.
        public float Strength;
        // Meta Hand Tracking Aim state was valid this frame.
        public bool HasMeta;
        // Meta's own (platform-calibrated) index-pinch flag; read only when HasMeta.
        public bool MetaPinching;
        // Meta system gesture on this hand: its pinch belongs to the OS (CR-7).
        public bool SystemGesture;
    }

    public struct PinchReleaseParams
    {
        // Strength at or above this presses (fires once).
        public float FireThreshold;
        // Strength at or below this releases and fully re-arms (the rounds 1-4 rule).
        public float ResetThreshold;
        // A drop this far below the press's peak releases; the next press must then
        // rise this far above the lowest point since. 0 = off (absolute rule only).
        public float RelativeRelease;
        // With RelativeRelease on, a pinch also ends at FireThreshold minus this,
        // however deep the press went: a light press (peak 0.8-0.9, thumb 2.4-1.9 cm)
        // still ends at the resting pointing thumb (0.71). 0 = off (peak drop only).
        public float ReleaseFloorMargin;
        // Once the strength has settled at or below that floor after a release, the
        // next press needs at most FireThreshold plus this (not RelativeRelease above
        // the lowest point), so a light tap after a firm one fires.
        public float RearmMargin;
        // The strength rules count only when this many consecutive samples agree:
        // one-frame tracking outliers neither end a hold nor reopen a dropped pinch.
        // 0 or 1 = every sample counts (the rounds 1-4 rule).
        public int ConfirmFrames;
        // Meta's flag off for this many steps (after it was on in this press)
        // releases. 0 = the Meta flag is ignored.
        public int MetaReleaseFrames;
        // ...and only once the strength is at least this far below the press's peak
        // (the thumb moved) on ConfirmFrames samples in a row, so a flag flicker
        // while the fingers stay closed, plus one tracking outlier, does not end the
        // hold (deep review DR-6). 0 = the flag alone.
        public float MetaReleaseDrop;

        public static PinchReleaseParams Default => new PinchReleaseParams
        {
            FireThreshold = 0.8f,
            ResetThreshold = 0.6f,
            RelativeRelease = 0.2f,
            ReleaseFloorMargin = 0.05f,
            RearmMargin = 0.05f,
            ConfirmFrames = 2,
            MetaReleaseFrames = 2,
            MetaReleaseDrop = 0.05f,
        };
    }

    // Aim-hand pinch: fire edge + hold level with hysteresis (ADR 8).
    //
    // Press: strength rises to FireThreshold (on the sample it gets there: no added
    // shot latency). Release (round 5, D1), whichever comes first:
    // - Meta's index-pinch flag off for MetaReleaseFrames steps after it was on in
    //   this press, with the strength MetaReleaseDrop below the press's peak (a flag
    //   flicker while the fingers stay closed is not a release);
    // - strength at or below ResetThreshold;
    // - strength at or below the press's release level: RelativeRelease below its
    //   peak, but never under FireThreshold - ReleaseFloorMargin. This is the ASSIST
    //   fix: a pointing hand rests the thumb about 2.8 cm from the index (0.71),
    //   above the reset, so a pinch used to stay "held" for seconds and read as a
    //   charge; the floor makes a light press (peak 0.8-0.9) end there too (review
    //   T0-R1-1/R2-1). It does not count while Meta's flag is on (D1: the fallback).
    // The strength rules, and the Meta rule's drop, need ConfirmFrames consecutive
    // samples (review T0-R1-2/R2-3, deep review DR-6): one tracking outlier inside a
    // hold neither ends it nor, with the next sample back at the peak, fires a
    // second shot.
    //
    // A release above the reset re-arms relatively: the next press must rise
    // RelativeRelease above the lowest settled strength since (the highest of
    // ConfirmFrames samples in a row), so jitter at the release point never fires
    // twice. Once that low has settled at or below the floor, FireThreshold +
    // RearmMargin is enough, so a light tap after a firm one fires. Settled at or
    // below the reset = fully re-armed. With RelativeRelease, ConfirmFrames and
    // MetaReleaseFrames at 0 this is the rounds 1-4 absolute rule.
    //
    // Tracking loss, the Meta system gesture and RequireReopen drop the pinch; one
    // still closed must open (the same settled low) before it fires or holds, so a
    // single low reacquire frame is not a reopen.
    //
    // The rounds 1-4 overload also ignores the pinch while the aim-hand fist is held
    // and for a short time after: a closed fist brings thumb and index close enough
    // to read as a pinch.
    public class PinchTrigger
    {
        // Longest ConfirmFrames window (larger values are clamped to it).
        public const int MaxConfirmFrames = 4;

        private float _suppressTimer;
        private bool _held;
        private bool _mustReopen;
        // Lowest settled strength since the last release or reopen request (+inf
        // right after a reopen request, until ConfirmFrames samples have come in).
        private float _trough = float.PositiveInfinity;

        // The last samples since the last reopen request (ring buffer, no allocation per step).
        private readonly float[] _recent = new float[MaxConfirmFrames];
        private int _recentCount;
        private int _recentNext;

        // The current press.
        private float _peak;
        private float _min;
        private float _last;
        private bool _metaSeen;
        private int _metaOffFrames;
        // Consecutive samples MetaReleaseDrop below the peak (DR-6).
        private int _metaDropFrames;
        // Consecutive samples at or below the release level / the reset.
        private int _lowFrames;
        private int _resetFrames;

        // Rounds 1-4: the absolute rule on one strength value, plus the fist suppression.
        public PinchState Step(bool tracked, float pinchStrength, float fireThreshold, float resetThreshold,
            bool fistHeld, float fistSuppressTime, float dt)
        {
            var p = new PinchReleaseParams { FireThreshold = fireThreshold, ResetThreshold = resetThreshold };

            // A half-occluded pinching hand drops out for a frame or two; when it
            // comes back still pinched it must not fire (or restart a charge).
            if (!tracked) return Step(new PinchSample(), p);

            if (fistHeld) _suppressTimer = fistSuppressTime;
            else if (_suppressTimer > 0f) _suppressTimer -= dt;

            if (fistHeld || _suppressTimer > 0f)
            {
                RequireReopen();
                return default;
            }

            return Step(new PinchSample { Tracked = true, Strength = pinchStrength }, p);
        }

        public PinchState Step(in PinchSample sample, in PinchReleaseParams p)
        {
            var state = new PinchState();

            if (!sample.Tracked || sample.SystemGesture)
            {
                if (_held) state.Release = Ended(PinchReleaseBy.Lost, _last);
                RequireReopen();
                return state;
            }

            float v = sample.Strength;
            int confirm = ConfirmFrames(p);
            float settled = Remember(v, confirm);

            if (_held)
            {
                PinchReleaseBy by = ReleaseRule(sample, p, confirm);
                if (by == PinchReleaseBy.None)
                {
                    state.Held = true;
                    return state;
                }

                state.Release = Ended(by, v);
                _held = false;
                _trough = settled;
                _mustReopen = settled > p.ResetThreshold;
                return state;
            }

            if (settled < _trough) _trough = settled;
            if (_trough <= p.ResetThreshold) _mustReopen = false;
            bool armed = !_mustReopen || (p.RelativeRelease > 0f && v >= RearmLevel(p));
            if (!armed || v < p.FireThreshold) return state;

            _held = true;
            _mustReopen = false;
            _peak = v;
            _min = v;
            _last = v;
            _metaSeen = sample.HasMeta && sample.MetaPinching;
            _metaOffFrames = 0;
            _metaDropFrames = 0;
            _lowFrames = 0;
            _resetFrames = 0;
            state.FireTriggered = true;
            state.Held = true;
            return state;
        }

        // Drops any pinch in progress; a pinch still closed must open before it
        // fires or holds (tracking loss, input source switched back on after pause).
        public void RequireReopen()
        {
            _held = false;
            _mustReopen = true;
            _trough = float.PositiveInfinity;
            _recentCount = 0;
        }

        private static int ConfirmFrames(in PinchReleaseParams p)
        {
            if (p.ConfirmFrames <= 1) return 1;
            return p.ConfirmFrames < MaxConfirmFrames ? p.ConfirmFrames : MaxConfirmFrames;
        }

        // Stores the sample; returns the highest of the last `confirm` samples (the
        // strength has been at or below it that many steps in a row), or +inf until
        // that many have come in since the last reopen request.
        private float Remember(float v, int confirm)
        {
            _recent[_recentNext] = v;
            _recentNext = (_recentNext + 1) % MaxConfirmFrames;
            if (_recentCount < MaxConfirmFrames) _recentCount++;
            if (_recentCount < confirm) return float.PositiveInfinity;

            float high = v;
            for (int i = 2; i <= confirm; i++)
            {
                float older = _recent[(_recentNext - i + MaxConfirmFrames) % MaxConfirmFrames];
                if (older > high) high = older;
            }
            return high;
        }

        // While held, the strength at or below which the relative rule releases.
        private float ReleaseLevel(in PinchReleaseParams p)
        {
            float level = _peak - p.RelativeRelease;
            if (p.ReleaseFloorMargin > 0f)
            {
                float floor = p.FireThreshold - p.ReleaseFloorMargin;
                if (floor > level) level = floor;
            }
            return level;
        }

        // After a release above the reset, the strength a new press must reach.
        private float RearmLevel(in PinchReleaseParams p)
        {
            float level = _trough + p.RelativeRelease;
            if (p.ReleaseFloorMargin > 0f && _trough <= p.FireThreshold - p.ReleaseFloorMargin)
            {
                float cap = p.FireThreshold + (p.RearmMargin > 0f ? p.RearmMargin : 0f);
                if (cap < level) level = cap;
            }
            return level;
        }

        // While held: which rule (if any) releases on this sample. Updates the press stats otherwise.
        private PinchReleaseBy ReleaseRule(in PinchSample sample, in PinchReleaseParams p, int confirm)
        {
            float v = sample.Strength;
            bool metaPinching = sample.HasMeta && sample.MetaPinching;

            // The flag only counts as a release after it was on in this press: a slow
            // pinch passes FireThreshold before Meta's flag (full strength) turns on.
            // A frame without a valid aim state neither counts nor resets.
            if (sample.HasMeta)
            {
                if (sample.MetaPinching)
                {
                    _metaSeen = true;
                    _metaOffFrames = 0;
                }
                else if (_metaSeen)
                {
                    _metaOffFrames++;
                }
            }

            _resetFrames = v <= p.ResetThreshold ? _resetFrames + 1 : 0;
            _lowFrames = p.RelativeRelease > 0f && !metaPinching && v <= ReleaseLevel(p) ? _lowFrames + 1 : 0;
            // Confirmed like the strength rules (DR-6): with the flag off and the
            // fingers still closed, one outlier sample used to end the hold.
            _metaDropFrames = v <= _peak - p.MetaReleaseDrop ? _metaDropFrames + 1 : 0;

            if (p.MetaReleaseFrames > 0 && _metaSeen && _metaOffFrames >= p.MetaReleaseFrames
                && (p.MetaReleaseDrop <= 0f || _metaDropFrames >= confirm))
                return PinchReleaseBy.Meta;
            if (_resetFrames >= confirm) return PinchReleaseBy.Absolute;
            if (_lowFrames >= confirm) return PinchReleaseBy.Relative;

            // Still held (the first of the confirming samples counts as held too).
            if (v > _peak) _peak = v;
            if (v < _min) _min = v;
            _last = v;
            return PinchReleaseBy.None;
        }

        private PinchRelease Ended(PinchReleaseBy by, float strength)
        {
            return new PinchRelease
            {
                By = by,
                PeakStrength = _peak,
                MinStrength = _min,
                ReleaseStrength = strength,
                MetaSeen = _metaSeen,
            };
        }
    }
}
