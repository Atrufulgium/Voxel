// I'm not doing any *actual* tesselation.
// It's just to access the entire triangle to build the mesh normals and uvs properly.
#pragma vertex vert
#pragma domain domain
#pragma hull hull
#pragma fragment frag

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "UnityStandardCoreForward.cginc"

#include "VoxelHelpers.cginc"

struct v2f {
    #if defined(IS_FORWARD_PASS)
        VertexOutputForwardBase vertOutput;
    #elif defined(IS_ADD_PASS)
        VertexOutputForwardAdd vertOutput;
    #endif
    nointerpolation uint material : BLENDINDICES;
    float3 normalWorld : NORMAL;
    float3 normalModel : NORMAL1;
    float2 texUV : TEXCOORD9;
};

[UNITY_domain("quad")]
v2f domain(
    TessFactors factors,
    OutputPatch<appdata, 4> patch,
    float2 tessUV : SV_DomainLocation
) {
    VertexInput v;
    // v.vertex : POSITION;
    // v.normal : NORMAL;
    // v.uv0, uv1 : TEXCOORD0,1;
    // v.uv2 : TEXCOORD2 if DYNAMICLIGHTMAP_ON || UNITY_PASS_META;
    // tangent : TANGENT if _TANGENT_TO_WORLD.


    float4 pos; float3 normal; float3 tangent; float2 uv; uint material;
    computeVertNormTanUVMaterial(
        /* in  */ patch, tessUV,
        /* out */ pos, normal, tangent, uv, material
    );
    
    v.vertex = pos;
    v.normal = normal;
    v.uv0 = uv;
    v.uv1 = 0;
    #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
        v.uv2 = 0;
    #endif
    #if defined(_TANGENT_TO_WORLD)
        v.tangent = tangent;
    #endif

    v2f ret;

    #if defined(IS_FORWARD_PASS)
        ret.vertOutput = vertForwardBase(v);
    #elif defined(IS_ADD_PASS)
        ret.vertOutput = vertForwardAdd(v);
    #endif

    ret.material = material;
    ret.normalModel = normal;
    ret.normalWorld = UnityObjectToWorldNormal(normal);
    ret.texUV = uv;
    return ret;
}

SamplerState sampler_VoxelTex;
Texture2DArray _VoxelTex;

fixed4 frag (v2f i) : SV_Target {
    // Apply to a pure white texture (the default). This gives the light info.
    #if defined(IS_FORWARD_PASS)
        float3 lighting = fragForwardBaseInternal(i.vertOutput).x;
    #elif defined(IS_ADD_PASS)
        float3 lighting = fragForwardAddInternal(i.vertOutput);
    #endif

    // Now apply our custom texture.
    float3 texture_uv = float3(getUVs(i.normalModel, i.texUV), 0);
    texture_uv.z = i.material;
    float3 c = _VoxelTex.Sample(sampler_VoxelTex, texture_uv).rgb;
    
    return float4(c * lighting, 1);
}