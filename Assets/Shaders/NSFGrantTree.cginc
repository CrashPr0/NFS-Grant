// Shared by NSFGrant/StylizedLeaves and NSFGrant/StylizedSolid (the
// procedural potted trees from ProceduralPlanter) so the leaves and the
// branches they grow from sway with exactly the same displacement, and
// both are lit by the same rig.
#ifndef NSFGRANT_TREE_INCLUDED
#define NSFGRANT_TREE_INCLUDED

float _WindStrength;   // metres of sway at the top of the crown
float _WindSpeed;

// Sin-free hash (Dave Hoskins), stable on mobile GPUs.
float TreeHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Trilinear value noise in [0,1].
float TreeNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(TreeHash13(i),                   TreeHash13(i + float3(1, 0, 0)), u.x),
                     lerp(TreeHash13(i + float3(0, 1, 0)), TreeHash13(i + float3(1, 1, 0)), u.x), u.y),
                lerp(lerp(TreeHash13(i + float3(0, 0, 1)), TreeHash13(i + float3(1, 0, 1)), u.x),
                     lerp(TreeHash13(i + float3(0, 1, 1)), TreeHash13(i + float3(1, 1, 1)), u.x), u.y), u.z);
}

float3 TreeOrigin()
{
    return float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
}

// Slow breeze, zero at the trunk base and growing with height, so the pot
// and trunk stay planted. The phase comes from the planter's floor
// position, which the wood and leaf renderers share (both sit at the
// planter pivot), so a tree moves as one piece.
float3 TreeWind(float3 worldPos, float3 origin)
{
    float h = saturate((worldPos.y - origin.y - 0.6) / 1.1);
    h *= h;
    float phase = dot(origin.xz, float2(0.61, 0.43));
    float t = _Time.y * _WindSpeed;
    float2 sway = float2(sin(t + phase), sin(t * 0.77 + phase * 1.7 + 1.3));
    return float3(sway.x, 0.0, sway.y) * (_WindStrength * h);
}

// Key light fixed in the planter's own frame (up, front and to one side),
// not in world space: every planter is placed the same way relative to
// its doorway, so every doorway's trees look identical (no room gets
// better-lit greenery). Pipeline-independent like NSFGrant/HandShaded.
float3 TreeKeyDir()
{
    return normalize(mul((float3x3)unity_ObjectToWorld, float3(0.45, 0.75, -0.5)));
}

static const float3 kTreeKeyColor = float3(1.0, 0.95, 0.86);

#endif
