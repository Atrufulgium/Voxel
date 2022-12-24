Shader "Custom/Voxel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Cull Back

            CGPROGRAM
            #include "VoxelContent.cginc"
            ENDCG
        }
    }
}
