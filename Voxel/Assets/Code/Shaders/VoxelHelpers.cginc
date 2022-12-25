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

// Converts an object-space-axis-aligned plane into normals and uvs.
// To save some calculation effort, also requires world space coords.
void get_normals_uvs(float4 vertex_object_space, float3 vertex_world_space, out float3 normal, out float2 uv) {
    // Calculate the uvs from the (*,*,0)/(*,0,*)/(0,*,*) derivs that tell us
    // what this quad looks like. Even though the vector values are camera
    // dependent, whether it's zero or an * isn't. (..Mostly)
    // So yes, those are actual zeroes, because we work with voxels/planes.
    // (Do note that we need *both* ddx and ddy because floating point
    //  instabilities occasionally reported multiple flat directions with only
    //  ddx when looking at it from straight ahead. We can't have both unstable
    //  at the same time.)
    float3 frac_obj_space = frac(vertex_object_space);
    int3 flat = (ddx(frac_obj_space) == 0) * (ddy(frac_obj_space) == 0);
    uv;
    if (flat.z)
        uv = frac_obj_space.xy;
    else if (flat.y)
        uv = frac_obj_space.xz;
    else
        uv = frac_obj_space.yz;

    // We want the normals in world-space. We only have the normals up to
    // in-/outwards pointing (sign), and using those is a pain. Just use the
    // depth we have access to to construct a bitangent basis to get a normal.
    float3 world_ddx = ddx(vertex_world_space);
    float3 world_ddy = ddy(vertex_world_space);
    normal = normalize(cross(world_ddy, world_ddx)); // order: trial&error
}