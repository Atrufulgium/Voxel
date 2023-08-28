// See also the Chunk.Vertex struct over in c#-land.
struct appdata {
    // Contained in the factors, you get:
    //   #1: factor 33: x-position 0, .., 32
    //   #2: factor 33: y-position 0, .., 32
    //   #3: factor 33: z-position 0, .., 32
    //   #4: remainder:   material 0, .., 119513
    uint data : BLENDINDICES;
};

appdata vert(appdata v) {
    return v;
}

struct TessFactors {
    float edge[4] : SV_TessFactor;
    float inside[2] : SV_InsideTessFactor;
};

TessFactors GetTessFactors(InputPatch<appdata, 4> patch) {
    // As mentioned earlier we're not actually tesselating.
    // We're only doing this to gain access to the entire triangle.
    TessFactors f;
    [unroll]
    for (int i = 0; i < 4; i++)
        f.edge[i] = 1;
    f.inside[0] = 0;
    f.inside[1] = 0;
    return f;
}

[UNITY_domain("quad")]
[UNITY_outputcontrolpoints(4)]
[UNITY_outputtopology("triangle_cw")]
[UNITY_partitioning("integer")]
[UNITY_patchconstantfunc("GetTessFactors")]
appdata hull(
    InputPatch<appdata, 4> patch,
    uint id : SV_OUTPUTCONTROLPOINTID
) {
    return patch[id];
}

// Unpacks the above struct into useful data.
void unpack(appdata v, out float4 pos, out uint material) {
    // Unpack
    pos.x = v.data % 33;
    v.data /= 33;
    pos.y = v.data % 33;
    v.data /= 33;
    pos.z = v.data % 33;
    v.data /= 33;
    pos.w = 1;

    material = v.data;
}

void computeVertNormTanUVMaterial(
    in OutputPatch<appdata, 4> patch,
    in float2 tessUV,
    
    out float4 pos, // model pos
    out float3 normal,
    out float3 tangent,
    out float2 uv,
    out uint material
) {
    int4 posses[4];
    uint _;
    unpack(patch[0], posses[0], material);
    unpack(patch[1], posses[1], _);
    unpack(patch[2], posses[2], _);
    unpack(patch[3], posses[3], _);

    // The appdata[4] is clockwise.
    float2 coordUV = tessUV;
    if (coordUV.y == 1)
        coordUV.x = 1 - coordUV.x;
    int index = (int)dot(coordUV, float2(1,2));
    pos = posses[index];
    
    int3 dx1 = posses[1].xyz - posses[0].xyz;
    int3 dx2 = posses[3].xyz - posses[0].xyz;
    normal = cross(dx1, dx2);
    // Now the normal is correct up to sign. The correct sign is the one such
    // that the normal points to the halfspace containing the camera.
    float3 basePoint = posses[0];
    float3 testPoint = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
    if (dot(normal, testPoint - basePoint) < 0)
        normal *= -1;
    
    tangent = float3(normal.y, -normal.x, normal.z);
    
    // Texture UVs can also be calculated, though not yet frac'd.
    float3 dxdiag = posses[2].xyz - posses[0].xyz;
    // Scale the uvs so that 2 voxels = 1 texture width
    // Don't forget to offset!
    dxdiag *= 0.5;
    // 0.5 if odd, 0 if even
    float3 uvoffset3 = frac(posses[0] * 0.5);
    float2 uvmax;
    float2 uvoffset;
    if (normal.x != 0) {
        uvmax = dxdiag.zy;
        uvoffset = uvoffset3.zy;
    } else if (normal.y != 0) {
        uvmax = dxdiag.xz;
        uvoffset = uvoffset3.xz;
    } else if (normal.z != 0) {
        uvmax = dxdiag.yx;
        uvoffset = uvoffset3.yx;
    } else {
        uvmax = 0;
    }
    uv = tessUV * uvmax + uvoffset;
}

#define FACE float
#define XPOS 0
#define YPOS 1
#define ZPOS 2
#define XNEG 3
#define YNEG 4
#define ZNEG 5

FACE getFace(float3 modelNormal) {
    if (modelNormal.x > 0)
        return XPOS;
    else if (modelNormal.x < 0)
        return XNEG;
    else if (modelNormal.y > 0)
        return YPOS;
    else if (modelNormal.y < 0)
        return YNEG;
    else if (modelNormal.z > 0)
        return ZPOS;
    else
        return ZNEG;
}

float2 getUVs(float3 modelNormal, float2 uv) {
    FACE face = getFace(modelNormal);
    uv = frac(uv);
    if (face == ZPOS || face == ZNEG)
        uv = uv.yx;
    if (face == XNEG || face == YNEG || face == ZPOS)
        uv.x = 1 - uv.x;
    
    uv.x = (uv.x + face) / 6;
    return uv;
}