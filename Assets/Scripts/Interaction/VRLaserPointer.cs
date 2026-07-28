using UnityEngine;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// A thin laser line projecting forward from a controller, with a dot
    /// where it meets geometry - a pointing/aiming aid that makes it
    /// obvious where each hand is directed. Purely visual: selection stays
    /// on the gaze ray (see VRInteractor), because the study deliberately
    /// keeps the attention signal and the interaction signal in one
    /// coordinate frame - a laser-driven selection would split them and
    /// change the data model, so that is intentionally NOT done here.
    ///
    /// Uses NSFGrant/UnlitTransparentColor (single-pass-instanced-safe) for
    /// both the line and the dot, so the laser renders correctly in each
    /// eye. Ignores trigger colliders so the invisible SdgStation sensor
    /// volumes don't stop the beam short. Hides with its controller.
    /// </summary>
    public class VRLaserPointer : MonoBehaviour
    {
        [SerializeField] private XRInputBridge.Hand hand = XRInputBridge.Hand.Right;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float startOffset = 0.08f;
        [SerializeField] private Color beamColor = new Color(0.45f, 0.85f, 1f, 0.7f);

        private LineRenderer _line;
        private Transform _dot;
        private Renderer _dotRenderer;

        private void Awake()
        {
            var lineGo = new GameObject("LaserBeam");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.startWidth = 0.006f;
            _line.endWidth = 0.006f;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.material = MakeMaterial(beamColor);

            var dotGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dotGo.name = "LaserDot";
            Destroy(dotGo.GetComponent<Collider>());
            dotGo.transform.SetParent(transform, false);
            dotGo.transform.localScale = Vector3.one * 0.03f;
            _dot = dotGo.transform;
            _dotRenderer = dotGo.GetComponent<Renderer>();
            _dotRenderer.sharedMaterial = MakeMaterial(new Color(beamColor.r, beamColor.g, beamColor.b, 0.95f));
            _dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            bool connected = XRInputBridge.IsConnected(hand);
            if (_line.enabled != connected)
            {
                _line.enabled = connected;
                _dot.gameObject.SetActive(connected);
            }
            if (!connected)
            {
                return;
            }

            Vector3 origin = transform.position + transform.forward * startOffset;
            Vector3 end;
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                _dot.position = hit.point;
                _dot.gameObject.SetActive(true);
            }
            else
            {
                end = origin + transform.forward * maxDistance;
                _dot.gameObject.SetActive(false);
            }

            _line.SetPosition(0, origin);
            _line.SetPosition(1, end);
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.SetColor("_Color", color);
            return material;
        }
    }
}
