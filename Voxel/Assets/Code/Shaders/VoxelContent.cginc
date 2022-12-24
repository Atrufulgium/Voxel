#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"

// See also the Chunk.Vertex struct over in c#-land's Chunk.cs.
struct appdata {
    // Contained in the factors, you get:
    //   #1: factor 33: x-position 0, .., 32
    //   #2: factor 33: y-position 0, .., 32
    //   #3: factor 33: z-position 0, .., 32
    //   #4: remainder:   material 0, .., 119513
    uint data : BLENDINDICES;
};

struct v2f {
    float4 vertex : SV_POSITION;
    float4 vertex_object_space : TEXCOORD0;
    float3 vertex_world_space : TEXCOORD1;
    nointerpolation uint material : TEXCOORD2;
};

sampler2D _MainTex;
float4 _MainTex_ST;

v2f vert (appdata v) {
    v2f o;
    // Unpack
    o.vertex_object_space.x = v.data % 33;
    v.data /= 33;
    o.vertex_object_space.y = v.data % 33;
    v.data /= 33;
    o.vertex_object_space.z = v.data % 33;
    v.data /= 33;
    o.vertex_object_space.w = 1;

    o.material = v.data;

    // And prep for the frag shader
    o.vertex = UnityObjectToClipPos(o.vertex_object_space);
    o.vertex_world_space = mul(unity_ObjectToWorld, o.vertex_object_space);
    return o;
}

fixed4 frag (v2f i) : SV_Target {
    // Calculate the uvs from the (*,*,0)/(*,0,*)/(0,*,*) derivs that tell us
    // what this quad looks like. Even though the vector values are camera
    // dependent, whether it's zero or an * isn't. (..Mostly)
    // So yes, those are actual zeroes, because we work with voxels/planes.
    // (Do note that we need *both* ddx and ddy because floating point
    //  instabilities occasionally reported multiple flat directions with only
    //  ddx when looking at it from straight ahead. We can't have both unstable
    //  at the same time.)
    float3 frac_obj_space = frac(i.vertex_object_space);
    int3 flat = (ddx(frac_obj_space) == 0) * (ddy(frac_obj_space) == 0);
    float2 uv;
    if (flat.z)
        uv = frac_obj_space.xy;
    else if (flat.y)
        uv = frac_obj_space.xz;
    else
        uv = frac_obj_space.yz;

    // We want the normals in world-space. We only have the normals up to
    // in-/outwards pointing (sign), and using those is a pain. Just use the
    // depth we have access to to construct a bitangent basis to get a normal.
    float3 world_ddx = ddx(i.vertex_world_space);
    float3 world_ddy = ddy(i.vertex_world_space);
    float3 normal = normalize(cross(world_ddy, world_ddx)); // order: trial&error

    // 𝘕𝘰𝘸 𝘸𝘦 𝘤𝘢𝘯 𝘧𝘪𝘯𝘢𝘭𝘭𝘺 𝘨𝘦𝘵 𝘵𝘰 𝘥𝘰 𝘳𝘦𝘨𝘶𝘭𝘢𝘳 𝘴𝘩𝘪𝘵

    float3 light_vector = normalize(float3(1,3,2));
    float ambient = 0.5;

    return float4((ambient + (1 - ambient)*dot(normal, light_vector)) * float3(uv * 0.5 + 0.5, i.material * 0.5), 1);
}