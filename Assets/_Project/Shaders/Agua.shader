// Callejones: gotas y ondas del goteo (Paso 21).
//
// Sin luces: el color sale de la textura y de _BaseColor (las gotas brillan un poco para que se
// vean en la oscuridad). Recorta por _Cutoff, que DripSystem va subiendo para que la onda se
// deshaga de a pixeles. Tiene la niebla del color del cielo y los colores de pocos bits de
// PS1 Lit, pero sus vertices NO saltan a la grilla: una gota de 1.6 cm de ancho, con el salto,
// se aplastaria a cero pixeles y desapareceria a ratos (justo el parpadeo que habia que evitar).
//
// _Offset corre la profundidad para que la onda no pelee con el piso o el charco de abajo.
Shader "Callejones/Agua"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Recorte", Range(0, 1)) = 0.5
        _Offset("Corrimiento de profundidad", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Agua"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            Offset [_Offset], [_Offset]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _Offset;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Las mismas variables globales que PS1 Lit (RetroSettings) y que la niebla (SkyFog).
            float _PS1ColorLevels;
            float _PS1DitherStrength;
            float _CallejonesSkyFogOn;
            float4 _CallejonesSkyHorizon;
            float4 _CallejonesSkyGlow;
            float4 _CallejonesSkyMid;
            float4 _CallejonesSkyZenith;
            float4 _CallejonesSkyHeights;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            static const float kBayer[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            // Copia de PS1Quantize (PS1LitForwardPass.hlsl).
            half3 Quantize(half3 color, float2 pixel)
            {
                if (_PS1ColorLevels <= 0.0)
                    return color;
                uint2 p = uint2(pixel) % 4u;
                float threshold = (kBayer[p.y * 4u + p.x] + 0.5) / 16.0 - 0.5;
                float3 c = pow(max(float3(color), 0.0), 1.0 / 2.2);
                c = floor(c * _PS1ColorLevels + threshold * _PS1DitherStrength + 0.5) / _PS1ColorLevels;
                return half3(pow(max(c, 0.0), 2.2));
            }

            // Copia de CallejonesSkyColor (PS1LitForwardPass.hlsl).
            half3 SkyColor(float3 dir)
            {
                float4 h = _CallejonesSkyHeights;
                float elev = degrees(asin(clamp(dir.y, -1.0, 1.0)));
                float t1 = saturate((elev - h.x) / max(h.y - h.x, 0.01));
                float t2 = saturate((elev - h.y) / max(h.z - h.y, 0.01));
                float t3 = saturate((elev - h.z) / max(90.0 - h.z, 0.01));
                float3 sky = lerp(_CallejonesSkyHorizon.rgb, _CallejonesSkyGlow.rgb, smoothstep(0.0, 1.0, t1));
                sky = lerp(sky, _CallejonesSkyMid.rgb, smoothstep(0.0, 1.0, t2));
                return half3(lerp(sky, _CallejonesSkyZenith.rgb, t3));
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);
                half3 color = tex.rgb * _BaseColor.rgb;
            #if defined(FOG_LINEAR)
                // Igual que CallejonesMixFog: distancia desde la camara, la altura cuenta menos.
                float3 d = input.positionWS - _WorldSpaceCameraPos;
                float w = _CallejonesSkyFogOn > 0.5 ? _CallejonesSkyHeights.w : 1.0;
                float visible = saturate(length(float3(d.x, d.y * w, d.z)) * unity_FogParams.z + unity_FogParams.w);
                half3 fogColor = _CallejonesSkyFogOn > 0.5 ? SkyColor(normalize(d)) : unity_FogColor.rgb;
                color = lerp(fogColor, color, visible);
            #endif
                return half4(Quantize(color, input.positionCS.xy), 1.0);
            }
            ENDHLSL
        }
    }
}
