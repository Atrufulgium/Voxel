Shader "Voxel/DrawVoxels"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Cull Off
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #include "DrawVoxelsContent.cginc"
            ENDCG
        }
    }
}
