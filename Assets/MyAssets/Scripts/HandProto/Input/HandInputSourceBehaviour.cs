using HandHero.Core;
using UnityEngine;

// Scene-assignable base for input sources (Unity can't serialize interfaces).
// Each concrete source samples once per frame in Update and runs before the
// controllers (DefaultExecutionOrder on each subclass), so every consumer sees
// the same Current for the frame.
public abstract class HandInputSourceBehaviour : MonoBehaviour, IHandInputSource
{
    public const int ExecutionOrder = -50;

    public HandInputData Current { get; private set; }

    protected abstract HandInputData Sample();

    protected virtual void Update()
    {
        Current = Sample();
    }

    protected virtual void OnDisable()
    {
        Current = default;
    }
}
