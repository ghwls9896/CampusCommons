Shader "Campus/Local Time Skybox"
{
    Properties
    {
        _Tex ("Cubemap", Cube) = "grey" {}
        _Tint ("Tint", Color) = (.5,.5,.5,.5)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _SunDirection ("Direction to sun", Vector) = (1,0,0,0)
        _SunStrength ("Sun visibility", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float4 _SunDirection;
            half _SunStrength;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.direction = v.vertex.xyz; return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                // The source panorama contains a fixed sun. Replace its small disc
                // with adjacent cloud pixels before drawing the moving sun.
                const float3 bakedSun = float3(.73907, .40674, -.53697);
                float3 tangent = normalize(cross(float3(0,1,0), bakedSun));
                float mask = smoothstep(.9945, .9982, dot(direction, bakedSun));
                half3 original = DecodeHDR(texCUBE(_Tex, direction), _Tex_HDR);
                half3 nearby = DecodeHDR(texCUBE(_Tex, normalize(direction + tangent * .16)), _Tex_HDR);
                half3 sky = lerp(original, nearby, mask) * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                float alignment = dot(direction, normalize(_SunDirection.xyz));
                float disc = smoothstep(.99968, .99982, alignment);
                float glow = pow(saturate(alignment), 700.0) * .16;
                half3 sunlight = half3(1.0, .89, .70) * (disc * 1.3 + glow) * _SunStrength;
                return half4(sky + sunlight, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
