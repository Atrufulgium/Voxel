// See also the Chunk.Vertex struct over in c#-land's Chunk.cs.
struct appdata {
    // Contained in the factors, you get:
    //   #1: factor 33: x-position 0, .., 32
    //   #2: factor 33: y-position 0, .., 32
    //   #3: factor 33: z-position 0, .., 32
    //   #4: remainder:   material 0, .., 119513
    uint data : BLENDINDICES;
};

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

#define FACE float
#define XPOS 0
#define YPOS 1
#define ZPOS 2
#define XNEG 3
#define YNEG 4
#define ZNEG 5

// Converts an object-space-axis-aligned plane into normals and uvs.
// To save some calculation effort, also requires world space coords.
// Spits out which direction our face points in model space because that
// requires basically no extra effort.
void get_normals_uvs(float4 vertex_object_space, float3 vertex_world_space, out float3 normal, out float2 uv, out FACE face) {
    // We want the normals in world-space. We only have the normals up to
    // in-/outwards pointing (sign), and using those is a pain. Just use the
    // depth we have access to to construct a bitangent basis to get a normal.
    float3 world_ddx = ddx(vertex_world_space);
    float3 world_ddy = ddy(vertex_world_space);
    normal = normalize(cross(world_ddy, world_ddx)); // order: trial&error

    // Calculate the uvs from the (*,*,0)/(*,0,*)/(0,*,*) derivs that tell us
    // what this quad looks like. Even though the vector values are camera
    // dependent, whether it's zero or an * isn't. (..Mostly)
    // So yes, those are actual zeroes, because we work with voxels/planes.
    // (Do note that we need *both* ddx and ddy because floating point
    //  instabilities occasionally reported multiple flat directions with only
    //  ddx when looking at it from straight ahead. We can't have both unstable
    //  at the same time.)
    
    float3 model_ddx = ddx(vertex_object_space);
    float3 model_ddy = ddy(vertex_object_space);
    float3 model_normal = normalize(cross(model_ddy, model_ddx));
    // This *could* maybe be more efficient but *eh*.
    if (model_normal.x > 0)
        face = XPOS;
    else if (model_normal.x < 0)
        face = XNEG;
    else if (model_normal.y > 0)
        face = YPOS;
    else if (model_normal.y < 0)
        face = YNEG;
    else if (model_normal.z > 0)
        face = ZPOS;
    else
        face = ZNEG;

    float3 frac_obj_space = frac(vertex_object_space);
    int3 flat = (frac(model_ddx) == 0) * (frac(model_ddy) == 0);
    if (flat.z)
        uv = frac_obj_space.xy;
    else if (flat.y)
        uv = frac_obj_space.xz;
    else
        uv = frac_obj_space.zy;
    
    // Because we're basing the UVs off world coordinates, both sides get the
    // same uvs, which means that 3/6 have flipped uvs. Fix that.
    if (face == XNEG || face == YNEG || face == ZPOS)
        uv.x = 1 - uv.x;
}