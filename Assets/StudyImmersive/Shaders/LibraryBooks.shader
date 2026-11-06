Shader "StudyImmersive/LibraryBooks"
{
    Properties
    {
        _Color ("Leather tint", Color) = (.6,.4,.25,1)
        _MainTex ("Leather detail", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = .35
        _Glossiness ("Smoothness", Range(0,1)) = .2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap;
        fixed4 _Color;
        half _BumpScale,_Glossiness;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 detail=tex2D(_MainTex,IN.uv_MainTex).rgb;
            half grain=dot(detail,half3(.3,.59,.11));
            o.Albedo=saturate(_Color.rgb*(.9+.36*grain)+detail*.12);
            o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_BumpMap),_BumpScale);
            o.Metallic=0;o.Smoothness=_Glossiness;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
