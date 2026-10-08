using UnityEngine;

// Beam hit effect for the greybox: pops up and shrinks away at the impact
// point. Used as PointingBeamController.hitEffectPrefab (the beam destroys the
// instance after a second). Runs on game time, so it freezes while paused.
public class ImpactFlash : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private float maxSize = 1.2f;

    private float _age;

    private void OnEnable()
    {
        _age = 0f;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= lifetime)
        {
            gameObject.SetActive(false);
            return;
        }

        // Fast rise, slower fall: sin over the lifetime, skewed toward the start.
        float t = Mathf.Sqrt(_age / lifetime);
        transform.localScale = Vector3.one * (maxSize * Mathf.Sin(t * Mathf.PI));
    }
}
