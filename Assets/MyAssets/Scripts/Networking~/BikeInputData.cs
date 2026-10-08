using Fusion;
using UnityEngine;

// Button indices used inside the NetworkButtons bitfield.
public enum BikeButtons
{
    Jump = 0,
    Fire = 1,
}

// Everything the server needs to simulate one tick of this player.
// Collected on the client every tick (see HardwareInputCollector),
// consumed in FixedUpdateNetwork on both the predicting client and the server.
public struct BikeInputData : INetworkInput
{
    // Drive hand
    public float Throttle;            // trigger, 0..1
    public float Brake;               // grip, 0..1
    public float Steer;               // joystick X, -1..1

    public NetworkButtons Buttons;    // Jump, Fire

    // Gun pose in WORLD space at the moment of input.
    // Used for the server-side lag-compensated raycast so hits are judged
    // against where the shooter actually saw the target.
    public Vector3 GunPosition;
    public Quaternion GunRotation;

    // VR rig poses LOCAL to the XR Origin (which follows the bike).
    // Used to drive the networked avatar that remote players see.
    public Vector3 HeadPosition;
    public Quaternion HeadRotation;
    public Vector3 LeftHandPosition;
    public Quaternion LeftHandRotation;
    public Vector3 RightHandPosition;
    public Quaternion RightHandRotation;
}
