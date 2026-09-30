using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NSFGrant.Core;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Visible feedback for an <see cref="InteractableObject"/>, which adds
    /// this automatically. Before it existed nothing in the rooms reacted to
    /// the pointer: a participant couldn't tell a clickable exhibit from a
    /// wall, or whether a click (or VR trigger pull) had registered.
    ///
    ///   - Hover: a soft glowing frame fades in around the object's collider
    ///     (a rectangle for box colliders - panels, action buttons - and a
    ///     floor ring for anything else, e.g. the docent) and breathes
    ///     gently while the pointer stays.
    ///   - Activation: the frame flashes and ripples outward.
    ///   - <see cref="latchSelection"/> (the call-to-action options): the
    ///     chosen option keeps a dim frame, and choosing a sibling moves it,
    ///     so the wall shows the participant's current pick.
    ///
    /// Shown only when the condition enables interaction (not Passive), and
    /// built identically for every object of a kind, so no room or zone
    /// gets a different affordance. One small colliderless mesh per object,
    /// drawn only while visible.
    /// </summary>
    [RequireComponent(typeof(InteractableObject))]
    public class InteractionHighlight : MonoBehaviour
    {
        [Tooltip("Keep a dim frame on this object after it is chosen, clearing latched siblings (one choice per group).")]
        [SerializeField] private bool latchSelection;

        [SerializeField] private Color glowColor = new Color(1f, 0.86f, 0.55f, 1f);
        [SerializeField] private float frameGap = 0.04f;
        [SerializeField] private float frameThickness = 0.028f;

        private const float HoverAlpha = 0.8f;
        private const float LatchAlpha = 0.4f;
        private const float FadeSpeed = 9f;
        private const float FlashSeconds = 0.45f;

        public bool LatchSelection
        {
            get => latchSelection;
            set => latchSelection = value;
        }

        private InteractableObject _interactable;
        private Transform _frame;
        private Vector3 _frameScale;
        private Renderer _renderer;
        private Material _material;
        private float _alpha;
        private float _flash = -1f;   // seconds since the last activation, <0 = none
        private bool _latched;

        private void Awake()
        {
            _interactable = GetComponent<InteractableObject>();
        }

        private void OnEnable()
        {
            _interactable.Activated += OnActivated;
        }

        private void OnDisable()
        {
            _interactable.Activated -= OnActivated;
        }

        private void Start()
        {
            BuildFrame();
        }

        private static bool InteractionEnabled =>
            StudyConditionManager.Instance == null || StudyConditionManager.Instance.InteractionEnabled;

        private void OnActivated(InteractableObject _)
        {
            _flash = 0f;
            if (!latchSelection)
            {
                return;
            }
            _latched = true;
            if (transform.parent == null)
            {
                return;
            }
            foreach (var sibling in transform.parent.GetComponentsInChildren<InteractionHighlight>())
            {
                if (sibling != this && sibling.latchSelection && sibling.transform.parent == transform.parent)
                {
                    sibling._latched = false;
                }
            }
        }

        private void Update()
        {
            if (_renderer == null)
            {
                return;
            }

            float target = 0f;
            if (InteractionEnabled)
            {
                if (_interactable.IsHovered)
                {
                    // Slow breathing so a held hover still reads as "live".
                    target = HoverAlpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));
                }
                else if (_latched)
                {
                    target = LatchAlpha;
                }
            }
            _alpha = Mathf.MoveTowards(_alpha, target, FadeSpeed * Time.unscaledDeltaTime);

            float ripple = 0f;
            float flashBoost = 0f;
            if (_flash >= 0f)
            {
                _flash += Time.unscaledDeltaTime;
                float t = _flash / FlashSeconds;
                if (t >= 1f)
                {
                    _flash = -1f;
                }
                else
                {
                    float ease = 1f - (1f - t) * (1f - t);
                    ripple = ease;
                    flashBoost = 1f - t;
                }
            }

            float alpha = Mathf.Clamp01(Mathf.Max(_alpha, flashBoost));
            bool visible = alpha > 0.005f;
            if (_renderer.enabled != visible)
            {
                _renderer.enabled = visible;
            }
            if (!visible)
            {
                return;
            }

            Color c = Color.Lerp(glowColor, Color.white, flashBoost * 0.6f);
            c.a = alpha;
            _material.SetColor("_Color", c);
            _frame.localScale = _frameScale * (1f + ripple * 0.06f);
        }

        // ------------------------------------------------------------ mesh

        private void BuildFrame()
        {
            Collider col = null;
            foreach (var c in GetComponents<Collider>())
            {
                if (!c.isTrigger)
                {
                    col = c;
                    break;
                }
            }
            if (col == null)
            {
                return;
            }

            // Frames are sized in meters, so the holder undoes the object's
            // own scale (the action buttons are scaled cubes).
            var holder = new GameObject("InteractionHighlight").transform;
            holder.SetParent(transform, false);
            Vector3 lossy = transform.lossyScale;
            holder.localScale = new Vector3(SafeInv(lossy.x), SafeInv(lossy.y), SafeInv(lossy.z));

            Mesh mesh;
            if (col is BoxCollider box)
            {
                holder.localPosition = box.center;
                Vector3 size = Vector3.Scale(box.size, Abs(lossy));
                mesh = RectFrameMesh(size.x / 2f + frameGap, size.y / 2f + frameGap,
                                     size.z / 2f + 0.01f, frameThickness);
            }
            else
            {
                // Ring on the floor around the object's base, above the
                // rooms' floor tint (top ~3.5 cm) and carpet runner (~5.6 cm).
                float radius = col.bounds.extents.x > 0f ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : 0.4f;
                holder.localPosition = transform.InverseTransformPoint(
                    new Vector3(transform.position.x, transform.position.y + 0.065f, transform.position.z));
                mesh = RingMesh(radius + 0.28f, radius + 0.34f, 48);
            }

            _frame = holder;
            _frameScale = holder.localScale;
            holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = holder.gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            _material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            _renderer.sharedMaterial = _material;
            _renderer.enabled = false;
        }

        private static float SafeInv(float v) => Mathf.Abs(v) > 1e-5f ? 1f / v : 1f;
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// Four bars (closed boxes) around a rectangle in the XY plane, deep
        /// enough to wrap the object, so the frame reads from either side.
        /// </summary>
        private static Mesh RectFrameMesh(float hx, float hy, float hz, float t)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // top, bottom, left, right
            AddBox(verts, tris, new Vector3(0f, hy + t / 2f, 0f), new Vector3(hx + t, t / 2f, hz));
            AddBox(verts, tris, new Vector3(0f, -hy - t / 2f, 0f), new Vector3(hx + t, t / 2f, hz));
            AddBox(verts, tris, new Vector3(-hx - t / 2f, 0f, 0f), new Vector3(t / 2f, hy, hz));
            AddBox(verts, tris, new Vector3(hx + t / 2f, 0f, 0f), new Vector3(t / 2f, hy, hz));
            var mesh = new Mesh { name = "HighlightFrame" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> v, List<int> t, Vector3 c, Vector3 h)
        {
            int b = v.Count;
            for (int i = 0; i < 8; i++)
            {
                v.Add(c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
            }
            int[] faces =
            {
                0, 2, 3, 1,  4, 5, 7, 6,  0, 1, 5, 4,
                2, 6, 7, 3,  0, 4, 6, 2,  1, 3, 7, 5,
            };
            for (int f = 0; f < faces.Length; f += 4)
            {
                t.AddRange(new[] { b + faces[f], b + faces[f + 1], b + faces[f + 2],
                                   b + faces[f], b + faces[f + 2], b + faces[f + 3] });
            }
        }

        private static Mesh RingMesh(float inner, float outer, int segments)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(d * inner);
                verts.Add(d * outer);
                if (i < segments)
                {
                    int k = i * 2;
                    tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }
            var mesh = new Mesh { name = "HighlightRing" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
