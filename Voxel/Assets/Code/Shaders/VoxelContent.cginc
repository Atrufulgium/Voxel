// I'm not doing any *actual* tesselation.
// It's just to access the entire triangle to build the mesh normals and uvs properly.
#pragma vertex vert
#pragma domain domain
#pragma hull hull
#pragma fragment frag

#include "UnityCG.cginc"
#include "UnityStandardCoreForward.cginc"
#include "AutoLight.cginc"

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
    #if defined(PIXEL_SHADOWS)
        float4 modelPos : TEXCOORD10;
    #endif
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
    // Note the light functions hard-require something called "v"


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

    // Note: these clippos the pos
    #if defined(IS_FORWARD_PASS)
        ret.vertOutput = vertForwardBase(v);
    #elif defined(IS_ADD_PASS)
        ret.vertOutput = vertForwardAdd(v);
    #endif

    #if defined(PIXEL_SHADOWS)
        ret.modelPos = pos;
    #endif
    ret.material = material;
    ret.normalModel = normal;
    ret.normalWorld = UnityObjectToWorldNormal(normal);
    ret.texUV = uv;
    return ret;
}

SamplerState sampler_VoxelTex;
Texture2DArray _VoxelTex;

struct VertexContainer {
    float4 vertex;
};

fixed4 frag (v2f i) : SV_Target {
    // Apply to a pure white texture (the default). This gives the light info.
    #if defined(IS_FORWARD_PASS)
        #if defined(PIXEL_SHADOWS)
            // Make the shadow-map be sampled not smoothly but pixelated.
            // For that, we need to re-calculate where we should read the
            // relevant lighting maps, as the interpolated coords are no good.
            // Lighting functions hard-require a v.vertex.
            // These ones specifically in model space, see AutoLight.cginc:23.
            VertexContainer v;
            v.vertex = (floor(8 * i.modelPos) + 0.5) / 8; //pixelate here
            float4 oldPos = i.vertOutput.pos;
            i.vertOutput.pos = UnityObjectToClipPos(v.vertex);
            COMPUTE_LIGHT_COORDS(i.vertOutput);
            TRANSFER_SHADOW(i.vertOutput);
            i.vertOutput.pos = oldPos;
        #endif
        // Hooray we can leave the remainder to Unity
        float3 lighting = fragForwardBaseInternal(i.vertOutput);
    #elif defined(IS_ADD_PASS)
        #if defined(PIXEL_SHADOWS)
            // The same as the above
            VertexContainer v;
            v.vertex = (floor(8 * i.modelPos) + 0.5) / 8;
            float4 oldPos = i.vertOutput.pos;
            i.vertOutput.pos = UnityObjectToClipPos(v.vertex);
            COMPUTE_LIGHT_COORDS(i.vertOutput);
            TRANSFER_SHADOW(i.vertOutput);
            i.vertOutput.pos = oldPos;
        #endif
        float3 lighting = fragForwardAddInternal(i.vertOutput);
    #endif

    // Now apply our custom texture.
    float3 texture_uv = float3(getUVs(i.normalModel, i.texUV), 0);
    texture_uv.z = i.material;
    float3 c = _VoxelTex.Sample(sampler_VoxelTex, texture_uv).rgb;
    
    return float4(c * lighting, 1);
}