Shader "Hidden/Hooligan/TutorialGaussian"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        // Paired bilinear taps implement a separable nine-tap Gaussian.
        fixed4 Gaussian(float2 uv,float2 d)
        {
            return tex2D(_MainTex,uv)*.227027
                +(tex2D(_MainTex,uv+d*1.384615)+tex2D(_MainTex,uv-d*1.384615))*.316216
                +(tex2D(_MainTex,uv+d*3.230769)+tex2D(_MainTex,uv-d*3.230769))*.070270;
        }
        fixed4 Horizontal(v2f_img i):SV_Target {return Gaussian(i.uv,float2(_MainTex_TexelSize.x,0));}
        fixed4 Vertical(v2f_img i):SV_Target {return Gaussian(i.uv,float2(0,_MainTex_TexelSize.y));}
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Horizontal
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Vertical
            ENDCG
        }
    }
}
