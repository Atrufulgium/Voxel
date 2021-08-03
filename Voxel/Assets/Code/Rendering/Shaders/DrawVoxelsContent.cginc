#pragma vertex Vertex
#pragma geometry Geometry
#pragma fragment Fragment

#include "UnityCG.cginc"
#include "VoxelData.cginc"

struct geometry_out {
    float4 position : SV_POSITION;
    float3 normal : NORMAL;
    float4 color : TEXCOORD0;
};

int voxelCount;
StructuredBuffer<float4> palette;
StructuredBuffer<voxel> voxelData;

float Vertex(uint id : SV_VERTEXID) : TEXCOORD0 {
    return float4(id, 0, 0, 0);
}

geometry_out ManualVertex(geometry_out vert) {
    vert.position = UnityObjectToClipPos(vert.position);
    return vert;
}

// Create an axis-aligned square quad and adds it to the stream.
// (Well, assumes bottomLeft--topRight forms such a quad.)
// Try-by-error the meanings of "bottom left" and "delta" for facings.
void CreateQuad(float3 bottomLeft, float3 topRight, float4 color, inout TriangleStream<geometry_out> outStream) {
    // Taking .yx, .xz, .zy of one and the other of the other will result in the
    // other three points of the quad, which are always ordered clockwise.
    // (To see this, just take the origin and topRight a vector with two 1s and a 0,
    //  and go through the three cases.)
    float3 p1 = float3(bottomLeft.x, topRight.yz);
    float3 p2 = float3(topRight.x, bottomLeft.y, topRight.z);
    float3 p3 = float3(topRight.xy, bottomLeft.z);
    float3 temp;
    // Rotate the points s.t. p3 is the far one.
    // This works properly only if the input satisfied the "axis-aligned" part.
    // As all cases are essentially uniformly randomly chosen (depends on what axis
    // the quad lives), the branching here is a little painful.
    if (all(p1 == topRight)) {
        temp = p3;
        p3 = p1;
        p1 = p2;
        p2 = temp;
    } else if (all(p2 == topRight)) {
        temp = p3;
        p3 = p2;
        p2 = p1;
        p1 = temp;
    }

    geometry_out o;
    o.color = color;
    o.normal = normalize(cross(p2 - bottomLeft, p1 - bottomLeft));

    o.position = float4(bottomLeft, 1);
    outStream.Append(ManualVertex(o));
    o.position = float4(p1, 1);
    outStream.Append(ManualVertex(o));
    o.position = float4(p2, 1);
    outStream.Append(ManualVertex(o));
    o.position = float4(p3, 1);
    outStream.Append(ManualVertex(o));
    outStream.RestartStrip();
}

// At most rendering 3 quads, which inefficiently results in 4 verts each.
[maxvertexcount(12)]
void Geometry(point float4 id[1] : TEXCOORD0, inout TriangleStream<geometry_out> outStream) {
    // voxel v = voxelData[(uint)id[0]];
    // float size = rcp(VoxelPerUnit(v));
    // float3 relativePos = VoxelPosition(v);
    // float4 color = palette[VoxelColor(v)];

    
    geometry_out o;
    o.color = float4(1,0,0,1);
    o.normal = 0;

    o.position = float4(0,0,0,1);
    outStream.Append(ManualVertex(o));
    o.position = float4( 1,0,0,1);
    outStream.Append(ManualVertex(o));
    o.position = float4(0, 1,0,1);
    outStream.Append(ManualVertex(o));
    outStream.RestartStrip();
    o.position = float4( 1,0,0,1);
    outStream.Append(ManualVertex(o));
    o.position = float4(0,0,0,1);
    outStream.Append(ManualVertex(o));
    o.position = float4(0, 1,0,1);
    outStream.Append(ManualVertex(o));
    outStream.RestartStrip();

    return;

    // Not checking whether it's efficient to render yet.
    
    // CreateQuad(relativePos, relativePos + float3(size,size,0), color, outStream);
    // CreateQuad(relativePos + float3(size,size,0), relativePos, color, outStream);
}

fixed4 Fragment(geometry_out i) : SV_TARGET
{
    return i.color;
}