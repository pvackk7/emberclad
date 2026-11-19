Shader "Emberclad/LivingMenu"
{
    Properties
    {
        [PerRendererData] _MainTex ("Backdrop", 2D) = "white" {}
        _MenuTime ("Time", Float) = 0
        _PaintedTavern ("Painted Tavern", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _MenuTime;
            float _PaintedTavern;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 c = tex2D(_MainTex, uv);
                float warm = smoothstep(1.2, 1.7, c.r / max(c.g, .01)) * smoothstep(1.4, 2.0, c.g / max(c.b, .01));
                float windows = (1 - smoothstep(.31, .36, uv.x)) * smoothstep(.31, .35, uv.y) * (1 - smoothstep(.64, .69, uv.y));
                c.rgb *= 1 + warm * windows * (.025 * sin(_MenuTime * 3.1) + .013 * sin(_MenuTime * 7.7));
                float2 fireOffset = (uv - float2(.836, .20)) / float2(.055, .11);
                float fire = saturate(1 - dot(fireOffset, fireOffset)) * warm * _PaintedTavern;
                c.rgb *= 1 + fire * (.065 * sin(_MenuTime * 6.3 + uv.y * 54) + .035 * sin(_MenuTime * 13.7 - uv.y * 31));
                return c * i.color;
            }
            ENDCG
        }
    }
}
