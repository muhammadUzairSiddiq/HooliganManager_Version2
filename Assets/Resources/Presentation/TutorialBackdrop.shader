Shader "Hooligan/UI/TutorialBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _BackdropTex ("Colour world", 2D) = "black" {}
        _Ready ("Capture ready", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float4 screen:TEXCOORD0; };
            sampler2D _BackdropTex;
            float4 _BackdropTex_TexelSize;
            float _Ready;
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;
                o.screen=ComputeScreenPos(o.vertex);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Gold ring vertices stay crisp; only the exterior veil samples the world.
                if(i.color.r>.5||_Ready<.5)return i.color;
                float2 uv=i.screen.xy/i.screen.w;
                fixed3 c=tex2D(_BackdropTex,uv).rgb;
                return fixed4(c*(1-i.color.a)+i.color.rgb*.15,.94);
            }
            ENDCG
        }
    }
}
