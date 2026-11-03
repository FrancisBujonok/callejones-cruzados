# -*- coding: utf-8 -*-
"""
Genera las calles que se ven al salir del callejon por el torii (Paso 22). Tres calles
distintas; el juego elige una en cada partida (ExitStreet).

Detras del torii hay una caja abierta de 8.8 m de ancho y 10 m de largo: el piso, dos fachadas
a los costados y un telon al fondo. El piso y las fachadas son lo que hay de verdad en esos
10 m. El telon es una foto de la calle que sigue, tomada desde donde termina el juego (justo
antes del torii): este script la "saca" lanzando un rayo por cada pixel contra una calle
simple (piso, fachadas, edificios de atras y un fondo lejano) y despues dibuja encima los
faroles, carteles, semaforos, postes y gente. Como las fachadas del telon son las mismas que
las de los costados, la calle sigue sin corte.

Medidas (coordenadas del modulo de salida, en metros; z hacia adelante, y hacia arriba):
    ojo en (0, 1.6, 0); telon en z = 10; fachadas en x = -4.4 y x = 4.4 desde z = 0 (donde terminan
    las paredes del callejon); vereda desde |x| = 3.
    20 pixeles por metro (5 cm por pixel), como el resto: pixeles duros.

Salen, por cada calle N (0, 1 y 2), en Assets/_Project/Textures/Calles:
    CalleN_Suelo.png   piso de los primeros 10 m (x de -4.4 a 4.4, z de 0 a 10)
    CalleN_Izq.png     fachada izquierda de los primeros 10 m (columna = z)
    CalleN_Der.png     fachada derecha
    CalleN_Fondo.png   el telon (8.8 x 18 m), con el cielo transparente: atras se ve el del juego

    0  Calle de faroles: guirnaldas de faroles, negocios abiertos, semaforo y cartel de
       contramano en la esquina, una moto que viene (como la foto de Taipei).
    1  Callecita tranquila: casas bajas, plantas, una expendedora, postes con cables y pocas
       luces; al fondo, un negocio con luz calida.
    2  Avenida: edificios mas altos, un almacen de 24 horas con luz blanca, un camion
       estacionado, semaforos y una senda peatonal.

Uso (desde la carpeta del proyecto):
    pip install pillow numpy
    python Herramientas/generar_calles.py

Despues: Callejones > Calle de la salida (arma la calle detras del torii).
"""
import math
import os
import random

import numpy as np
from PIL import Image

SALIDA = os.path.join("Assets", "_Project", "Textures", "Calles")
PX = 20                      # pixeles por metro
EYE = np.array([0.0, 1.6, 0.0])
ZB = 10.0                    # telon
SIDE = 4.4                   # linea de fachadas
CURB = 3.0                   # cordon de la vereda
Z0 = 0.0                     # donde empiezan las fachadas de los costados (fin de las paredes del callejon)
ZFAR = 70.0                  # fondo lejano
BACK = 11.0                  # segunda fila de edificios (por encima de las fachadas)
FH = 18.0                    # alto de las fachadas (textura)
BH = 18.0                    # alto del telon (igual que las fachadas: si no, lo de atras se cortaria)
HAZE = np.array([38.0, 30.0, 46.0])  # bruma: el color del cielo cerca del horizonte


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=float)


# ------------------------------------------------------------------ fachadas

class Facade:
    """Una fila de edificios a lo largo de z (columna = z * PX), con su luz sobre la vereda."""

    def __init__(self, length, height, rng):
        self.w = int(round(length * PX))
        self.h = int(round(height * PX))
        self.img = np.zeros((self.h, self.w, 4), dtype=float)
        self.light = np.zeros((self.w, 3), dtype=float)  # luz que tira cada columna sobre la vereda
        self.rng = rng

    def rect(self, z0, z1, y0, y1, color, alpha=255.0):
        c0, c1 = int(round((z0 - Z0) * PX)), int(round((z1 - Z0) * PX))
        r0, r1 = self.h - int(round(y1 * PX)), self.h - int(round(y0 * PX))
        c0, c1 = max(0, c0), min(self.w, c1)
        r0, r1 = max(0, r0), min(self.h, r1)
        if c1 <= c0 or r1 <= r0:
            return
        self.img[r0:r1, c0:c1, :3] = color
        self.img[r0:r1, c0:c1, 3] = alpha

    def noise(self, z0, z1, y0, y1, amount):
        c0, c1 = max(0, int((z0 - Z0) * PX)), min(self.w, int((z1 - Z0) * PX))
        r0, r1 = max(0, self.h - int(y1 * PX)), min(self.h, self.h - int(y0 * PX))
        if c1 <= c0 or r1 <= r0:
            return
        n = self.rng.random((r1 - r0, c1 - c0, 1)) if hasattr(self.rng, "random") else 0
        self.img[r0:r1, c0:c1, :3] *= 1.0 - amount + 2 * amount * n

    def glow(self, z0, z1, color, strength):
        c0, c1 = max(0, int((z0 - Z0) * PX)), min(self.w, int((z1 - Z0) * PX))
        self.light[c0:c1] += color * strength


def glyphs(f, z0, z1, y0, y1, color, rng):
    """Letras inventadas: bloques claros en un cartel (de lejos parecen caracteres)."""
    n = max(1, int((z1 - z0) / 0.45))
    step = (z1 - z0) / n
    for i in range(n):
        gz = z0 + i * step + step * 0.18
        gw = step * 0.64
        for _ in range(3):
            a = gz + rng.uniform(0, gw * 0.6)
            b = min(gz + gw, a + rng.uniform(gw * 0.25, gw * 0.7))
            c = rng.uniform(y0 + 0.05, y1 - 0.12)
            f.rect(a, b, c, c + rng.uniform(0.05, 0.12), color)


def building_row(f, nprng, rng, style):
    """Llena una fachada con edificios de un estilo: 'faroles', 'barrio' o 'avenida'."""
    walls = {
        "faroles": ["#8A7A6A", "#9C8E7E", "#7E6F62", "#A39A8C", "#6F7A72"],
        "barrio": ["#77716A", "#8B8274", "#6E6A66", "#857B70"],
        "avenida": ["#8E8A84", "#7F858A", "#A09A90", "#6C7276", "#93897D"],
    }[style]
    widths = {"faroles": (3.5, 6.5), "barrio": (4.0, 8.0), "avenida": (5.0, 9.0)}[style]
    floors = {"faroles": (2, 4), "barrio": (1, 2), "avenida": (3, 5)}[style]
    z = Z0
    while z < ZFAR:
        w = rng.uniform(*widths)
        z1 = min(ZFAR + 2, z + w)
        n = rng.randint(*floors)
        top = 3.4 + 3.0 * n
        wall = rgb(rng.choice(walls)) * rng.uniform(0.75, 0.95)
        f.rect(z, z1, 0, top, wall * 0.62)            # la pared (de noche, oscura)
        f.rect(z, z1, top, top + 0.35, wall * 0.42)   # parapeto
        f.rect(z + 0.05, z + 0.12, 0, top, wall * 0.4)  # junta entre edificios
        if rng.random() < 0.35:                        # tanque de agua en la azotea
            tz = rng.uniform(z + 0.5, max(z + 0.6, z1 - 1.5))
            f.rect(tz, tz + 1.0, top + 0.35, top + 1.4, rgb("#3F5A6E") * 0.6)

        # Planta baja
        kind = rng.random()
        if style == "barrio":
            if kind < 0.45:   # pared con plantas
                f.rect(z, z1, 0, 1.8, wall * 0.5)
                for _ in range(int(w * 1.5)):
                    bz = rng.uniform(z, z1)
                    f.rect(bz, bz + rng.uniform(0.3, 0.8), 0, rng.uniform(0.6, 1.6), rgb("#1F3A22") * rng.uniform(0.7, 1.2))
            elif kind < 0.6:  # expendedora
                vz = rng.uniform(z + 0.3, z1 - 1.2)
                f.rect(vz, vz + 0.9, 0, 1.8, rgb("#D8E8F0"))
                f.rect(vz + 0.08, vz + 0.82, 0.9, 1.6, rgb("#F4F8FF"))
                for k in range(3):
                    f.rect(vz + 0.12 + k * 0.24, vz + 0.28 + k * 0.24, 1.0, 1.15, rgb(rng.choice(["#D04040", "#4070D0", "#E0C040"])))
                f.glow(vz - 0.6, vz + 1.5, rgb("#B8D0FF"), 0.9)
            else:             # puerta y ventana de casa
                dz = rng.uniform(z + 0.4, z1 - 1.2)
                f.rect(dz, dz + 0.9, 0, 2.1, rgb("#2A2420"))
                if rng.random() < 0.5:
                    f.rect(dz + 1.4, dz + 2.6, 1.0, 2.0, rgb("#E0B070"))
                    f.glow(dz + 1.0, dz + 3.0, rgb("#E0A060"), 0.35)
        elif style == "avenida" and kind < 0.3:  # almacen de 24 horas
            f.rect(z + 0.2, z1 - 0.2, 0.0, 3.0, rgb("#F2F6F0"))
            f.rect(z + 0.2, z1 - 0.2, 3.0, 3.5, rgb("#2E9E5A"))
            f.rect(z + 0.2, z1 - 0.2, 2.6, 2.75, rgb("#C03030"))
            for k in range(int((z1 - z - 0.4) / 0.8)):
                sz = z + 0.4 + k * 0.8
                f.rect(sz, sz + 0.5, 0.3, 2.0, rgb("#C8D0C8"))
            f.glow(z - 0.5, z1 + 0.5, rgb("#E8FFF0"), 1.4)
        else:
            if kind < 0.55:   # negocio abierto
                lit = rgb(rng.choice(["#F0C070", "#F4D8A0", "#E8E0C8", "#F0A060"]))
                f.rect(z + 0.15, z1 - 0.15, 0.0, 2.6, lit * 0.9)
                for k in range(int((z1 - z) / 1.1)):          # cosas adentro
                    sz = z + 0.3 + k * 1.1
                    f.rect(sz, sz + 0.6, 0.0, rng.uniform(0.8, 1.6), lit * 0.45)
                f.glow(z, z1, lit, 1.0)
            elif kind < 0.85:  # persiana
                f.rect(z + 0.15, z1 - 0.15, 0.0, 2.6, rgb("#5A5A5C"))
                for yy in np.arange(0.1, 2.6, 0.12):
                    f.rect(z + 0.15, z1 - 0.15, yy, yy + 0.03, rgb("#3C3C3E"))
            else:              # entrada oscura
                f.rect(z + 0.5, z + 1.5, 0.0, 2.3, rgb("#1A1614"))
            # cartel sobre el negocio
            board = rgb(rng.choice(["#B02A20", "#1E5E3A", "#203E7A", "#C8A030", "#E8E0D0"]))
            f.rect(z + 0.2, z1 - 0.2, 2.75, 3.3, board)
            glyphs(f, z + 0.3, z1 - 0.3, 2.8, 3.25, rgb("#F4ECD8") if board.mean() < 140 else rgb("#2A2018"), rng)
            if style == "faroles" and rng.random() < 0.5:     # toldo
                f.rect(z + 0.1, z1 - 0.1, 2.6, 2.75, rgb(rng.choice(["#7A2A20", "#2A4A6A", "#6A5A2A"])))

        # Pisos de arriba
        for k in range(n):
            y0 = 3.4 + 3.0 * k
            f.rect(z, z1, y0 - 0.06, y0 + 0.04, wall * 0.45)  # borde de losa
            nw = max(1, int((z1 - z) / 1.7))
            sp = (z1 - z) / nw
            for i in range(nw):
                wz = z + i * sp + sp * 0.2
                ww = sp * 0.6
                r = rng.random()
                if r < 0.33:
                    c = rgb(rng.choice(["#E8B068", "#F0C890", "#D89858"]))
                elif r < 0.43:
                    c = rgb("#7090C8")
                else:
                    c = rgb("#1A1E24")
                f.rect(wz, wz + ww, y0 + 0.9, y0 + 2.2, c)
                f.rect(wz, wz + ww, y0 + 1.5, y0 + 1.56, wall * 0.5)  # marco
                if rng.random() < 0.35:   # aire acondicionado
                    f.rect(wz + ww * 0.1, wz + ww * 0.1 + 0.7, y0 + 0.35, y0 + 0.8, rgb("#9A968C") * 0.7)
                if style != "avenida" and rng.random() < 0.3:  # baranda de balcon
                    f.rect(wz - 0.1, wz + ww + 0.1, y0 + 0.85, y0 + 0.9, rgb("#2A2A2A"))
                    for bz in np.arange(wz - 0.1, wz + ww + 0.1, 0.18):
                        f.rect(bz, bz + 0.03, y0 + 0.2, y0 + 0.9, rgb("#2A2A2A"))
        z = z1


def make_facade(seed, style):
    rng = random.Random(seed)
    nprng = np.random.default_rng(seed)
    f = Facade(ZFAR + 2 - Z0, FH, nprng)
    building_row(f, nprng, rng, style)
    # textura: un poco de ruido para que no sea plano
    m = f.img[..., 3] > 0
    f.img[..., :3] *= np.where(m[..., None], 0.94 + 0.12 * nprng.random(f.img.shape[:2])[..., None], 1.0)
    return f


def skyline(seed, low, high, windows, width=140.0):
    """Edificios lejanos: una silueta con pocas ventanas (fondo y segunda fila)."""
    rng = random.Random(seed)
    w, h = int(width * PX / 2), int(high * PX / 2)  # a media resolucion: estan lejos
    img = np.zeros((h, w, 4), dtype=float)
    x = 0
    while x < w:
        bw = int(rng.uniform(4, 12) * PX / 2)
        bh = int(rng.uniform(low, high) * PX / 2)
        col = rgb("#2A2830") * rng.uniform(0.8, 1.2)
        img[h - bh:, x:x + bw, :3] = col
        img[h - bh:, x:x + bw, 3] = 255
        for _ in range(int(bw * bh * windows / 60)):
            wx, wy = rng.randrange(x, min(w, x + bw)), rng.randrange(h - bh, h)
            img[wy:wy + 2, wx:wx + 2, :3] = rgb(rng.choice(["#D8A860", "#E8C890", "#8098C8"]))
        x += bw
    return img


# ------------------------------------------------------------------ el piso

def ground(x, z, f_left, f_right, style, lamps, near=False):
    """Color del piso en (x, z). x y z son arreglos."""
    rng = np.random.default_rng(7)
    ax = np.abs(x)
    asphalt = rgb("#26272A")
    walk = rgb("#4A4A4C")
    col = np.where((ax >= CURB)[..., None], walk, asphalt) * 1.0
    # baldosas de la vereda
    seam = ((np.mod(z, 0.6) < 0.04) | (np.mod(ax, 0.6) < 0.04)) & (ax >= CURB)
    col = np.where(seam[..., None], col * 0.75, col)
    curb = (ax >= CURB - 0.12) & (ax < CURB)
    col = np.where(curb[..., None], rgb("#5A5A58"), col)
    # senda peatonal (zebra) junto al torii, y en la avenida tambien mas adelante
    zebra = (ax < CURB - 0.3) & (np.mod(x + 10.0, 0.9) < 0.45)
    band = (z > 0.9) & (z < 2.7)
    if style == "avenida":
        band = band | ((z > 27.0) & (z < 29.5))
    col = np.where((zebra & band)[..., None], rgb("#B8B8B0"), col)
    if style == "avenida":   # linea del medio
        dash = (ax < 0.07) & (np.mod(z, 4.0) < 2.0) & (z > 3.0)
        col = np.where(dash[..., None], rgb("#C8C8B8"), col)
    # luz de los negocios sobre la vereda (y un poco sobre la calle)
    zi = np.clip(((z - Z0) * PX).astype(int), 0, f_left.w - 1)
    light_l = f_left.light[zi]
    light_r = f_right.light[zi]
    fall_l = np.exp(-(x + SIDE) / 1.3)[..., None]
    fall_r = np.exp(-(SIDE - x) / 1.3)[..., None]
    lit = light_l * fall_l + light_r * fall_r
    # faroles de la calle
    for lx, lz, lc, li in lamps:
        d2 = (x - lx) ** 2 + (z - lz) ** 2
        lit = lit + rgb(lc)[None, None, :] * li * np.exp(-d2 / 5.0)[..., None]
    col = col * (0.55 + np.minimum(lit / 255.0, 1.0) * 1.3)
    if near:
        # el resplandor rojo del torii, que esta justo detras
        red = np.exp(-(z ** 2) / 7.0) * np.exp(-(x ** 2) / 10.0)
        col = col + np.array([115.0, 24.0, 14.0]) * red[..., None]
    n = rng.random(col.shape[:2])[..., None]
    return np.clip(col * (0.9 + 0.2 * n), 0, 255)


# ------------------------------------------------------------------ el telon

def project(p):
    """Punto 3D -> pixel del telon (columna, fila) y escala (pixeles por metro a esa distancia)."""
    t = (ZB - EYE[2]) / (p[2] - EYE[2])
    X = EYE[0] + t * (p[0] - EYE[0])
    Y = EYE[1] + t * (p[1] - EYE[1])
    W, H = int(SIDE * 2 * PX), int(BH * PX)
    col = (X + SIDE) * PX
    row = H - Y * PX
    return col, row, t * PX


def render_backdrop(f_left, f_right, sky_far, back_l, back_r, style, lamps, ss=2):
    W, H = int(SIDE * 2 * PX), int(BH * PX)
    Ws, Hs = W * ss, H * ss
    cols = (np.arange(Ws) + 0.5) / ss
    rows = (np.arange(Hs) + 0.5) / ss
    X = cols / PX - SIDE
    Y = BH - rows / PX
    Xg, Yg = np.meshgrid(X, Y)
    dx, dy, dz = Xg - EYE[0], Yg - EYE[1], np.full_like(Xg, ZB - EYE[2])
    out = np.zeros((Hs, Ws, 4))
    depth = np.full((Hs, Ws), np.inf)

    def take(mask, color, dist, alpha=None):
        nonlocal out, depth
        m = mask & (dist < depth)
        if alpha is not None:
            m = m & (alpha > 0)
        depth = np.where(m, dist, depth)
        out[..., :3] = np.where(m[..., None], color, out[..., :3])
        out[..., 3] = np.where(m, 255, out[..., 3])

    def facade_hit(xp, fac, flip):
        t = (xp - EYE[0]) / dx
        z = EYE[2] + t * dz
        y = EYE[1] + t * dy
        ok = (t > 0) & (z >= ZB) & (z < ZFAR) & (y >= 0) & (y < FH)
        c = np.clip(((z - Z0) * PX).astype(int), 0, fac.w - 1)
        r = np.clip((fac.h - y * PX).astype(int), 0, fac.h - 1)
        tex = fac.img[r, c]
        return ok, tex[..., :3], tex[..., 3], t * np.sqrt(dx ** 2 + dy ** 2 + dz ** 2)

    for xp, fac in ((-SIDE, f_left), (SIDE, f_right)):
        ok, colr, a, dist = facade_hit(xp, fac, False)
        take(ok, colr, dist, a)

    # segunda fila (por encima de las fachadas) y fondo lejano
    def plane_hit_x(xp, img, width_m, height_m):
        t = (xp - EYE[0]) / dx
        z = EYE[2] + t * dz
        y = EYE[1] + t * dy
        ok = (t > 0) & (z >= ZB) & (z < ZFAR) & (y >= 0) & (y < height_m)
        c = np.clip(((z - ZB) / width_m * img.shape[1]).astype(int), 0, img.shape[1] - 1)
        r = np.clip(((height_m - y) / height_m * img.shape[0]).astype(int), 0, img.shape[0] - 1)
        tex = img[r, c]
        return ok, tex[..., :3], tex[..., 3], t * np.sqrt(dx ** 2 + dy ** 2 + dz ** 2)

    for xp, img in ((-BACK, back_l), (BACK, back_r)):
        ok, colr, a, dist = plane_hit_x(xp, img, ZFAR - ZB, img.shape[0] * 2 / PX)
        take(ok, colr, dist, a)

    t = (ZFAR - EYE[2]) / dz
    xf, yf = EYE[0] + t * dx, EYE[1] + t * dy
    hfar = sky_far.shape[0] * 2 / PX
    okf = (yf >= 0) & (yf < hfar) & (np.abs(xf) < 70)
    cf = np.clip(((xf + 70) / 140 * sky_far.shape[1]).astype(int), 0, sky_far.shape[1] - 1)
    rf = np.clip(((hfar - yf) / hfar * sky_far.shape[0]).astype(int), 0, sky_far.shape[0] - 1)
    tex = sky_far[rf, cf]
    take(okf, tex[..., :3], t * np.sqrt(dx ** 2 + dy ** 2 + dz ** 2), tex[..., 3])

    # piso
    down = dy < 0
    tg = np.where(down, -EYE[1] / np.where(down, dy, -1), np.inf)
    xg, zg = EYE[0] + tg * dx, EYE[2] + tg * dz
    okg = down & (zg >= ZB) & (np.abs(xg) <= SIDE)
    gcol = ground(np.where(okg, xg, 0.0), np.where(okg, zg, ZB), f_left, f_right, style, lamps)
    take(okg, gcol, tg * np.sqrt(dx ** 2 + dy ** 2 + dz ** 2))

    # la bruma de lo lejano (la que el shader no puede poner: todo el telon esta a 10 m)
    haze = np.clip((depth - 18.0) / 70.0, 0, 0.62)
    haze = np.where(np.isfinite(depth), haze, 0)
    out[..., :3] = out[..., :3] * (1 - haze[..., None]) + HAZE * haze[..., None]
    return out, depth, ss


class Painter:
    """Dibuja objetos 3D sobre el telon, con prueba de profundidad."""

    def __init__(self, img, depth, ss):
        self.img, self.depth, self.ss = img, depth, ss
        self.H, self.W = depth.shape

    def dist(self, p):
        return float(np.linalg.norm(np.array(p) - EYE))

    def haze(self, color, d):
        h = min(0.62, max(0.0, (d - 18.0) / 70.0))
        return np.array(color) * (1 - h) + HAZE * h

    def disc(self, p, radius, color, glow=0.0, glow_color=None):
        c, r, s = project(p)
        c, r, rad = c * self.ss, r * self.ss, max(0.6, radius * s * self.ss)
        d = self.dist(p)
        col = self.haze(color, d)
        rr = rad * (1 + glow * 2.5)
        c0, c1 = int(max(0, c - rr - 1)), int(min(self.W, c + rr + 2))
        r0, r1 = int(max(0, r - rr - 1)), int(min(self.H, r + rr + 2))
        if c1 <= c0 or r1 <= r0:
            return
        yy, xx = np.mgrid[r0:r1, c0:c1]
        dd = np.sqrt((xx + 0.5 - c) ** 2 + (yy + 0.5 - r) ** 2)
        front = d < self.depth[r0:r1, c0:c1]
        if glow > 0:
            g = np.clip(1 - (dd - rad) / (rr - rad + 1e-6), 0, 1) ** 2 * glow * front
            gc = np.array(glow_color if glow_color is not None else color)
            reg = self.img[r0:r1, c0:c1]
            reg[..., :3] = reg[..., :3] * (1 - g[..., None] * 0.6) + gc * g[..., None] * 0.6
            reg[..., 3] = np.maximum(reg[..., 3], g * 200)
        m = (dd <= rad) & front
        self.img[r0:r1, c0:c1, :3][m] = col
        self.img[r0:r1, c0:c1, 3][m] = 255
        self.depth[r0:r1, c0:c1][m] = d

    def quad(self, pts, color):
        """Cuadrilatero 3D (4 puntos en orden)."""
        proj = [project(p) for p in pts]
        poly = [(c * self.ss, r * self.ss) for c, r, _ in proj]
        d = self.dist(np.mean(np.array(pts), axis=0))
        col = self.haze(color, d)
        xs, ys = [p[0] for p in poly], [p[1] for p in poly]
        c0, c1 = int(max(0, min(xs))), int(min(self.W, max(xs) + 1))
        r0, r1 = int(max(0, min(ys))), int(min(self.H, max(ys) + 1))
        if c1 <= c0 or r1 <= r0:
            return
        yy, xx = np.mgrid[r0:r1, c0:c1]
        px, py = xx + 0.5, yy + 0.5
        inside = np.ones(px.shape, dtype=bool)
        sign = None
        for i in range(4):
            ax, ay = poly[i]
            bx, by = poly[(i + 1) % 4]
            cr = (bx - ax) * (py - ay) - (by - ay) * (px - ax)
            s = cr >= 0
            if sign is None:
                sign = s
                inside &= np.ones_like(s)
                first = s
            inside &= (s == first)
        m = inside & (d < self.depth[r0:r1, c0:c1])
        self.img[r0:r1, c0:c1, :3][m] = col
        self.img[r0:r1, c0:c1, 3][m] = 255
        self.depth[r0:r1, c0:c1][m] = d

    def line(self, a, b, width, color, steps=60):
        for i in range(steps + 1):
            t = i / steps
            p = np.array(a) * (1 - t) + np.array(b) * t
            self.disc(p, width, color)

    def catenary(self, a, b, sag, width, color, steps=50):
        for i in range(steps + 1):
            t = i / steps
            p = np.array(a) * (1 - t) + np.array(b) * t
            p[1] -= sag * 4 * t * (1 - t)
            self.disc(p, width, color)

    def person(self, x, z, h=1.7, color=(16, 14, 16)):
        self.quad([(x - 0.22, 0, z), (x + 0.22, 0, z), (x + 0.2, h - 0.25, z), (x - 0.2, h - 0.25, z)], color)
        self.disc((x, h - 0.12, z), 0.13, color)


def lantern_string(p, z, y=5.2, sag=0.5, lit=(255, 120, 45)):
    a, b = (-SIDE + 0.2, y, z), (SIDE - 0.2, y, z)
    p.catenary(a, b, sag, 0.012, (20, 18, 18))
    for x in np.arange(-SIDE + 0.6, SIDE - 0.5, 0.8):
        t = (x - a[0]) / (b[0] - a[0])
        yy = y - sag * 4 * t * (1 - t) - 0.3
        p.disc((x, yy, z), 0.17, lit, glow=0.6, glow_color=(255, 140, 70))


def street_lamp(p, x, z, color, height=4.6):
    side = 1 if x > 0 else -1
    p.line((x, 0, z), (x, height, z), 0.05, (30, 30, 32))
    p.line((x, height, z), (x - side * 0.6, height, z), 0.035, (30, 30, 32))
    p.disc((x - side * 0.65, height - 0.08, z), 0.12, color, glow=1.4, glow_color=color)


# ------------------------------------------------------------------ las tres calles

def build(n):
    style = ["faroles", "barrio", "avenida"][n]
    rng = random.Random(500 + n)
    f_left = make_facade(1000 + n * 10, style)
    f_right = make_facade(1001 + n * 10, style)
    far = skyline(2000 + n, 12, 46 if style != "barrio" else 24, 0.6)
    back_l = skyline(2100 + n, 14, 26 if style != "barrio" else 12, 0.25, ZFAR - ZB)
    back_r = skyline(2200 + n, 14, 26 if style != "barrio" else 12, 0.25, ZFAR - ZB)

    lamps = []
    if style == "faroles":
        lamps = [(3.6, z, "#F0D0A0", 0.7) for z in (14.0, 30.0, 46.0)]
    elif style == "barrio":
        lamps = [(-3.6, z, "#C8D8F0", 0.6) for z in (6.0, 22.0, 40.0)]
    else:
        lamps = [(sx * 3.7, z, "#F4E0B0", 0.8) for z in (12.0, 26.0, 40.0, 54.0) for sx in (-1, 1)]

    img, depth, ss = render_backdrop(f_left, f_right, far, back_l, back_r, style, lamps)
    p = Painter(img, depth, ss)

    for lx, lz, lc, _ in lamps:
        if lz > ZB:
            street_lamp(p, lx, lz, tuple(rgb(lc)))
    if style == "faroles":
        for z in range(52, 11, -5):
            lantern_string(p, float(z))
        for z, c in ((15.0, "#E04040"), (27.0, "#40C0B0"), (41.0, "#E0A030")):   # carteles verticales
            p.quad([(-SIDE + 0.15, 3.2, z), (-SIDE + 0.75, 3.2, z), (-SIDE + 0.75, 5.6, z), (-SIDE + 0.15, 5.6, z)], tuple(rgb(c)))
        # esquina: semaforo en rojo, cartel redondo de contramano y cartel verde de calle
        p.line((-3.7, 0, 11.2), (-3.7, 3.6, 11.2), 0.05, (40, 40, 40))
        p.line((-3.7, 3.4, 11.2), (-2.6, 3.4, 11.2), 0.04, (40, 40, 40))
        p.quad([(-2.75, 3.15, 11.2), (-2.35, 3.15, 11.2), (-2.35, 3.6, 11.2), (-2.75, 3.6, 11.2)], (20, 20, 20))
        p.disc((-2.55, 3.48, 11.15), 0.08, (255, 40, 30), glow=1.2, glow_color=(255, 60, 40))
        p.disc((-3.1, 3.5, 11.15), 0.33, (230, 120, 30))
        p.quad([(-3.32, 3.46, 11.1), (-2.88, 3.46, 11.1), (-2.88, 3.54, 11.1), (-3.32, 3.54, 11.1)], (240, 240, 230))
        p.quad([(-4.0, 1.8, 11.6), (-3.75, 1.8, 11.6), (-3.75, 3.2, 11.6), (-4.0, 3.2, 11.6)], (40, 190, 150))
        for x, z in ((-3.4, 18.0), (2.9, 23.0), (-1.2, 36.0), (3.2, 44.0)):
            p.person(x, z)
        p.disc((0.7, 0.9, 32.0), 0.12, (255, 250, 230), glow=2.2, glow_color=(255, 245, 220))  # la moto
        p.person(0.7, 32.2, 1.5)
    elif style == "barrio":
        for z in (14.0, 34.0, 54.0):   # postes con cables
            p.line((-3.9, 0, z), (-3.9, 7.0, z), 0.08, (34, 32, 30))
            p.line((-4.3, 6.6, z), (-3.3, 6.6, z), 0.04, (34, 32, 30))
        for z0, z1 in ((14.0, 34.0), (34.0, 54.0)):
            for dxw in (-0.4, 0.0, 0.5):
                p.catenary((-3.9 + dxw, 6.6, z0), (-3.9 + dxw, 6.6, z1), 0.6, 0.012, (18, 18, 18))
            p.catenary((-3.9, 6.6, z0 + 2), (3.9, 6.0, z0 + 6), 0.4, 0.012, (18, 18, 18))
        # al fondo, un negocio con luz calida
        p.quad([(-1.5, 0, 60.0), (1.8, 0, 60.0), (1.8, 2.8, 60.0), (-1.5, 2.8, 60.0)], (240, 190, 110))
        p.disc((0.2, 1.4, 59.9), 0.8, (250, 210, 140), glow=1.5, glow_color=(250, 200, 120))
        for x, z in ((2.8, 26.0), (-2.2, 47.0)):
            p.person(x, z)
    else:
        # camion estacionado a la derecha
        z0, z1, x0, x1 = 14.0, 19.5, 1.6, 2.8
        p.quad([(x0, 0.4, z0), (x1, 0.4, z0), (x1, 2.9, z0), (x0, 2.9, z0)], (150, 150, 146))
        p.quad([(x0, 0.4, z0), (x0, 0.4, z1), (x0, 2.9, z1), (x0, 2.9, z0)], (120, 120, 118))
        p.quad([(x0 + 0.1, 1.6, z0 - 0.01), (x1 - 0.1, 1.6, z0 - 0.01), (x1 - 0.1, 2.4, z0 - 0.01), (x0 + 0.1, 2.4, z0 - 0.01)], (30, 36, 44))
        p.disc((x0 + 0.2, 0.8, z0 - 0.02), 0.09, (255, 245, 220), glow=1.6)
        p.disc((x1 - 0.2, 0.8, z0 - 0.02), 0.09, (255, 245, 220), glow=1.6)
        # semaforos en el cruce
        for sx, c in ((-1, (40, 220, 110)), (1, (255, 50, 40))):
            p.line((sx * 3.5, 0, 30.0), (sx * 3.5, 4.2, 30.0), 0.06, (40, 40, 40))
            p.quad([(sx * 3.5 - 0.2, 3.6, 30.0), (sx * 3.5 + 0.2, 3.6, 30.0), (sx * 3.5 + 0.2, 4.3, 30.0), (sx * 3.5 - 0.2, 4.3, 30.0)], (20, 20, 20))
            p.disc((sx * 3.5, 3.95, 29.95), 0.1, c, glow=1.4, glow_color=c)
        for z, c in ((18.0, "#E8E0D0"), (33.0, "#E04040")):
            p.quad([(SIDE - 0.15, 3.6, z), (SIDE - 0.8, 3.6, z), (SIDE - 0.8, 6.2, z), (SIDE - 0.15, 6.2, z)], tuple(rgb(c)))
        for x, z in ((-3.5, 16.0), (-3.2, 24.0), (3.4, 28.5), (0.5, 28.2), (-3.6, 41.0)):
            p.person(x, z)

    # bajar a la resolucion final (promedio de ss x ss) y alfa duro
    H, W = img.shape[0] // ss, img.shape[1] // ss
    small = img.reshape(H, ss, W, ss, 4).mean(axis=(1, 3))
    alpha = np.where(small[..., 3] >= 128, 255, 0)
    small[..., :3] = np.where(small[..., 3:4] > 0, small[..., :3] * 255.0 / np.maximum(small[..., 3:4], 1), small[..., :3])
    small[..., 3] = alpha
    fondo = np.clip(small, 0, 255).astype(np.uint8)

    # los primeros 10 m, de verdad: piso y fachadas de los costados
    zs = Z0 + (np.arange(int((ZB - Z0) * PX)) + 0.5) / PX
    xs = -SIDE + (np.arange(int(2 * SIDE * PX)) + 0.5) / PX
    Xg, Zg = np.meshgrid(xs, zs)
    suelo = ground(Xg, Zg, f_left, f_right, style, lamps, near=True)
    suelo = np.flipud(suelo)  # fila 0 = el fondo (z = 10)
    suelo = np.dstack([suelo, np.full(suelo.shape[:2], 255.0)]).astype(np.uint8)

    ncol = int((ZB - Z0) * PX)
    izq = np.clip(f_left.img[:, :ncol], 0, 255).astype(np.uint8)
    der = np.clip(f_right.img[:, :ncol], 0, 255).astype(np.uint8)
    for arr in (izq, der):
        arr[..., 3] = np.where(arr[..., 3] >= 128, 255, 0)
    return fondo, suelo, izq, der


def main():
    os.makedirs(SALIDA, exist_ok=True)
    for n in range(3):
        fondo, suelo, izq, der = build(n)
        Image.fromarray(fondo, "RGBA").save(os.path.join(SALIDA, "Calle%d_Fondo.png" % n))
        Image.fromarray(suelo, "RGBA").save(os.path.join(SALIDA, "Calle%d_Suelo.png" % n))
        Image.fromarray(izq, "RGBA").save(os.path.join(SALIDA, "Calle%d_Izq.png" % n))
        Image.fromarray(der, "RGBA").save(os.path.join(SALIDA, "Calle%d_Der.png" % n))
        print("Calle %d lista" % n)


if __name__ == "__main__":
    main()
