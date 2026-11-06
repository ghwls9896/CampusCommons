Shader "GrandLibrary/Portal"
{
    Properties { _Color ("Portal color", Color) = (0.2,0.8,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1; float r=length(p); float angle=atan2(p.y,p.x);
                float swirl=sin(r*22-angle*3-_Time.y*2.1)*.5+.5;
                float wave=sin(r*37+angle*5+_Time.y*1.4)*.5+.5;
                float glow=pow(saturate(1-r),.45);
                fixed3 color=lerp(_Color.rgb*.22,_Color.rgb*1.7,swirl*.38+glow*.42+wave*.12);
                return fixed4(color,saturate(.72+glow*.2));
            }
            ENDCG
        }
    }
}
