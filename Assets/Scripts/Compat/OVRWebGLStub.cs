// Compatibility shim for the Meta XR Core SDK (OVRInput / OVREyeGaze /
// OVRCameraRig / OVRManager), for builds where the real SDK is absent.
//
// TWO SITUATIONS IT COVERS
//
// 1. WebGL player builds (always). Meta's SDK assembly targets Editor +
//    Standalone + Android and is NOT part of a WebGL player, so a browser
//    build would fail to compile with "OVRInput could not be found". A
//    browser never runs the VR rig anyway (PlatformRigSwitcher enables the
//    desktop mouse/keyboard rig on non-XR), so the OVR calls just need to
//    COMPILE and return harmless no-ops.
//
// 2. "Meta-free" builds via the NSFGRANT_NO_META scripting define (opt-in
//    escape hatch). If the Meta SDK can't be installed at all — e.g. its
//    npm registry (npm.developer.oculus.com) is unreachable from your
//    network, which aborts Unity's whole package resolve and cascades into
//    VERA's InputActionProperty compile errors — you can drop the Meta
//    package entirely and still compile the project (Editor + WebGL) for a
//    desktop/browser demo. See docs/WEBGL_BUILD.md → "Meta won't install".
//
// GUARD: #if (UNITY_WEBGL && !UNITY_EDITOR) || NSFGRANT_NO_META
//   - Default (no define): the shim is OFF in the Editor and in
//     Standalone/Android builds, which use the REAL Meta SDK — the Quest
//     path is completely untouched. It's ON only inside the actual WebGL
//     player binary, where the real SDK is absent.
//   - With NSFGRANT_NO_META defined AND the Meta package removed: the shim
//     is ON everywhere (Editor + all players), so the project compiles with
//     no Meta SDK present at all. (If you set the define but leave the Meta
//     package installed, you'll get duplicate-type errors — remove the
//     package, that's the point of the switch.)
#if (UNITY_WEBGL && !UNITY_EDITOR) || NSFGRANT_NO_META
using UnityEngine;

/// <summary>No-op stand-in for Meta's OVRInput. Reports no controllers and
/// no input, so the (disabled) VR components stay inert.</summary>
public static class OVRInput
{
    public enum Controller { None = 0, LTouch, RTouch }
    public enum Button { None = 0, PrimaryIndexTrigger }
    public enum Axis1D { None = 0, PrimaryIndexTrigger }
    public enum Axis2D { None = 0, PrimaryThumbstick }

    public static bool GetDown(Button button, Controller controller) => false;
    public static bool IsControllerConnected(Controller controller) => false;
    public static float Get(Axis1D axis, Controller controller) => 0f;
    public static Vector2 Get(Axis2D axis, Controller controller) => Vector2.zero;
    public static void SetControllerVibration(float frequency, float amplitude, Controller controller) { }
}

/// <summary>Stand-in for Meta's OVREyeGaze. Eye tracking never exists on
/// these targets, so it reports disabled/zero confidence and the gaze
/// pipeline falls back to head gaze.</summary>
public class OVREyeGaze : MonoBehaviour
{
    public enum EyeId { Left, Right }
    public EyeId Eye { get; set; }
    public bool EyeTrackingEnabled => false;
    public float Confidence => 0f;
}

/// <summary>Stand-in for Meta's OVRCameraRig so serialized/self-wiring
/// lookups compile. The desktop rig is the active one here, so these
/// anchors are simply never used (builder code null-checks them).</summary>
public class OVRCameraRig : MonoBehaviour
{
    public Transform centerEyeAnchor;
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;
}

/// <summary>Stand-in for Meta's OVRManager (referenced only by the editor
/// scene builders). Holds the tracking-origin value with no effect.</summary>
public class OVRManager : MonoBehaviour
{
    public enum TrackingOrigin { EyeLevel = 0, FloorLevel, Stage }
    public TrackingOrigin trackingOriginType = TrackingOrigin.FloorLevel;
}
#endif
