struct voxel {
    // MSB [sSet vvvv __fz fyfx cccc zzzz yyyy xxxx] LSB
    // Where:
    //   x: position [0,15]         fx: MSB [Render -x | Render +x] LSB
    //   y: position [0,15]         fy: idem, y
    //   z: position [0,15]         fz: idem, z
    //   c: color    [0,15]          t: whether half-transparent
    //   v: voxels per unit [0,15]   e: whether emissive
    //   S: whether casting shadows  s: whether receiving shadows
    uint data;
};

float3 VoxelPosition(voxel vd) {
    int3 vec = int3(vd.data, vd.data >> 4, vd.data >> 8);
    vec &= 0xF;
    return vec;
}

int VoxelColor(voxel vd) {
    return (vd.data >> 12) & 0xF;
}

bool3 VoxelRenderPositive(voxel vd) {
    return (int3(65536, 262144, 1048576) & vd.data) > 0;
}

bool3 VoxelRenderNegative(voxel vd) {
    return (int3(131072, 524288, 2097152) & vd.data) > 0;
}

float VoxelPerUnit(voxel vd) {
    return (vd.data >> 24) & 0xF;
}