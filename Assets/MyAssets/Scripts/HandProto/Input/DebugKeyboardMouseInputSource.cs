using HandHero.Core;
using UnityEngine;
using UnityEngine.InputSystem;

// Editor/desktop stand-in for the hands, so the game is playable without a headset.
//   Right mouse held   = clutch (fist); mouse motion drags the hero in the view plane,
//                        scroll wheel pushes it away / pulls it closer
//   W A S D / Q E      = clutch + drag with the keyboard (forward/left/back/right, down/up)
//   Mouse position     = aim ray through the cursor
//   Left click / Space = pinch (fire)
//   C held             = both palms together (charge shot; fires on release)
//   F                  = palm push (shockwave)
// It emits the same HandInputData as the hand source, in virtual "hand meters",
// so HandPuppeteerController.positionScale applies unchanged.
[DefaultExecutionOrder(HandInputSourceBehaviour.ExecutionOrder)]
public class DebugKeyboardMouseInputSource : HandInputSourceBehaviour
{
    [Header("References")]
    [Tooltip("Camera for aim rays and drag axes. Falls back to Camera.main")]
    [SerializeField] private Camera viewCamera;

    [Header("Mouse")]
    [Tooltip("Virtual hand meters per mouse pixel while right-dragging (x positionScale = world meters)")]
    [SerializeField] private float handMetersPerPixel = 0.0005f;
    [Tooltip("Virtual hand meters per scroll notch, along the view direction")]
    [SerializeField] private float handMetersPerScrollNotch = 0.02f;

    [Header("Keyboard")]
    [Tooltip("Virtual hand speed in meters/second while a WASDQE key is held")]
    [SerializeField] private float keyboardHandSpeed = 0.15f;

    protected override HandInputData Sample()
    {
        var data = new HandInputData();
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return data;

        Transform view = cam.transform;
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        Vector3 delta = Vector3.zero;
        bool held = false;

        if (mouse != null && mouse.rightButton.isPressed)
        {
            held = true;
            Vector2 md = mouse.delta.ReadValue();
            delta += (view.right * md.x + view.up * md.y) * handMetersPerPixel;

            // Scroll units differ by platform/version; one notch per frame is enough here.
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f) delta += view.forward * (Mathf.Sign(scroll) * handMetersPerScrollNotch);
        }

        if (keyboard != null)
        {
            Vector3 k = new Vector3(
                Axis(keyboard.dKey, keyboard.aKey),
                Axis(keyboard.eKey, keyboard.qKey),
                Axis(keyboard.wKey, keyboard.sKey));
            if (k != Vector3.zero)
            {
                held = true;
                Vector3 dir = view.right * k.x + view.up * k.y + view.forward * k.z;
                delta += dir.normalized * (keyboardHandSpeed * Time.deltaTime);
            }
        }

        data.ClutchHeld = held;
        data.ClutchDelta = held ? delta : Vector3.zero;

        if (mouse != null)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            data.HasAim = true;
            data.AimOrigin = ray.origin;
            data.AimDirection = ray.direction;
            data.FireTriggered = mouse.leftButton.wasPressedThisFrame;
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            data.FireTriggered = true;

        // Same rules as the hand source: charging blocks normal fire and shockwaves.
        if (keyboard != null && keyboard.cKey.isPressed)
        {
            data.Gestures |= HandGestures.ChargeHeld;
            data.FireTriggered = false;
        }
        else if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
        {
            data.Gestures |= HandGestures.Shockwave;
        }

        return data;
    }

    private static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive,
        UnityEngine.InputSystem.Controls.KeyControl negative)
    {
        return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}
