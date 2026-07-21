// WebGL compatibility shim for the Meta XR Core SDK.
//
// WHY THIS EXISTS
// The Meta XR Core SDK (OVRInput / OVREyeGaze / OVRCameraRig) ships an
// assembly that targets Editor + Standalone + Android and is NOT included
// in WebGL player builds. Our gaze + interaction scripts reference those
// OVR types directly and with no platform guards, so a WebGL player build
// would fail to compile with "OVRInput could not be found". A browser
// build never runs the VR rig anyway (PlatformRigSwitcher enables the
// desktop mouse/keyboard rig on non-XR), so the OVR calls only need to
// *compile* on WebGL and return harmless no-op values.
//
// This file provides exactly the OVR API surface our runtime code uses,
// in the global namespace where the real SDK also lives.
//
// GUARD: #if UNITY_WEBGL && !UNITY_EDITOR
//   - In the Editor (any active build target) the REAL Meta SDK is loaded,
//     so this shim is compiled OUT to avoid a duplicate-type collision.
//   - In Standalone / Android player builds the shim is off (not WEBGL);
//     the real SDK compiles as before. The Quest path is untouched.
//   - Only the actual WebGL *player* binary (WEBGL defined, EDITOR not)
//     compiles against these stubs, and only there is the real SDK absent.
//
// IF A WEBGL BUILD REPORTS "duplicate definition of OVRInput": that means
// this project's Meta SDK version DOES compile its assembly for WebGL, so
// the shim is unnecessary — delete this file and rebuild. If instead the
// build reports "OVRInput not found", this shim is what resolves it.
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;

/// <summary>WebGL no-op stand-in for Meta's OVRInput. Reports no controllers
/// and no input, so the (disabled) VR components stay inert on the web.</summary>
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

/// <summary>WebGL stand-in for Meta's OVREyeGaze component. Eye tracking never
/// exists in a browser, so it always reports disabled/zero confidence and the
/// gaze pipeline falls back to head gaze.</summary>
public class OVREyeGaze : MonoBehaviour
{
    public bool EyeTrackingEnabled => false;
    public float Confidence => 0f;
}

/// <summary>WebGL stand-in for Meta's OVRCameraRig so serialized/self-wiring
/// lookups compile. The desktop rig is the active one on WebGL, so these
/// anchors are simply never used.</summary>
public class OVRCameraRig : MonoBehaviour
{
    public Transform centerEyeAnchor;
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;
}
#endif
