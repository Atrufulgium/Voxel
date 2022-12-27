#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fwdbase
#include "AutoLight.cginc"
#include "UnityCG.cginc"
#include "VoxelHelpers.cginc"

struct v2f {
    // This name for shading :(
    float4 pos : SV_POSITION;
    float4 vertex_object_space : TEXCOORD0;
    float3 vertex_world_space : TEXCOORD1;
    nointerpolation uint material : TEXCOORD2;
    // 3,4 for first free texcoords.
    // See also https://forum.unity.com/threads/adding-shadows-to-custom-shader-vert-frag.108612/#post-719200
    LIGHTING_COORDS(3,4)
};

// Needed for shading, hackily enough.
struct aVertex {
    float4 vertex;
};

SamplerState sampler_VoxelTex;
Texture2DArray _VoxelTex;

v2f vert (appdata a) {
    v2f o;
    // Unpack
    unpack(a, o.vertex_object_space, o.material);

    // And prep for the frag shader
    o.pos = UnityObjectToClipPos(o.vertex_object_space);
    o.vertex_world_space = mul(unity_ObjectToWorld, o.vertex_object_space);

    // Also emulate AutoLight's TRANSFER_VERTEX_TO_FRAGMENT(o);
    // For this, we need a `v` with only a member `vertex`
    aVertex v;
    v.vertex = o.pos;
    TRANSFER_VERTEX_TO_FRAGMENT(o);
    return o;
}

fixed4 frag (v2f i) : SV_Target {
    float3 normal;
    float2 uv;
    FACE face;
    get_normals_uvs(i.vertex_object_space, i.vertex_world_space, normal, uv, face);
    // 𝘕𝘰𝘸 𝘸𝘦 𝘤𝘢𝘯 𝘧𝘪𝘯𝘢𝘭𝘭𝘺 𝘨𝘦𝘵 𝘵𝘰 𝘥𝘰 𝘳𝘦𝘨𝘶𝘭𝘢𝘳 𝘴𝘩𝘪𝘵

    float3 texture_uv = float3(uv, 0);
    texture_uv.x = (uv.x + face) / 6;
    texture_uv.z = i.material;

    float3 light_vector = normalize(float3(1,3,2));
    float ambient = 0.3;
    float atten = LIGHT_ATTENUATION(i); 
    float3 c = atten.xxx + 0.1; //(ambient + atten*(1 - ambient)*dot(normal, light_vector)) * float3(uv * 0.5 + 0.5, i.material * 0.5);
    c *= _VoxelTex.Sample(sampler_VoxelTex, texture_uv).rgb;

    return float4(c, 1);
}