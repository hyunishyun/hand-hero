using UnityEngine;

// A short effect served by a BeamImpactPool (beam hits, P5; kill bursts, P12).
// It plays from OnEnable and calls Finish when done: pooled instances go back to
// their pool, a stray instance just deactivates (its spawner destroys it).
public abstract class PooledEffect : MonoBehaviour
{
    private BeamImpactPool _owner;

    internal void SetOwner(BeamImpactPool owner) => _owner = owner;

    protected void Finish()
    {
        if (_owner != null) _owner.Return(this);
        else gameObject.SetActive(false);
    }
}
