# -*- coding: utf-8 -*-
"""
Genera los sonidos y las texturas del goteo de Callejones Cruzados (Paso 21).

Sonidos (Assets/_Project/Audio), mono, 44.1 kHz:
    Goteo_0..3.wav        una gota que cae en un charco: un "plip" (burbuja cuyo tono sube
                          rapido), un click al tocar el agua y el eco corto del callejon.
    Goteo_Chapa_0..1.wav  una gota sobre la chapa del techado: un "tic" metalico con varios
                          parciales que se apagan rapido.

Texturas (Assets/_Project/Textures/Goteo), con pixeles duros como en la PS1:
    Charco.png  64 x 64: la forma irregular de un charco (alfa 0 o 1) con el borde mas claro.
    Onda.png    32 x 32: un anillo. El alfa de cada pixel es distinto: al subir el recorte del
                material, el anillo se deshace de a pixeles (asi se "desvanece" la onda).

Uso (desde la carpeta del proyecto):
    pip install pillow
    python Herramientas/generar_goteo.py

Despues: Callejones > Goteo, ventanas y carteles.
"""
import math
import os
import random
import struct
import wave

from PIL import Image

AUDIO = os.path.join("Assets", "_Project", "Audio")
TEXTURAS = os.path.join("Assets", "_Project", "Textures", "Goteo")
SR = 44100


def guardar_wav(ruta, muestras):
    pico = max(1e-6, max(abs(s) for s in muestras))
    escala = 0.8 / pico
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * escala)) * 32767)) for s in muestras))


def eco(seco, rng):
    """Las paredes del callejon: tres reflejos cortos, cada uno mas apagado y mas opaco."""
    salida = list(seco)
    for retardo, ganancia in ((0.037, 0.26), (0.061, 0.17), (0.097, 0.10)):
        d = int((retardo + rng.uniform(-0.004, 0.004)) * SR)
        previo = 0.0
        for i in range(len(seco)):
            if i + d >= len(salida):
                break
            previo += 0.35 * (seco[i] - previo)  # pasa bajos de un polo: el eco suena mas sordo
            salida[i + d] += previo * ganancia
    return salida


def plip(rng, f0):
    n = int(0.36 * SR)
    muestras = [0.0] * n
    fase = 0.0
    subida = rng.uniform(1.0, 1.6)
    for i in range(n):
        t = i / SR
        # La burbuja: el tono sube casi al doble en unos 40 ms (por eso suena "plip").
        f = f0 * (1.0 + subida * min(t, 0.06) / 0.04)
        fase += 2.0 * math.pi * f / SR
        amp = math.exp(-t / 0.022) * (1.0 - math.exp(-t / 0.0008))
        s = math.sin(fase) * amp
        # El golpe contra el agua: un click de ruido muy corto y un "tud" grave.
        if t < 0.004:
            s += rng.uniform(-1.0, 1.0) * 0.45 * math.exp(-t / 0.0008)
        s += math.sin(2.0 * math.pi * 170.0 * t) * 0.14 * math.exp(-t / 0.012)
        muestras[i] = s
    return eco(muestras, rng)


def tic(rng):
    n = int(0.3 * SR)
    muestras = [0.0] * n
    parciales = [(2300, 1.0, 0.05), (3870, 0.6, 0.035), (5150, 0.4, 0.025), (7020, 0.25, 0.015), (430, 0.35, 0.03)]
    variacion = rng.uniform(0.93, 1.07)
    for i in range(n):
        t = i / SR
        s = 0.0
        for f, a, decae in parciales:
            s += math.sin(2.0 * math.pi * f * variacion * t) * a * math.exp(-t / decae)
        if t < 0.003:
            s += rng.uniform(-1.0, 1.0) * 0.6 * math.exp(-t / 0.0006)
        muestras[i] = s * (1.0 - math.exp(-t / 0.0004))
    return eco(muestras, rng)


def charco(rng):
    tam = 64
    img = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
    px = img.load()
    fases = [rng.uniform(0, 2 * math.pi) for _ in range(3)]
    def radio(ang):
        return 27.0 * (0.72 + 0.14 * math.sin(3 * ang + fases[0]) + 0.08 * math.sin(5 * ang + fases[1])
                       + 0.05 * math.sin(9 * ang + fases[2]))
    dentro = [[False] * tam for _ in range(tam)]
    for y in range(tam):
        for x in range(tam):
            dx, dy = x - 31.5, y - 31.5
            if math.hypot(dx, dy) < radio(math.atan2(dy, dx)):
                dentro[y][x] = True
    # Dos charquitos sueltos al costado.
    for _ in range(2):
        cx, cy, r = rng.uniform(8, 56), rng.uniform(8, 56), rng.uniform(2.5, 4.5)
        for y in range(tam):
            for x in range(tam):
                if math.hypot(x - cx, y - cy) < r:
                    dentro[y][x] = True
    for y in range(tam):
        for x in range(tam):
            if not dentro[y][x]:
                continue
            borde = any(not (0 <= x + ox < tam and 0 <= y + oy < tam and dentro[y + oy][x + ox])
                        for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if borde:
                px[x, y] = (62, 68, 76, 255)
            else:
                v = rng.randint(-3, 3)
                px[x, y] = (34 + v, 39 + v, 47 + v, 255)
    return img


def onda(rng):
    tam = 32
    img = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
    px = img.load()
    for y in range(tam):
        for x in range(tam):
            d = math.hypot(x - 15.5, y - 15.5)
            if 11.5 <= d <= 14.6:
                a = 0.32 + 0.68 * rng.random()
            elif 6.0 <= d <= 7.6:
                a = 0.32 + 0.38 * rng.random()  # el anillo de adentro se deshace primero
            else:
                continue
            px[x, y] = (205, 218, 232, int(a * 255))
    return img


def main():
    os.makedirs(AUDIO, exist_ok=True)
    os.makedirs(TEXTURAS, exist_ok=True)
    rng = random.Random(2121)
    for i, f0 in enumerate((980, 1180, 1390, 1620)):
        guardar_wav(os.path.join(AUDIO, "Goteo_%d.wav" % i), plip(rng, f0))
    for i in range(2):
        guardar_wav(os.path.join(AUDIO, "Goteo_Chapa_%d.wav" % i), tic(rng))
    charco(rng).save(os.path.join(TEXTURAS, "Charco.png"))
    onda(rng).save(os.path.join(TEXTURAS, "Onda.png"))
    print("Sonidos en %s y texturas en %s" % (AUDIO, TEXTURAS))


if __name__ == "__main__":
    main()
