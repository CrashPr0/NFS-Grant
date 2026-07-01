using System.Collections;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Hand-rolled teleport + snap-turn locomotion for the VR rig. No XR
    /// Interaction Toolkit or Meta Interaction SDK is installed (see
    /// Packages/manifest.json - only com.meta.xr.sdk.core), and the hall
    /// spans a ~32 m hub-to-room arc, far past any realistic Quest Guardian
    /// space, so this is required functionality: without it a VR
    /// participant could only ever reach the hub they spawn in.
    ///
    /// Right thumbstick forward + hold aims a parabolic arc from the right
    /// controller; releasing teleports the rig root to the landing point if
    /// it lands on a <see cref="TeleportSurface"/>. Left thumbstick
    /// left/right flick snap-turns the rig in fixed increments. Both
    /// bracket a brief fade-to-black - standard comfort locomotion, and it
    /// also keeps a sudden viewpoint jump from showing up as a spurious
    /// spike in the gaze/head-position data. Every teleport/turn is logged
    /// (StudyEventLogger) so VR navigation paths stay analyzable alongside
    /// the desktop click/movement log - see docs/STUDY_DESIGN.md.
    /// </summary>
    public class VRLocomotion : MonoBehaviour
    {
        [Tooltip("The rig root that gets moved/rotated (the object PlatformRigSwitcher toggles).")]
        [SerializeField] private Transform rigRoot;

        [Tooltip("Aim origin/direction for the teleport arc (right controller). Falls back to centerEye if unset.")]
        [SerializeField] private Transform controllerAnchor;

        [Tooltip("Parent for the screen-fade quad (the headset's center eye anchor).")]
        [SerializeField] private Transform centerEye;

        [Tooltip("Degrees per snap-turn.")]
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float fadeSeconds = 0.12f;
        [SerializeField] private float arcSpeed = 9f;
        [SerializeField] private float arcGravity = 14f;
        [SerializeField] private int arcSegments = 24;
        [SerializeField] private float segmentStep = 0.05f;
        [SerializeField] private float thumbstickDeadzone = 0.35f;

        private LineRenderer _arc;
        private Transform _reticle;
        private Renderer _reticleRenderer;
        private Material _arcValidMat;
        private Material _arcInvalidMat;
        private Material _reticleValidMat;
        private Material _reticleInvalidMat;

        private Material _fadeMaterial;
        private float _fadeAlpha;
        private Coroutine _fadeRoutine;

        private bool _aiming;
        private bool _hasValidTarget;
        private Vector3 _landingPoint;
        private bool _turnArmed = true;

        private void Awake()
        {
            if (rigRoot == null)
            {
                rigRoot = transform;
            }
            if (controllerAnchor == null)
            {
                controllerAnchor = centerEye;
            }
            BuildArcVisuals();
            BuildFadeQuad();
        }

        private void Update()
        {
            HandleTeleportAim();
            HandleSnapTurn();
        }

        private void HandleTeleportAim()
        {
            if (controllerAnchor == null)
            {
                return;
            }

            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            bool wantsAim = stick.y > thumbstickDeadzone;

            if (wantsAim)
            {
                _aiming = true;
                EvaluateArc();
                _arc.enabled = true;
                _reticle.gameObject.SetActive(_hasValidTarget);
            }
            else if (_aiming)
            {
                _aiming = false;
                _arc.enabled = false;
                _reticle.gameObject.SetActive(false);
                if (_hasValidTarget)
                {
                    Teleport(_landingPoint);
                }
            }
        }

        /// <summary>
        /// Samples a projectile arc from the controller and walks it
        /// segment-by-segment with Physics.Linecast until it hits
        /// something (or runs out of segments) - the first hit is the
        /// landing point, valid only if it carries a TeleportSurface.
        /// </summary>
        private void EvaluateArc()
        {
            Vector3 origin = controllerAnchor.position;
            Vector3 velocity = controllerAnchor.forward * arcSpeed;
            var points = new Vector3[arcSegments];
            points[0] = origin;

            _hasValidTarget = false;
            Vector3 previous = origin;
            int drawn = 1;
            for (int i = 1; i < arcSegments; i++)
            {
                float t = i * segmentStep;
                Vector3 next = origin + velocity * t + 0.5f * Vector3.down * arcGravity * t * t;

                if (Physics.Linecast(previous, next, out RaycastHit hit))
                {
                    points[drawn++] = hit.point;
                    _hasValidTarget = hit.collider.GetComponentInParent<TeleportSurface>() != null;
                    _landingPoint = hit.point;
                    break;
                }

                points[drawn++] = next;
                previous = next;
            }

            _arc.positionCount = drawn;
            _arc.SetPositions(points);
            _arc.material = _hasValidTarget ? _arcValidMat : _arcInvalidMat;

            if (_hasValidTarget)
            {
                _reticle.position = _landingPoint + Vector3.up * 0.02f;
            }
            _reticleRenderer.sharedMaterial = _hasValidTarget ? _reticleValidMat : _reticleInvalidMat;
        }

        private void HandleSnapTurn()
        {
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

            if (Mathf.Abs(stick.x) < thumbstickDeadzone)
            {
                _turnArmed = true;
                return;
            }
            if (!_turnArmed)
            {
                return;
            }
            _turnArmed = false;

            float angle = stick.x > 0f ? snapTurnAngle : -snapTurnAngle;
            StartCoroutine(SnapTurnRoutine(angle));
        }

        private void Teleport(Vector3 destination)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }
            _fadeRoutine = StartCoroutine(TeleportRoutine(destination));
        }

        private IEnumerator TeleportRoutine(Vector3 destination)
        {
            Vector3 from = rigRoot.position;
            yield return Fade(1f, fadeSeconds);

            // Keep the rig's current height; only the floor point moves.
            rigRoot.position = new Vector3(destination.x, rigRoot.position.y, destination.z);
            StudyEventLogger.Instance?.LogWorldEvent("teleport", "",
                $"from_x={from.x:F2};from_z={from.z:F2}", destination);

            yield return Fade(0f, fadeSeconds);
        }

        private IEnumerator SnapTurnRoutine(float angle)
        {
            yield return Fade(1f, fadeSeconds * 0.6f);

            rigRoot.RotateAround(rigRoot.position, Vector3.up, angle);
            StudyEventLogger.Instance?.LogEvent("vr_snap_turn", "", $"degrees={angle:F0}");

            yield return Fade(0f, fadeSeconds * 0.6f);
        }

        private IEnumerator Fade(float target, float duration)
        {
            float start = _fadeAlpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _fadeAlpha = duration > 0f ? Mathf.Lerp(start, target, t / duration) : target;
                ApplyFadeAlpha();
                yield return null;
            }
            _fadeAlpha = target;
            ApplyFadeAlpha();
        }

        private void ApplyFadeAlpha()
        {
            if (_fadeMaterial == null)
            {
                return;
            }
            Color c = _fadeMaterial.color;
            c.a = _fadeAlpha;
            _fadeMaterial.color = c;
        }

        private void BuildArcVisuals()
        {
            var arcGo = new GameObject("TeleportArc");
            arcGo.transform.SetParent(transform, false);
            _arc = arcGo.AddComponent<LineRenderer>();
            _arc.startWidth = 0.03f;
            _arc.endWidth = 0.03f;
            _arc.useWorldSpace = true;
            _arc.positionCount = 0;
            _arc.enabled = false;
            _arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arc.receiveShadows = false;

            _arcValidMat = MakeUnlitMaterial(new Color(0.3f, 0.9f, 0.5f, 0.9f));
            _arcInvalidMat = MakeUnlitMaterial(new Color(0.9f, 0.3f, 0.3f, 0.7f));
            _arc.material = _arcInvalidMat;

            var reticleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticleGo.name = "TeleportReticle";
            Destroy(reticleGo.GetComponent<Collider>());
            reticleGo.transform.SetParent(transform, false);
            reticleGo.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            _reticle = reticleGo.transform;
            _reticleRenderer = reticleGo.GetComponent<Renderer>();
            _reticleValidMat = MakeUnlitMaterial(new Color(0.3f, 0.9f, 0.5f, 0.55f));
            _reticleInvalidMat = MakeUnlitMaterial(new Color(0.9f, 0.3f, 0.3f, 0.45f));
            _reticleRenderer.sharedMaterial = _reticleValidMat;
            reticleGo.SetActive(false);
        }

        /// <summary>
        /// A large, camera-facing quad parented close in front of the eye
        /// for the fade-to-black transition (VR has no screen-space IMGUI
        /// to fade instead). Placed well past any plausible near-clip
        /// distance and oversized so it fills the FOV regardless of
        /// headset - precision doesn't matter for a solid color fade.
        /// </summary>
        private void BuildFadeQuad()
        {
            if (centerEye == null)
            {
                return;
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VRFadeQuad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(centerEye, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = Vector3.one * 5f;

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _fadeMaterial = MakeUnlitMaterial(new Color(0f, 0f, 0f, 0f));
            renderer.sharedMaterial = _fadeMaterial;
        }

        /// <summary>
        /// NSFGrant/UnlitTransparentColor renders both faces (Cull Off), so
        /// quad/plane orientation doesn't matter here. Falls back to
        /// Sprites/Default if the shader is missing.
        /// </summary>
        private static Material MakeUnlitMaterial(Color color)
        {
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.SetColor("_Color", color);
            return material;
        }
    }
}
