Shader "Custom/Voxel"
{
    Properties
    {
        [Toggle(PIXEL_SHADOWS)] _EnablePixelShadows("Enable Pixelated Shadows", Float) = 1

        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        // Things we need because we're relying on the Standard Shader.
        // Don't wanna expose these, don't modify these.
        [HideInInspector] _Color("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Lighting On

        Pass
        {
            Cull Back
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #define IS_FORWARD_PASS

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
            #pragma multi_compile_fwdbase
            #pragma multi_compile __ PIXEL_SHADOWS

            #include "VoxelContent.cginc"
            ENDCG
        }

        Pass
        {
            Cull Back
            Blend One One
            ZWrite Off
            ZTest LEqual
            Tags { "LightMode" = "ForwardAdd" }

            CGPROGRAM
            #define IS_ADD_PASS

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile __ PIXEL_SHADOWS

            #include "VoxelContent.cginc"
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #include "VoxelShadowContent.cginc"
            ENDCG
        }
    }

    FallBack "Standard"
}
