using System;
using UnityEngine;

namespace HandHero.Core
{
    [Serializable]
    public struct TabletopParams
    {
        [Tooltip("Apparent arena width on the table, meters (the 35 m arena shrinks to this)")]
        public float TableWidth;
        [Tooltip("Where the arena center appears, meters from the eye at start (x right, y up, z forward)")]
        public Vector3 CenterFromEye;

        public static TabletopParams Default => new TabletopParams
        {
            TableWidth = 1f,
            // 35x20x35 m arena -> 1.0 x 0.57 x 1.0 m: floor ~0.5 m below the
            // seated eye (table height), nearest edge 0.2 m in front.
            CenterFromEye = new Vector3(0f, -0.2f, 0.7f),
        };
    }

    // How the seat sees the arena (T10). The XR Origin stays at the world origin
    // with no rotation (ADR 4/5); a layout only sets its uniform scale and the
    // static Camera Offset (eye start point, in origin-local meters). Gameplay
    // keeps running in full-size arena units either way:
    //   eye (world)  = CameraOffsetLocal * WorldScale
    //   apparent     = (world - eye) / WorldScale
    public struct ViewLayout
    {
        public float WorldScale;
        public Vector3 CameraOffsetLocal;

        // Life-size VR arena: the eye sits eyeHeight above the origin.
        public static ViewLayout Arena(float eyeHeight)
        {
            return new ViewLayout { WorldScale = 1f, CameraOffsetLocal = new Vector3(0f, eyeHeight, 0f) };
        }

        // Miniature arena: scale the viewer up until the arena looks TableWidth
        // wide, then place the eye so the arena center appears at CenterFromEye.
        public static ViewLayout Tabletop(Vector3 arenaCenter, float arenaWidth, TabletopParams p)
        {
            float scale = p.TableWidth > 0f && arenaWidth > 0f ? arenaWidth / p.TableWidth : 1f;
            return new ViewLayout { WorldScale = scale, CameraOffsetLocal = arenaCenter / scale - p.CenterFromEye };
        }

        public Vector3 EyeWorld => CameraOffsetLocal * WorldScale;

        public Vector3 ApparentFromEye(Vector3 worldPoint)
        {
            return (worldPoint - EyeWorld) / WorldScale;
        }
    }
}
