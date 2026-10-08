using System;
using UnityEngine;

public struct BeamHit
{
    public Vector3 Point;
    public Vector3 Direction;
    public float Damage;
    public FlyingCharacter Shooter;
}

// Put on a hero (or anything) that reacts to beams. PointingBeamController
// calls Receive on the first collider it hits; health (T4), bot evasion and
// effects subscribe to Hit, so the beam doesn't need to know who listens.
public class BeamHitReceiver : MonoBehaviour
{
    public event Action<BeamHit> Hit;

    public void Receive(BeamHit hit)
    {
        Hit?.Invoke(hit);
    }
}
