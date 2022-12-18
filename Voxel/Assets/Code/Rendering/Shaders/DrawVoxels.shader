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
            Cull Back
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #include "DrawVoxelsContent.cginc"
            ENDCG
        }
    }
}
