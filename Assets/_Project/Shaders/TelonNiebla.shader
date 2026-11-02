// Callejones: telon de niebla (Paso 20).
//
// Pinta lo mismo que se ve de una pared muy lejana: el color de la niebla, que es el degrade del
// cielo segun hacia donde se mira (lo escribe SkyFog). Va en cada conexion libre de las piezas
// (las que todavia no tienen vecina): CorridorTreadmill lo prende y lo apaga.
//
// Por que: con edificios de 30 m, al fondo de un pasillo largo se veia el cielo (estrellas,
// nubes, la ciudad) hasta muy arriba, y al aparecer la pieza siguiente eso se tapaba de golpe.
// Con el telon, el fondo ya se ve como niebla antes, y la pieza nueva (tambien hundida en la
// niebla) aparece sin que cambie nada.
//
// Sin luces ni texturas. Tiene el mismo temblor de vertices y los mismos colores de pocos bits
// que PS1 Lit, para que sus bordes no se despeguen de las paredes de al lado.
Shader "Callejones/Telon de niebla"
{
    Properties { }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Las mismas variables globales que PS1 Lit (RetroSettings) y que la niebla (SkyFog).
        float _PS1SnapResolution;
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
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
        };

        // Copia de PS1SnapVertex (PS1LitForwardPass.hlsl).
        float4 SnapVertex(float4 positionCS)
        {
            if (_PS1SnapResolution <= 0.0 || positionCS.w <= 0.0001)
                return positionCS;
            float2 grid = float2(_PS1SnapResolution * _ScreenParams.x / _ScreenParams.y, _PS1SnapResolution) * 0.5;
            float2 ndc = positionCS.xy / positionCS.w;
            ndc = round(ndc * grid) / grid;
            positionCS.xy = ndc * positionCS.w;
            return positionCS;
        }

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            o.positionCS = SnapVertex(TransformWorldToHClip(o.positionWS));
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Telon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

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

            // Copia de CallejonesSkyColor (PS1LitForwardPass.hlsl): el degrade de CieloNocturno.
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

            half4 Frag(Varyings input) : SV_Target
            {
                // Sin SkyFog, la niebla es de un solo color.
                half3 color = unity_FogColor.rgb;
                if (_CallejonesSkyFogOn > 0.5)
                    color = SkyColor(normalize(input.positionWS - _WorldSpaceCameraPos));
                return half4(Quantize(color, input.positionCS.xy), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
