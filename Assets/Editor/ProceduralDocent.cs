using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NSFGrant.Docent;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Code-generated room docent (MINERVA / HINA / BHUMI), replacing the
    /// capsule-and-sphere blockout: a projected AI guide in a flowing robe
    /// on a projector pedestal.
    ///
    ///   - Pedestal: lathed dark-metal disc with a glowing theme ring and
    ///     emitter, plus a soft additive projection beam (NSFGrant/LightShaft
    ///     flipped so it is brightest at the emitter).
    ///   - Figure (floats and bobs above the pedestal): lathed robe with
    ///     cloth folds that deepen toward the hem, an oval torso and
    ///     shoulders, glowing hem / belt / collar trim, a relaxed left arm
    ///     and a right arm (own pivot) presenting the speech card.
    ///   - Head (own pivot, so it can look at the visitor): a smooth face
    ///     shell with a glass visor, two blinking eyes (drawn in the
    ///     shader), glowing ear pods and a tilted halo.
    ///
    /// All shaded by NSFGrant/DocentHologram (cel bands, Fresnel rim with
    /// scanlines, rising scan sweep). Animation is DocentPresence plus the
    /// shader - no rig, no Animator. Geometry is identical for every room
    /// (baked once per scene build into Assets/StudyContent, gitignored);
    /// only the equal-luminance theme color differs, as with the rest of
    /// each room.
    /// </summary>
    public static class ProceduralDocent
    {
        private const string AssetDir = "Assets/StudyContent";

        // Figure root height above the floor (the pedestal top is 0.105).
        private const float FloatHeight = 0.16f;
        // Head center and right shoulder, in figure space.
        private static readonly Vector3 HeadCenter = new Vector3(0f, 1.625f, 0f);
        private static readonly Vector3 RightShoulder = new Vector3(0.23f, 1.315f, 0f);

        private static Mesh _pedestal, _figure, _head, _rightArm;

        private static readonly Color Robe = new Color(0.7f, 0.72f, 0.76f, 0f);
        private static readonly Color RobeDark = new Color(0.1f, 0.1f, 0.12f, 0f);
        private static readonly Color Trim = new Color(0.95f, 0.95f, 0.97f, 1f);
        private static readonly Color Skin = new Color(0.66f, 0.68f, 0.73f, 0f);
        private static readonly Color Face = new Color(0.8f, 0.81f, 0.84f, 0f);

        /// <summary>Drops cached meshes; call at the start of every scene build.</summary>
        public static void Reset()
        {
            _pedestal = _figure = _head = _rightArm = null;
        }

        /// <summary>
        /// Builds the docent's visuals under <paramref name="docent"/> (whose
        /// +Z faces visitors) and wires a DocentPresence to animate them.
        /// Returns the presence; the caller adds collider, AOI and text.
        /// </summary>
        public static DocentPresence Create(Transform docent, Color theme, Transform speechCard)
        {
            EnsureMeshes();

            Color tint = Color.Lerp(Color.white, theme, 0.07f);
            var bodyMat = MakeMaterial($"{docent.name}_Body", theme, tint, m => m.SetFloat("_Stole", 1f));
            var headMat = MakeMaterial($"{docent.name}_Head", theme, tint, m => m.SetFloat("_Visor", 1f));
            var pedestalMat = MakeMaterial($"{docent.name}_Pedestal", theme, Color.white, m =>
            {
                m.SetFloat("_RimStrength", 0.35f);
                m.SetFloat("_Breath", 0f);
                m.SetFloat("_Emission", 2.2f);
            });

            var pedestal = AddPart(docent, "Pedestal", _pedestal, pedestalMat);

            var figure = new GameObject("Figure").transform;
            figure.SetParent(docent, false);
            figure.localPosition = new Vector3(0f, FloatHeight, 0f);
            var body = AddPart(figure, "Body", _figure, bodyMat);

            var headPivot = new GameObject("HeadPivot").transform;
            headPivot.SetParent(figure, false);
            headPivot.localPosition = HeadCenter;
            var head = AddPart(headPivot, "Head", _head, headMat);

            var armPivot = new GameObject("RightArmPivot").transform;
            armPivot.SetParent(figure, false);
            armPivot.localPosition = RightShoulder;
            var arm = AddPart(armPivot, "RightArm", _rightArm, bodyMat);

            CreateBeam(docent, theme);

            var presence = docent.gameObject.AddComponent<DocentPresence>();
            var so = new SerializedObject(presence);
            so.FindProperty("figure").objectReferenceValue = figure;
            so.FindProperty("head").objectReferenceValue = headPivot;
            so.FindProperty("rightArm").objectReferenceValue = armPivot;
            so.FindProperty("speechCard").objectReferenceValue = speechCard;
            var renderers = so.FindProperty("glowRenderers");
            var list = new[] { body, head, arm, pedestal };
            renderers.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return presence;
        }

        private static Renderer AddPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static Material MakeMaterial(string name, Color theme, Color tint, System.Action<Material> setup)
        {
            var shader = Shader.Find("NSFGrant/DocentHologram");
            if (shader == null)
            {
                Debug.LogWarning("[ProceduralDocent] NSFGrant/DocentHologram not found; docent will be flat.");
                return new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { name = name };
            }
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_Theme", theme);
            material.SetColor("_Tint", tint);
            setup(material);
            return material;
        }

        /// <summary>
        /// Soft projection beam over the pedestal: the default cylinder with
        /// NSFGrant/LightShaft, turned upside down so the shaft's bright end
        /// sits on the emitter and fades out above the head.
        /// </summary>
        private static void CreateBeam(Transform docent, Color theme)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "ProjectionBeam";
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(docent, false);
            beam.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            beam.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            beam.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            var renderer = beam.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var shader = Shader.Find("NSFGrant/LightShaft");
            if (shader == null)
            {
                Object.DestroyImmediate(beam);
                return;
            }
            var material = new Material(shader) { name = $"{docent.name}_Beam" };
            material.SetColor("_Color", Color.Lerp(theme, Color.white, 0.35f));
            material.SetFloat("_Intensity", 0.32f);
            material.SetFloat("_BottomFalloff", 1.8f);
            material.SetFloat("_ShimmerStrength", 0.25f);
            material.SetFloat("_ShimmerSpeed", 1.3f);
            renderer.sharedMaterial = material;
        }

        // ---------------------------------------------------------- meshes

        private static void EnsureMeshes()
        {
            if (_figure != null)
            {
                return;
            }
            _pedestal = SaveMesh(BuildPedestal(), "DiscoveryHallDocentPedestal");
            _figure = SaveMesh(BuildFigure(), "DiscoveryHallDocentFigure");
            _head = SaveMesh(BuildHead(), "DiscoveryHallDocentHead");
            _rightArm = SaveMesh(BuildRightArm(), "DiscoveryHallDocentRightArm");
        }

        private struct P
        {
            public float R, Y;
            public Color C;
            public P(float r, float y, Color c) { R = r; Y = y; C = c; }
        }

        private static Mesh BuildPedestal()
        {
            var metal = new Color(0.15f, 0.16f, 0.19f, 0f);
            var bevel = new Color(0.27f, 0.28f, 0.32f, 0f);
            var ring = new Color(1f, 1f, 1f, 1f);
            var emitter = new Color(0.5f, 0.5f, 0.55f, 0.55f);
            var profile = new[]
            {
                new P(0.000f, 0.000f, metal),
                new P(0.440f, 0.000f, metal),
                new P(0.470f, 0.012f, metal),
                new P(0.480f, 0.040f, metal),
                new P(0.476f, 0.070f, bevel),
                new P(0.455f, 0.095f, bevel),
                new P(0.420f, 0.104f, bevel),
                new P(0.410f, 0.104f, ring),
                new P(0.345f, 0.104f, ring),
                new P(0.335f, 0.104f, metal),
                new P(0.315f, 0.092f, metal),
                new P(0.300f, 0.092f, emitter),
                new P(0.000f, 0.098f, emitter),
            };
            var mb = new GridMesh();
            Lathe(mb, profile, 56, (y, a) => 1f, (y, a) => 0f, _ => 0f, y => -2f);
            return mb.ToMesh("DocentPedestal");
        }

        private static Mesh BuildFigure()
        {
            var profile = new[]
            {
                new P(0.000f, 0.100f, RobeDark),
                new P(0.300f, 0.030f, RobeDark),
                new P(0.330f, 0.000f, Trim),
                new P(0.337f, 0.032f, Trim),
                new P(0.332f, 0.046f, Robe),
                new P(0.300f, 0.200f, Robe),
                new P(0.262f, 0.400f, Robe),
                new P(0.222f, 0.600f, Robe),
                new P(0.186f, 0.800f, Robe),
                new P(0.168f, 0.915f, Robe),
                new P(0.170f, 0.922f, Trim),
                new P(0.172f, 0.962f, Trim),
                new P(0.174f, 0.970f, Robe),
                new P(0.190f, 1.080f, Robe),
                new P(0.200f, 1.190f, Robe),
                new P(0.203f, 1.270f, Robe),
                new P(0.190f, 1.330f, Robe),
                new P(0.150f, 1.385f, Robe),
                new P(0.112f, 1.405f, Trim),
                new P(0.078f, 1.432f, Trim),
                new P(0.060f, 1.440f, Skin),
                new P(0.055f, 1.510f, Skin),
                new P(0.000f, 1.520f, Skin),
            };
            var mb = new GridMesh();
            Lathe(mb, profile, 64,
                // Oval torso: wider across the shoulders than front-to-back;
                // the skirt stays round.
                scaleX: (y, a) => Mathf.Lerp(1f, 1.3f, Smooth(0.75f, 1.2f, y) * (1f - Smooth(1.37f, 1.43f, y))),
                // Cloth folds: seven soft pleats, deepest at the hem.
                radialOffset: (y, a) =>
                {
                    float depth = 0.02f * Mathf.Pow(Mathf.Clamp01(1f - y / 0.85f), 1.3f);
                    return depth * (Mathf.Sin(a * 7f + 0.4f) * 0.7f + Mathf.Sin(a * 12f + 1.9f) * 0.3f);
                },
                breath: y => Mathf.Clamp01(1f - Mathf.Abs(y - 1.17f) / 0.25f),
                height: y => y,
                scaleZ: (y, a) => Mathf.Lerp(1f, 0.72f, Smooth(0.75f, 1.2f, y) * (1f - Smooth(1.37f, 1.43f, y))));

            // Relaxed left arm (docent's left = -X), hanging slightly forward.
            AddArm(mb, new Vector3(-0.23f, 1.315f, 0f), new Vector3(-0.3f, 1.06f, 0.02f),
                   new Vector3(-0.32f, 0.94f, 0.05f), new Vector3(-0.31f, 0.8f, 0.1f), 0f);
            return mb.ToMesh("DocentFigure");
        }

        /// <summary>
        /// Right arm in its shoulder pivot's space, raised forward and out in
        /// a presenting gesture toward the speech card on the docent's right.
        /// </summary>
        private static Mesh BuildRightArm()
        {
            var mb = new GridMesh();
            AddArm(mb, Vector3.zero, new Vector3(0.07f, -0.2f, 0.02f),
                   new Vector3(0.13f, -0.3f, 0.12f), new Vector3(0.24f, -0.2f, 0.25f), RightShoulder.y);
            return mb.ToMesh("DocentRightArm");
        }

        /// <summary>
        /// Sleeve as a tapered tube along a cubic curve, flaring into a bell
        /// cuff with a glowing band, closed by a hand (flattened ellipsoid)
        /// continuing the forearm. <paramref name="heightOffset"/> converts
        /// the mesh's y to figure height for the shader's sweep.
        /// </summary>
        private static void AddArm(GridMesh mb, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float heightOffset)
        {
            const int rings = 14, sides = 14;
            var pos = new Vector3[rings + 1, sides + 1];
            var col = new Color[rings + 1, sides + 1];
            var uv2 = new Vector2[rings + 1, sides + 1];
            Vector3 prevT = Bezier.Tangent(p0, p1, p2, p3, 0f);
            Vector3 frameN = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                Vector3 c = Bezier.Point(p0, p1, p2, p3, t);
                Vector3 tan = Bezier.Tangent(p0, p1, p2, p3, t);
                frameN = Quaternion.FromToRotation(prevT, tan) * frameN;
                prevT = tan;
                Vector3 frameB = Vector3.Cross(tan, frameN);
                // Shoulder 0.064 -> forearm 0.045 -> bell cuff 0.068.
                float r = Mathf.Lerp(0.064f, 0.045f, Smooth(0f, 0.625f, t));
                r += 0.024f * Smooth(0.62f, 1f, t);
                bool cuff = t > 0.86f && t < 0.97f;
                Color color = cuff ? Trim : Robe;
                if (i == rings)
                {
                    r *= 0.55f;              // rolled-in cuff edge, into the hand
                    color = RobeDark;
                }
                for (int j = 0; j <= sides; j++)
                {
                    float a = (float)j / sides * Mathf.PI * 2f;
                    Vector3 n = frameN * Mathf.Cos(a) + frameB * Mathf.Sin(a);
                    pos[i, j] = c + n * r;
                    col[i, j] = color;
                    uv2[i, j] = new Vector2(0f, pos[i, j].y + heightOffset);
                }
            }
            Vector3 endTan = Bezier.Tangent(p0, p1, p2, p3, 1f);
            mb.AddGrid(pos, col, uv2, -Bezier.Tangent(p0, p1, p2, p3, 0f), endTan);

            // Hand: an open, slightly flattened palm continuing the forearm.
            Vector3 side = Vector3.Cross(endTan, Vector3.up).normalized;
            Vector3 flat = Vector3.Cross(side, endTan).normalized;
            Vector3 center = p3 + endTan * 0.06f;
            AddEllipsoid(mb, center, side * 0.033f, endTan * 0.055f, flat * 0.02f,
                         Skin, 10, 14, 0f, heightOffset);
        }

        private static Mesh BuildHead()
        {
            var mb = new GridMesh();
            // Face shell (visor and eyes come from the shader, _Visor 1).
            AddEllipsoid(mb, Vector3.zero, new Vector3(0.11f, 0f, 0f), new Vector3(0f, 0.135f, 0f),
                         new Vector3(0f, 0f, 0.12f), Face, 20, 32, 0f, HeadCenter.y);
            // Glowing ear pods.
            foreach (float x in new[] { -1f, 1f })
            {
                AddEllipsoid(mb, new Vector3(x * 0.104f, 0.0f, -0.005f), new Vector3(0.022f, 0f, 0f),
                             new Vector3(0f, 0.042f, 0f), new Vector3(0f, 0f, 0.042f),
                             Trim, 8, 14, 0f, HeadCenter.y);
            }
            // Halo: a thin tilted ring above and behind the head.
            AddTorus(mb, new Vector3(0f, 0.16f, -0.05f), Quaternion.Euler(-22f, 0f, 0f),
                     0.108f, 0.007f, Trim, 40, 6, HeadCenter.y);
            return mb.ToMesh("DocentHead");
        }

        // --------------------------------------------------------- shapes

        /// <summary>HLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        private static float Smooth(float edge0, float edge1, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        private delegate float Field(float y, float angle);

        /// <summary>
        /// Surface of revolution from a (radius, height) profile, around +Y,
        /// with an elliptical cross-section (scaleX / scaleZ) and a radial
        /// displacement (folds). The seam sits at the back (-Z).
        /// </summary>
        private static void Lathe(GridMesh mb, P[] profile, int segments, Field scaleX, Field radialOffset,
            System.Func<float, float> breath, System.Func<float, float> height, Field scaleZ = null)
        {
            int rows = profile.Length;
            var pos = new Vector3[rows, segments + 1];
            var col = new Color[rows, segments + 1];
            var uv2 = new Vector2[rows, segments + 1];
            for (int i = 0; i < rows; i++)
            {
                var p = profile[i];
                for (int j = 0; j <= segments; j++)
                {
                    // Angle 0 at -Z (back), so the seam is out of sight.
                    float a = (float)j / segments * Mathf.PI * 2f;
                    float r = p.R + (p.R > 0.02f ? radialOffset(p.Y, a) : 0f);
                    float sx = scaleX(p.Y, a);
                    float sz = scaleZ != null ? scaleZ(p.Y, a) : 1f;
                    pos[i, j] = new Vector3(Mathf.Sin(a) * r * sx, p.Y, -Mathf.Cos(a) * r * sz);
                    col[i, j] = p.C;
                    uv2[i, j] = new Vector2(breath(p.Y), height(p.Y));
                }
            }
            mb.AddGrid(pos, col, uv2, Vector3.down, Vector3.up);
        }

        private static void AddEllipsoid(GridMesh mb, Vector3 center, Vector3 ax, Vector3 ay, Vector3 az,
            Color color, int rings, int segments, float breath, float heightOffset)
        {
            var pos = new Vector3[rings + 1, segments + 1];
            var col = new Color[rings + 1, segments + 1];
            var uv2 = new Vector2[rings + 1, segments + 1];
            for (int i = 0; i <= rings; i++)
            {
                float theta = Mathf.PI * (1f - (float)i / rings);   // bottom pole first
                for (int j = 0; j <= segments; j++)
                {
                    float phi = (float)j / segments * Mathf.PI * 2f;
                    Vector3 p = center + ax * (Mathf.Sin(theta) * Mathf.Sin(phi))
                                       + ay * Mathf.Cos(theta)
                                       - az * (Mathf.Sin(theta) * Mathf.Cos(phi));
                    pos[i, j] = p;
                    col[i, j] = color;
                    uv2[i, j] = new Vector2(breath, p.y + heightOffset);
                }
            }
            mb.AddGrid(pos, col, uv2, -ay.normalized, ay.normalized);
        }

        private static void AddTorus(GridMesh mb, Vector3 center, Quaternion rotation, float major, float minor,
            Color color, int segments, int sides, float heightOffset)
        {
            var pos = new Vector3[segments + 1, sides + 1];
            var col = new Color[segments + 1, sides + 1];
            var uv2 = new Vector2[segments + 1, sides + 1];
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    Vector3 local = dir * (major + minor * Mathf.Cos(v)) + Vector3.up * (minor * Mathf.Sin(v));
                    Vector3 p = center + rotation * local;
                    pos[i, j] = p;
                    col[i, j] = color;
                    uv2[i, j] = new Vector2(0f, p.y + heightOffset);
                }
            }
            mb.AddGrid(pos, col, uv2, Vector3.up, Vector3.up, wrapRows: true);
        }

        // ---------------------------------------------------------- assets

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            Directory.CreateDirectory(AssetDir);
            string path = $"{AssetDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static class Bezier
        {
            public static Vector3 Point(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            {
                float u = 1f - t;
                return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
            }

            public static Vector3 Tangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            {
                float u = 1f - t;
                return (3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c)).normalized;
            }
        }

        /// <summary>
        /// Accumulates grids of vertices (rows x columns, the last column
        /// repeating the first around a closed shape) with normals from
        /// central differences - smooth across the seam, and taken from the
        /// given fallback where a row collapses to a pole. Triangles are
        /// wound to face along their normals.
        /// </summary>
        private sealed class GridMesh
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Color> _c = new List<Color>();
            private readonly List<Vector2> _uv2 = new List<Vector2>();
            private readonly List<int> _t = new List<int>();

            public void AddGrid(Vector3[,] pos, Color[,] col, Vector2[,] uv2,
                Vector3 firstRowPole, Vector3 lastRowPole, bool wrapRows = false)
            {
                int rows = pos.GetLength(0), cols = pos.GetLength(1);
                int seg = cols - 1;
                int baseIndex = _v.Count;

                // Each row is a closed ring; its mean is the axis point
                // (lathe, ellipsoid), the curve point (tube) or the tube
                // centerline (torus).
                var ringCenter = new Vector3[rows];
                for (int i = 0; i < rows; i++)
                {
                    for (int k = 0; k < seg; k++)
                    {
                        ringCenter[i] += pos[i, k];
                    }
                    ringCenter[i] /= seg;
                }

                // Rows and columns run the same way around the whole grid,
                // so cross(dv, du) points to one consistent side; one vote
                // over all vertices (flat faces abstain) picks outward.
                var normals = new Vector3[rows, cols];
                var isPole = new bool[rows, cols];
                float vote = 0f;
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        int jm = j == 0 ? seg - 1 : j - 1;
                        int jp = j == seg ? 1 : j + 1;
                        int im = wrapRows ? (i == 0 ? rows - 2 : i - 1) : Mathf.Max(i - 1, 0);
                        int ip = wrapRows ? (i == rows - 1 ? 1 : i + 1) : Mathf.Min(i + 1, rows - 1);
                        Vector3 n = Vector3.Cross(pos[ip, j] - pos[im, j], pos[i, jp] - pos[i, jm]);
                        if (n.sqrMagnitude < 1e-12f)
                        {
                            isPole[i, j] = true;
                            continue;
                        }
                        n.Normalize();
                        normals[i, j] = n;
                        vote += Vector3.Dot(n, pos[i, j] - ringCenter[i]);
                    }
                }
                float sign = vote < 0f ? -1f : 1f;

                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        _v.Add(pos[i, j]);
                        _n.Add(isPole[i, j] ? (i == 0 ? firstRowPole : lastRowPole) : normals[i, j] * sign);
                        _c.Add(col[i, j]);
                        _uv2.Add(uv2[i, j]);
                    }
                }
                for (int i = 0; i < rows - 1; i++)
                {
                    for (int j = 0; j < seg; j++)
                    {
                        int a = baseIndex + i * cols + j;
                        int b = a + 1;
                        int c = a + cols + 1;
                        int d = a + cols;
                        AddTriangle(a, b, c);
                        AddTriangle(a, c, d);
                    }
                }
            }

            private void AddTriangle(int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
                if (face.sqrMagnitude < 1e-14f)
                {
                    return;   // degenerate (pole or repeated profile point)
                }
                Vector3 hint = _n[a] + _n[b] + _n[c];
                if (Vector3.Dot(face, hint) < 0f)
                {
                    (b, c) = (c, b);
                }
                _t.Add(a);
                _t.Add(b);
                _t.Add(c);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetColors(_c);
                mesh.SetUVs(1, _uv2);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
