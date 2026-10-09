using UnityEngine;

namespace HandHero.Core
{
    public struct FlightParams
    {
        public float Stiffness;
        public float Damping;
        public float MaxSpeed;
        public float GlideDrag;
    }

    public struct FlightState
    {
        public Vector3 Position;
        public Vector3 Velocity;
    }

    // Axis-aligned arena box. A default (Enabled = false) value does not clamp.
    public struct ArenaBounds
    {
        public bool Enabled;
        public Vector3 Center;
        public Vector3 Size;

        public ArenaBounds(Vector3 center, Vector3 size)
        {
            Enabled = true;
            Center = center;
            Size = size;
        }

        public Vector3 Clamp(Vector3 p)
        {
            if (!Enabled) return p;
            Vector3 half = Size * 0.5f;
            Vector3 local = p - Center;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return Center + local;
        }

        // Where an aim ray that hit nothing should point: where it leaves the arena
        // box, or the nearest arena point if it misses the box. Keeps the reticle
        // (and the beam's aim) inside the arena instead of far behind it, which in
        // the small tabletop view looked like the aim jumping off the table.
        public Vector3 AimFallback(Ray ray, float farDistance = 80f)
        {
            Vector3 far = ray.GetPoint(farDistance);
            if (!Enabled) return far;

            Vector3 min = Center - Size * 0.5f;
            Vector3 max = Center + Size * 0.5f;
            float tNear = float.NegativeInfinity;
            float tFar = float.PositiveInfinity;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = ray.origin[axis];
                float d = ray.direction[axis];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < min[axis] || o > max[axis]) return Clamp(far);
                    continue;
                }
                float t1 = (min[axis] - o) / d;
                float t2 = (max[axis] - o) / d;
                tNear = Mathf.Max(tNear, Mathf.Min(t1, t2));
                tFar = Mathf.Min(tFar, Mathf.Max(t1, t2));
            }

            return tFar >= Mathf.Max(tNear, 0f) ? ray.GetPoint(tFar) : Clamp(far);
        }
    }

    // Speed-capped spring toward a target point, gliding with drag when there is
    // no target. No gravity. This is the hero flight model and, later, the
    // server-side simulation (ADR 10): input = "where the hand wants me",
    // the model decides how the character actually moves.
    //
    // Frame-rate independent (BC-3): damping is exponential, and a frame longer
    // than 1/60 s is split into ~1/90 s sub-steps (at most MaxSubSteps), so a
    // hitch never flips the velocity and throws the hero away from its target.
    public static class SpringFlightModel
    {
        public const int MaxSubSteps = 4;
        private const float SingleStepMaxDt = 1f / 60f;
        private const float SubStepDt = 1f / 90f;

        public static int SubStepCount(float dt)
        {
            if (dt <= SingleStepMaxDt) return 1;
            return Mathf.Clamp(Mathf.CeilToInt(dt / SubStepDt - 1e-4f), 1, MaxSubSteps);
        }

        public static FlightState Step(FlightState state, bool hasTarget, Vector3 target,
            FlightParams p, ArenaBounds bounds, float dt)
        {
            int steps = SubStepCount(dt);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
                state = SingleStep(state, hasTarget, target, p, bounds, h);
            return state;
        }

        private static FlightState SingleStep(FlightState state, bool hasTarget, Vector3 target,
            FlightParams p, ArenaBounds bounds, float dt)
        {
            Vector3 velocity = state.Velocity;

            if (hasTarget)
            {
                Vector3 toTarget = target - state.Position;
                velocity += toTarget * (p.Stiffness * dt);
                velocity *= Mathf.Exp(-p.Damping * dt);
            }
            else
            {
                velocity *= Mathf.Exp(-p.GlideDrag * dt);
            }

            velocity = Vector3.ClampMagnitude(velocity, p.MaxSpeed);

            return new FlightState
            {
                Position = bounds.Clamp(state.Position + velocity * dt),
                Velocity = velocity,
            };
        }
    }
}
