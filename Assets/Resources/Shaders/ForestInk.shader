Shader "Emberclad/Forest Ink"
{
    Properties
    {
        _MainTex ("Illustration", 2D) = "white" {}
        [PerRendererData] _RendererColor ("Sprite colour", Color) = (1,1,1,1)
        _Moon ("Blue moon", Float) = 0
        _GrassOnly ("Grass in front of roots", Float) = 0
        _Lamplit ("Receives lantern light", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half4 _RendererColor;
            half _Moon, _Lamplit, _GrassOnly;
            float4 _ForestWick, _ForestPool;
            struct Input { float4 vertex: POSITION; float2 uv: TEXCOORD0; half4 color: COLOR; };
            struct Varying { float4 position: SV_POSITION; float2 uv: TEXCOORD0; float2 world: TEXCOORD1; half4 color: COLOR; };
            Varying vert(Input v)
            {
                Varying o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv = v.uv;
                o.color = v.color * _RendererColor;
                return o;
            }
            half4 frag(Varying i): SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                half alpha = saturate((tex.a - .022h) * 1.075h) * i.color.a;
                half moss = smoothstep(.0015h,.008h,tex.g - tex.b);
                half lip = smoothstep(.67h,.73h,i.uv.y);
                alpha *= lerp(1.0h, moss * lip, _GrassOnly);
                half shade = saturate(dot(tex.rgb, half3(.20h,.70h,.10h)) * 1.75h);
                half3 lunar = lerp(half3(.045h,.20h,.35h), half3(.42h,.87h,1.0h), shade);
                half3 ink = lerp(tex.rgb, lunar, _Moon) * i.color.rgb;
                float2 local = (i.world - _ForestWick.xy) * float2(.75, 1.12);
                float2 ground = (i.world - _ForestPool.xy) * float2(.49, 1.62);
                half light = max(saturate(1 - dot(local,local)), saturate(1 - dot(ground,ground)) * .72h);
                ink += _Lamplit * _ForestWick.z * light * light * (.085h + ink) * half3(.86h,.38h,.075h);
                return half4(ink, alpha);
            }
            ENDCG
        }
    }
}
