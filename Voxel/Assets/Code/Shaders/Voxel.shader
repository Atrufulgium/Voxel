Shader "Custom/Voxel"
{
    Properties
    {
        
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Lighting On

        // Regular draw
        Pass
        {
            Cull Back
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #include "VoxelContent.cginc"
            ENDCG
        }

        // As a shadowcaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual Cull Off

            CGPROGRAM
            #include "VoxelShadowContent.cginc"
            ENDCG
        }
    }
}
