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
        Relative, // strength fell RelativeRelease below this press's peak (only the new rule releases)
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
        // Meta's flag off for this many steps (after it was on in this press)
        // releases. 0 = the Meta flag is ignored.
        public int MetaReleaseFrames;

        public static PinchReleaseParams Default => new PinchReleaseParams
        {
            FireThreshold = 0.8f,
            ResetThreshold = 0.6f,
            RelativeRelease = 0.2f,
            MetaReleaseFrames = 2,
        };
    }

    // Aim-hand pinch: fire edge + hold level with hysteresis (ADR 8).
    //
    // Press: strength rises to FireThreshold. Release (round 5, D1), whichever comes
    // first: Meta's index-pinch flag off for MetaReleaseFrames steps after it was on
    // in this press; strength back down to ResetThreshold; strength RelativeRelease
    // below the peak of this press. The last one is the ASSIST fix: a pointing hand
    // rests the thumb about 2.8 cm from the index (strength 0.71), above the reset,
    // so a pinch used to stay "held" for seconds and read as a charge.
    // A release above the reset re-arms relatively too: the next press must rise
    // RelativeRelease above the lowest point since, so jitter at the release point
    // never fires twice. With RelativeRelease and MetaReleaseFrames at 0 this is the
    // rounds 1-4 absolute rule.
    //
    // Tracking loss, the Meta system gesture and RequireReopen drop the pinch; one
    // still closed must open (to the reset, or RelativeRelease below its highest
    // point since) before it fires or holds.
    //
    // The rounds 1-4 overload also ignores the pinch while the aim-hand fist is held
    // and for a short time after: a closed fist brings thumb and index close enough
    // to read as a pinch.
    public class PinchTrigger
    {
        private float _suppressTimer;
        private bool _held;
        private bool _mustReopen;
        // Lowest strength since the last release or reopen request (+inf right after
        // a reopen request, so the first sample sets it).
        private float _trough = float.PositiveInfinity;

        // The current press.
        private float _peak;
        private float _min;
        private float _last;
        private bool _metaSeen;
        private int _metaOffFrames;

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
            if (_held)
            {
                PinchReleaseBy by = ReleaseRule(sample, p);
                if (by == PinchReleaseBy.None)
                {
                    state.Held = true;
                    return state;
                }

                state.Release = Ended(by, v);
                _held = false;
                _mustReopen = v > p.ResetThreshold;
                _trough = v;
                return state;
            }

            if (v < _trough) _trough = v;
            if (v <= p.ResetThreshold) _mustReopen = false;
            bool armed = !_mustReopen || (p.RelativeRelease > 0f && v >= _trough + p.RelativeRelease);
            if (!armed || v < p.FireThreshold) return state;

            _held = true;
            _mustReopen = false;
            _peak = v;
            _min = v;
            _last = v;
            _metaSeen = sample.HasMeta && sample.MetaPinching;
            _metaOffFrames = 0;
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
        }

        // While held: which rule (if any) releases on this sample. Updates the press stats otherwise.
        private PinchReleaseBy ReleaseRule(in PinchSample sample, in PinchReleaseParams p)
        {
            float v = sample.Strength;

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

            if (p.MetaReleaseFrames > 0 && _metaSeen && _metaOffFrames >= p.MetaReleaseFrames)
                return PinchReleaseBy.Meta;
            if (v <= p.ResetThreshold) return PinchReleaseBy.Absolute;
            if (p.RelativeRelease > 0f && v <= _peak - p.RelativeRelease) return PinchReleaseBy.Relative;

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
