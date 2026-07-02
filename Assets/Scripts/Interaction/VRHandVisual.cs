using UnityEngine;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Stylized controller-tracked hand, built from primitives at runtime
    /// and parented to a controller anchor so it follows 1:1. Until now the
    /// participant had NO body representation at all - selection and the
    /// teleport arc fired from thin air, which reads as broken in the
    /// headset. A visible hand anchors both.
    ///
    /// Built procedurally (like the rest of the scene) rather than from
    /// Meta's controller-model prefabs: the rig is assembled in code
    /// specifically to avoid version-fragile prefab GUID references, and
    /// OVRControllerHelper renders nothing without its prefab wiring.
    ///
    /// Feedback: the hand closes slightly and warms in color as the index
    /// trigger is squeezed (analog), and the whole visual hides if the
    /// controller disconnects. Colliderless and AttentionTarget-free, so it
    /// can neither intercept gaze rays nor appear as an AOI in the data.
    /// </summary>
    public class VRHandVisual : MonoBehaviour
    {
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

        private static readonly Color RestColor = new Color(0.80f, 0.83f, 0.90f);
        private static readonly Color GripColor = new Color(1.00f, 0.82f, 0.55f);

        private GameObject _root;
        private Transform _fingers;
        private Material _material;

        private void Awake()
        {
            _root = new GameObject("HandModel");
            _root.transform.SetParent(transform, false);

            // Palm: flattened capsule lying along the controller's forward.
            AddPart(PrimitiveType.Capsule, "Palm",
                new Vector3(0f, -0.01f, -0.03f), new Vector3(90f, 0f, 0f),
                new Vector3(0.055f, 0.045f, 0.045f));

            // Finger block: a rounded bar ahead of the palm that rotates
            // down as the trigger squeezes, reading as a closing fist.
            var fingersGo = new GameObject("Fingers");
            fingersGo.transform.SetParent(_root.transform, false);
            fingersGo.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            _fingers = fingersGo.transform;
            AddPart(PrimitiveType.Capsule, "FingerBlock",
                new Vector3(0f, 0f, 0.025f), new Vector3(90f, 0f, 0f),
                new Vector3(0.05f, 0.028f, 0.035f), _fingers);

            // Thumb nub on the inner side (mirrored for the left hand).
            float thumbSide = controller == OVRInput.Controller.LTouch ? 1f : -1f;
            AddPart(PrimitiveType.Capsule, "Thumb",
                new Vector3(thumbSide * 0.035f, 0.005f, 0.005f),
                new Vector3(90f, thumbSide * -30f, 0f),
                new Vector3(0.02f, 0.022f, 0.02f));

            // Small aim tip so the selection ray / teleport arc visibly
            // leaves the hand instead of materializing mid-air.
            AddPart(PrimitiveType.Sphere, "AimTip",
                new Vector3(0f, 0f, 0.075f), Vector3.zero,
                Vector3.one * 0.016f);
        }

        private void AddPart(PrimitiveType type, string name,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale,
            Transform parent = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent != null ? parent : _root.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<MeshRenderer>();
            // One lit material instance shared by the whole hand, cloned
            // from the primitive's pipeline default so it works under both
            // Built-in and URP (Material.color maps to the main color in
            // either) and picks up the scene lighting.
            if (_material == null)
            {
                _material = new Material(renderer.sharedMaterial) { color = RestColor };
            }
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Update()
        {
            bool connected = OVRInput.IsControllerConnected(controller);
            if (_root.activeSelf != connected)
            {
                _root.SetActive(connected);
            }
            if (!connected)
            {
                return;
            }

            float squeeze = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            _material.color = Color.Lerp(RestColor, GripColor, squeeze);
            _fingers.localRotation = Quaternion.Euler(squeeze * 40f, 0f, 0f);
        }
    }
}
