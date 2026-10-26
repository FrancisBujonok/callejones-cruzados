using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: el sonido.
/// Menu: Callejones > Solo sonido (tambien corre dentro de "Construir atmosfera (todo)").
///
/// Como las texturas, todos los sonidos se SINTETIZAN por codigo: ruido filtrado, senos y
/// envolventes. No hay archivos descargados. Se guardan como WAV en Assets/_Project/Audio
/// a 22050 Hz (la mitad de un CD, como muchos juegos de PS1) y se importan en ADPCM,
/// el mismo tipo de compresion que usaba la consola.
///
/// Sonidos:
/// - Ambiente_Viento (loop): viento en el callejon, un silbido en las rafagas y un rumor grave de ciudad.
/// - Farol_Zumbido (loop): zumbido electrico de 50 Hz con chisporroteo. Va en cada farol, en 3D.
/// - Paso_0..3: cuatro pasos distintos sobre piedra mojada.
/// - Miedo_Drone (loop): acorde grave y disonante con una respiracion. Sube con el miedo.
/// - Miedo_Latido: un latido (dos golpes). Se repite mas rapido cuanto mas miedo.
/// - Evento_Descubrimiento: barrido que crece y un golpe metalico al descubrir un cambio.
/// - Evento_CambioLejano: una persiana que golpea y traquetea lejos, cuando el laberinto cambia a tus espaldas.
/// - Torii_Campanas (loop): acorde hueco, campanas de viento y cascabeles de santuario (suzu).
///
/// Los loops no tienen "costura": todas sus frecuencias completan un numero entero de ciclos
/// en el largo del loop, y los filtros se pasan dos veces (ver doc, Paso 9).
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string AudioDir = Root + "/Audio";
    private const int SampleRate = 22050;

    private delegate void SoundSynth(float[] buffer, System.Random rng);

    private class Sounds
    {
        public AudioClip viento, zumbido, drone, latido, descubrimiento, cambioLejano, torii;
        public AudioClip[] pasos;
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Callejones/Solo sonido", priority = 22)]
    public static void SoundOnly()
    {
        if (!CanRun()) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath) == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }
        SetupSound(BuildSounds());
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Sonido construido. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ 1. generar los clips

    private static Sounds BuildSounds()
    {
        var s = new Sounds
        {
            viento = GetOrCreateSound("Ambiente_Viento", 16f, 101, true, SynthWind),
            zumbido = GetOrCreateSound("Farol_Zumbido", 4f, 202, true, SynthHum),
            drone = GetOrCreateSound("Miedo_Drone", 16f, 404, true, SynthDrone),
            latido = GetOrCreateSound("Miedo_Latido", 0.75f, 505, false, SynthHeartbeat),
            descubrimiento = GetOrCreateSound("Evento_Descubrimiento", 5f, 606, false, SynthDiscovery),
            cambioLejano = GetOrCreateSound("Evento_CambioLejano", 1.6f, 707, false, SynthDistantChange),
            torii = GetOrCreateSound("Torii_Campanas", 12f, 808, true, SynthToriiBells),
            pasos = new AudioClip[4],
        };
        for (int i = 0; i < s.pasos.Length; i++)
            s.pasos[i] = GetOrCreateSound("Paso_" + i, 0.4f, 301 + i, false, SynthStep);
        return s;
    }

    private static AudioClip GetOrCreateSound(string name, float seconds, int seed, bool loop, SoundSynth synth)
    {
        EnsureFolder(AudioDir);
        string path = AudioDir + "/" + name + ".wav";

        var buffer = new float[Mathf.RoundToInt(seconds * SampleRate)];
        synth(buffer, new System.Random(seed)); // semilla fija: siempre sale el mismo sonido

        if (loop)
        {
            // Loops: todos al mismo volumen promedio (RMS 0.1, unos -20 dB), sin pasar de 0.95.
            NormalizeSound(buffer, 0.1f, 0.95f);
        }
        else
        {
            // Sonidos cortos: el pico a 0.9 y un final suave para que no corten con un "clic".
            NormalizeSound(buffer, 0f, 0.9f);
            FadeOut(buffer, 0.02f);
        }

        WriteWav(path, buffer);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        // Cortos: se descomprimen al cargar (cero costo al reproducir).
        // Loops largos: quedan comprimidos en memoria y se descomprimen mientras suenan.
        settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.ADPCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    /// <summary>WAV basico: cabecera RIFF + muestras de 16 bits, mono.</summary>
    private static void WriteWav(string path, float[] samples)
    {
        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);                 // largo del bloque fmt
            w.Write((short)1);           // 1 = PCM sin comprimir
            w.Write((short)1);           // 1 canal
            w.Write(SampleRate);
            w.Write(SampleRate * 2);     // bytes por segundo
            w.Write((short)2);           // bytes por muestra
            w.Write((short)16);          // bits por muestra
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (float f in samples)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(f, -1f, 1f) * 32767f));
        }
    }

    /// <summary>
    /// Quita el desplazamiento (DC) y ajusta el volumen.
    /// targetRms > 0: lleva el volumen promedio a ese valor, sin que el pico pase maxPeak.
    /// targetRms = 0: solo lleva el pico a maxPeak.
    /// </summary>
    private static void NormalizeSound(float[] b, float targetRms, float maxPeak)
    {
        double mean = 0;
        foreach (float f in b) mean += f;
        mean /= b.Length;

        double sumSq = 0;
        float peak = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            b[i] -= (float)mean;
            sumSq += b[i] * b[i];
            peak = Mathf.Max(peak, Mathf.Abs(b[i]));
        }
        if (peak <= 0f) return;

        float rms = (float)System.Math.Sqrt(sumSq / b.Length);
        float gain = maxPeak / peak;
        if (targetRms > 0f) gain = Mathf.Min(targetRms / rms, gain);
        for (int i = 0; i < b.Length; i++) b[i] *= gain;
    }

    private static void FadeOut(float[] b, float seconds)
    {
        int n = Mathf.Min(b.Length, Mathf.RoundToInt(seconds * SampleRate));
        for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= i / (float)n;
    }

    // ------------------------------------------------------------------ herramientas de sintesis

    private static float Bipolar(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    /// <summary>
    /// Oscilador lento que completa 'cycles' vueltas exactas en n muestras.
    /// Al ser un numero entero de vueltas, el final empalma con el principio.
    /// </summary>
    private static float Lfo(int cycles, int i, int n, float phase = 0f)
    {
        return (float)System.Math.Sin(2.0 * System.Math.PI * cycles * i / n + phase);
    }

    private static float Sine(double freq, int i, float phase = 0f)
    {
        return (float)System.Math.Sin(2.0 * System.Math.PI * freq * i / SampleRate + phase);
    }

    private static float SineAt(double freq, float t, float phase = 0f)
    {
        return (float)System.Math.Sin(2.0 * System.Math.PI * freq * t + phase);
    }

    /// <summary>Coeficiente de un filtro pasa bajos de un polo: y += a * (x - y).</summary>
    private static float OnePole(float cutoff)
    {
        return 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
    }

    /// <summary>
    /// Filtro de estado variable (version "TPT", estable a cualquier frecuencia).
    /// BandN deja pasar una banda alrededor de 'cutoff'; Q alto = banda angosta (casi un tono).
    /// </summary>
    private struct Svf
    {
        private float ic1, ic2, a1, a2, a3, k;

        public void Set(float cutoff, float q)
        {
            float g = Mathf.Tan(Mathf.PI * Mathf.Min(cutoff, SampleRate * 0.45f) / SampleRate);
            k = 1f / q;
            a1 = 1f / (1f + g * (g + k));
            a2 = g * a1;
            a3 = g * a2;
        }

        /// <summary>Pasa banda con ganancia 1 en el centro.</summary>
        public float BandN(float x)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            return v1 * k;
        }
    }

    // ------------------------------------------------------------------ 2. los sonidos

    // Viento: ruido que pasa por un filtro de banda que se abre y se cierra (rafagas),
    // un filtro muy angosto que "silba" solo en las rafagas fuertes y un rumor grave.
    // Dos vueltas: en la segunda, los filtros ya traen el estado del final, asi empalma.
    private static void SynthWind(float[] o, System.Random r)
    {
        int n = o.Length;
        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = Bipolar(r);

        var wind = new Svf();
        var whistle = new Svf();
        float rumbleA = OnePole(70f), rumble1 = 0f, rumble2 = 0f;

        for (int lap = 0; lap < 2; lap++)
        for (int i = 0; i < n; i++)
        {
            float slow = 0.5f + 0.5f * (0.6f * Lfo(1, i, n) + 0.4f * Lfo(3, i, n, 1.3f));
            float fast = 0.5f + 0.5f * Lfo(7, i, n, 0.4f);
            float gust = Mathf.Clamp01(slow * 0.85f + fast * 0.15f);

            wind.Set(300f + 450f * gust, 0.9f);
            float w = wind.BandN(white[i]) * (0.25f + 0.75f * gust * gust);

            whistle.Set(820f + 120f * slow + 160f * fast, 14f);
            float s = whistle.BandN(white[i]) * Mathf.Max(0f, gust - 0.45f) * 2.2f;

            rumble1 += rumbleA * (white[i] - rumble1);
            rumble2 += rumbleA * (rumble1 - rumble2);
            float rumble = rumble2 * 5f;

            if (lap == 1) o[i] = w + 0.9f * s + 0.5f * rumble;
        }
    }

    // Zumbido electrico: armonicos de 50 Hz (la frecuencia de la red en Argentina y en el este
    // de Japon), mas fuertes los pares, con un leve vaiven y chisporroteos sueltos.
    // 4 s de loop: 50 Hz por 4 s = 200 ciclos exactos, asi que empalma solo.
    private static void SynthHum(float[] o, System.Random r)
    {
        int n = o.Length;
        var phase = new float[17];
        for (int h = 0; h < phase.Length; h++) phase[h] = (float)(r.NextDouble() * System.Math.PI * 2.0);

        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            for (int h = 2; h <= 16; h++)
            {
                float amp = (h % 2 == 0 ? 1f : 0.55f) / Mathf.Pow(h - 1, 1.1f);
                sum += amp * Sine(50.0 * h, i, phase[h]);
            }
            float wobble = 1f + 0.12f * Lfo(2, i, n) + 0.05f * Lfo(5, i, n, 2f);
            o[i] = sum * wobble;
        }

        // Chisporroteo: clics de ruido muy cortos. Se escriben "dando la vuelta" (modulo n)
        // para que un clic cerca del final siga al principio del loop.
        int clicks = Mathf.RoundToInt(n / (float)SampleRate * 5f);
        int clickLen = Mathf.RoundToInt(0.004f * SampleRate);
        for (int c = 0; c < clicks; c++)
        {
            int start = r.Next(n);
            float amp = 0.05f + 0.35f * Rand(r) * Rand(r);
            for (int j = 0; j < clickLen; j++)
            {
                float t = j / (float)SampleRate;
                o[(start + j) % n] += amp * Bipolar(r) * Mathf.Exp(-t / 0.0008f);
            }
        }
    }

    // Paso sobre piedra mojada: golpe de taco (clic agudo), cuerpo grave que baja de tono,
    // roce de suela, punta del pie un instante despues y un poco de agua.
    private static void SynthStep(float[] o, System.Random r)
    {
        float v = 0.85f + 0.3f * Rand(r);
        float heel = 0.55f + 0.2f * Rand(r);
        float toeTime = 0.075f + 0.03f * Rand(r);
        float thumpFreq = 150f + 40f * Rand(r);

        var click = new Svf(); click.Set(2200f * v, 0.8f);
        var grit = new Svf(); grit.Set(750f * v, 0.7f);
        var toe = new Svf(); toe.Set(2800f * v, 0.9f);
        float splashLow = 0f;
        double thumpPhase = 0.0;

        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = Bipolar(r);

            float c = click.BandN(noise) * Mathf.Exp(-t / 0.006f) * heel;

            float f = thumpFreq * (0.6f + 0.4f * Mathf.Exp(-t / 0.025f));
            thumpPhase += 2.0 * System.Math.PI * f / SampleRate;
            float thump = (float)System.Math.Sin(thumpPhase) * Mathf.Exp(-t / 0.03f) * (1f - Mathf.Exp(-t / 0.002f)) * 0.45f;

            float gt = t - 0.015f;
            float g = grit.BandN(noise) * (gt > 0f ? (1f - Mathf.Exp(-gt / 0.008f)) * Mathf.Exp(-gt / 0.07f) * 0.6f : 0f);

            float tt = t - toeTime;
            float to = toe.BandN(noise) * (tt > 0f ? Mathf.Exp(-tt / 0.005f) * 0.35f : 0f);

            splashLow += 0.35f * (noise - splashLow);
            float st = t - 0.01f;
            float splash = (noise - splashLow) * (st > 0f ? Mathf.Exp(-st / 0.035f) * 0.12f : 0f);

            o[i] = c + thump + g + to + splash;
        }
    }

    // Drone del miedo: notas graves que chocan (La y La#, Mi y Fa: intervalos de semitono),
    // repetidas una octava arriba para que se oigan tambien en parlantes chicos,
    // un poco saturadas, una "respiracion" de ruido y un silbido agudo con vibrato.
    // 16 s de loop: todas las frecuencias son multiplos de 1/16 Hz.
    private static void SynthDrone(float[] o, System.Random r)
    {
        int n = o.Length;
        double[] freqs = { 55.0, 58.25, 82.5, 87.375, 110.0, 116.5, 165.0, 174.75 };
        float[] amps = { 1f, 0.75f, 0.5f, 0.4f, 0.45f, 0.35f, 0.3f, 0.25f };
        int[] swells = { 1, 2, 3, 1, 2, 3, 1, 2 };

        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = Bipolar(r);
        var breath = new Svf();

        for (int lap = 0; lap < 2; lap++)
        for (int i = 0; i < n; i++)
        {
            float chord = 0f;
            for (int k = 0; k < freqs.Length; k++)
                chord += amps[k] * (0.6f + 0.4f * Lfo(swells[k], i, n, k * 1.7f)) * Sine(freqs[k], i);
            chord = (float)System.Math.Tanh(1.4 * chord);

            breath.Set(450f + 150f * Lfo(4, i, n), 2f);
            float inhale = 0.5f + 0.5f * Lfo(4, i, n, -1.57f);
            float b = breath.BandN(white[i]) * inhale * inhale * 0.9f;

            // Vibrato de 5 Hz (80 vueltas en 16 s) aplicado a la fase: sigue empalmando.
            double vibrato = 2.5 * System.Math.Sin(2.0 * System.Math.PI * 80.0 * i / n);
            float high = (float)System.Math.Sin(2.0 * System.Math.PI * 1760.0 * i / SampleRate + vibrato)
                         * 0.035f * Mathf.Max(0f, Lfo(1, i, n, 0.5f));

            if (lap == 1) o[i] = chord * 0.5f + b + high;
        }
    }

    // Latido: dos golpes graves ("lub-dub") que bajan de tono, filtrados para que suenen sordos.
    // Cada golpe tiene ademas un "toc" corto de 120 Hz: sin el, en parlantes chicos no se oiria.
    private static void SynthHeartbeat(float[] o, System.Random r)
    {
        Beat(o, 0f, 1f, r);
        Beat(o, 0.27f, 0.7f, r);

        float a = OnePole(400f), y1 = 0f, y2 = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            y1 += a * (o[i] - y1);
            y2 += a * (y1 - y2);
            o[i] = y2;
        }
    }

    private static void Beat(float[] o, float start, float amp, System.Random r)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate);
        double phase = 0.0;
        float rumble = 0f;
        for (int j = 0; s0 + j < o.Length; j++)
        {
            float t = j / (float)SampleRate;
            float f = 45f + 40f * Mathf.Exp(-t / 0.04f);
            phase += 2.0 * System.Math.PI * f / SampleRate;
            float env = (1f - Mathf.Exp(-t / 0.004f)) * Mathf.Exp(-t / 0.085f);
            float knock = SineAt(120.0, t) * (1f - Mathf.Exp(-t / 0.002f)) * Mathf.Exp(-t / 0.03f) * 0.5f;
            rumble += 0.05f * (Bipolar(r) - rumble);
            o[s0 + j] += amp * (env * (float)System.Math.Sin(phase) + knock + rumble * 2.5f * Mathf.Exp(-t / 0.03f));
        }
    }

    // Descubrimiento: un barrido de ruido que crece (como un platillo al reves) y, en el golpe,
    // un bombo grave mas dos campanas metalicas desafinadas entre si (un semitono).
    private static void SynthDiscovery(float[] o, System.Random r)
    {
        const float hit = 1.1f;
        double[] ratios = { 1.0, 2.32, 4.25, 6.63, 9.38 };   // parciales "inarmonicos": suena a metal
        float[] amps = { 0.5f, 0.35f, 0.25f, 0.18f, 0.12f };
        float[] decays = { 2.2f, 1.5f, 1.0f, 0.7f, 0.5f };

        var sweep = new Svf();
        float low = 0f;
        double boomPhase = 0.0;

        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = Bipolar(r);

            float x = Mathf.Clamp01(t / hit);
            sweep.Set(250f + 2600f * x * x, 1.5f);
            float swell = sweep.BandN(noise) * x * x * x * (t < hit ? 1f : Mathf.Exp(-(t - hit) / 0.01f));

            low += 0.08f * (noise - low);
            float u = t - hit;
            float boom = 0f, burst = 0f, metal = 0f;
            if (u >= 0f)
            {
                float f = 36f + 18f * Mathf.Exp(-u / 0.15f);
                boomPhase += 2.0 * System.Math.PI * f / SampleRate;
                boom = (float)System.Math.Sin(boomPhase) * Mathf.Exp(-u / 0.7f) * (1f - Mathf.Exp(-u / 0.003f));
                burst = low * 3f * Mathf.Exp(-u / 0.12f);
                for (int k = 0; k < ratios.Length; k++)
                {
                    float e = amps[k] * Mathf.Exp(-u / decays[k]);
                    metal += e * SineAt(196.0 * ratios[k], u) + 0.5f * e * SineAt(207.65 * ratios[k], u, 1f);
                }
            }

            o[i] = (float)System.Math.Tanh(swell * 0.9f + boom * 0.9f + burst * 0.4f + metal * 0.5f);
        }
    }

    // Cambio lejano: una persiana metalica que golpea y traquetea, cada rebote mas corto
    // y mas suave. Filtrada como si llegara de lejos, detras de una pared.
    private static void SynthDistantChange(float[] o, System.Random r)
    {
        float low = 0f;
        double phase = 0.0;
        int thudLen = Mathf.Min(o.Length, Mathf.RoundToInt(0.5f * SampleRate));
        for (int i = 0; i < thudLen; i++)
        {
            float t = i / (float)SampleRate;
            low += 0.06f * (Bipolar(r) - low);
            phase += 2.0 * System.Math.PI * 85.0 / SampleRate;
            float attack = 1f - Mathf.Exp(-t / 0.002f);
            o[i] += (low * 3f * Mathf.Exp(-t / 0.07f) + 0.8f * (float)System.Math.Sin(phase) * Mathf.Exp(-t / 0.09f)) * attack;
        }

        float time = 0.07f, gap = 0.085f, amp = 0.75f;
        while (time < 1.0f && amp > 0.05f)
        {
            Impact(o, time, amp, r);
            time += gap * (0.8f + 0.4f * Rand(r));
            gap *= 0.86f;
            amp *= 0.8f;
        }

        float a = OnePole(2200f), y1 = 0f, y2 = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            y1 += a * (o[i] - y1);
            y2 += a * (y1 - y2);
            o[i] = y2;
        }
    }

    private static void Impact(float[] o, float start, float amp, System.Random r)
    {
        float[] modes = { 620f, 1130f, 1870f, 2650f };
        float detune = 0.94f + 0.12f * Rand(r);
        int s0 = Mathf.RoundToInt(start * SampleRate);
        int len = Mathf.RoundToInt(0.12f * SampleRate);
        for (int j = 0; j < len && s0 + j < o.Length; j++)
        {
            float t = j / (float)SampleRate;
            float v = Bipolar(r) * Mathf.Exp(-t / 0.0015f) * 0.6f;
            for (int k = 0; k < modes.Length; k++)
                v += 0.5f / (k + 1) * Mathf.Exp(-t / (0.05f - k * 0.008f)) * SineAt(modes[k] * detune, t, k);
            o[s0 + j] += amp * v;
        }
    }

    // Torii: un acorde hueco (La y Mi, sin tercera), aire suave, campanas de viento en la
    // escala japonesa "in" (Mi, Fa, La, Si, Do) y tres sacudidas de cascabeles de santuario.
    // Las campanas se escriben "dando la vuelta" (modulo n): su cola sigue al principio del loop.
    private static void SynthToriiBells(float[] o, System.Random r)
    {
        int n = o.Length;
        double[] padFreqs = { 110.0, 165.0, 220.0, 220.0 + 1.0 / 12.0, 330.0 }; // multiplos de 1/12 Hz
        float[] padAmps = { 0.5f, 0.35f, 0.3f, 0.3f, 0.15f };

        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = Bipolar(r);
        var air = new Svf();

        for (int lap = 0; lap < 2; lap++)
        for (int i = 0; i < n; i++)
        {
            float pad = 0f;
            for (int k = 0; k < padFreqs.Length; k++)
                pad += padAmps[k] * (0.7f + 0.3f * Lfo(k % 3 + 1, i, n, k)) * Sine(padFreqs[k], i);

            air.Set(1800f + 400f * Lfo(2, i, n), 0.8f);
            float wind = air.BandN(white[i]) * (0.6f + 0.4f * Lfo(3, i, n, 2f)) * 0.07f;

            if (lap == 1) o[i] = pad * 0.35f + wind;
        }

        double[] scale = { 659.26, 698.46, 880.0, 987.77, 1046.5 };
        for (int k = 0; k < 6; k++)
            Chime(o, r.Next(n), scale[r.Next(scale.Length)], 0.25f + 0.2f * Rand(r));

        for (int shake = 0; shake < 3; shake++)
        {
            float t0 = (shake + 0.2f + 0.5f * Rand(r)) * (n / 3f) / SampleRate;
            for (int j = 0; j < 8; j++)
            {
                int start = Mathf.RoundToInt((t0 + 0.45f * Rand(r)) * SampleRate);
                Jingle(o, start, 0.12f + 0.08f * Rand(r), r);
            }
        }
    }

    // Campana de viento: tres parciales con las proporciones de un tubo que vibra libre.
    private static void Chime(float[] o, int start, double freq, float amp)
    {
        double[] ratios = { 1.0, 2.756, 5.404 };
        float[] amps = { 1f, 0.4f, 0.18f };
        float[] decays = { 3.2f, 1.4f, 0.5f };
        int n = o.Length;
        int len = Mathf.Min(n, Mathf.RoundToInt(10f * SampleRate));
        for (int j = 0; j < len; j++)
        {
            float t = j / (float)SampleRate;
            float v = 0f;
            for (int k = 0; k < ratios.Length; k++)
                v += amps[k] * Mathf.Exp(-t / decays[k]) * SineAt(freq * ratios[k], t);
            o[(start + j) % n] += amp * v * (1f - Mathf.Exp(-t / 0.002f));
        }
    }

    // Cascabel (suzu): agudo, corto, con un clic al principio.
    private static void Jingle(float[] o, int start, float amp, System.Random r)
    {
        double freq = 2600.0 + 1600.0 * r.NextDouble();
        double[] ratios = { 1.0, 1.41, 1.83 };
        float decay = 0.12f + 0.13f * Rand(r);
        int n = o.Length;
        int len = Mathf.RoundToInt(0.6f * SampleRate);
        for (int j = 0; j < len; j++)
        {
            float t = j / (float)SampleRate;
            float v = Bipolar(r) * Mathf.Exp(-t / 0.001f) * 0.3f;
            for (int k = 0; k < ratios.Length; k++)
                v += Mathf.Exp(-t / (decay / (k + 1))) * SineAt(freq * ratios[k], t, k) / (k + 1);
            o[(start + j) % n] += amp * v;
        }
    }

    // ------------------------------------------------------------------ 3. colocar el sonido

    private static void SetupSound(Sounds s)
    {
        // 1. Zumbido en cada farol (se edita el prefab: llega a todos los modulos).
        GameObject farol = PrefabUtility.LoadPrefabContents(FarolPath);
        try
        {
            Transform luz = farol.transform.Find("Luz");
            AudioSource hum = luz.GetComponent<AudioSource>();
            if (hum == null) hum = luz.gameObject.AddComponent<AudioSource>();
            Configure(hum, s.zumbido, true, 1f, 200);
            Make3D(hum, 0.5f, 6f);
            // Volumen bajo: con dos o tres faroles cerca, el zumbido se suma.
            farol.GetComponent<LanternLight>().SetupAudio(hum, 0.15f);
            PrefabUtility.SaveAsPrefabAsset(farol, FarolPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(farol);
        }

        FearMeter fear = FindInScene<FearMeter>(null);
        StepCounter steps = FindInScene<StepCounter>(null);
        VisibilityChecker visibility = FindInScene<VisibilityChecker>(null);
        MazeDirector director = FindInScene<MazeDirector>(null);
        LoopExit loopExit = FindInScene<LoopExit>(null);
        Transform exit = FindInScene<Transform>(t => t.name == "ModuloSalida");
        Transform torii = exit != null ? exit.Find("Torii") : null;

        // 2. Objeto "Sonido" en la escena: ambiente, miedo y efectos.
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Sonido") Undo.DestroyObjectImmediate(go);

        var root = new GameObject("Sonido");
        Undo.RegisterCreatedObjectUndo(root, "Crear Sonido");

        AudioSource bed = NewSource(root.transform, "Ambiente", s.viento, true, 10);
        bed.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 6000f;
        AudioSource drone = NewSource(root.transform, "Miedo", s.drone, true, 20);
        drone.volume = 0f;
        AudioSource body = NewSource(root.transform, "Cuerpo", null, false, 30);
        body.bypassReverbZones = true; // el latido esta "adentro" del jugador: sin eco del callejon
        AudioSource stings = NewSource(root.transform, "Efectos", null, false, 40);
        AudioSource distant = NewSource(root.transform, "Lejano", null, false, 60);
        Make3D(distant, 2f, 20f);

        var ambience = root.AddComponent<AmbienceController>();
        ambience.Setup(fear, visibility, director, loopExit, torii,
            fear != null ? fear.transform : null,
            bed, drone, body, stings, distant,
            s.latido, s.descubrimiento, s.cambioLejano);

        // 3. En el Player: pasos, eco de pasos detras y la reverberacion del callejon.
        if (steps != null)
        {
            Transform player = steps.transform;
            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Sonido del jugador");
            DestroyChild(player, "Pasos");
            DestroyChild(player, "PasosEco");
            DestroyChild(player, "Reverb");

            AudioSource stepSource = NewSource(player, "Pasos", null, false, 50);
            AudioSource echo = NewSource(player, "PasosEco", null, false, 70);
            Make3D(echo, 1.5f, 12f);
            echo.transform.localPosition = new Vector3(0f, -0.8f, -4f);

            FootstepAudio footsteps = player.GetComponent<FootstepAudio>();
            if (footsteps == null) footsteps = Undo.AddComponent<FootstepAudio>(player.gameObject);
            Undo.RecordObject(footsteps, "Pasos");
            footsteps.Setup(steps, fear, stepSource, echo, s.pasos);
            EditorUtility.SetDirty(footsteps);

            // La zona de reverberacion viaja con el jugador: siempre esta adentro de ella.
            var reverbGo = new GameObject("Reverb");
            reverbGo.transform.SetParent(player, false);
            var zone = reverbGo.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.Alley;
            zone.minDistance = 40f;
            zone.maxDistance = 50f;
        }

        // 4. El torii: suena en 3D y empieza solo cuando la salida se activa (Play On Awake).
        if (torii != null)
        {
            DestroyChild(torii, "SonidoTorii");
            AudioSource bells = NewSource(torii, "SonidoTorii", s.torii, true, 20);
            bells.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            bells.playOnAwake = true;
            bells.volume = 0.8f;
            Make3D(bells, 2f, 22f);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static AudioSource NewSource(Transform parent, string name, AudioClip clip, bool loop, int priority)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        Configure(source, clip, loop, 1f, priority);
        return source;
    }

    /// <summary>Valores comunes. Prioridad: 0 = lo mas importante; Unity corta primero los numeros altos.</summary>
    private static void Configure(AudioSource source, AudioClip clip, bool loop, float volume, int priority)
    {
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = volume;
        source.priority = priority;
        source.spatialBlend = 0f; // 2D: suena igual en los dos oidos
        source.dopplerLevel = 0f;
    }

    /// <summary>
    /// 3D: el volumen baja con la distancia y el sonido se ubica a izquierda o derecha.
    /// Linear: llega a cero justo en maxDistance (Logarithmic nunca llega a cero).
    /// Doppler en 0: la cinta teletransporta modulos y el doppler haria saltar el tono.
    /// </summary>
    private static void Make3D(AudioSource source, float minDistance, float maxDistance)
    {
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
    }
}
