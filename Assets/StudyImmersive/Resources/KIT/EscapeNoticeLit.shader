Shader "Campus/EscapeNoticeLit" {
 Properties { _MainTex ("Notice", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
 SubShader {
  Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  CGPROGRAM
  #pragma surface surf Lambert addshadow
  #pragma target 3.0
  sampler2D _MainTex; fixed4 _Color;
  struct Input { float2 uv_MainTex; };
  void surf(Input IN, inout SurfaceOutput o) {
   float2 uv=IN.uv_MainTex;
   clip(min(min(uv.x-.13, .87-uv.x), min(uv.y-.215, .785-uv.y)));
   fixed4 c=tex2D(_MainTex,uv)*_Color; o.Albedo=c.rgb; o.Alpha=1;
  }
  ENDCG
 }
 Fallback Off
}
