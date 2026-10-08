using UnityEngine;

namespace HandHero.Core
{
    // CURSOR aim: a 3D aim marker the aim-hand fist drags with the same relative
    // (mouse-style) mapping as the puppeteer (ADR 6). Grabbing starts from where
    // the marker is (no snap); released, it stays put. Clamped to the arena.
    public class AimCursorModel
    {
        private readonly ClutchMapper _mapper = new ClutchMapper();

        public Vector3 Position { get; private set; }
        public bool IsDragging => _mapper.IsClutched;

        // Moves the marker (round start / respawn). A fist still closed regrabs from here.
        public void Reset(Vector3 position)
        {
            Position = position;
            _mapper.Reset();
        }

        public Vector3 Step(bool held, Vector3 delta, float positionScale, ArenaBounds bounds)
        {
            ClutchResult r = _mapper.Step(held, delta, Position, positionScale);
            if (r.Clutched) Position = bounds.Clamp(r.Target);
            return Position;
        }
    }
}
