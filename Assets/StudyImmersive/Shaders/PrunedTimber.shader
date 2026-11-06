Shader "StudyImmersive/PrunedTimber"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Wood", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = .45
        _Glossiness ("Smoothness", Range(0,1)) = .2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap;
        fixed4 _Color;
        half _BumpScale,_Glossiness;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz;
            if (IN.worldPos.y>.55 && IN.worldPos.y<4.6)
            {
                float d=min(min(distance(p,float2(-14,-7)),distance(p,float2(12,-13))),
                            min(min(distance(p,float2(-15,5)),distance(p,float2(14,11))),
                                min(distance(p,float2(-3,-13)),distance(p,float2(4,-13)))));
                clip(d-1.82);
            }
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=c.rgb;o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_BumpMap),_BumpScale);
            o.Metallic=0;o.Smoothness=_Glossiness;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
