// I'm not doing any *actual* tesselation.
// It's just to access the entire triangle to build the mesh normals and uvs properly.
#pragma vertex vert
#pragma domain domain
#pragma hull hull
#pragma fragment fragShadowCaster

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "UnityStandardShadow.cginc"

#include "VoxelHelpers.cginc"

// Strict superset of VertexInput
struct VertexInputTangent {
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float2 uv0      : TEXCOORD0;
    half3 tangent   : TANGENT;
};

[UNITY_domain("quad")]
void domain(
    TessFactors factors,
    OutputPatch<appdata, 4> patch,
    float2 tessUV : SV_DomainLocation
    
    , out float4 opos : SV_POSITION
    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
    , out VertexOutputShadowCaster o
    #endif
    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
    , out VertexOutputStereoShadowCaster os
    #endif
) {
    VertexInputTangent v;

    float4 pos; float3 normal; float3 tangent; float2 uv; uint material;
    computeVertNormTanUVMaterial(
        /* in  */ patch, tessUV,
        /* out */ pos, normal, tangent, uv, material
    );

    v.vertex = pos;
    v.normal = normal;
    v.uv0 = uv;
    v.tangent = tangent;
    
    vertShadowCaster(
        (VertexInput)v

        , opos
        #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
        , o
        #endif
        #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
        , os
        #endif
    );
}
