#pragma vertex vert
#pragma fragment frag
#pragma target 2.0
#pragma multi_compile_shadowcaster
#include "UnityCG.cginc"
#include "VoxelHelpers.cginc"

// Based on the basic VertexLit shader, but modified to suit my custom input.
// For the macros, see
// https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/6a63f93bc1f20ce6cd47f981c7494e8328915621/CGIncludes/UnityCG.cginc#L930

// The TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) = TRANSFER_SHADOW_CASTER_NOPOS(o,o.pos) is
//     o.vec = mul(unity_ObjectToWorld, v.vertex).xyz - _LightPositionRange.xyz;
//     o.pos = UnityObjectToClipPos(v.vertex);
// when point light `#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)`, and
//                                                                          (Alternatively, without normals:)
//     o.pos = UnityClipSpaceShadowCasterPos(v.vertex, v.normal);           o.pos = UnityObjectToClipPos(v.vertex.xyz);
//     o.pos = UnityApplyLinearShadowBias(opos);                            o.pos = UnityApplyLinearShadowBias(opos);
// when directional or spotlight `#ELSE` (wrt above).

// The SHADOW_CASTER_FRAGMENT(i) is
//     return UnityEncodeCubeShadowDepth ((length(i.vec) + unity_LightShadowBias.x) * _LightPositionRange.w);
// when point light, and
//     return 0;
// when directinoal or spotlight.
// As such, this one doesn't need to be modified.

struct v2f {
    V2F_SHADOW_CASTER;          // float3 vec : TEXCOORD0 or nothing at all
};

v2f vert(appdata v) {
    v2f o;
    float4 vertex;
    uint material;
    unpack(v, vertex, material);

    //TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
    o.vec = mul(unity_ObjectToWorld, vertex) - _LightPositionRange.xyz;
    o.pos = UnityObjectToClipPos(o.vec);
#else
    o.pos = UnityObjectToClipPos(vertex);
    o.pos = UnityApplyLinearShadowBias(o.pos);
#endif

    return o;
}

float4 frag(v2f i) : SV_Target {
    SHADOW_CASTER_FRAGMENT(i)
}