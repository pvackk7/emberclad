Shader "Emberclad/Forest Ink"
{
    Properties
    {
        _MainTex ("Illustration", 2D) = "white" {}
        [PerRendererData] _RendererColor ("Sprite colour", Color) = (1,1,1,1)
        _Moon ("Blue moon", Float) = 0
        _UnlitGlass ("Unlit lantern glass", Float) = 0
        _DistanceMist ("Distant forest haze", Float) = 0
        _PineNight ("Moonlit pine foliage", Float) = 0
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
            half _DistanceMist, _UnlitGlass, _Moon, _Lamplit, _GrassOnly, _PineNight;
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
                half foliage = step(tex.r, tex.g) * _PineNight;
                half3 pine = tex.g * half3(.47h,.80h,.98h);
                tex.rgb = lerp(tex.rgb, pine, foliage);
                half brassLight = smoothstep(.06h,.20h,tex.r - tex.b);
                tex.rgb = lerp(tex.rgb, half3(.012h,.019h,.024h) + tex.g * .035h, _UnlitGlass * brassLight);
                half shade = saturate(dot(tex.rgb, half3(.20h,.70h,.10h)) * 1.75h);
                half3 lunar = lerp(half3(.045h,.20h,.35h), half3(.42h,.87h,1.0h), shade);
                half3 ink = lerp(tex.rgb, lunar, _Moon) * i.color.rgb;
                ink = lerp(ink, half3(.016h, .032h, .049h), _DistanceMist);
                // Lantern light: a bright inner lobe, a wide soft lobe and a pool along the
                // ground under the post. All three are Gaussians, so there is no edge anywhere;
                // the light just thins out into the night. It scales the surface's own colour
                // (lit ground, bark, grass, rock) instead of adding orange to the air in front.
                float2 dl = (i.world - _ForestWick.xy) * float2(.82, 1.0);
                float2 dg = (i.world - _ForestPool.xy) * float2(.5, 1.5);
                float r2 = dot(dl, dl), g2 = dot(dg, dg);
                half light = .62h * exp(-r2 * .95h) + .70h * exp(-r2 * .09h) + .34h * exp(-g2 * .55h);
                light *= _Lamplit * _ForestWick.z;
                ink += light * half3(1.0h, .55h, .22h) * (ink * 2.8h + .006h);
                return half4(ink, alpha);
            }
            ENDCG
        }
    }
}
