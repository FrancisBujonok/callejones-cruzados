// Callejones: cielo nocturno para el skybox (Paso 18).
//
// Todo se calcula con la direccion en la que se mira, sin texturas:
//   1. Degrade: abajo el MISMO color de la niebla (asi el fondo de los pasillos no hace corte),
//      despues el resplandor anaranjado de la ciudad y arriba un azul casi negro.
//   2. Estrellas: el cielo se divide en celditas (las caras de un cubo) y algunas, al azar, son
//      una estrella de un pixel que titila. Cerca del horizonte no hay: las tapa el resplandor.
//   3. Luna grande con manchas, un halo suave y nubes que pasan delante.
//   4. A lo lejos: siluetas de edificios con algunas ventanas prendidas, torres con una luz roja
//      que parpadea y una vuelta al mundo con lamparitas de colores (el carnaval).
//   5. Bruma: cerca del horizonte todo se funde con el degrade. Las piezas que el generador pone
//      a lo lejos aparecen ahi, y contra un cielo liso (y del color de la niebla) no se notan.
//   6. Al final, los mismos pocos colores con dithering que el resto del juego (PS1Quantize).
//
// Las alturas y tamanos se escriben en grados, que es como se piensan al mirar el cielo.
Shader "Callejones/Cielo Nocturno"
{
    Properties
    {
        [Header(Degrade)]
        _HorizonColor ("Horizonte (igual a la niebla)", Color) = (0.114, 0.047, 0.027, 1)
        _GlowColor ("Resplandor de la ciudad", Color) = (0.243, 0.098, 0.051, 1)
        _MidColor ("Medio", Color) = (0.133, 0.102, 0.2, 1)
        _ZenithColor ("Arriba de todo", Color) = (0.051, 0.063, 0.141, 1)
        _HorizonHold ("Alto del horizonte liso (grados)", Range(0, 20)) = 6
        _GlowHeight ("Alto del resplandor (grados)", Range(5, 60)) = 20
        _MidHeight ("Alto del medio (grados)", Range(10, 89)) = 45

        [Header(Estrellas)]
        _StarColor ("Color", Color) = (0.93, 0.9, 0.82, 1)
        _StarDensity ("Cantidad", Range(0, 0.05)) = 0.007
        _StarGrid ("Celdas por cara (mas = estrellas mas chicas)", Range(60, 600)) = 240
        _StarMinHeight ("Desde que altura (grados)", Range(0, 60)) = 22

        [Header(Luna)]
        _MoonColor ("Color", Color) = (0.9, 0.84, 0.67, 1)
        _MoonAzimuth ("Hacia donde (grados, 0 = adelante del laberinto)", Range(-180, 180)) = 30
        _MoonElevation ("Altura (grados)", Range(5, 89)) = 55
        _MoonSize ("Radio (grados)", Range(0.5, 15)) = 4.5
        _MoonHalo ("Halo", Range(0, 1)) = 0.35

        [Header(Nubes)]
        _CloudColor ("Color", Color) = (0.23, 0.13, 0.16, 1)
        _CloudAmount ("Cantidad", Range(0, 1)) = 0.45
        _CloudScale ("Tamano", Range(0.2, 5)) = 1.2
        _CloudSpeed ("Velocidad", Range(0, 0.2)) = 0.015

        [Header(La ciudad a lo lejos)]
        _CityColor ("Siluetas", Color) = (0.07, 0.027, 0.02, 1)
        _WindowColor ("Ventanas", Color) = (0.82, 0.54, 0.27, 1)
        _CityHeight ("Altura de los edificios (grados)", Range(0, 25)) = 17
        _WindowAmount ("Ventanas prendidas", Range(0, 0.3)) = 0.06
        _Wheel ("Vuelta al mundo (0 = no, 1 = si)", Range(0, 1)) = 1
        _WheelAzimuth ("Hacia donde esta la vuelta al mundo (grados)", Range(-180, 180)) = -140
        _WheelElevation ("Altura del centro de la vuelta al mundo (grados)", Range(5, 40)) = 17

        [Header(Bruma del horizonte)]
        _HazeBottom ("Desde (grados): por debajo, solo el degrade", Range(0, 20)) = 7
        _HazeTop ("Hasta (grados): por encima, sin bruma", Range(0, 30)) = 11
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            float4 _HorizonColor, _GlowColor, _MidColor, _ZenithColor;
            float _HorizonHold, _GlowHeight, _MidHeight;
            float4 _StarColor;
            float _StarDensity, _StarGrid, _StarMinHeight;
            float4 _MoonColor;
            float _MoonAzimuth, _MoonElevation, _MoonSize, _MoonHalo;
            float4 _CloudColor;
            float _CloudAmount, _CloudScale, _CloudSpeed;
            float4 _CityColor, _WindowColor;
            float _CityHeight, _WindowAmount, _Wheel, _WheelAzimuth, _WheelElevation;
            float _HazeBottom, _HazeTop;

            // Las escribe RetroSettings (igual que para el shader PS1 Lit).
            float _PS1ColorLevels;
            float _PS1DitherStrength;

            static const float kBayer[16] =
            {
                0.0, 8.0, 2.0, 10.0,
                12.0, 4.0, 14.0, 6.0,
                3.0, 11.0, 1.0, 9.0,
                15.0, 7.0, 13.0, 5.0
            };

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz; // en el skybox, la posicion del vertice ES la direccion
                return o;
            }

            // ---------------------------------------------------------------- azar

            float Hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            float3 DirFrom(float azimuth, float elevation)
            {
                float az = radians(azimuth), el = radians(elevation);
                return float3(cos(el) * sin(az), sin(el), cos(el) * cos(az));
            }

            // ---------------------------------------------------------------- estrellas

            // El cielo se proyecta sobre las 6 caras de un cubo y cada cara se divide en celdas.
            // Asi las celdas miden casi lo mismo en todo el cielo (en una grilla de latitud y
            // longitud se amontonarian arriba, justo donde se mira).
            float Stars(float3 d)
            {
                float3 a = abs(d);
                float2 uv;
                float face;
                if (a.x >= a.y && a.x >= a.z) { uv = d.yz / a.x; face = d.x > 0 ? 1 : 2; }
                else if (a.y >= a.z)          { uv = d.xz / a.y; face = d.y > 0 ? 3 : 4; }
                else                          { uv = d.xy / a.z; face = d.z > 0 ? 5 : 6; }

                float2 cell = floor(uv * _StarGrid * 0.5);
                float h = Hash21(cell + face * float2(311.0, -173.0));
                if (h > _StarDensity) return 0.0;

                float b = Hash21(cell + float2(17.0, 31.0) + face);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + 3.0 * b) + b * 40.0);
                // b al cubo: muchas estrellas tenues y pocas brillantes, como en el cielo de verdad.
                return (0.2 + 0.8 * b * b * b) * twinkle;
            }

            // ---------------------------------------------------------------- ciudad

            // Edificios de 1.3 grados de ancho, con alturas al azar (agrupadas de a tres para que
            // se formen manzanas) y cada tanto una torre alta con una luz roja arriba.
            float3 City(float az, float elev, float3 col)
            {
                if (elev < 0.3) return col;

                float w = 1.3;
                float ci = floor(az / w);
                float group = floor(ci / 3.0);
                float hb = Hash11(ci * 1.37 + 3.1) * 0.6 + Hash11(group * 7.13) * 0.4;
                float h = 2.5 + hb * max(_CityHeight - 2.5, 0.0);
                bool tower = Hash11(ci * 4.71) > 0.965;
                if (tower) h = _CityHeight + 7.0;
                if (elev > h) return col;

                col = _CityColor.rgb;
                float2 wc = floor(float2(az / 0.45, elev / 0.55));
                if (elev < h - 0.6 && Hash21(wc + ci) < _WindowAmount) col = _WindowColor.rgb * 0.65;

                // Luz roja de las torres: parpadea lento, cada torre a su ritmo.
                float center = (ci + 0.5) * w;
                if (tower && elev > h - 0.5 && abs(az - center) < 0.25)
                {
                    float blink = step(0.0, sin(_Time.y * 2.2 + ci));
                    col = lerp(col, float3(1.0, 0.12, 0.06) * 1.6, blink);
                }
                return col;
            }

            // Distancia de un punto a un segmento (para dibujar las patas de la vuelta al mundo).
            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float t = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * t);
            }

            // Vuelta al mundo: aro, rayos que giran despacio, dos patas y 24 lamparitas de colores.
            // Se dibuja sobre el plano tangente al cielo en su direccion (como una calcomania).
            float3 Wheel(float3 d, float3 col)
            {
                float3 wd = DirFrom(_WheelAzimuth, _WheelElevation);
                float dd = dot(d, wd);
                if (dd < 0.95) return col;

                float3 wr = normalize(cross(float3(0, 1, 0), wd));
                float3 wu = cross(wd, wr);
                float2 p = float2(dot(d, wr), dot(d, wu)) / dd;
                float R = tan(radians(7.5));
                float r = length(p);
                float t = 0.0025;

                float structure = 0.0;
                structure += step(abs(r - R), t);
                structure += step(abs(r - R * 0.9), t * 0.6);
                float th = atan2(p.y, p.x) + _Time.y * 0.05;
                float spokeAngle = (frac(th / 6.2831853 * 16.0 + 0.5) - 0.5) * (6.2831853 / 16.0);
                structure += step(abs(spokeAngle) * r, t * 0.7) * step(r, R);
                structure += step(SegmentDistance(p, float2(0, 0), float2(-R * 0.62, -R * 1.45)), t);
                structure += step(SegmentDistance(p, float2(0, 0), float2(R * 0.62, -R * 1.45)), t);
                if (structure > 0.0) col = lerp(_CityColor.rgb, _GlowColor.rgb, 0.35);

                // Lamparitas: rosa, ambar y cian, que van cambiando como en una feria.
                float k = floor(frac(th / 6.2831853) * 24.0);
                float bulbAngle = (k + 0.5) / 24.0 * 6.2831853 - _Time.y * 0.05;
                float2 bulb = float2(cos(bulbAngle), sin(bulbAngle)) * R;
                if (length(p - bulb) < t * 2.2)
                {
                    float phase = frac(k / 3.0 + floor(_Time.y * 1.5) / 3.0);
                    float3 bulbColor = phase < 0.33 ? float3(1.0, 0.31, 0.64)
                                     : phase < 0.66 ? float3(1.0, 0.7, 0.28)
                                                    : float3(0.28, 0.9, 1.0);
                    col = bulbColor * 1.4;
                }
                return col;
            }

            // ---------------------------------------------------------------- pocos colores

            float3 PS1Quantize(float3 color, float2 pixel)
            {
                if (_PS1ColorLevels <= 0.0) return color;
                uint2 q = uint2(pixel) % 4u;
                float threshold = (kBayer[q.y * 4u + q.x] + 0.5) / 16.0 - 0.5;
                float3 c = pow(max(color, 0.0), 1.0 / 2.2);
                c = floor(c * _PS1ColorLevels + threshold * _PS1DitherStrength + 0.5) / _PS1ColorLevels;
                return pow(max(c, 0.0), 2.2);
            }

            // ---------------------------------------------------------------- todo junto

            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float elev = degrees(asin(clamp(d.y, -1.0, 1.0)));
                float az = degrees(atan2(d.x, d.z));

                // 1. Degrade. Por debajo del horizonte, el color de la niebla (ahi se ve el vacio
                //    al final de los pasillos y no tiene que notarse).
                float t1 = saturate((elev - _HorizonHold) / max(_GlowHeight - _HorizonHold, 0.01));
                float t2 = saturate((elev - _GlowHeight) / max(_MidHeight - _GlowHeight, 0.01));
                float t3 = saturate((elev - _MidHeight) / max(90.0 - _MidHeight, 0.01));
                float3 sky = lerp(_HorizonColor.rgb, _GlowColor.rgb, smoothstep(0.0, 1.0, t1));
                sky = lerp(sky, _MidColor.rgb, smoothstep(0.0, 1.0, t2));
                sky = lerp(sky, _ZenithColor.rgb, t3);
                float3 col = sky;

                // 2. Estrellas.
                float starVisible = saturate((elev - _StarMinHeight) / 10.0);
                col += _StarColor.rgb * Stars(d) * starVisible * 0.9;

                // 3. Luna: halo, disco con manchas (en bloques, pixelado) y borde mas oscuro.
                float3 moonDir = DirFrom(_MoonAzimuth, _MoonElevation);
                float ang = degrees(acos(clamp(dot(d, moonDir), -1.0, 1.0)));
                col += _MoonColor.rgb * _MoonHalo * 0.25 * exp(-max(ang - _MoonSize, 0.0) / (_MoonSize * 2.5));
                if (ang < _MoonSize)
                {
                    float3 right = normalize(cross(float3(0, 1, 0), moonDir));
                    float3 up = cross(moonDir, right);
                    float2 mp = float2(dot(d, right), dot(d, up)) / sin(radians(_MoonSize));
                    mp = floor(mp * 12.0) / 12.0;
                    float maria = Fbm(mp * 2.2 + 3.1);
                    float limb = sqrt(saturate(1.0 - dot(mp, mp)));
                    col = _MoonColor.rgb * (0.75 + 0.25 * limb) * (1.0 - 0.22 * step(0.55, maria));
                }

                // Nubes: un techo plano lejano. Tres tonos, como pintadas; cerca de la luna, el
                // borde se ilumina.
                float2 cuv = d.xz / max(d.y, 0.08) * _CloudScale * 0.35 + _Time.y * _CloudSpeed * float2(1.0, 0.3);
                float n = Fbm(cuv);
                float cover = smoothstep(1.0 - _CloudAmount, 1.0 - _CloudAmount + 0.22, n);
                cover = floor(cover * 3.0 + 0.5) / 3.0;
                cover *= saturate((elev - 4.0) / 12.0);
                float3 cloudColor = _CloudColor.rgb + _MoonColor.rgb * 0.25 * exp(-ang / 20.0);
                col = lerp(col, cloudColor, cover * 0.85);

                // 4. La ciudad lejana, delante de todo lo anterior. 5. La bruma.
                if (_Wheel > 0.5) col = Wheel(d, col);
                col = City(az, elev, col);

                // Bruma: cerca del horizonte todo vuelve al degrade liso (y abajo de 6 grados, al
                // color de la niebla). Asi la base de la ciudad se pierde en la bruma y, sobre todo,
                // las piezas nuevas que el generador pone a lo lejos (a 22 m, tapan hasta unos
                // 7 grados) no tapan ventanas de golpe: delante de un cielo liso no se notan.
                float haze = 1.0 - smoothstep(_HazeBottom, max(_HazeTop, _HazeBottom + 0.01), elev);
                col = lerp(col, sky, haze);

                // 6. Pocos colores y dithering, como el resto del juego.
                col = PS1Quantize(col, i.pos.xy);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
