Shader "Hooligan/InstancedSupporter"
{
 Properties { _BaseColor("Club colour",Color)=(0.1,0.6,0.2,1) }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 colour:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; half4 colour:COLOR0; };
   V Vert(A v)
   {
    UNITY_SETUP_INSTANCE_ID(v);
    V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    half3 c = v.colour.a < .5 ? _BaseColor.rgb : v.colour.rgb;
    o.colour=half4(c*(.72+.28*saturate(dot(v.normalOS,normalize(float3(.4,1,.3))))),1);
    return o;
   }
   half4 Frag(V i):SV_Target { return i.colour; }
   ENDHLSL
  }
 }
}
