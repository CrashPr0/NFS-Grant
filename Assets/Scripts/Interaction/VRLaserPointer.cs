using UnityEngine;
using NSFGrant.Core;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// A thin laser line projecting forward from a controller, with a dot
    /// where it meets geometry. It is also the headset's selection pointer:
    /// the object the beam touches is reported as hovered (it glows, see
    /// <see cref="InteractionHighlight"/>, and the beam brightens), and
    /// VRInteractor activates it on that hand's trigger.
    ///
    /// Selection used to stay on the gaze ray only (head direction on Quest
    /// 3), with the laser purely visual - so pointing the beam at an exhibit
    /// and pulling the trigger did nothing unless the head happened to be
    /// centered on it too. Gaze is still sampled every frame for the
    /// attention data, and activations record which pointer made them
    /// (source=vr_laser vs. vr_gaze), so the two stay separable.
    ///
    /// Uses NSFGrant/UnlitTransparentColor (single-pass-instanced-safe) for
    /// both the line and the dot, so the laser renders correctly in each
    /// eye. Ignores trigger colliders so the invisible SdgStation sensor
    /// volumes don't stop the beam short. Hides with its controller.
    /// </summary>
    public class VRLaserPointer : MonoBehaviour
    {
        [SerializeField] private XRInputBridge.Hand hand = XRInputBridge.Hand.Right;

        /// <summary>Which controller this beam belongs to (VRSurveyPanel aims with it).</summary>
        public XRInputBridge.Hand Hand => hand;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float startOffset = 0.08f;
        [SerializeField] private Color beamColor = new Color(0.45f, 0.85f, 1f, 0.7f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.62f, 0.95f);

        /// <summary>The interactable the beam is on, or null.</summary>
        public InteractableObject HoverTarget { get; private set; }

        /// <summary>Where the beam meets geometry (valid when HoverTarget is set).</summary>
        public Vector3 HitPoint { get; private set; }

        private LineRenderer _line;
        private Transform _dot;
        private Renderer _dotRenderer;
        private Material _lineMaterial;
        private Material _dotMaterial;
        private bool _hoverTint;

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
            _lineMaterial = MakeMaterial(beamColor);
            _line.material = _lineMaterial;

            var dotGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dotGo.name = "LaserDot";
            Destroy(dotGo.GetComponent<Collider>());
            dotGo.transform.SetParent(transform, false);
            dotGo.transform.localScale = Vector3.one * 0.03f;
            _dot = dotGo.transform;
            _dotRenderer = dotGo.GetComponent<Renderer>();
            _dotMaterial = MakeMaterial(new Color(beamColor.r, beamColor.g, beamColor.b, 0.95f));
            _dotRenderer.sharedMaterial = _dotMaterial;
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
                SetHoverTarget(null);
                return;
            }

            Vector3 origin = transform.position + transform.forward * startOffset;
            Vector3 end;
            InteractableObject target = null;
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                _dot.position = hit.point;
                _dot.gameObject.SetActive(true);
                // An open survey panel owns the beam (VRInteractor ignores
                // the trigger then too), so nothing behind it lights up.
                target = VRSurveyPanel.IsOpen ? null : hit.collider.GetComponentInParent<InteractableObject>();
                HitPoint = hit.point;
            }
            else
            {
                end = origin + transform.forward * maxDistance;
                _dot.gameObject.SetActive(false);
            }
            SetHoverTarget(target);

            // Brighter beam and a bigger dot on something selectable - only
            // where the condition lets it respond (not Passive).
            bool tint = target != null && (StudyConditionManager.Instance == null ||
                                           StudyConditionManager.Instance.InteractionEnabled);
            if (tint != _hoverTint)
            {
                _hoverTint = tint;
                Color c = tint ? hoverColor : beamColor;
                _lineMaterial.SetColor("_Color", c);
                _dotMaterial.SetColor("_Color", new Color(c.r, c.g, c.b, 0.95f));
                _dot.localScale = Vector3.one * (tint ? 0.045f : 0.03f);
                _line.startWidth = _line.endWidth = tint ? 0.009f : 0.006f;
            }

            _line.SetPosition(0, origin);
            _line.SetPosition(1, end);
        }

        private void OnDisable()
        {
            SetHoverTarget(null);
        }

        private void SetHoverTarget(InteractableObject target)
        {
            if (target == HoverTarget)
            {
                return;
            }
            if (HoverTarget != null)
            {
                HoverTarget.SetHovered(this, false);
            }
            HoverTarget = target;
            if (HoverTarget != null)
            {
                HoverTarget.SetHovered(this, true);
            }
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
