using System.Collections.Generic;
using UnityEngine;

namespace HandHero.Core
{
    // Replays a recorded sequence of HandInputData frames — for tests and
    // headset-free checks ("fist -> drag -> release -> aim -> pinch").
    // Build with the fluent helpers, then call Advance() once per simulated frame.
    public class ScriptedHandInputSource : IHandInputSource
    {
        private readonly List<HandInputData> _frames = new List<HandInputData>();
        private int _index = -1;

        // Aim carried into every frame added after Aim() (a pointing hand stays pointed).
        private bool _hasAim;
        private Vector3 _aimOrigin;
        private Vector3 _aimDirection;

        public HandInputData Current =>
            _index >= 0 && _index < _frames.Count ? _frames[_index] : default;

        public int FrameCount => _frames.Count;

        // Moves to the next frame. Returns false once the script is exhausted
        // (Current is then the default: no clutch, no aim, no fire).
        public bool Advance()
        {
            if (_index < _frames.Count) _index++;
            return _index < _frames.Count;
        }

        public ScriptedHandInputSource Add(HandInputData frame, int count = 1)
        {
            for (int i = 0; i < count; i++) _frames.Add(frame);
            return this;
        }

        public ScriptedHandInputSource Idle(int frames)
        {
            return Add(WithAim(new HandInputData()), frames);
        }

        // Closes the fist for one frame without moving.
        public ScriptedHandInputSource Grab()
        {
            return Add(WithAim(new HandInputData { ClutchHeld = true }));
        }

        // Moves the clutched hand by handDelta (meters) spread evenly over frames.
        public ScriptedHandInputSource Drag(Vector3 handDelta, int frames)
        {
            Vector3 step = handDelta / Mathf.Max(1, frames);
            return Add(WithAim(new HandInputData { ClutchHeld = true, ClutchDelta = step }), frames);
        }

        public ScriptedHandInputSource Release(int frames = 1)
        {
            return Idle(frames);
        }

        public ScriptedHandInputSource Aim(Vector3 origin, Vector3 direction, int frames = 1)
        {
            _hasAim = true;
            _aimOrigin = origin;
            _aimDirection = direction.normalized;
            return Idle(frames);
        }

        public ScriptedHandInputSource LoseAim(int frames = 1)
        {
            _hasAim = false;
            return Idle(frames);
        }

        // One frame with FireTriggered (a completed pinch) at the current aim.
        public ScriptedHandInputSource Pinch()
        {
            var frame = WithAim(new HandInputData());
            frame.FireTriggered = true;
            return Add(frame);
        }

        public ScriptedHandInputSource Gesture(HandGestures gestures, int frames = 1)
        {
            var frame = WithAim(new HandInputData());
            frame.Gestures = gestures;
            return Add(frame, frames);
        }

        private HandInputData WithAim(HandInputData frame)
        {
            frame.HasAim = _hasAim;
            frame.AimOrigin = _aimOrigin;
            frame.AimDirection = _aimDirection;
            return frame;
        }
    }
}
