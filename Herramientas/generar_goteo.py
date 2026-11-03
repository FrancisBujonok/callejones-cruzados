# -*- coding: utf-8 -*-
"""
Genera los sonidos y las texturas del goteo de Callejones Cruzados (Paso 21, sonidos nuevos en
el Paso 22).

Sonidos (Assets/_Project/Audio), mono, 44.1 kHz. Simples a proposito: el callejon (la zona de
reverberacion del jugador) ya les pone el espacio, asi que aca no hay eco.
    Goteo_0..1.wav      una gota en el piso: un "tic" corto y suave (un tono que se apaga en
                        unos 10 ms, con una subida minima) y un golpecito grave casi inaudible.
    Goteo_Chapa_0.wav   una gota sobre la chapa del techado: un "tin" con dos parciales.

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


def tic(rng, f0, largo=0.12):
    """Gota en el piso: un tono corto que sube apenas y un golpecito grave."""
    n = int(largo * SR)
    muestras = [0.0] * n
    fase = 0.0
    previo = 0.0
    for i in range(n):
        t = i / SR
        f = f0 * (1.0 + 0.12 * min(t, 0.015) / 0.015)
        fase += 2.0 * math.pi * f / SR
        ataque = 1.0 - math.exp(-t / 0.0006)
        s = math.sin(fase) * math.exp(-t / 0.010) * ataque
        s += math.sin(2.0 * math.pi * 300.0 * t) * 0.22 * math.exp(-t / 0.006) * ataque
        previo += 0.5 * (s - previo)  # pasa bajos suave: sin brillo metalico
        muestras[i] = previo
    return muestras


def tin(rng, largo=0.15):
    """Gota en la chapa: dos parciales y un grave corto, sin ruido."""
    n = int(largo * SR)
    muestras = [0.0] * n
    v = rng.uniform(0.96, 1.04)
    for i in range(n):
        t = i / SR
        ataque = 1.0 - math.exp(-t / 0.0004)
        s = (math.sin(2.0 * math.pi * 2100.0 * v * t) * math.exp(-t / 0.018)
             + 0.35 * math.sin(2.0 * math.pi * 3400.0 * v * t) * math.exp(-t / 0.010)
             + 0.2 * math.sin(2.0 * math.pi * 520.0 * t) * math.exp(-t / 0.012))
        muestras[i] = s * ataque
    return muestras


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
    for i, f0 in enumerate((1250, 1500)):
        guardar_wav(os.path.join(AUDIO, "Goteo_%d.wav" % i), tic(rng, f0))
    guardar_wav(os.path.join(AUDIO, "Goteo_Chapa_0.wav"), tin(rng))
    texturas = random.Random(2122)  # aparte: si cambian los sonidos, las texturas no cambian
    charco(texturas).save(os.path.join(TEXTURAS, "Charco.png"))
    onda(texturas).save(os.path.join(TEXTURAS, "Onda.png"))
    print("Sonidos en %s y texturas en %s" % (AUDIO, TEXTURAS))


if __name__ == "__main__":
    main()
