# -*- coding: utf-8 -*-
"""
Genera las texturas de los edificios de Callejones Cruzados (Paso 20): las casas que hay
arriba de los puestos, como en Kowloon.

Salen tres PNG chiquitos, con pixeles duros como en la PS1:

    Fachadas.png      atlas de 4 x 4 "pisos": cada celda de 32 x 32 pixeles es un tramo de
                      3 x 3 m de pared con una ventana.
                      Filas    = tipo de pared: azulejos verdes, concreto manchado,
                                 pintura rosa descascarada, azulejos crema.
                      Columnas = ventana: apagada, luz calida con cortina, luz fria de tele,
                                 cortina cerrada con la lucecita roja de un altar.
    Fachadas_Luz.png  la emision del material: las ventanas prendidas a pleno y el resto de la
                      pared muy tenue (BRILLO_PARED), como si la iluminara el resplandor de la
                      ciudad. Sin eso, los pisos altos quedaban negros: las luces de la calle no
                      llegan hasta ahi.
    Paleta.png        16 colores en cuadraditos de 4 x 4: rejas, aires acondicionados, ropa
                      colgada, plantas, tanques, chapas. Los detalles de los edificios son
                      cubos que apuntan todos a un color de esta paleta: un solo material.

Uso (desde la carpeta del proyecto):
    pip install pillow
    python Herramientas/generar_fachadas.py

La herramienta de Unity (Callejones > Edificios, techados y patio) lee estos PNG.
"""
import os
import random
from PIL import Image

SALIDA = os.path.join("Assets", "_Project", "Textures", "Edificios")
CELDA = 32

# Paredes: (color base, color de la junta o de las manchas)
PAREDES = [
    ((112, 150, 132), (84, 112, 99)),    # azulejos verdes (los de Hong Kong)
    ((128, 120, 108), (80, 74, 66)),     # concreto manchado
    ((150, 104, 98), (176, 150, 130)),   # pintura rosa descascarada
    ((170, 158, 120), (128, 118, 90)),   # azulejos crema
]

# Cuanto brilla la pared por si sola (el resplandor de la ciudad), de 0 a 1.
BRILLO_PARED = 0.10

MARCO = (150, 150, 140)
ALFEIZAR = (70, 66, 60)

# Paleta de detalles, en el orden en que la usa AtmosphereBuilder.Buildings.cs.
PALETA = [
    (38, 34, 32),     # 0 reja (hierro oscuro)
    (176, 168, 150),  # 1 aire acondicionado
    (112, 60, 36),    # 2 oxido
    (92, 92, 88),     # 3 cano y soporte
    (150, 44, 38),    # 4 ropa roja
    (52, 82, 140),    # 5 ropa azul
    (206, 200, 186),  # 6 ropa blanca
    (196, 160, 60),   # 7 ropa amarilla
    (186, 104, 136),  # 8 ropa rosa
    (58, 88, 44),     # 9 planta
    (64, 108, 150),   # 10 tanque de agua
    (96, 90, 82),     # 11 concreto (bordes de losa, parapetos)
    (104, 108, 112),  # 12 chapa
    (120, 136, 86),   # 13 plastico del techado
    (124, 84, 52),    # 14 madera
    (14, 12, 10),     # 15 negro
]


def mezclar(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def oscurecer(c, f):
    return tuple(max(0, min(255, int(c[i] * f))) for i in range(3))


def pared(px, ox, oy, estilo, rng):
    base, otro = PAREDES[estilo]
    for y in range(CELDA):
        for x in range(CELDA):
            c = base
            if estilo in (0, 3):
                paso = 3 if estilo == 0 else 4
                if x % paso == 0 or y % paso == 0:
                    c = otro
            elif estilo == 1:
                c = oscurecer(base, 0.9 + 0.2 * rng.random())
            elif estilo == 2:
                c = oscurecer(base, 0.92 + 0.12 * rng.random())
            px[ox + x, oy + y] = c
    # Descascarado (rosa): manchas mas claras donde se cayo la pintura.
    if estilo == 2:
        for _ in range(3):
            cx, cy = rng.randrange(2, 30), rng.randrange(2, 30)
            for y in range(cy - 2, cy + 3):
                for x in range(cx - 3, cx + 4):
                    if 0 <= x < CELDA and 0 <= y < CELDA and rng.random() < 0.7:
                        px[ox + x, oy + y] = otro
    # Manchas de agua que bajan (todas las paredes): la humedad de Kowloon.
    for _ in range(4):
        x = rng.randrange(0, CELDA)
        largo = rng.randrange(6, 20)
        y0 = rng.randrange(0, 12)
        for y in range(y0, min(CELDA, y0 + largo)):
            c = px[ox + x, oy + y]
            px[ox + x, oy + y] = oscurecer(c, 0.78)
    # Borde de la losa abajo.
    for x in range(CELDA):
        px[ox + x, oy + CELDA - 1] = oscurecer(base, 0.5)
        px[ox + x, oy + CELDA - 2] = oscurecer(base, 0.7)


def ventana(px, luz, ox, oy, tipo, rng):
    """Ventana de 18 x 15 px (1.7 x 1.4 m) con marco de aluminio, montante y alfeizar."""
    x0, x1, y0, y1 = 7, 24, 6, 20
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            borde = x in (x0, x1) or y in (y0, y1) or x == 15 or y == 10
            if borde:
                px[ox + x, oy + y] = MARCO
                continue
            t = (y - y0) / float(y1 - y0)
            brillo = None
            if tipo == 0:   # apagada: vidrio oscuro con un reflejo
                c = (20, 26, 31)
                if (x + y) % 7 == 0 and y < 15:
                    c = (42, 54, 64)
            elif tipo == 1:  # luz calida, cortina corrida a la izquierda
                c = mezclar((236, 176, 92), (176, 110, 56), t)
                if x <= x0 + 4:
                    c = (150, 76, 44)
                if 19 <= x <= 21 and 12 <= y <= 19:  # una lampara de pie
                    c = (80, 44, 24)
                brillo = c
            elif tipo == 2:  # luz fria de tele
                c = mezclar((54, 84, 126), (30, 44, 70), t)
                if 17 <= x <= 22 and 13 <= y <= 17:
                    c = (130, 180, 228)
                if 9 <= x <= 13 and 15 <= y <= 19:  # respaldo de una silla
                    c = (16, 20, 28)
                brillo = c
            else:            # cortina cerrada y la lucecita roja de un altar
                c = (86, 40, 34) if (x % 3) else (70, 32, 28)
                brillo = oscurecer(c, 0.5)
                if 9 <= x <= 11 and 16 <= y <= 18:
                    c = (255, 70, 50)
                    brillo = c
                elif 8 <= x <= 12 and 15 <= y <= 19:
                    c = (170, 52, 40)
                    brillo = c
            px[ox + x, oy + y] = c
            if brillo is not None:
                luz[ox + x, oy + y] = brillo
    for x in range(x0 - 1, x1 + 2):
        px[ox + x, oy + y1 + 1] = ALFEIZAR
    # La mugre que chorrea del alfeizar.
    for x in (x0, x1):
        for y in range(y1 + 2, min(CELDA - 2, y1 + 2 + rng.randrange(3, 8))):
            px[ox + x, oy + y] = oscurecer(px[ox + x, oy + y], 0.7)


def main():
    os.makedirs(SALIDA, exist_ok=True)
    atlas = Image.new("RGB", (CELDA * 4, CELDA * 4))
    luz = Image.new("RGB", (CELDA * 4, CELDA * 4), (0, 0, 0))
    pa, pl = atlas.load(), luz.load()
    for fila in range(4):
        for col in range(4):
            rng = random.Random(1000 + fila * 10 + col)
            ox, oy = col * CELDA, fila * CELDA
            pared(pa, ox, oy, fila, rng)
            ventana(pa, pl, ox, oy, col, rng)
    # Lo que no es una ventana prendida brilla apenas, con el color de la pared.
    for y in range(CELDA * 4):
        for x in range(CELDA * 4):
            if pl[x, y] == (0, 0, 0):
                pl[x, y] = oscurecer(pa[x, y], BRILLO_PARED)
    atlas.save(os.path.join(SALIDA, "Fachadas.png"))
    luz.save(os.path.join(SALIDA, "Fachadas_Luz.png"))

    paleta = Image.new("RGB", (16, 16))
    pp = paleta.load()
    for i, c in enumerate(PALETA):
        cx, cy = (i % 4) * 4, (i // 4) * 4
        for y in range(4):
            for x in range(4):
                pp[cx + x, cy + y] = c
    paleta.save(os.path.join(SALIDA, "Paleta.png"))
    print("Fachadas.png, Fachadas_Luz.png y Paleta.png en %s" % SALIDA)


if __name__ == "__main__":
    main()
