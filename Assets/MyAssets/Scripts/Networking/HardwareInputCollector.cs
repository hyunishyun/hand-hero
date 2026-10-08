using UnityEngine;
using UnityEngine.InputSystem;

// Lives in the scene (NOT networked). Reads raw VR hardware every frame
// and packs it into a BikeInputData when the NetworkRunner asks for input.
// This replaces the direct InputAction reads that used to live inside
// MotorcycleController and GunController.
public class HardwareInputCollector : MonoBehaviour
{
    public static HardwareInputCollector Instance { get; private set; }

    [Header("Drive Hand Actions")]
    [SerializeField] private InputActionProperty accelerateAction; // trigger
    [SerializeField] private InputActionProperty brakeAction;      // grip
    [SerializeField] private InputActionProperty steerAction;      // joystick (Vector2)
    [SerializeField] private InputActionProperty jumpAction;       // button

    [Header("Gun Hand Actions")]
    [SerializeField] private InputActionProperty fireAction;       // trigger

    [Header("XR References")]
    [SerializeField] private Transform xrOrigin;   // rig root that follows the local bike
    [SerializeField] private Transform head;       // main camera
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform gunBarrel;  // muzzle transform on the gun hand

    private void Awake()
    {
        Instance = this;
        EnableAction(accelerateAction);
        EnableAction(brakeAction);
        EnableAction(steerAction);
        EnableAction(jumpAction);
        EnableAction(fireAction);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static void EnableAction(InputActionProperty prop)
    {
        if (prop.action != null && !prop.action.enabled)
            prop.action.Enable();
    }

    // Called from NetworkGameLauncher.OnInput once per tick.
    public BikeInputData Collect()
    {
        var data = new BikeInputData
        {
            Throttle = accelerateAction.action?.ReadValue<float>() ?? 0f,
            Brake = brakeAction.action?.ReadValue<float>() ?? 0f,
            Steer = steerAction.action?.ReadValue<Vector2>().x ?? 0f,
        };

        data.Buttons.Set((int)BikeButtons.Jump,
            (jumpAction.action?.ReadValue<float>() ?? 0f) > 0.5f);
        data.Buttons.Set((int)BikeButtons.Fire,
            (fireAction.action?.ReadValue<float>() ?? 0f) > 0.5f);

        if (gunBarrel != null)
        {
            data.GunPosition = gunBarrel.position;
            data.GunRotation = gunBarrel.rotation;
        }

        // Rig poses relative to the XR Origin so the remote avatar can be
        // glued to the bike with the same offset and stay in sync.
        if (xrOrigin != null)
        {
            if (head != null)
            {
                data.HeadPosition = xrOrigin.InverseTransformPoint(head.position);
                data.HeadRotation = Quaternion.Inverse(xrOrigin.rotation) * head.rotation;
            }
            if (leftHand != null)
            {
                data.LeftHandPosition = xrOrigin.InverseTransformPoint(leftHand.position);
                data.LeftHandRotation = Quaternion.Inverse(xrOrigin.rotation) * leftHand.rotation;
            }
            if (rightHand != null)
            {
                data.RightHandPosition = xrOrigin.InverseTransformPoint(rightHand.position);
                data.RightHandRotation = Quaternion.Inverse(xrOrigin.rotation) * rightHand.rotation;
            }
        }

        return data;
    }
}
