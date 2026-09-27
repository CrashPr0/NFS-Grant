using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Code-generated potted tree for the hall's doorways and room entrances:
    /// a lathed ceramic pot (foot, bellied body, brass band, rolled rim, soil),
    /// a recursively branching trunk, and leaf-card clusters at the branch
    /// tips shaded by NSFGrant/StylizedLeaves (alpha-cut leaf cards with
    /// spherical normals, cel bands, backlit subsurface glow). Wood and pot
    /// use NSFGrant/StylizedSolid.
    ///
    /// One fixed seed, so every doorway and room gets the identical tree
    /// (rooms must stay identical; see CounterbalanceManager). The meshes and
    /// the leaf-cluster sprite are baked once per scene build into
    /// Assets/StudyContent (gitignored, regenerated) and shared by every
    /// planter, so all trees batch.
    /// </summary>
    public static class ProceduralPlanter
    {
        private const int TreeSeed = 20260927;
        private const string AssetDir = "Assets/StudyContent";

        // Soil surface height; the trunk starts just below it.
        private const float SoilY = 0.40f;
        // Crown footprint limit: planters stand 0.5 m from walls and next to
        // zone panels, so the canopy may not spread wider than this.
        private const float MaxCrownRadius = 0.44f;
        private const int MaxDepth = 2;

        private static Mesh _pot, _wood, _leaves;
        private static Material _potMaterial, _woodMaterial, _leafMaterial;

        /// <summary>Drops cached assets; call at the start of every scene build.</summary>
        public static void Reset()
        {
            _pot = _wood = _leaves = null;
            _potMaterial = _woodMaterial = _leafMaterial = null;
        }

        /// <summary>
        /// Places a planter. <paramref name="mirror"/> flips it left-right, so
        /// a pair framing a doorway is symmetric (and lit symmetrically: the
        /// shaders' key light is fixed in the planter's own frame, on its
        /// local -Z side - turn it with <paramref name="yawDegrees"/> so that
        /// side faces where visitors approach from).
        /// </summary>
        public static GameObject Create(Transform parent, Vector3 localPos, bool mirror, float yawDegrees = 0f)
        {
            EnsureBuilt();
            var planter = new GameObject("Planter");
            planter.transform.SetParent(parent, false);
            planter.transform.localPosition = localPos;
            planter.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            planter.transform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            AddPart(planter.transform, "Pot", _pot, _potMaterial);
            AddPart(planter.transform, "Wood", _wood, _woodMaterial);
            AddPart(planter.transform, "Leaves", _leaves, _leafMaterial);
            return planter;
        }

        private static void AddPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // The stylized shaders have no shadow pass and ignore probes.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void EnsureBuilt()
        {
            if (_pot != null)
            {
                return;
            }

            var rng = new Random(TreeSeed);
            var tips = new List<Tip>();
            _wood = SaveMesh(BuildWood(rng, tips), "DiscoveryHallPlanterWood");
            CrownEllipsoid(tips, out Vector3 crownCenter, out Vector2 crownRadii);
            _leaves = SaveMesh(BuildLeaves(rng, tips, crownCenter, crownRadii), "DiscoveryHallPlanterLeaves");
            _pot = SaveMesh(BuildPot(rng), "DiscoveryHallPlanterPot");
            var leafTexture = SaveTexture(BakeLeafCluster(rng), "DiscoveryHallPlanterLeafCluster");

            _potMaterial = MakeMaterial("NSFGrant/StylizedSolid", "PlanterPot", m =>
            {
                m.SetFloat("_Gloss", 1f);
                m.SetFloat("_WindStrength", 0f);
            });
            _woodMaterial = MakeMaterial("NSFGrant/StylizedSolid", "PlanterWood", m =>
            {
                m.SetFloat("_BarkStreaks", 1f);
                m.SetFloat("_WindStrength", 0.015f);
            });
            _leafMaterial = MakeMaterial("NSFGrant/StylizedLeaves", "PlanterLeaves", m =>
            {
                m.SetTexture("_MainTex", leafTexture);
                m.SetVector("_CrownCenter", crownCenter);
                m.SetVector("_CrownRadii", new Vector4(crownRadii.x, crownRadii.y, 0f, 0f));
            });
        }

        private static Material MakeMaterial(string shaderName, string name, System.Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[ProceduralPlanter] {shaderName} not found; planter part will be untextured.");
                shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                return new Material(shader) { name = name };
            }
            var material = new Material(shader) { name = name, enableInstancing = true };
            setup(material);
            return material;
        }

        // ------------------------------------------------------------ wood

        private struct Tip
        {
            public Vector3 Position;
            public float Radius;
            public Tip(Vector3 position, float radius) { Position = position; Radius = radius; }
        }

        private static Mesh BuildWood(Random rng, List<Tip> tips)
        {
            var mb = new MeshBuilder();
            Vector3 lean = RandomPerpendicular(Vector3.up, rng) * 0.06f;
            Grow(mb, rng, tips, new Vector3(0f, SoilY - 0.03f, 0f), (Vector3.up + lean).normalized,
                 0.5f, 0.044f, 0);
            FitCrown(mb, tips);
            // Round off the top of the crown: one cluster above the others'
            // middle (hides the twigs' ends from above too).
            Vector3 middle = Vector3.zero;
            float top = float.MinValue;
            foreach (var tip in tips)
            {
                middle += tip.Position;
                top = Mathf.Max(top, tip.Position.y);
            }
            middle /= tips.Count;
            tips.Add(new Tip(new Vector3(middle.x, top + 0.02f, middle.z), 0.18f));
            return mb.ToMesh("PlanterWood");
        }

        /// <summary>
        /// One branch as a tapered tube along a gently bent cubic curve, then
        /// its children: three limbs off the trunk, two twigs off each limb,
        /// each tilted out and nudged upward (a potted tree reaching for
        /// light). Twig ends carry the big leaf clusters; limb ends carry
        /// smaller ones that fill the crown's middle.
        /// </summary>
        private static void Grow(MeshBuilder mb, Random rng, List<Tip> tips, Vector3 start,
            Vector3 dir, float length, float radius, int depth)
        {
            Vector3 side = RandomPerpendicular(dir, rng);
            Vector3 end = start + dir * length + side * (length * 0.16f) + Vector3.up * (length * 0.06f);
            Vector3 c1 = start + dir * (length * 0.4f) - side * (length * 0.05f);
            Vector3 c2 = Vector3.Lerp(start, end, 0.72f) + side * (length * 0.1f);
            bool isTip = depth == MaxDepth;
            float endRadius = radius * (isTip ? 0.3f : 0.68f);

            AddTube(mb, rng, start, c1, c2, end, radius, endRadius,
                    sides: depth == 0 ? 9 : depth == 1 ? 7 : 6,
                    rings: depth == 0 ? 8 : 5,
                    flare: depth == 0 ? 1.6f : 1f,
                    joint: depth > 0);

            if (isTip)
            {
                tips.Add(new Tip(end, 0.17f + (float)rng.NextDouble() * 0.04f));
                return;
            }

            Vector3 endDir = (end - c2).normalized;
            int children = depth == 0 ? 3 : 2;
            float azimuth0 = (float)rng.NextDouble() * 360f;
            Vector3 basis = RandomPerpendicular(endDir, rng);
            for (int i = 0; i < children; i++)
            {
                float azimuth = azimuth0 + i * 360f / children + Jitter(rng, 18f);
                float tilt = ((depth == 0 ? 36f : 30f) + Jitter(rng, 7f)) * Mathf.Deg2Rad;
                Vector3 perp = Quaternion.AngleAxis(azimuth, endDir) * basis;
                Vector3 childDir = (endDir * Mathf.Cos(tilt) + perp * Mathf.Sin(tilt)).normalized;
                childDir = Vector3.Slerp(childDir, Vector3.up, 0.2f).normalized;
                Grow(mb, rng, tips, end - endDir * (endRadius * 0.8f), childDir,
                     length * 0.68f, endRadius, depth + 1);
            }
            if (depth == 1)
            {
                tips.Add(new Tip(end + endDir * 0.04f, 0.14f));
            }
        }

        private static void AddTube(MeshBuilder mb, Random rng, Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3 p3, float r0, float r1, int sides, int rings, float flare, bool joint)
        {
            float vOffset = (float)rng.NextDouble() * 10f;   // bark pattern differs per branch
            Vector3 prevTangent = Bezier.Tangent(p0, p1, p2, p3, 0f);
            Vector3 frameN = Vector3.Cross(prevTangent, Mathf.Abs(prevTangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float along = 0f;
            Vector3 prevPoint = p0;
            int baseIndex = mb.Count;

            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                Vector3 point = Bezier.Point(p0, p1, p2, p3, t);
                Vector3 tangent = Bezier.Tangent(p0, p1, p2, p3, t);
                // Parallel transport: rotate the frame with the tangent so the
                // tube doesn't twist.
                frameN = Quaternion.FromToRotation(prevTangent, tangent) * frameN;
                prevTangent = tangent;
                Vector3 frameB = Vector3.Cross(tangent, frameN);
                along += Vector3.Distance(prevPoint, point);
                prevPoint = point;

                float radius = Mathf.Lerp(r0, r1, t) * (1f + (flare - 1f) * Mathf.Pow(1f - t, 6f));
                // Darker at the trunk base and where a branch leaves its parent.
                float occlusion = joint ? Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 1f, t * 4f))
                                        : Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, t * 3f));
                Color bark = Color.Lerp(new Color(0.19f, 0.14f, 0.1f), new Color(0.29f, 0.21f, 0.14f), t * 0.5f + (joint ? 0.3f : 0f));
                bark *= occlusion;
                bark.a = 0f;

                for (int j = 0; j <= sides; j++)
                {
                    float angle = (float)j / sides * Mathf.PI * 2f;
                    Vector3 normal = frameN * Mathf.Cos(angle) + frameB * Mathf.Sin(angle);
                    mb.Add(point + normal * radius, normal, bark,
                           new Vector2((float)j / sides, vOffset + along * 2.5f));
                }
            }
            mb.AddGrid(baseIndex, rings, sides);
        }

        /// <summary>
        /// Squeezes the whole tree toward the trunk axis if its crown would
        /// spread past <see cref="MaxCrownRadius"/> (normals get the inverse
        /// scale, so shading stays right).
        /// </summary>
        private static void FitCrown(MeshBuilder mb, List<Tip> tips)
        {
            float extent = 0f;
            foreach (var tip in tips)
            {
                extent = Mathf.Max(extent, new Vector2(tip.Position.x, tip.Position.z).magnitude + tip.Radius);
            }
            if (extent <= MaxCrownRadius)
            {
                return;
            }
            float s = MaxCrownRadius / extent;
            mb.ScaleXZ(s);
            for (int i = 0; i < tips.Count; i++)
            {
                var p = tips[i].Position;
                tips[i] = new Tip(new Vector3(p.x * s, p.y, p.z * s), tips[i].Radius * Mathf.Lerp(s, 1f, 0.5f));
            }
        }

        // ---------------------------------------------------------- leaves

        /// <summary>
        /// Center and radii (horizontal, vertical) of the ellipsoid around all
        /// leaf clusters; the leaf shader lights the crown as this volume.
        /// </summary>
        private static void CrownEllipsoid(List<Tip> tips, out Vector3 center, out Vector2 radii)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var tip in tips)
            {
                min = Vector3.Min(min, tip.Position - Vector3.one * tip.Radius);
                max = Vector3.Max(max, tip.Position + Vector3.one * tip.Radius);
            }
            center = (min + max) * 0.5f;
            radii = new Vector2(Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f, (max.y - min.y) * 0.5f);
        }

        /// <summary>
        /// Leaf cards scattered through each tip's cluster volume, biased to
        /// the outside. Each card is stored as four vertices at its center;
        /// NSFGrant/StylizedLeaves expands them toward the viewer (UV =
        /// corner, UV2 = half size and spin). Vertex color: r occlusion
        /// (inner cards darker), g tint, b flutter phase, a outerness
        /// (flutter/subsurface weight). Normals point out of the crown, for
        /// tools; the shader derives its own from the crown ellipsoid.
        /// </summary>
        private static Mesh BuildLeaves(Random rng, List<Tip> tips, Vector3 crown, Vector2 crownRadii)
        {
            var mb = new MeshBuilder();
            foreach (var tip in tips)
            {
                int cards = Mathf.RoundToInt(tip.Radius * 140f);
                for (int k = 0; k < cards; k++)
                {
                    Vector3 dir = RandomUnitVector(rng);
                    float outer = Mathf.Sqrt((float)rng.NextDouble());
                    float dist = tip.Radius * Mathf.Lerp(0.15f, 0.85f, outer);
                    Vector3 center = tip.Position + Vector3.Scale(dir * dist, new Vector3(1f, 0.8f, 1f))
                                     + Vector3.up * (tip.Radius * 0.15f);
                    float half = tip.Radius * Mathf.Lerp(0.5f, 0.72f, (float)rng.NextDouble());
                    float spin = (float)rng.NextDouble() * Mathf.PI * 2f;

                    Vector3 fromCrown = new Vector3((center.x - crown.x) / crownRadii.x,
                                                    (center.y - crown.y) / crownRadii.y,
                                                    (center.z - crown.z) / crownRadii.x);
                    float occlusion = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, outer) * Mathf.Lerp(0.6f, 1.05f, fromCrown.magnitude));
                    var color = new Color(occlusion, (float)rng.NextDouble(), (float)rng.NextDouble(), outer);
                    Vector3 normal = fromCrown.sqrMagnitude > 1e-6f ? fromCrown.normalized : Vector3.up;

                    int index = mb.Count;
                    for (int c = 0; c < 4; c++)
                    {
                        var uv = new Vector2(c == 1 || c == 2 ? 1f : 0f, c >= 2 ? 1f : 0f);
                        mb.Add(center, normal, color, uv, new Vector2(half, spin));
                    }
                    mb.AddQuadUnchecked(index, index + 1, index + 2, index + 3);
                }
            }
            var mesh = mb.ToMesh("PlanterLeaves");
            // Vertices sit at card centers: grow the bounds by the largest
            // card plus room for the breeze, so the crown never culls early.
            var bounds = mesh.bounds;
            bounds.Expand(0.5f);
            mesh.bounds = bounds;
            return mesh;
        }

        /// <summary>
        /// 256x256 leaf-clump sprite: small pointed leaves scattered at random
        /// angles through a disc (a leaf mass, not a rosette), back ones
        /// darker, all inside a circle so a card's square edge never shows.
        /// RGB is a gray shade the shader multiplies into its greens; A is
        /// the cut-out mask.
        /// </summary>
        private static Texture2D BakeLeafCluster(Random rng)
        {
            const int size = 256;
            var shade = new float[size * size];
            var alpha = new float[size * size];
            for (int i = 0; i < shade.Length; i++) shade[i] = 0.8f;

            var center = new Vector2(0.5f, 0.5f);
            const int leafCount = 20;
            for (int i = 0; i < leafCount; i++)
            {
                // Earlier leaves are "behind": darker.
                float depth = (float)i / (leafCount - 1);
                float angle = (float)rng.NextDouble() * 360f;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 axis = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                float length = Mathf.Lerp(0.2f, 0.27f, (float)rng.NextDouble());
                float width = Mathf.Lerp(0.12f, 0.16f, (float)rng.NextDouble());
                // Leaf midpoints spread through a disc; outer leaves point
                // roughly outward so the clump's edge is leafy, not clipped.
                float ring = Mathf.Sqrt((float)rng.NextDouble()) * 0.24f;
                float at = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector2 mid = center + new Vector2(Mathf.Cos(at), Mathf.Sin(at)) * ring;
                if (ring > 0.12f)
                {
                    Vector2 outwardDir = (mid - center).normalized;
                    axis = (axis * 0.4f + outwardDir).normalized;
                    angle = Mathf.Atan2(axis.x, axis.y) * Mathf.Rad2Deg;
                }
                Vector2 origin = mid - axis * (length * 0.5f);
                // Keep the tip (plus a margin) inside radius 0.46.
                float reach = Vector2.Distance(center, origin + axis * length) + width * 0.25f;
                if (reach > 0.46f) length -= reach - 0.46f;
                DrawLeaf(shade, alpha, size, origin, angle, length, width,
                         Mathf.Lerp(0.6f, 1f, depth) * Mathf.Lerp(0.9f, 1f, (float)rng.NextDouble()));
            }

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade[i]) * 255f);
                pixels[i] = new Color32(g, g, g, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "PlanterLeafCluster",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>
        /// Paints one leaf (pointed at the tip, rounded at the base) growing
        /// from <paramref name="origin"/> at <paramref name="angleDeg"/> from
        /// vertical, anti-aliased, over what is already painted.
        /// </summary>
        private static void DrawLeaf(float[] shade, float[] alpha, int size, Vector2 origin,
            float angleDeg, float length, float width, float brightness)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            var axis = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            var across = new Vector2(axis.y, -axis.x);
            Vector2 tipPos = origin + axis * length;
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(origin.x, tipPos.x) - width) * size));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(origin.x, tipPos.x) + width) * size));
            int minY = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(origin.y, tipPos.y) - width) * size));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(origin.y, tipPos.y) + width) * size));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) - origin;
                    float t = Vector2.Dot(p, axis) / length;
                    if (t < 0f || t > 1f)
                    {
                        continue;
                    }
                    float halfWidth = width * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f) * (1f - 0.3f * t);
                    float s = Mathf.Abs(Vector2.Dot(p, across));
                    float coverage = Mathf.Clamp01(0.5f - (s - halfWidth) * size);
                    if (coverage <= 0f)
                    {
                        continue;
                    }
                    float across01 = s / Mathf.Max(halfWidth, 1e-4f);
                    float rib = Mathf.SmoothStep(0f, 1f, across01 * 3f);
                    float edge = Mathf.SmoothStep(0.75f, 1f, across01);
                    float leafShade = brightness * Mathf.Lerp(0.72f, 1f, rib) * Mathf.Lerp(1f, 0.8f, edge)
                                      * Mathf.Lerp(0.9f, 1.05f, t);
                    int i = y * size + x;
                    shade[i] = Mathf.Lerp(shade[i], leafShade, coverage);
                    alpha[i] = Mathf.Max(alpha[i], coverage);
                }
            }
        }

        // ------------------------------------------------------------- pot

        private struct ProfilePoint
        {
            public float R, Y;
            public Color Color;   // a = glaze gloss
            public ProfilePoint(float r, float y, Color color) { R = r; Y = y; Color = color; }
        }

        /// <summary>
        /// Ceramic pot as a surface of revolution: dark foot, bellied glazed
        /// body with a raised brass band, a rolled rim, the inner wall, and a
        /// slightly domed soil top. Normals are smooth along the profile
        /// except at real corners (the foot step), where the ring is split.
        /// </summary>
        private static Mesh BuildPot(Random rng)
        {
            var foot = new Color(0.1f, 0.1f, 0.11f, 0.15f);
            var glaze = new Color(0.16f, 0.24f, 0.25f, 1f);
            var brass = new Color(0.78f, 0.6f, 0.32f, 1f);
            var inner = new Color(0.07f, 0.06f, 0.06f, 0f);
            // Glaze darkens toward the foot, grounding the pot on the floor
            // (the stylized shaders don't receive shadows).
            var glazeLow = new Color(glaze.r * 0.55f, glaze.g * 0.55f, glaze.b * 0.55f, 1f);
            var glazeMid = Color.Lerp(glazeLow, glaze, 0.6f);
            var profile = new[]
            {
                new ProfilePoint(0.000f, 0.000f, foot),
                new ProfilePoint(0.150f, 0.000f, foot),
                new ProfilePoint(0.168f, 0.012f, foot),
                new ProfilePoint(0.165f, 0.040f, foot),
                new ProfilePoint(0.176f, 0.050f, glazeLow),
                new ProfilePoint(0.205f, 0.120f, glazeMid),
                new ProfilePoint(0.228f, 0.210f, glaze),
                new ProfilePoint(0.238f, 0.300f, glaze),
                new ProfilePoint(0.236f, 0.348f, glaze),
                new ProfilePoint(0.240f, 0.351f, brass),
                new ProfilePoint(0.241f, 0.366f, brass),
                new ProfilePoint(0.236f, 0.370f, glaze),
                new ProfilePoint(0.232f, 0.392f, glaze),
                new ProfilePoint(0.250f, 0.405f, glaze),
                new ProfilePoint(0.258f, 0.420f, glaze),
                new ProfilePoint(0.250f, 0.433f, glaze),
                new ProfilePoint(0.232f, 0.434f, glaze),
                new ProfilePoint(0.222f, 0.425f, inner),
                new ProfilePoint(0.214f, 0.380f, inner),
            };

            var mb = new MeshBuilder();
            const int segments = 40;
            Lathe(mb, profile, segments);

            // Soil: a shallow dome inside the rim, mottled.
            const int soilRings = 8;
            int soilBase = mb.Count;
            for (int ring = 0; ring <= soilRings; ring++)
            {
                float t = (float)ring / soilRings;
                float r = 0.217f * t;
                float y = Mathf.Lerp(SoilY + 0.012f, SoilY - 0.002f, t * t);
                for (int j = 0; j <= segments; j++)
                {
                    float angle = (float)j / segments * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var normal = (Vector3.up + dir * (0.1f * t)).normalized;
                    Vector3 sp = dir * r;
                    float mottle = 0.95f + 0.12f * Mathf.Sin(sp.x * 61f + sp.z * 17f) * Mathf.Sin(sp.z * 53f - sp.x * 29f)
                                   + 0.06f * Mathf.Sin(sp.x * 131f - sp.z * 97f);
                    var soil = new Color(0.2f * mottle, 0.14f * mottle, 0.1f * mottle, 0f);
                    mb.Add(dir * r + Vector3.up * y, normal, soil, new Vector2((float)j / segments, t));
                }
            }
            mb.AddGrid(soilBase, soilRings, segments);
            return mb.ToMesh("PlanterPot");
        }

        private static void Lathe(MeshBuilder mb, ProfilePoint[] profile, int segments)
        {
            int n = profile.Length;
            var segmentNormals = new Vector2[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                var d = new Vector2(profile[i + 1].R - profile[i].R, profile[i + 1].Y - profile[i].Y);
                segmentNormals[i] = new Vector2(d.y, -d.x).normalized;   // outward in (r, y)
            }

            // Rings: one per profile point, or two (split normals) at corners.
            var rings = new List<(int point, Vector2 normal)>();
            for (int i = 0; i < n; i++)
            {
                if (i == 0) { rings.Add((i, segmentNormals[0])); continue; }
                if (i == n - 1) { rings.Add((i, segmentNormals[n - 2])); continue; }
                Vector2 a = segmentNormals[i - 1], b = segmentNormals[i];
                if (Vector2.Angle(a, b) > 50f)
                {
                    rings.Add((i, a));
                    rings.Add((i, b));
                }
                else
                {
                    rings.Add((i, (a + b).normalized));
                }
            }

            int baseIndex = mb.Count;
            foreach (var (point, normal2) in rings)
            {
                var p = profile[point];
                for (int j = 0; j <= segments; j++)
                {
                    float angle = (float)j / segments * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var normal = (dir * normal2.x + Vector3.up * normal2.y).normalized;
                    mb.Add(dir * p.R + Vector3.up * p.Y, normal, p.Color, new Vector2((float)j / segments, p.Y));
                }
            }
            for (int k = 0; k < rings.Count - 1; k++)
            {
                if (rings[k].point == rings[k + 1].point)
                {
                    continue;   // the two halves of a split corner: no quads between them
                }
                mb.AddGridBand(baseIndex + k * (segments + 1), baseIndex + (k + 1) * (segments + 1), segments);
            }
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

        private static Texture2D SaveTexture(Texture2D texture, string name)
        {
            Directory.CreateDirectory(AssetDir);
            string path = $"{AssetDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            texture.name = name;
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        // ---------------------------------------------------------- helpers

        private static float Jitter(Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;

        private static Vector3 RandomUnitVector(Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        private static Vector3 RandomPerpendicular(Vector3 v, Random rng)
        {
            Vector3 any = Vector3.Cross(v, Mathf.Abs(v.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            return Quaternion.AngleAxis((float)rng.NextDouble() * 360f, v) * any;
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

        /// <summary>Vertex/triangle lists with outward-facing triangle helpers.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<Vector2> _uvs2 = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            public int Count => _vertices.Count;

            public void Add(Vector3 position, Vector3 normal, Color color, Vector2 uv, Vector2 uv2 = default)
            {
                _vertices.Add(position);
                _normals.Add(normal);
                _colors.Add(color);
                _uvs.Add(uv);
                _uvs2.Add(uv2);
            }

            /// <summary>Triangle wound so it faces along its vertices' normals.</summary>
            private void AddTriangle(int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
                Vector3 hint = _normals[a] + _normals[b] + _normals[c];
                if (Vector3.Dot(face, hint) < 0f)
                {
                    (b, c) = (c, b);
                }
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            /// <summary>Quad in the given order (for camera-facing cards, whose
            /// vertices coincide until the shader expands them).</summary>
            public void AddQuadUnchecked(int a, int b, int c, int d)
            {
                _triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            public void AddQuad(int a, int b, int c, int d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }

            /// <summary>Quads between two rings of (segments + 1) vertices.</summary>
            public void AddGridBand(int ringA, int ringB, int segments)
            {
                for (int j = 0; j < segments; j++)
                {
                    AddQuad(ringA + j, ringA + j + 1, ringB + j + 1, ringB + j);
                }
            }

            /// <summary>Quads over (rings + 1) consecutive rings of (segments + 1) vertices.</summary>
            public void AddGrid(int baseIndex, int rings, int segments)
            {
                for (int i = 0; i < rings; i++)
                {
                    AddGridBand(baseIndex + i * (segments + 1), baseIndex + (i + 1) * (segments + 1), segments);
                }
            }

            public void ScaleXZ(float s)
            {
                for (int i = 0; i < _vertices.Count; i++)
                {
                    var v = _vertices[i];
                    _vertices[i] = new Vector3(v.x * s, v.y, v.z * s);
                    var n = _normals[i];
                    _normals[i] = new Vector3(n.x / s, n.y, n.z / s).normalized;
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetUVs(0, _uvs);
                mesh.SetUVs(1, _uvs2);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
