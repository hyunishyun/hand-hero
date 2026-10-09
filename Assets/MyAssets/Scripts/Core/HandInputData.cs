using System;
using UnityEngine;

namespace HandHero.Core
{
    // Discrete gestures beyond clutch/fire. Recognition happens in the input
    // source; the simulation only sees these flags.
    [Flags]
    public enum HandGestures : byte
    {
        None = 0,
        ChargeHeld = 1 << 0, // both hands together (T5): charge shot while held
        Shockwave = 1 << 1,  // palm push (T5, Q7): one-frame trigger, slows nearby enemy
    }

    // One frame of player intent, independent of where it came from (XR hands,
    // keyboard/mouse, bot, test script, and later the network) — ADR 9.
    // Mirrors what the Fusion INetworkInput struct will carry.
    public struct HandInputData
    {
        // Puppeteer clutch (fist) is closed.
        public bool ClutchHeld;
        // World-space puppeteer-hand motion since the previous frame, in hand
        // meters (unscaled). Zero when the clutch is not held.
        public Vector3 ClutchDelta;

        // Aim ray in world space; HasAim is false when the aiming hand is lost
        // (the reticle then freezes at its last point).
        public bool HasAim;
        public Vector3 AimOrigin;
        public Vector3 AimDirection;

        // True for exactly one frame per pinch (edge, already hysteresis-gated).
        public bool FireTriggered;
        // Aim-hand pinch is closed (level): the charge shot hold. FireTriggered is the edge.
        public bool PinchHeld;
        // CURSOR aim fire: index-finger trigger (edge) and its hold (charge).
        public bool TriggerFired;
        public bool TriggerHeld;

        // Aim-hand fist clutch (CURSOR aim): drags the 3D aim marker. Delta in
        // tracking-space meters since the previous frame, zero when open.
        public bool AimClutchHeld;
        public Vector3 AimClutchDelta;

        public HandGestures Gestures;

        // Hand tracking dropped out (tracking-lost cue, D7). Only hand-tracked sources
        // set these; keyboard, bot and test input leave them false.
        public bool AimHandLost;
        public bool ClutchHandLost;

        public Ray AimRay => new Ray(AimOrigin, AimDirection);

        public bool Has(HandGestures gesture) => (Gestures & gesture) == gesture;
    }

    // A per-frame input producer. Current is stable for the whole frame so
    // several consumers (puppeteer, beam) see the same edges and deltas.
    public interface IHandInputSource
    {
        HandInputData Current { get; }
    }
}
