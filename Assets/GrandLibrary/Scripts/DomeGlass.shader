Shader "Campus/DomeGlass" {
 Properties { _Color ("Glass tint", Color) = (0.6, 0.75, 0.8, 0.08) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Color;
   struct appdata { float4 vertex : POSITION; };
   struct v2f { float4 vertex : SV_POSITION; };
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); return o; }
   fixed4 frag(v2f i) : SV_Target { return _Color; }
   ENDCG
  }
 }
}
