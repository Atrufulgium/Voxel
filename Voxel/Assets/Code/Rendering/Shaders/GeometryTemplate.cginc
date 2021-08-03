// From https://halisavakis.com/my-take-on-shaders-geometry-shaders/
// Misc notes: https://gamedev.stackexchange.com/a/107908
//             It's important to match the geom in with the actual mesh.
#pragma vertex vert
#pragma geometry geom
#pragma fragment frag
// make fog work
#pragma multi_compile_fog

#include "UnityCG.cginc"

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2g
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
};

struct g2f
{
    float2 uv : TEXCOORD0;
    UNITY_FOG_COORDS(1)
    float4 vertex : SV_POSITION;
};

sampler2D _MainTex;
float4 _MainTex_ST;

v2g vert (appdata v)
{
    v2g o;
    o.vertex = v.vertex;
    o.uv = v.uv;
    return o;
}

[maxvertexcount(3)]
void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
{
    g2f o;

    for(int i = 0; i < 3; i++)
    {
        o.vertex = UnityObjectToClipPos(IN[i].vertex);
        UNITY_TRANSFER_FOG(o,o.vertex);
        o.uv = TRANSFORM_TEX(IN[i].uv, _MainTex);
        triStream.Append(o);
    }

    triStream.RestartStrip();
}

fixed4 frag (g2f i) : SV_Target
{
    // sample the texture
    fixed4 col = tex2D(_MainTex, i.uv);
    // apply fog
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}