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
            CGPROGRAM
            #include "VoxelContent.cginc"
            ENDCG
        }
    }
}
