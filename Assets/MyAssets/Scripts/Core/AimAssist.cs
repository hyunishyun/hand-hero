using System.Collections.Generic;
using UnityEngine;

namespace HandHero.Core
{
    // Target magnetism for aiming. A hand ray is too jittery (1-2 degrees) for a
    // hero that spans ~3 degrees at 20 m, so the aim snaps to a nearby target.
    // Acquire/release use two thresholds so a target at the edge never flickers
    // (ADR 8). `current` is the caller's previous pick already mapped into this
    // frame's list (-1 if none or gone); returns an index into `candidates` or -1.
    public static class AimAssist
    {
        // ASSIST mode: angle between the aim ray and the direction to each target.
        public static int SelectByAngle(Ray ray, IReadOnlyList<Vector3> candidates, int current,
            float acquireDeg, float releaseDeg)
        {
            if (current >= 0 && current < candidates.Count &&
                TryAngle(ray, candidates[current], out float kept) && kept <= releaseDeg)
                return current;

            if (acquireDeg <= 0f) return -1;

            int best = -1;
            float bestAngle = acquireDeg;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!TryAngle(ray, candidates[i], out float angle) || angle > bestAngle) continue;
                best = i;
                bestAngle = angle;
            }
            return best;
        }

        // CURSOR mode: 3D distance from the aim marker to each target.
        public static int SelectByRadius(Vector3 point, IReadOnlyList<Vector3> candidates, int current,
            float acquireRadius, float releaseRadius)
        {
            if (current >= 0 && current < candidates.Count &&
                Vector3.Distance(point, candidates[current]) <= releaseRadius)
                return current;

            if (acquireRadius <= 0f) return -1;

            int best = -1;
            float bestDistance = acquireRadius;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = Vector3.Distance(point, candidates[i]);
                if (d > bestDistance) continue;
                best = i;
                bestDistance = d;
            }
            return best;
        }

        // False for targets behind the ray origin.
        private static bool TryAngle(Ray ray, Vector3 target, out float angle)
        {
            Vector3 to = target - ray.origin;
            angle = 0f;
            if (Vector3.Dot(to, ray.direction) <= 0f) return false;
            angle = Vector3.Angle(ray.direction, to);
            return true;
        }
    }
}
