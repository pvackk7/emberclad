Shader "ST/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _VisorEmber ("Visor ember", Range(0,1)) = 0
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
        CGPROGRAM
            #pragma vertex vertLit
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _Flash, _VisorEmber;
            fixed4 _FlashColor;
            float4 _ForestWick;
            struct v2fLit { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float2 world : TEXCOORD1; };
            v2fLit vertLit(appdata_t IN)
            {
                v2f b = SpriteVert(IN);
                v2fLit o;
                o.vertex = b.vertex; o.color = b.color; o.texcoord = b.texcoord;
                o.world = mul(unity_ObjectToWorld, UnityFlipSprite(IN.vertex.xyz, _Flip)).xy;
                return o;
            }
            fixed4 frag(v2fLit IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                if (_VisorEmber > 0)
                {
                    float2 p = float2(IN.texcoord.x, 1 - IN.texcoord.y) * float2(1536, 1024);
                    float slit = smoothstep(294, 312, p.x) * (1 - smoothstep(432, 449, p.x))
                        * smoothstep(285, 301, p.y + .18 * (p.x - 385)) * (1 - smoothstep(321, 326, p.y));
                    float vents = smoothstep(358, 368, p.x) * (1 - smoothstep(437, 445, p.x))
                        * smoothstep(351, 361, p.y) * (1 - smoothstep(396, 408, p.y));
                    float dark = 1 - smoothstep(GammaToLinearSpaceExact(.22), GammaToLinearSpaceExact(.34), c.g);
                    #ifdef UNITY_COLORSPACE_GAMMA
                    dark = 1 - smoothstep(.22, .34, c.g);
                    #endif
                    float t = _Time.y;
                    float tongue = .5 + .5 * sin(p.x * .113 + sin(p.y * .073 + t * 6) + t * 3.6);
                    float heat = saturate(.16 + .42 * tongue + .32 * sin(p.x * .046 - t * 2.9)
                        + .19 * sin(p.y * .091 + t * 4.7));
                    float3 fire = lerp(float3(.70,.13,.018), float3(1,.78,.26), heat);
                    c.rgb = lerp(c.rgb, fire, saturate(slit + vents * .66) * dark * _VisorEmber);
                }
                // Nearby lantern light (same falloff as the forest), favouring bright surfaces so
                // polished armour picks up warm highlights while cloth and shadow stay dark.
                float2 dl = (IN.world - _ForestWick.xy) * float2(.82, 1.0);
                float r2 = dot(dl, dl);
                half lamp = (.55 * exp(-r2 * .95) + .50 * exp(-r2 * .09)) * _ForestWick.z;
                half shine = smoothstep(.10, .55, dot(c.rgb, half3(.30, .59, .11)));
                c.rgb += lamp * half3(1.0, .55, .22) * (c.rgb * (1.1 + 3.0 * shine) + .004);
                c.rgb = lerp(c.rgb * IN.color.rgb, _FlashColor.rgb, _Flash); c.a *= IN.color.a; c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
