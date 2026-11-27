Shader "Campus/EscapeWorldTextLit" {
 Properties { _MainTex ("Font Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) _DimEmission ("Dark ambient", Float) = 0 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Cull Off ZWrite Off ZTest LEqual
  CGPROGRAM
  #pragma surface surf Lambert alpha:fade
  #pragma target 3.0
  sampler2D _MainTex; fixed4 _Color; half _DimEmission;
  struct Input { float2 uv_MainTex; fixed4 color : COLOR; };
  void surf(Input IN, inout SurfaceOutput o) {
   fixed4 c=IN.color*_Color; o.Albedo=c.rgb; o.Emission=c.rgb*_DimEmission;
   o.Alpha=c.a*tex2D(_MainTex,IN.uv_MainTex).a;
  }
  ENDCG
 }
 Fallback Off
}
