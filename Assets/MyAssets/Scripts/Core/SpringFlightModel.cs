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
    }

    // Speed-capped spring toward a target point, gliding with drag when there is
    // no target. No gravity. This is the hero flight model and, later, the
    // server-side simulation (ADR 10): input = "where the hand wants me",
    // the model decides how the character actually moves.
    public static class SpringFlightModel
    {
        public static FlightState Step(FlightState state, bool hasTarget, Vector3 target,
            FlightParams p, ArenaBounds bounds, float dt)
        {
            Vector3 velocity = state.Velocity;

            if (hasTarget)
            {
                Vector3 toTarget = target - state.Position;
                velocity += toTarget * (p.Stiffness * dt);
                velocity -= velocity * (p.Damping * dt);
            }
            else
            {
                velocity -= velocity * (p.GlideDrag * dt);
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
