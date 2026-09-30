using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Interaction;

namespace NSFGrant.Docent
{
    /// <summary>
    /// Brings a room docent (built by ProceduralDocent) to life without a
    /// rig or Animator:
    ///   - Idle: the figure floats and bobs gently over its pedestal, and
    ///     the presenting arm sways a little.
    ///   - Attention: the head turns to follow the visitor when they are in
    ///     front of the docent and within a few meters (clamped, smoothed),
    ///     and settles back to neutral when they leave.
    ///   - Pointed at (mouse or VR laser): the rim and trim brighten.
    ///   - Clicked / triggered: the docent waves and its speech card pops.
    ///     This listens to <see cref="InteractableObject.Activated"/>, which
    ///     only fires where the condition enables interaction, so Passive
    ///     participants' clicks are still logged but get no response.
    /// Every docent runs the same code with the same constants, so all rooms
    /// behave identically. Breathing, blinking, scanlines and the scan
    /// sweep are in NSFGrant/DocentHologram.
    /// </summary>
    [RequireComponent(typeof(InteractableObject))]
    public class DocentPresence : MonoBehaviour
    {
        [SerializeField] private Transform figure;
        [SerializeField] private Transform head;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform speechCard;
        [SerializeField] private Renderer[] glowRenderers;

        [Header("Attention")]
        [SerializeField] private float lookRange = 7f;
        [SerializeField] private float maxYaw = 65f;
        [SerializeField] private float maxPitch = 22f;

        private const float BobHeight = 0.018f;
        private const float WaveSeconds = 1.8f;

        private static readonly int HighlightId = Shader.PropertyToID("_Highlight");

        private InteractableObject _interactable;
        private MaterialPropertyBlock _block;
        private Vector3 _figureBase;
        private Quaternion _headBase;
        private Quaternion _armBase;
        private Vector3 _cardScale;
        private float _wave = -1f;
        private float _highlight;
        private float _appliedHighlight = -1f;

        private void Awake()
        {
            _interactable = GetComponent<InteractableObject>();
            _block = new MaterialPropertyBlock();
            if (figure != null) _figureBase = figure.localPosition;
            if (head != null) _headBase = head.localRotation;
            if (rightArm != null) _armBase = rightArm.localRotation;
            if (speechCard != null) _cardScale = speechCard.localScale;
        }

        private void OnEnable()
        {
            _interactable.Activated += OnActivated;
        }

        private void OnDisable()
        {
            _interactable.Activated -= OnActivated;
        }

        private void OnActivated(InteractableObject _)
        {
            _wave = 0f;
        }

        private void Update()
        {
            float t = Time.time;
            float dt = Time.deltaTime;

            if (figure != null)
            {
                figure.localPosition = _figureBase + Vector3.up * (Mathf.Sin(t * 1.25f) * BobHeight);
            }

            UpdateHead(dt);

            float waveAmount = 0f;
            float wavePhase = 0f;
            if (_wave >= 0f)
            {
                _wave += dt;
                float u = _wave / WaveSeconds;
                if (u >= 1f)
                {
                    _wave = -1f;
                }
                else
                {
                    // Raise quickly, wave, lower smoothly.
                    waveAmount = Smooth(0f, 0.18f, u) * (1f - Smooth(0.72f, 1f, u));
                    wavePhase = _wave;
                }
            }

            if (rightArm != null)
            {
                float idle = Mathf.Sin(t * 0.9f) * 2.5f;
                float raise = waveAmount * 70f;
                float wag = Mathf.Sin(wavePhase * 11f) * 16f * waveAmount;
                rightArm.localRotation = _armBase * Quaternion.Euler(-waveAmount * 20f, 0f, idle + raise + wag);
            }

            if (speechCard != null)
            {
                float pop = _wave >= 0f ? Mathf.Sin(Mathf.Clamp01(_wave / 0.35f) * Mathf.PI) : 0f;
                speechCard.localScale = _cardScale * (1f + 0.06f * pop);
            }

            bool interactive = StudyConditionManager.Instance == null ||
                               StudyConditionManager.Instance.InteractionEnabled;
            float target = interactive && _interactable.IsHovered ? 0.7f : 0f;
            target = Mathf.Max(target, waveAmount);
            _highlight = Mathf.MoveTowards(_highlight, target, dt * 4f);
            ApplyHighlight(_highlight);
        }

        /// <summary>HLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        private static float Smooth(float edge0, float edge1, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        private void UpdateHead(float dt)
        {
            if (head == null)
            {
                return;
            }

            Quaternion goal = _headBase;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - head.position;
                if (toCam.sqrMagnitude < lookRange * lookRange)
                {
                    // Direction in the docent's own frame (+Z = its front).
                    Vector3 local = transform.InverseTransformDirection(toCam);
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(yaw) < 115f)
                    {
                        float flat = new Vector2(local.x, local.z).magnitude;
                        float pitch = -Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg;
                        goal = Quaternion.Euler(Mathf.Clamp(pitch, -maxPitch, maxPitch),
                                                Mathf.Clamp(yaw, -maxYaw, maxYaw), 0f) * _headBase;
                    }
                }
            }
            // Idle tilt so a settled head never looks frozen.
            goal *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.6f) * 3f);
            head.localRotation = Quaternion.Slerp(head.localRotation, goal, 1f - Mathf.Exp(-5f * dt));
        }

        private void ApplyHighlight(float value)
        {
            if (Mathf.Abs(value - _appliedHighlight) < 0.002f || glowRenderers == null)
            {
                return;
            }
            _appliedHighlight = value;
            foreach (var r in glowRenderers)
            {
                if (r == null)
                {
                    continue;
                }
                r.GetPropertyBlock(_block);
                _block.SetFloat(HighlightId, value);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
